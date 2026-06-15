# CppSharp parser-include redirect (the supported v145 binding-generation config)

> Audience: core contributors who regenerate `Generated/UtinniCore.cs` or touch
> the build toolchain. Plugin authors don't need this.

## TL;DR

The CppSharp binding generator (`UtinniCoreDotNetGen`) parses the UtinniCore C++
headers with its **vendored clang 11** parser, but the main UtinniCore C++ build
compiles with the **v145 (MSVC 14.5x) toolset**. Those two clang versions are
incompatible with the *same* MSVC STL, so the generator is configured to parse
against the **VS 2019 MSVC 14.29 STL** (the STL clang 11 was originally paired
with) while the build keeps using v145. That parser-include redirect lives in
`UtinniCoreDotNetGen/Program.cs::ConfigureCppSharpParserStl()`.

**This redirect is the SUPPORTED config — not a temporary workaround.** No
released CppSharp can parse the v145 STL (see the spike result below), so there
is no near-term path to "run the generator natively on v145". The redirect is
load-bearing and must stay until a clang-20-bearing CppSharp ships.

## The CPPS-01 spike result (why the redirect is permanent for now)

`tools/cppsharp-clang-capability-spike.ps1` reads the `#if __clang_major__ < N`
gate out of each installed MSVC toolset's `yvals_core.h` and tabulates N against
the clang versions CppSharp ships. The mechanical result on this box:

| MSVC toolset | STL requires clang | clang 11 (vendored) parses? | clang 19 (latest released CppSharp v1.2) parses? |
|--------------|--------------------|------------------------------|---------------------------------------------------|
| 14.29 (VS 2019 v142) | 11+ | YES | YES |
| 14.44 (VS 2022) | 19+ | NO | YES |
| 14.51 / 14.52 (VS 2026 v145) | 20+ | NO | NO |

The conclusion is purely arithmetic: the v145 STL gate hard-requires clang **20
or newer**; the vendored CppSharp ships clang **11** and the newest *released*
CppSharp (v1.2, 2025-11-19) ships clang **19**. Since `11 < 20` AND `19 < 20`,
**no released CppSharp parses the v145 STL.** This is what re-set the phase's
acceptance from "retire the redirect" to "harden the redirect" — see
`.planning/phases/17-cppsharp-v145-hardening/`.

Run the spike yourself:

```
powershell -ExecutionPolicy Bypass -File tools/cppsharp-clang-capability-spike.ps1
```

It is a read-only report (never throws, never modifies); a missing toolset is
reported as "(not found)", not an error.

## Why the 14.29 redirect is load-bearing (and ABI-safe)

The MSVC STL's clang-version gate is **header-resident**, not baked into the
compiler binary — `yvals_core.h` carries `#if __clang_major__ < N` / `STL1000`.
VS 2019's MSVC 14.29 STL explicitly accepts clang 11, because clang 11 was
current when VS 2019 16.10 shipped (June 2021). That is the *original supported
pairing* for the vendored CppSharp 0.10.5 (which is "based on LLVM 11.0.0").
Pointing the parser at the 14.29 STL gives a guaranteed-clean parse of
`<vector>`, `<string>`, `<tuple>`, etc., decoupled from whatever MSVC version
the C++ build actually uses.

**ABI-stability assumption (load-bearing):** the generated C# bindings stay
layout-correct for the v145-built `UtinniCore.dll` because CppSharp reads
`sizeof`/`alignof` from the parsed **AST**, not from the live STL, and the
Microsoft STL ABI is stable within `_MSVC_STL_VERSION = 143` across MSVC 14.29 →
14.52. So a `std::string` (and every other projected STL type) has the same
in-memory layout whether parsed by the 14.29 headers or compiled by v145.
(RESEARCH Assumption A3; already shipping & live-smoked since Phase 6, commit
`2f57dfa`.) `StdEdited.cs` is hand-curated against exactly this 0.10.5 + 14.29
pairing, so it needs no change under the redirect (Assumption A4).

## Prerequisites (machine-local toolchain state)

These must be installed on every dev box and the self-hosted CI runner. They are
NOT in git; CI already verify-checks the first two.

| Prerequisite | Version | Role |
|--------------|---------|------|
| VS 2019 BuildTools — MSVC v142 STL | **14.29.30133** | The parser-include redirect target (clang-11-paired STL). |
| VS 2026 (Dev18) — v145 toolset | **14.51 / 14.52** | The actual C++ build toolset. |
| Windows 10 SDK | **10.0.19041** (preferred; resolver falls back to highest installed) | The UCRT/shared/um includes the redirect chases into. |

Install VS 2019 BuildTools + MSVC v142 if missing:

```
choco install visualstudio2019buildtools --package-parameters="--add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.v142"
```

## Environment overrides the resolver reads

`ConfigureCppSharpParserStl()` resolves all paths via env-var → vswhere →
default-path probe (never a hard-coded install path). The env overrides are:

| Env var | Read by | Purpose |
|---------|---------|---------|
| `UTINNI_VS2019_ROOT` | `ResolveVs2019Root()` | Point the redirect at a non-default VS 2019 install (CI hosts / custom layouts). |
| `UTINNI_SLN_DIR` | `SlnDirResolver.Resolve()` | Override the solution-dir resolution when `\bin\` isn't in the generator's output path. |

When neither is set, the resolver uses `vswhere -version "[16.0,17.0)"` to find
the VS 2019 install, then falls back to the standard
`Microsoft Visual Studio\2019\{BuildTools,Community,Professional,Enterprise}`
default paths. The Windows SDK resolver prefers `10.0.19041.*` and otherwise
takes the highest installed `10.0.*` carrying `ucrt`+`shared`+`um`.

## Retiring the redirect (the future-work exit)

The redirect can only be retired once a CppSharp release ships **clang 20+**
(reaching the v145 STL gate). CI carries a tripwire (added later in Phase 17)
that warns LOUD when a newer-than-clang-19 CppSharp release appears, so the team
knows the native-v145 path has finally opened. Until then, removing the redirect
would ship a config that depends on a CppSharp that does not exist — do NOT do
it. The deferred upgrade (CppSharp v1.2 / net9 migration) reaches only MSVC
14.4x/v143, still needs a redirect, and is tracked as future-milestone work.

## See also

- [Regenerating bindings](regen-bindings.md) — the full regen + re-bless procedure.
- `UtinniCoreDotNetGen/Program.cs` — `ConfigureCppSharpParserStl()` (the live redirect) + the resolvers.
- `.planning/research/cppsharp-msvc-14.5-upgrade.md` — the full Path-1/2/3 analysis.
- `tools/cppsharp-clang-capability-spike.ps1` — the CPPS-01 spike that produced the table above.
