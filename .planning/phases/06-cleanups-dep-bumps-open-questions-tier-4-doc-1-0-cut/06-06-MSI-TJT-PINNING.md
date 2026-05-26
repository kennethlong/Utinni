# 06-06 — MSI ↔ TheJawaToolbox cross-repo pinning decision (D-21)

**Date:** 2026-05-25
**Plan:** 06-06 Task 3
**Decision owner:** planner (Kenneth Long)

## Context

Per DEC-C4 the V1 distribution pairs Utinni with **TheJawaToolbox (TJT)**, which
lives in the sibling repo `kennethlong/UtinniPlugins`. The `release.yml` MSI build
must bundle a *specific, reproducible* TJT build. D-21 requires the bundled TJT to
be pinned at an explicit `UtinniPlugins` commit SHA, and the pinning strategy is
the planner's choice between two options.

## Options considered

### OPT-A — git submodule

Add `kennethlong/UtinniPlugins` as a git submodule (e.g. at
`external-plugins/UtinniPlugins/`, deliberately NOT under `external/` which is
reserved for vcpkg-eligible native deps). The release workflow runs
`git submodule update --init`; the pin SHA lives in `.gitmodules` + the gitlink.

- **Pro:** most reproducible; the pin travels with every clone; `git` enforces it.
- **Con:** every contributor pays submodule ceremony (init/update, detached-HEAD
  confusion) for a dependency that only matters during the rare release build.
  Adds a gitlink to the main tree that the day-to-day C++/C# build never needs.

### OPT-B — CI checkout-with-ref *(SELECTED)*

`release.yml` adds a second `actions/checkout@v4` step targeting
`kennethlong/UtinniPlugins` at a hard-coded 40-char SHA literal, into a sibling
path so TJT's `..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll` HintPath
resolves.

- **Pro:** zero overhead for non-release contributors; the pin is visible and
  reviewable directly in the workflow YAML; the MSI is built only on release
  tags, so the SHA is meaningful only at release time anyway.
- **Con:** the pin is a YAML literal rather than a git-enforced gitlink, so it can
  only drift via an explicit, reviewable edit to `release.yml` (mitigated by the
  fact that `release.yml` changes go through the same review as any other commit —
  see threat T-06-06-09).

## Decision: **OPT-B (checkout-with-ref)**

The submodule overhead (OPT-A) is not justified for a single rare workflow. The
MSI is produced only on `v1.0*` tag pushes; the UtinniPlugins commit is
content-addressable and reviewable as a literal in the workflow. This matches the
[[feedback-utinniplugins-authority]] cross-repo paired-commit model already in use.

## The pin

| Field | Value |
|-------|-------|
| Repository | `kennethlong/UtinniPlugins` |
| Pinned SHA (40-char) | `c9cfa9d01417bea772142136b69ec333dd30fa3f` |
| Commit | `feat(tjt): receive TJT.ico from Utinni framework (paired with utinni#a3093be)` |
| TJT solution | `The Jawa Toolbox/TheJawaToolbox.sln` |
| TJT plugin project | `The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj` |

> Update this SHA (and the `ref:` in `.github/workflows/release.yml`) in a paired
> commit whenever the bundled TJT is intentionally advanced for a new Utinni
> release. The two must always move together.

## Cross-repo build layout (why the checkout paths matter)

`TheJawaToolboxDotNet.csproj` references Utinni via:

```
<HintPath>..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll</HintPath>
```

From `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/`, that resolves to a
sibling `Utinni/bin/<Configuration>/UtinniCoreDotNet.dll`. Therefore `release.yml`
checks out **Utinni into `Utinni/`** and **UtinniPlugins into `UtinniPlugins/`**
under the workspace, so the two are siblings and the HintPath resolves after the
Utinni `Release|x86` build populates `Utinni/bin/Release/`.

## Toolchain note

`release.yml` runs on the **self-hosted `utinni-v145` runner**, NOT `windows-2022`.
The Utinni native build requires the v145 (VS 2026 / MSVC 14.5x) toolset plus
VS 2019 (MSVC 14.29) for the CppSharp parser STL pin — neither is available on any
GitHub-hosted image (see [[project-self-hosted-ci]]). The release workflow mirrors
`ci.yml`'s vcpkg bootstrap + toolchain-verify + MSBuild setup before building.
