---
title: CppSharp clang 11 cannot parse MSVC 14.5x STL — blocks PlatformToolset v145 bump
created: 2026-05-24
priority: high
area: build-toolchain
discovered_in: phase-06-wave-2
related:
  - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-PLAN.md  # D-09 toolset bump origin
  - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-VCPKG-RESEARCH.md  # CppSharp kept-vendored evidence
  - external/CppSharp/lib/lib/clang/11.0.0/                                                  # vendored clang 11 parser
  - UtinniCoreDotNetGen/                                                                     # consumer of CppSharp
suggested_resolves_phase: 6  # blocks Phase 6 D-09; parked behind worktree-agent-a4d0744552aa5c200
---

## Problem

Phase 6 D-09 bumps PlatformToolset v142 → v145 (the actual VS 2026 / Dev18 toolset; v144 is skipped in MSVC numbering). v145 builds the Utinni C++ side cleanly. The PostBuildEvent on `UtinniCore.vcxproj` that runs `UtinniCoreDotNetGen.exe` (which uses CppSharp for codegen) **fails** because the vendored CppSharp's clang 11 parser cannot read MSVC 14.5x's STL.

Reproducible:
```
msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /p:PlatformToolset=v145 /t:UtinniCore
```

Errors are 16+ instances of clang-11 choking on C++23 features in MSVC's STL:
- `vector(3642): overloaded 'operator()' cannot be a static member function`
- `xmemory(190): use of undeclared identifier '__builtin_verbose_trap'`
- `tuple(1121): static_assert failed "get<T>(tuple<...>) requires T to occur exactly once" (N4971 [tuple.elem]/5)`
- `xstring(3071/3045/1777/1585/2216): expected body of lambda expression`
- `xlocnum(1183): static_assert failed "unexpected type; shouldn't be float"`
- `xutility(282/320), __msvc_string_view.hpp(163/178): static_assert failed "unexpected size"`

Then: `CppSharp has encountered an error while parsing code.` followed by MSB3077 on the PostBuildEvent xcopy + UtinniCoreDotNetGen.exe chain.

## Why It's Blocking

D-09 success criterion in 06-02-PLAN.md requires `<PlatformToolset>v145</PlatformToolset>` in every .vcxproj and a clean Release x86 build. The build cannot complete on v145 until codegen can parse MSVC 14.5x. Phase 6 v1.0-rc.1 packaging in 06-06 depends on a successful build pipeline.

## Investigation Paths

1. **Configure CppSharp's parser to use OLDER MSVC STL include path** — e.g., point `CppSharp.Parser.Options.IncludeDirs` (or whatever the property is called in `UtinniCoreDotNetGen/Program.cs`) at `${VS2022}\VC\Tools\MSVC\14.44\include` while the main build uses v145. Decouples codegen-time STL parsing from build-time STL compilation. Lowest-risk if it works.

2. **Upgrade vendored CppSharp** — pull a CppSharp release that ships clang 17+ (or build from `mono/CppSharp` source). No vcpkg port exists for CppSharp. Likely requires API surface changes in `UtinniCoreDotNetGen/Program.cs`. Highest-risk, highest-yield (also handles future MSVC bumps).

3. **Stay on v142 / v143 until upstream catches up** — defer D-09 entirely. Phase 6 v1.0-rc.1 ships on v142 (current master). D-09 becomes a follow-on milestone item once a CppSharp path is decided. Cleanest unblock but does not deliver the D-09 goal.

## Parked State

`worktree-agent-a4d0744552aa5c200` (locked) at commit `83a8056` has the v145 fixup committed on top of the executor's three commits (`0ab49ae` + `7fa5e48` + `6cefd49`). NOT merged to master. To resume Phase 6 Wave 2:

- Path 1 (configure parser): pursue inside the worktree, add commit on top, build-verify, merge.
- Path 2 (upgrade CppSharp): probably needs its own plan; consume the worktree's v145 sweep as a starting point.
- Path 3 (defer D-09): revert the toolset sweep + this fixup commit (or cherry-pick only `0ab49ae` + `7fa5e48` + the VSIX-widen portion of `6cefd49`), merge the rest, then close the worktree.

## References

- `[[project-vs2026-cppsharp-block]]` (auto-memory: v145 = VS 2026 toolset; CppSharp clang 11 incompatibility)
- `[[project-vs2026-toolchain]]` (auto-memory: VS 2026 install paths)
- 06-02-VCPKG-RESEARCH.md (CppSharp port-quality evidence; "keep vendored")
- 06-02-SUMMARY.md (Rule-4 escalations enumerated)
