---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 03a
type: execute
wave: 1
depends_on: []
files_modified:
  - Utinni.Cli/Commands/ApplySaveTabCommand.cs
  - Utinni.Cli/Commands/ApplySaveIffCommand.cs
  - Utinni.Cli/Commands/ApplySaveStfCommand.cs
  - Utinni.Cli/Commands/ApplySaveOtCommand.cs
  - Utinni.Cli/Program.cs
  - Utinni.Cli.Tests/Commands/ApplySaveTabCommandTests.cs
  - Utinni.Cli.Tests/Commands/ApplySaveIffCommandTests.cs
  - Utinni.Cli.Tests/Commands/ApplySaveStfCommandTests.cs
  - Utinni.Cli.Tests/Commands/ApplySaveOtCommandTests.cs
  - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs
autonomous: true
requirements: [MCP-02, AUTH-05]
user_setup: []

must_haves:
  truths:
    - "Each apply-save-* verb, in ONE operation, applies exactly ONE typed mutation to a loose-override asset, verifies byte-identity on the UNTOUCHED region, and ONLY THEN WriteAtomic-commits the MUTATED bytes to the loose-override destination under --root."
    - "The committed bytes are the POST-mutation bytes (proven by a read-back: re-parsing the written file reflects the edited value) — closing the reviewer-confirmed gap where the old roundtrip->save two-step persisted the PRE-mutation file."
    - "If the untouched-region byte-identity check fails, the verb exits 2 with an error envelope and writes NOTHING (verify-before-commit; no partial/corrupt write)."
    - "Each verb resolves its destination via LooseOverridePath.Resolve(--root, relAsset) (rejects ../rooted, exit 2) — the same containment as the existing save verb."
    - "Each verb emits the locked {written, path, bytesWritten, backupPath(null for loose-override), validated} envelope PLUS bytesEqualUntouched, so the MCP host (14-03) can pass it through OPAQUELY and rely on the EXIT CODE (0 ok / 2 verify-failed) — never parsing bytesEqualUntouched to decide whether to persist."
    - "A .tre input to any apply-save-* verb is rejected with a usage error pointing at repack-tre (apply-save-* is never a destructive repack path)."
    - "Each verb is golden-tested FIRST in Utinni.Cli.Tests (the every-capability-is-a-golden-tested-CLI-verb-first discipline is satisfied because the verb genuinely exists)."
  artifacts:
    - path: "Utinni.Cli/Commands/ApplySaveTabCommand.cs"
      provides: "apply-save-tab: typed datatable cell/row/column mutation + verify-untouched + WriteAtomic loose-override"
      contains: "WriteAtomic"
    - path: "Utinni.Cli/Commands/ApplySaveOtCommand.cs"
      provides: "apply-save-ot: typed OT override add/remove/edit + verify-untouched + WriteAtomic loose-override"
      contains: "WriteAtomic"
  key_links:
    - from: "Utinni.Cli/Commands/ApplySaveTabCommand.cs"
      to: "LooseOverridePath.Resolve + DataTableWriter.Serialize + WriteAtomic"
      via: "apply ONE typed mutation -> serialize -> verify untouched -> commit mutated bytes"
      pattern: "LooseOverridePath\\.Resolve"
    - from: "Utinni.Cli/Program.cs"
      to: "the 4 new ApplySave*Options verbs"
      via: "ParseArguments Type[] + Dispatch switch"
      pattern: "ApplySaveTabOptions"
---

<objective>
Add a NEW family of golden-tested `utinni-cli` verbs — `apply-save-tab`, `apply-save-iff`, `apply-save-stf`, `apply-save-ot` — that each, in a SINGLE atomic operation: take typed structured mutation args -> apply exactly ONE typed edit to a loose-override asset -> serialize -> verify byte-identity on the UNTOUCHED region -> and ONLY on a clean verify, `WriteAtomic`-commit the MUTATED bytes to the loose-override destination resolved under `--root`. On a failed verify, exit 2 with an error envelope and write NOTHING. This collapses the (proven-broken) `roundtrip-*` (verify-in-memory-only) + `save` (re-serialize-the-UNCHANGED-on-disk-file) two-step into one verb that genuinely persists the edit, closing the TOCTOU window and the MCP host's domain-field-parsing problem.

Purpose: This is the persist path the cross-AI reviewers proved was MISSING. Verified directly against current source: `SaveCommand.cs:110` sets `sourcePath = destPath` (re-serializes the UNCHANGED file) and takes NO mutation args; `RoundtripTabCommand.cs:137` serializes `roundtrippedBytes`, compares, and DISCARDS them (no `WriteAtomic`/`File.WriteAllBytes`). The old 14-03 verified an edit it never persisted. These new verbs own the apply+verify+commit semantics as golden-tested CLI logic so the MCP layer (14-03) stays a thin dispatcher that decides persist-vs-fail on the EXIT CODE, never on a parsed domain field.
Output: 4 new CLI verbs wired into `Program.cs`, each with golden/Fact tests proving the read-back persistence and the failed-verify-no-write contract.

SCOPED, DOCUMENTED EXCEPTION to the "Phase 14 adds ZERO verbs" guard-rail: this phase's CONTEXT/ROADMAP locked "Phase 14 adds no verbs; MCP is a thin dispatcher over Phase-13 verbs." The cross-AI review (BLOCKING, both reviewers) proved that the Phase-13 verb set CANNOT express "apply one typed edit AND persist it" — `save` and `roundtrip-*` are separately incapable. Per the user-approved `--reviews` replan decision, we add this minimal, golden-tested-FIRST `apply-save-*` family in the CLI (the Phase-13 surface, net472), NOT in the MCP layer. The MCP host stays a thin dispatcher; the net10/stdio/separate-process seam is untouched. The exception is NAMED here and in MCP-SECURITY.md, not applied silently. These verbs also retroactively complete AUTH-05's "writes an edited asset" promise (the original AUTH-05 SAVE verb was re-serialize-only).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-RESEARCH.md
@.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-REVIEWS.md

<interfaces>
<!-- The existing verbs this family reconciles. apply-save-* = (roundtrip-* apply+verify) + (save WriteAtomic to loose-override) FUSED, persisting the MUTATED bytes. -->
```
// save (SaveCommand.cs) — re-serialize-in-place, NO mutation args, loose-override default:
//   loose-override mode: sourcePath = destPath  <-- re-serializes the UNCHANGED on-disk file (THE BUG).
//   envelope: { written, path, bytesWritten, backupPath(null), validated }; .tre -> usage error to repack-tre.

// roundtrip-tab (RoundtripTabCommand.cs) — applies ONE typed mutation, serializes to roundtrippedBytes,
//   compares untouched per-cell ROWS slices, then DISCARDS roundtrippedBytes (no write). Args:
//   --mutate-cell row,col + --mutate-value | --remove-row N | --remove-column idx-or-name.
//   Mutation application + CompareUntouchedCellSlices are the REUSABLE logic.
// roundtrip-iff (RoundtripIffCommand.cs) — --mutate-leaf <id> + --mutate-hex <hex> | --remove-leaf <id>;
//   untouched-leaf identity check.
// roundtrip-stf (RoundtripStfCommand.cs) — --edit-text KEY=VALUE; per-entry-slice untouched check + sourceCrc preserved.
// roundtrip-ot  (RoundtripOtCommand.cs)  — --add-override|--edit (+ --value-int) | --remove-override;
//   per-param-payload untouched check.

// LooseOverridePath.Resolve(root, relAsset) — containment; throws ArgumentException on ../rooted.
// JsonOutput.EmitSuccess(cmd, JObject) / EmitError(cmd, kind, msg, exitCode) — sorted-key envelope.
// Exit-code contract (uniform): 0 ok / 1 usage / 2 domain (incl. verify-failed) / 3 file-not-found.
```

apply-save-* verb shape this plan PUBLISHES (the MCP host in 14-03 wraps these 1:1):
```
// apply-save-tab <relAsset> --root <clientRoot> --mutate-cell row,col --mutate-value <v>
//   (also --remove-row N | --remove-column idx-or-name)
// apply-save-iff <relAsset> --root <clientRoot> --mutate-leaf <id> --mutate-hex <hex> (also --remove-leaf <id>)
// apply-save-stf <relAsset> --root <clientRoot> --edit-text KEY=VALUE
// apply-save-ot  <relAsset> --root <clientRoot> --add-override <field> --value-int <n> (also --edit / --remove-override)
//
// Behavior (ALL four): exactly ONE mutation required; load destPath = LooseOverridePath.Resolve(root, relAsset);
//   apply the typed mutation to the mutable model; serialize -> mutatedBytes; re-parse mutatedBytes
//   (structural validity); verify byte-identity on the UNTOUCHED region (reuse the roundtrip-* comparison);
//   IF untouched-region identity FAILS -> EmitError(..., exitCode:2) and DO NOT write;
//   ELSE WriteAtomic(destPath, mutatedBytes) and EmitSuccess with:
//     { backupPath:null, bytesEqualUntouched:true, bytesWritten, mutationApplied, path:destPath, validated, written:true }
//   .tre input -> usage error (exit 1) pointing at repack-tre.
//   exit codes: 0 ok / 1 usage (incl. mutation missing or .tre) / 2 verify-failed|parse|containment / 3 not-found.
```
</interfaces>

@Utinni.Cli/Commands/SaveCommand.cs
@Utinni.Cli/Commands/RoundtripTabCommand.cs
@Utinni.Cli/Commands/RoundtripIffCommand.cs
@Utinni.Cli/Commands/RoundtripStfCommand.cs
@Utinni.Cli/Commands/RoundtripOtCommand.cs
@Utinni.Cli/Program.cs
@Utinni.Cli/Output/JsonOutput.cs
@Utinni.Cli.Tests/Commands/SaveCommandTests.cs
@Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: apply-save-tab + apply-save-ot verbs (the two highest-value typed-edit-save paths) + golden tests</name>
  <files>Utinni.Cli/Commands/ApplySaveTabCommand.cs, Utinni.Cli/Commands/ApplySaveOtCommand.cs, Utinni.Cli/Program.cs, Utinni.Cli.Tests/Commands/ApplySaveTabCommandTests.cs, Utinni.Cli.Tests/Commands/ApplySaveOtCommandTests.cs</files>
  <read_first>
    - Utinni.Cli/Commands/RoundtripTabCommand.cs (REUSE its ApplyMutateCell/ApplyRemoveRow/ApplyRemoveColumn + CompareUntouchedCellSlices verbatim — the apply+verify logic; the ONLY net-new step is WriteAtomic the mutated bytes)
    - Utinni.Cli/Commands/RoundtripOtCommand.cs (REUSE its add/remove/edit-override application + CompareUntouchedParams)
    - Utinni.Cli/Commands/SaveCommand.cs (REUSE its LooseOverridePath.Resolve containment + WriteAtomic(destPath, bytes) + .tre -> repack-tre redirect; DetectFormat is NOT needed — the verb is format-specific)
    - Utinni.Cli/Program.cs (the 17-verb ParseArguments Type[] + Dispatch switch — add the 2 new verbs; note the 16-verb CommandLineParser arity cap is already worked around via the Type[] overload, so adding verbs is safe)
    - Utinni.Cli/Output/JsonOutput.cs (the sorted-key envelope to emit)
    - .planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-REVIEWS.md (the BLOCKING finding + Consensus #1-#4: persist the mutated bytes; verify-before-commit in ONE verb; failed-verify = exit 2 so the host never parses bytesEqualUntouched)
  </read_first>
  <behavior>
    ApplySaveTabCommandTests:
    - happy mutate-cell: write a DTII .tab fixture under a temp root; `apply-save-tab rel --root <root> --mutate-cell 0,1 --mutate-value <new>`; exit 0; envelope { written:true, bytesEqualUntouched:true, validated:true, path=resolved-abs }; READ-BACK: re-parse the WRITTEN file and assert cell(0,1) holds <new> (the persisted bytes reflect the edit) AND assert the file HASH differs from the pre-edit file.
    - happy remove-row / remove-column: exit 0; read-back confirms the row/column is gone and untouched cells preserved.
    - failed-verify-no-write: construct a fixture/mutation where the untouched-region check would fail (or inject a writer that perturbs an untouched cell) -> exit 2, error envelope, and assert the on-disk file is BYTE-UNCHANGED from before the call (no partial write).
    - no mutation supplied -> exit 1 usage error.
    - ../escape rel -> exit 2 PathContainment.
    - .tre input -> exit 1 usage error mentioning repack-tre.
    - missing file -> exit 3.
    ApplySaveOtCommandTests:
    - happy add-override / edit / remove-override: exit 0; READ-BACK re-parse the written OT and assert the local override is present/edited/absent; untouched params byte-identical; file hash changed.
    - failed-verify-no-write: exit 2, on-disk file byte-unchanged.
    - duplicate add-override (already local) -> exit 1; edit non-existent -> exit 1; missing --value-int -> exit 1.
  </behavior>
  <action>
    Implement `ApplySaveTabCommand.cs` with `[Verb("apply-save-tab", ...)]` and options: `[Value(0)] RelAsset`, `[Option("root", Required=true)] Root`, plus the SAME typed mutation options as `roundtrip-tab` (`--mutate-cell`, `--mutate-value`, `--remove-row`, `--remove-column`). Flow: validate exactly ONE mutation (mirror RoundtripTabCommand usage validation; zero mutations -> exit 1 — apply-save REQUIRES a mutation, unlike roundtrip's no-op path); `destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset)` (catch ArgumentException -> exit 2 PathContainment); `File.Exists(destPath)` else exit 3; load bytes; sniff `.tre` (EERT magic) -> exit 1 to repack-tre; parse to `MutableDataTableDocument`; apply the ONE typed mutation by REUSING RoundtripTabCommand's apply helpers (extract them to internal-shared statics if cleaner, or call equivalently — keep the mutation semantics byte-for-byte identical to roundtrip-tab so the existing goldens still describe the apply step); `mutatedBytes = new DataTableWriter(mutDoc).Serialize()`; re-parse `mutatedBytes` (structural validity, exit 2 on parse failure); compute `bytesEqualUntouched` via RoundtripTabCommand's `CompareUntouchedCellSlices` over loaded-vs-mutated; IF `!bytesEqualUntouched` -> `EmitError("apply-save-tab","VerifyFailed", "untouched-region byte-identity check failed; nothing written", exitCode:2)` and RETURN (no write); ELSE `WriteAtomic(destPath, mutatedBytes)` (reuse SaveCommand's atomic Flush(true) write), `validated = Reparses(mutatedBytes)`, and `EmitSuccess` with `{ backupPath:null, bytesEqualUntouched:true, bytesWritten:mutatedBytes.Length, comparisonGranularity, firstMismatch:null, mutationApplied, path:destPath, validated, written:true }`. Do the analogous implementation in `ApplySaveOtCommand.cs` (`[Verb("apply-save-ot")]`, options mirroring roundtrip-ot `--add-override`/`--remove-override`/`--edit`/`--value-int`, reuse RoundtripOtCommand apply + `CompareUntouchedParams`, `ObjectTemplateWriter`/`model.Serialize()`). Wire both verbs into `Program.cs` `ParseArguments(args, ... typeof(Commands.ApplySaveTabOptions), typeof(Commands.ApplySaveOtOptions))` and the `Dispatch` switch.

    Write `ApplySaveTabCommandTests.cs` + `ApplySaveOtCommandTests.cs` per `<behavior>`, mirroring `SaveCommandTests`/`RoundtripTabCommandTests` discipline (temp work dir, IDisposable cleanup, `InProcessCliRunner.Run`, fixture builders `DataTableFixtureBuilder`/`IffBuilder`). The READ-BACK assertion (re-parse the written file + assert the edited value + assert hash changed) and the failed-verify-no-write (on-disk byte-unchanged) assertions are MANDATORY — they are the gates the reviewers required.
  </action>
  <verify>
    <automated>dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --filter "ApplySaveTabCommandTests|ApplySaveOtCommandTests"</automated>
  </verify>
  <acceptance_criteria>The two test classes pass; each proves (a) the WRITTEN file re-parses to reflect the typed edit (read-back persistence), (b) a failed untouched-region verify exits 2 and leaves the on-disk file byte-unchanged, (c) ../escape -> exit 2, .tre -> exit 1 repack-tre, missing -> exit 3. The verbs are dispatchable via Program.cs.</acceptance_criteria>
  <done>apply-save-tab + apply-save-ot persist the MUTATED bytes (read-back-proven), verify-before-commit in ONE verb, and fail-closed (exit 2, no write) on a failed verify. Golden-tested first.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: apply-save-iff + apply-save-stf verbs + golden tests + dispatch-help golden refresh</name>
  <files>Utinni.Cli/Commands/ApplySaveIffCommand.cs, Utinni.Cli/Commands/ApplySaveStfCommand.cs, Utinni.Cli/Program.cs, Utinni.Cli.Tests/Commands/ApplySaveIffCommandTests.cs, Utinni.Cli.Tests/Commands/ApplySaveStfCommandTests.cs, Utinni.Cli.Tests/Commands/CommandDispatchTests.cs</files>
  <read_first>
    - Utinni.Cli/Commands/RoundtripIffCommand.cs (REUSE the --mutate-leaf/--mutate-hex/--remove-leaf application + untouched-leaf identity check; ParseHex helper)
    - Utinni.Cli/Commands/RoundtripStfCommand.cs (REUSE --edit-text KEY=VALUE application + CompareUntouchedEntrySlices + sourceCrc-preserved assertion)
    - Utinni.Cli/Commands/SaveCommand.cs (LooseOverridePath.Resolve + WriteAtomic; .tre redirect — though IFF/STF inputs are not .tre)
    - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs (the no-args/help dispatch golden that lists verbs — it will need the 4 new verbs added; refresh per the existing golden-update discipline, like Phase 11's dispatch-help refresh)
    - .planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-REVIEWS.md (Cursor divergent: save_datatable->roundtrip-tab naming — client datatables are often DTII .iff; document apply-save-iff covers plain IFF, apply-save-tab covers DTII-.tab/datatable semantics)
  </read_first>
  <behavior>
    ApplySaveIffCommandTests:
    - happy mutate-leaf: write a plain IFF fixture under temp root; `apply-save-iff rel --root <root> --mutate-leaf <id> --mutate-hex <hex>`; exit 0; READ-BACK: re-parse written file, assert the leaf's payload equals <hex> bytes, untouched leaves byte-identical, file hash changed.
    - happy remove-leaf: exit 0; read-back confirms the leaf is gone.
    - failed-verify-no-write: exit 2, on-disk byte-unchanged.
    - no mutation -> exit 1; ../escape -> exit 2; missing -> exit 3.
    ApplySaveStfCommandTests:
    - happy edit-text: write a .stf fixture (incl. a named-Joao non-ASCII entry); `apply-save-stf rel --root <root> --edit-text greeting=<new>`; exit 0; READ-BACK: re-parse written file, assert entry text == <new>, untouched entries byte-identical, sourceCrc of edited entry preserved, file hash changed.
    - failed-verify-no-write: exit 2, on-disk byte-unchanged.
    - edit-text for a missing key -> exit 2 (mirror roundtrip-stf); malformed KEY=VALUE -> exit 1.
    CommandDispatchTests: the dispatch/help/no-args output now lists apply-save-tab/iff/stf/ot among the verbs (golden refreshed).
  </behavior>
  <action>
    Implement `ApplySaveIffCommand.cs` (`[Verb("apply-save-iff")]`, options `RelAsset` + `--root` + `--mutate-leaf`/`--mutate-hex`/`--remove-leaf`) and `ApplySaveStfCommand.cs` (`[Verb("apply-save-stf")]`, options `RelAsset` + `--root` + `--edit-text`), following the EXACT same flow as Task 1's verbs: resolve loose-override destination, require ONE mutation, apply via the reused roundtrip-iff/roundtrip-stf logic, serialize, re-parse for validity, verify byte-identity on the untouched region (reuse the respective comparison), FAIL-CLOSED exit 2 + no write on a failed verify, ELSE WriteAtomic + EmitSuccess with the locked envelope + `bytesEqualUntouched:true` (apply-save-stf also surfaces `sourceCrcPreserved`). Wire both into `Program.cs` ParseArguments + Dispatch. Refresh `CommandDispatchTests` golden to include the 4 new verbs (the no-args/help output enumerates them) — mirror the established golden-refresh discipline (e.g. Phase 11's roundtrip-ot dispatch-help refresh). Document in code comments that apply-save-iff handles plain/generic IFF leaf edits while apply-save-tab handles typed DTII datatable semantics (Cursor naming concern) — the MCP tool names in 14-03 map accordingly.
  </action>
  <verify>
    <automated>dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --filter "ApplySaveIffCommandTests|ApplySaveStfCommandTests|CommandDispatchTests"</automated>
  </verify>
  <acceptance_criteria>The three test classes pass; apply-save-iff + apply-save-stf prove read-back persistence (incl. non-ASCII round-trip + sourceCrc preserved for stf), failed-verify-no-write, and usage/containment/not-found exit codes; the dispatch-help golden lists all 4 new verbs.</acceptance_criteria>
  <done>apply-save-iff + apply-save-stf persist the mutated bytes (read-back-proven), fail-closed on a failed verify, and the dispatch help enumerates the full apply-save-* family. All four verbs golden-tested first.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| --root + relAsset -> LooseOverridePath.Resolve | The loose-override destination is resolved + contained before any write. | composed filesystem path |
| typed mutation args -> mutable model -> WriteAtomic | The ONLY write surface; gated behind a verify-before-commit on the untouched region. | typed mutation -> file bytes |
| mutatedBytes -> verify -> commit | Verify-before-commit: a failed untouched-region check aborts the write (exit 2). | byte arrays |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-14-08 | Tampering | apply-save-* write without verify | mitigate | The verb verifies byte-identity on the UNTOUCHED region in-process BEFORE WriteAtomic; failed verify -> exit 2 + NO write (no TOCTOU between verify and commit — one verb, one process). Tests prove failed-verify leaves the file byte-unchanged. |
| T-14-01 | Elevation/Tampering | apply-save-* path arg | mitigate | LooseOverridePath.Resolve(--root, relAsset) rejects ../rooted (exit 2) — same containment as save. |
| T-14-14 | Tampering | apply-save-* reaching destructive repack | mitigate | A .tre input is rejected with a usage error to repack-tre (exit 1); apply-save-* writes only loose-override files, never repacks an archive. |
| T-14-15 | Tampering | persisted bytes != verified bytes | mitigate | The verb commits the SAME in-memory mutatedBytes it verified (no re-load, no re-serialize) — closing the reviewer-confirmed gap where save re-serialized the UNCHANGED on-disk file. Read-back tests prove the persisted file reflects the edit. |
</threat_model>

<verification>
- `dotnet test Utinni.Cli.Tests --filter "ApplySaveTabCommandTests|ApplySaveOtCommandTests|ApplySaveIffCommandTests|ApplySaveStfCommandTests"` green.
- Each verb's read-back test proves the WRITTEN file reflects the typed edit (persisted bytes are post-mutation).
- Each verb's failed-verify test proves exit 2 + on-disk file byte-unchanged.
- CommandDispatchTests golden lists apply-save-tab/iff/stf/ot.
- `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` (or the existing CLI build) still green — the new verbs compile into utinni-cli.exe.
</verification>

<success_criteria>
- 4 new golden-tested CLI verbs apply ONE typed edit + verify-untouched + WriteAtomic-commit the MUTATED bytes to loose-override in ONE operation.
- Read-back proves persistence; failed verify exits 2 and writes nothing.
- The MCP host (14-03) can wrap each verb 1:1 and decide persist-vs-fail on the EXIT CODE alone (never parsing bytesEqualUntouched).
- The "ZERO verbs" exception is named + golden-tested-first.
</success_criteria>

<output>
Create `.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-03a-SUMMARY.md` when done.
</output>
