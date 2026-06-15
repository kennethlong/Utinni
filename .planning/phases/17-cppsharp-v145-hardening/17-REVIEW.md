---
phase: 17-cppsharp-v145-hardening
reviewed: 2026-06-15T03:00:07Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - .github/workflows/ci.yml
  - UtinniCoreDotNet.Tests/AbiBlockHash.cs
  - UtinniCoreDotNet.Tests/AbiSurfaceTests.cs
  - UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs
  - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj
  - UtinniCoreDotNetGen/Program.cs
  - tools/cppsharp-clang-capability-spike.ps1
findings:
  critical: 1
  warning: 6
  info: 3
  total: 10
resolved:
  critical: 1
  warning: 5
  info: 0
  total: 6
open:
  warning: 1
  info: 3
  total: 4
status: partially_resolved
resolution_commits:
  - aa12a29  # CR-01 + WR-01/02/03/06 (enum capture, extractor hardening, re-bless)
  - 1bd2d84  # WR-05 (spike PS 5.1 robustness)
---

# Phase 17: Code Review Report

**Reviewed:** 2026-06-15T03:00:07Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Phase 17 adds foundation/CI hardening around the CppSharp 14.29 parser-include redirect: a per-block SHA256 ABI-surface gate (`AbiBlockHash` + `AbiSurfaceTests`), a frozen-plugin MEF-compose gate (`FrozenPluginComposeTests`), two new CI tripwire steps (a C++23 STL-header hard-fail scan and a clang-20 CppSharp-pin warn-loud step), and a read-only clang-capability spike script.

The CI tripwires are well-constructed: the hard-fail scan is correctly scoped to `UtinniCore/` with `-LiteralPath` quoting and a denylist that deliberately lives outside the scanned root (no self-trip), and the warn-loud step is genuinely incapable of throwing/blocking and performs no network egress. The frozen-plugin gate is wired correctly as `<Content>` (not a rebuildable project, not in the solution, committed and not gitignored) — Pitfall 6 is properly closed. The `Program.cs` change this phase is comment-only.

The dominant defect is in the ABI-surface extractor: **enum members are silently not captured**, despite the file's own header comment claiming they are part of the keyed surface. CppSharp emits enum values that no extraction pattern matches, so a reordered/renumbered enum value — a real binary-ABI break for plugins that bind integer enum values — passes the gate undetected. Several latent robustness issues in the brace-counting scope tracker round out the warnings.

The verification scope explicitly excludes the binary DLL fixture, the generated baseline txt, the denylist data file, and docs.

## Resolution (post-review fix pass, 2026-06-15)

Fixed in commits `aa12a29` (extractor) and `1bd2d84` (spike), then the baseline was re-blessed
from a fresh regen (4386 → 4456 hashes; determinism re-proven: a second independent regen yields
an identical set, 0 diff). AbiSurface filter 6 → 8 facts; full UtinniCoreDotNet.Tests lane 771 →
773, no regressions. The frozen DLL fixture was NOT touched (managed-extraction-only fix), so the
maintainer live-smoke approval remains valid.

| Finding | Severity | Status | Note |
|---------|----------|--------|------|
| CR-01 | Critical | ✅ Resolved | Enum members now keyed to enum FQN (`ENUMMEMBER|<fqn>name=value`); two negative facts prove renumber/rename trips the gate, pure reorder stays invisible |
| WR-01 | Warning | ✅ Resolved | `ScrubLiteralsAndComments` (column-preserving) blanks string/char/comment spans before brace counting + regex; EntryPoint match deliberately uses the unscrubbed line |
| WR-02 | Warning | ✅ Resolved | `[FieldOffset]` now scans forward past blank/comment/attribute lines to the next real field |
| WR-03 | Warning | ✅ Resolved | `[StructLayout(Size=N)]` key now carries the enclosing struct FQN (`pendingLayoutSize` deferral) |
| WR-05 | Warning | ✅ Resolved | Spike: `$null -ne $gate` (null-on-left) + explicit if/else; report output behavior-identical |
| WR-06 | Warning | ✅ Resolved | `ResolveBaselinePath` co-anchors to the resolved generated file's repo root; probes for the baseline FILE |
| WR-04 | Warning | ⏳ Open (deferred) | Key `internal static extern` managed signatures independently of the mangled EntryPoint — cascades into IN-01; deferred together |
| IN-01 | Info | ⏳ Open (deferred) | Test couples to `count==1`; will be revisited with WR-04 |
| IN-02 | Info | ⏳ Open (deferred) | Per-call `SHA256.Create()` — micro-optimization |
| IN-03 | Info | ⏳ Open (deferred) | Hard-fail scan emits to stdout rather than `::error::` annotations |

Remaining open: 1 Warning (WR-04) + 3 Info, all advisory and tracked here for a future pass.

## Critical Issues

### CR-01: Enum members are not captured by the ABI extractor — renumbered/reordered enum values pass the gate

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:48-53, 86-90, 205-213`

**Issue:** The class header comment (lines 48-53) and the companion comment at line 51 explicitly claim the keyed surface includes "public enum members and `[FieldOffset(N)]` field layout." It does not. CppSharp emits enum members as bare `Name = N,` lines (verified in the live `Generated/UtinniCore.cs`):

```
public enum Types
{
    VtIniValue = 0,
    VtString = 1,
    ...
}
```

These lines do not begin with `public`, so `PublicMemberRegex` (which hard-requires the `public` modifier at line 87) never matches them, and no other pattern (`EntryPointRegex`, `FieldOffsetRegex`, `StructLayoutRegex`, `PublicTypeRegex`) matches them either. The only captured surface for an enum is the `TYPE|enum|FQN` declaration line itself — the member name/value pairs are invisible to the hash set.

Consequence: a regen that reorders or renumbers an enum value (e.g. `VtString = 1` -> `VtString = 2`, or inserting a new value in the middle that shifts all following ordinals) is a genuine binary-ABI break for any pre-built plugin that hardcodes the integer value — exactly the class of breakage this gate exists to catch (Pitfall 2). The set is identical before and after, so the gate stays green and the break ships. This is a false-negative in the primary safety mechanism, and the code does not do what its own documentation asserts.

**Fix:** Add an enum-member extraction path keyed to the enclosing enum FQN. Detect that the current scope's innermost segment is an enum (track a parallel "is-enum" flag on the scope frames), and for member-value lines capture name+ordinal:

```csharp
// New regex (compiled, alongside the others):
private static readonly Regex EnumMemberRegex = new Regex(
    "^\\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*(?:=\\s*(?<val>-?\\w+))?\\s*,?\\s*$",
    RegexOptions.Compiled);

// In ExtractRawKeys, inside an enum scope only (gate on a scopeIsEnum stack
// parallel to braceFrame), BEFORE falling through to PublicMemberRegex:
if (scopeIsEnum)
{
    var em = EnumMemberRegex.Match(line);
    if (em.Success && em.Groups["name"].Value != "")
    {
        keys.Add("ENUMMEMBER|" + fqnPrefix
            + em.Groups["name"].Value + "=" + em.Groups["val"].Value);
        continue; // do not also try type/member patterns on this line
    }
}
```

Track `scopeIsEnum` by pushing `true` on the brace frame when the pending decl was an `enum`, popping in lockstep with `scope`/`braceFrame`. Then re-bless the baseline via the documented `--rebless` lockstep. Add a negative test (`ReorderedEnumMember_IsReportedAsChanged`) so the new path is proven to trip, mirroring the existing EntryPoint negative tests.

## Warnings

### WR-01: Brace-counting scope tracker is not literal-aware — a brace inside a string/char literal silently desyncs every downstream FQN

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:248-270` (`CountChar` + scope maintenance)

**Issue:** `CountChar` counts every `{`/`}` character on a line with no awareness of string literals, verbatim strings, char literals, or comments. Today's generated file happens to contain zero braces inside literals (verified), so the tracker works. But the extractor is fed `Generated/UtinniCore.cs` whose content is determined by CppSharp's projection of arbitrary C++ headers — a default-value string, an attribute argument, or a future generator version that emits a `{`/`}` inside a string or char literal would unbalance the brace stack. A single unbalanced brace shifts the FQN of every subsequent block, changing thousands of hashes and either red-screening the gate on a pure-reorder regen (false positive) or, worse, masking a real change behind compensating drift (false negative). For a gate whose entire value is determinism across reorder churn, this is a real fragility, not a style nit.

**Fix:** Strip string/char/line-comment content before counting braces (and before the member/type regex pass). A minimal pre-pass per line that blanks out `"..."`, `@"..."`, `'...'`, and trailing `// ...` spans is sufficient given CppSharp's single-line emission style; or escalate to Roslyn (the file's own comment at lines 59-61 names Roslyn as the documented escalation path if line-based extraction proves brittle).

### WR-02: `[FieldOffset(N)]` capture assumes the field decl is exactly the next line — an interposed attribute mis-keys the layout

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:177-184`

**Issue:** The FieldOffset branch keys the offset to `Normalize(lines[i + 1])` — it hard-assumes the field declaration is on the immediately following line. Today every `[FieldOffset]` is followed directly by its field (verified: no interposed attributes), so it works. But if a regen emits a second attribute between `[FieldOffset(N)]` and the field (e.g. `[MarshalAs(...)]`, which CppSharp can emit for marshaled fields), the captured `next` becomes the attribute text instead of the field type+name. The layout key then silently tracks the attribute line rather than the field, weakening (or falsely changing) the layout contract. The "next non-attribute, non-blank line" is the robust target, not "i+1".

**Fix:** Scan forward from `i+1`, skipping blank/comment/attribute (`^\s*\[`) lines, to the first real declaration line before forming the key:

```csharp
string next = "";
for (int k = i + 1; k < lines.Length; k++)
{
    string cand = lines[k].Trim();
    if (cand.Length == 0 || cand.StartsWith("//") || cand.StartsWith("[")) continue;
    next = Normalize(lines[k]);
    break;
}
keys.Add("FIELDOFFSET|" + fqnPrefix + foMatch.Groups["n"].Value + "|" + next);
```

### WR-03: `[StructLayout(... Size=N)]` key omits the struct's own name — collisions if a type ever nests two laid-out structs

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:187-191`

**Issue:** The StructLayout attribute is emitted on the line *before* the `public partial struct __Internal` declaration, so at capture time the scope stack does not yet contain the struct's own name. The key becomes `STRUCTLAYOUT|<enclosing-type-FQN>.size=N` — the struct's name is absent. Today each CppSharp type carries exactly one `__Internal` struct so there is no collision, but if any enclosing type ever nests two `[StructLayout]` structs with the same Size, the two keys collapse to one set entry and one of the layout markers becomes invisible to the diff. The size signal is also partly redundant with the FieldOffset keys, but a struct of all-default-offset fields could rely on Size alone.

**Fix:** Defer the StructLayout key emission until the struct's name is known: stash the pending `Size=N` and emit `STRUCTLAYOUT|<full-struct-FQN>|size=N` once the struct name is pushed onto the scope stack (same pendingDecl mechanism already used for the scope push). At minimum, qualify the key with the pending declaration name when one is in flight.

### WR-04: `internal static extern` P/Invoke managed signatures are not keyed independently of the mangled EntryPoint

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:84-90, 167-174`

**Issue:** The `__Internal` P/Invoke decls (`internal static extern <ret> Foo(<params>);`) are deliberately dropped by `PublicMemberRegex` (it requires `public`), and the comment (lines 84-85) states they are "covered structurally by their EntryPoint string instead." That is true for the *native* identity (the mangled name encodes the native signature), but the *managed* marshaling surface of the extern decl — return type, parameter CLR types, `CallingConvention` — is not hashed. A regen that kept the same EntryPoint mangled string but changed the managed return type or a parameter's marshaled CLR type (a CppSharp projection change, not a native change) would alter what the managed caller binds against without moving the set. This is a narrower miss than CR-01 (the mangled name rarely stays fixed while the managed projection changes), hence WARNING not BLOCKER, but it is a real gap in "the managed surface a plugin binds against."

**Fix:** Add an extern-signature key for `internal static extern` lines under an `__Internal` scope: capture the normalized managed signature (return + name + param types), FQN-qualified, e.g. `EXTERN|<struct-FQN>.<normalized-sig>`. This complements (does not replace) the EntryPoint anchor.

### WR-05: Spike script `if/else` expression assignment is not PowerShell-idiomatic and can yield `$null` columns on PS 5.1

**File:** `tools/cppsharp-clang-capability-spike.ps1:114-115, 118-120, 143, 150`

**Issue:** Lines like `$vendoredOk = if ($gate -ne $null) { ... } else { ... }` rely on PowerShell treating `if` as an expression. This works on PS 5.1/7 for direct assignment, but the pattern is fragile: when `$gate` is `$null` the `$vendoredOk`/`$latestOk` are set to `$null`, and downstream the three-way `if ($vendoredOk -eq $null) {'n/a'} elseif ($vendoredOk) {...}` depends on `$null -eq $null`. The script also uses `$gate -ne $null` (value on the left) rather than the PowerShell-recommended `$null -ne $gate`; with `Select-String` returning a collection, a left-side `$null` comparison against an array can evaluate element-wise and produce a surprising boolean. The script is read-only and `exit 0`, so this is robustness/correctness-of-report, not a build blocker.

**Fix:** Put `$null` on the left of equality comparisons (`$null -ne $gate`) and prefer explicit assignment over `if`-as-expression for the column values, or compute the `'YES'/'NO'/'n/a'` string directly in one ternary-style `if/else` block assigned to the column variable.

### WR-06: `Rebless` baseline path resolver returns the first existing Fixtures *directory* without verifying it is the intended repo

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:418-435` (`ResolveBaselinePath`)

**Issue:** Unlike `ResolveGeneratedPath` (which probes for the existence of the target *file*), `ResolveBaselinePath` walks up and returns the first directory whose parent `Fixtures` dir exists (`Directory.Exists(Path.GetDirectoryName(candidate))`). The generated-path resolver requires the actual `UtinniCore.cs` to exist; the baseline resolver only requires the *containing directory* to exist, so it will happily return a path in the first ancestor that has a `UtinniCoreDotNet.Tests/Fixtures/` dir — which, when `--rebless` is run from an unexpected cwd, can write the blessed baseline into the wrong tree (e.g. a sibling checkout) and silently desync the committed contract from the regen source. `Rebless` is a maintainer mutation entry point, so a wrong-tree write is a real foot-gun.

**Fix:** Anchor the baseline resolution to the same tree as the resolved generated file (resolve generated first, then form the baseline path relative to its repo root), or at minimum log the resolved absolute baseline path prominently before writing (the checklist prints it at line 382, but resolution should be co-anchored, not independently walked).

## Info

### IN-01: `AddedMember_IsReportedAsAdded` asserts count==1 but the snippet adds one EntryPoint AND one extern decl — passes only because the extern is unkeyed

**File:** `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs:131-157`

**Issue:** The "added" delta is asserted to be exactly 1. The added block in the snippet is a full `[DllImport(... EntryPoint="?gamma...")] internal static extern void Gamma();` — two source lines. The test passes with count==1 only because the extern decl line is silently dropped (see WR-04); if WR-04 is fixed to key extern signatures, this assertion becomes count==2 and the test breaks. Not a bug today, but a coupling that will surprise a future fix. Consider asserting `added.Count >= 1` plus a content check on the gamma EntryPoint, or update in lockstep with WR-04.

### IN-02: `Sha256Hex` allocates a fresh `SHA256` per call inside a per-block loop

**File:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs:118-126, 297-306`

**Issue:** `ExtractFromText` calls `Sha256Hex` per key, and `Sha256Hex` does `using (var sha = SHA256.Create())` each time — for the ~2,450-block real file that is thousands of SHA256 provider allocations/disposals. This is a correctness-neutral allocation pattern (out of v1 perf scope) but is trivially avoidable and would be flagged in any review of hot extraction code. Hoist a single `SHA256` instance (it is reusable across `ComputeHash` calls) or use the static `SHA256.HashData` if the target framework supported it (net472 does not, so reuse one instance).

### IN-03: Hard-fail scan reports `$violations` via `Write-Host` then throws — the per-file paths land in stdout, not the failure annotation

**File:** `.github/workflows/ci.yml` (Phase 17 CPPS-03a step, the `if ($violations.Count -gt 0)` block)

**Issue:** On a real violation the step writes each offending `file:line: #include <hdr>` via `Write-Host` and then `throw`s a generic message. The actionable detail (which file/line) is in the log stream above the thrown error, not in the GitHub failure annotation. Consider emitting the violations as `::error file=...,line=...::` workflow commands so they surface as inline annotations on the failing push, matching the warn-loud step's use of `::warning::`. Cosmetic/diagnostic only — the gate correctly blocks.

---

_Reviewed: 2026-06-15T03:00:07Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
