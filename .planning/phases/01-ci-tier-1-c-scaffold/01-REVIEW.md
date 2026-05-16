---
phase: 01-ci-tier-1-c-scaffold
reviewed: 2026-05-16T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - .editorconfig
  - .github/workflows/ci.yml
  - README.md
  - Utinni.sln
  - UtinniCore/UtinniCore.vcxproj
  - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj
  - UtinniCoreDotNet.Tests/HotkeyTests.cs
  - UtinniCoreDotNet.Tests/packages.lock.json
findings:
  critical: 0
  warning: 2
  info: 6
  total: 8
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-05-16
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Phase 01 ships a GitHub Actions CI workflow (`ci.yml`) on `windows-2022`, a new xUnit
test project (`UtinniCoreDotNet.Tests`) with two passing Facts and two Skip-documented
C-08 placeholders, a repo-root `.editorconfig`, a README CI badge, a `Utinni.sln`
update wiring in the test project, and a `UtinniCore.vcxproj` change adding the
DXSDK June 2010 include/lib paths to the `Release|Win32` configuration.

The implementation is generally sound: the test code matches the system-under-test
behavior, the workflow correctly project-targets `dotnet test` to dodge the known
mixed-C++/C# solution-targeted bug, and the platform/config mappings in `Utinni.sln`
follow the pre-existing `UtinniCoreDotNetGen` pattern.

Two correctness/robustness concerns warrant attention before this becomes load-bearing
for other contributors:

1. The DXSDK download has no integrity verification — a compromised CDN, DNS hijack,
   or Microsoft-side URL retirement would silently install whatever is at the URL or
   noisily fail. Pinning a SHA-256 is the standard mitigation for vendored installers
   in CI.
2. The new `IncludePath`/`LibraryPath` patch in `UtinniCore.vcxproj` was applied only
   to `Release|Win32`. `Debug|Win32` still lacks the DXSDK paths, so a future
   `Debug` build (locally or in CI) will fail with `d3dx9.h not found` on any machine
   that lacks a stale per-user `Microsoft.Cpp.Win32.user.props` providing them. CI
   only builds `Release`, so this is latent — but inconsistent.

The remaining six items are quality/maintenance concerns (missing concurrency
guard, unexplained `RestorePackagesConfig=true` flag, README badge URL implicitly
mirroring the working-tree branch, hard-coded absolute paths and GUIDs without
authoritative comments).

## Warnings

### WR-01: DXSDK installer downloaded without integrity verification

**File:** `.github/workflows/ci.yml:59-66`

**Issue:** `Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing` downloads a
~600 MB executable installer over HTTPS from `download.microsoft.com` and then
executes it with `/U` (unattended). There is no SHA-256 / SHA-512 / signature
verification of the downloaded `.exe` before execution.

Threat model: TLS protects against passive MITM, but does not protect against
(a) compromise of Microsoft's CDN, (b) future Microsoft-side replacement of the
file (Microsoft has retired legacy SDK downloads before), or (c) a misrouted
redirect. The `Test-Path ... d3dx9.h` check at line 67 verifies *installation
success*, not *artifact integrity* — a modified installer would still drop
`d3dx9.h` and pass the post-check.

The cache (`actions/cache@v4` keyed `dxsdk-jun2010-v1`) means a poisoned install
persists across all subsequent runs that hit the cache, amplifying the blast radius.

**Fix:** Pin the expected hash and verify before executing. The Microsoft
`DXSDK_Jun10.exe` file hash is widely published (SHA-256:
`4607ABDF2EBC5C92C4805A7CADCCF5BB3F86A11AB4A78423C09F25A14572D89B`; cross-check
with one or two independent mirrors before committing). Suggested PowerShell:

```powershell
$url = 'https://download.microsoft.com/download/a/e/7/ae743f1f-632b-4809-87a9-aa1bb3458e31/DXSDK_Jun10.exe'
$exe = Join-Path $env:RUNNER_TEMP 'DXSDK_Jun10.exe'
$expectedSha256 = '4607ABDF2EBC5C92C4805A7CADCCF5BB3F86A11AB4A78423C09F25A14572D89B'  # verify independently before merge
Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
$actualSha256 = (Get-FileHash -Path $exe -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "DXSDK_Jun10.exe hash mismatch: expected $expectedSha256, got $actualSha256"
}
Start-Process $exe -ArgumentList '/U' -Wait -NoNewWindow
```

This also doubles as a tripwire for link rot: if Microsoft re-publishes a different
build of the SDK at the same URL, CI fails loudly with an actionable error rather
than silently consuming the new bits.

### WR-02: DXSDK include/lib paths added to Release|Win32 only, not Debug|Win32

**File:** `UtinniCore/UtinniCore.vcxproj:61-70` (and missing from the `Debug|Win32`
`PropertyGroup` at lines 61-64)

**Issue:** The phase-01 diff added `IncludePath`/`LibraryPath` overrides to the
`Release|Win32` `PropertyGroup` (lines 68-69). The `RelWithDbgInfo|Win32`
`PropertyGroup` already had them (lines 74-75). The `Debug|Win32` `PropertyGroup`
(lines 61-64) still lacks them.

The CI workflow only builds `Release` so this is latent in CI, but any contributor
running `msbuild Utinni.sln /p:Configuration=Debug /p:Platform=x86` on a fresh
machine — including future Debug-config CI jobs — will hit `d3dx9.h: cannot open
include file` because the project no longer relies on a stray
`Microsoft.Cpp.Win32.user.props` for include resolution.

The inconsistency also surprises readers: two of three configs declare the DXSDK
path; one does not, with no comment explaining why.

**Fix:** Add the same two lines to the `Debug|Win32` PropertyGroup for consistency:

```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
  <LinkIncremental>true</LinkIncremental>
  <OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>
  <IncludePath>$(VC_IncludePath);$(WindowsSDK_IncludePath);C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\Include\;</IncludePath>
  <LibraryPath>$(VC_LibraryPath_x86);$(WindowsSDK_LibraryPath_x86);C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\Lib\x86\;</LibraryPath>
</PropertyGroup>
```

Alternative (cleaner long-term): factor the three paths into a single
`<PropertyGroup>` without a `Condition`, or use the `$(DXSDK_DIR)` environment
variable that the official June 2010 installer sets (see IN-02).

## Info

### IN-01: DXSDK absolute path hardcoded instead of $(DXSDK_DIR) macro

**File:** `UtinniCore/UtinniCore.vcxproj:68-69, 74-75`

**Issue:** The project hardcodes
`C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\` for both `Include\`
and `Lib\x86\`. The official June 2010 installer sets a `DXSDK_DIR` environment
variable that MSBuild can reference via `$(DXSDK_DIR)`. Hardcoding bites any
contributor who installs the SDK to a non-default path (Program Files on a non-C:
drive, or `D:\SDKs\DXSDK`).

The phase-01 author noted this in the prompt: "pre-existing convention but worth
documenting." Recording it here for future cleanup.

**Fix (future, not urgent):** Replace the absolute paths with `$(DXSDK_DIR)`:

```xml
<IncludePath>$(VC_IncludePath);$(WindowsSDK_IncludePath);$(DXSDK_DIR)Include\;</IncludePath>
<LibraryPath>$(VC_LibraryPath_x86);$(WindowsSDK_LibraryPath_x86);$(DXSDK_DIR)Lib\x86\;</LibraryPath>
```

`DXSDK_DIR` already ends with a trailing backslash by installer convention. The CI
workflow's `Install DirectX SDK` step would continue to work because the default
install path the workflow validates already matches what `DXSDK_DIR` would point at.

### IN-02: VC++ 2010 SP1 MSI ProductCodes hardcoded without documentation source

**File:** `.github/workflows/ci.yml:52-55`

**Issue:** The two MSI ProductCodes (`{1F1C2DFC-2D24-3E06-BCB8-725134ADF989}`
x86 SP1, `{1D8E6291-B0D5-35EC-8441-6616F567A0F7}` x64 SP1) are well-known KB
GUIDs, but the comment only references the general support article
(https://support.microsoft.com/help/2728613) — not the specific KB or MSDN page
that enumerates these exact GUIDs. A future maintainer trying to verify whether
the runner image now ships a different patch level cannot quickly check.

**Fix:** Annotate each GUID with its source KB and the redistributable name:

```powershell
$vc2010 = @(
  '{1F1C2DFC-2D24-3E06-BCB8-725134ADF989}',  # Microsoft Visual C++ 2010 SP1 Redistributable Package (x86) - KB2565063
  '{1D8E6291-B0D5-35EC-8441-6616F567A0F7}'   # Microsoft Visual C++ 2010 SP1 Redistributable Package (x64) - KB2565063
)
```

This is purely a traceability improvement; the GUIDs themselves are correct.

### IN-03: README badge URL hardcodes branch=master but workflow already targets master

**File:** `README.md:3`

**Issue:** The CI badge URL `...badge.svg?branch=master` hardcodes the branch
query parameter. If the default branch is ever renamed (e.g., `master` → `main`,
which Microsoft now defaults to for new repos), this badge silently goes stale
and shows "no status" without erroring. The omission of the query parameter
makes GitHub render the badge for the **default** branch automatically, which is
the more resilient default.

**Fix:** Drop the `?branch=master` query parameter:

```markdown
[![CI](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg)](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml)
```

Defensible to keep the explicit branch if multi-branch CI is anticipated and the
maintainer wants a single source of truth for "the master badge." If you keep it,
add a one-line comment near `on.push.branches` in `ci.yml` noting that the README
badge is hardcoded to that same branch name.

### IN-04: ci.yml lacks a concurrency group; duplicate pushes race

**File:** `.github/workflows/ci.yml:6-13`

**Issue:** With no `concurrency:` block, two rapid pushes to the same branch (or
push + amended-push during a rebase) start two parallel CI runs that compete for
the same cache write. The newer run's results may not invalidate the older's
in-progress workflow, costing CI minutes and producing confusing status updates
on the PR page.

**Fix:** Add a workflow-level concurrency group that cancels superseded runs on
the same ref:

```yaml
concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

Place this immediately after the `on:` block (above `permissions:`). Standard
GitHub Actions pattern; documented at
https://docs.github.com/en/actions/using-jobs/using-concurrency.

### IN-05: RestorePackagesConfig=true flag is misleading for the SDK-style test project

**File:** `.github/workflows/ci.yml:78`

**Issue:** `msbuild ... /p:RestorePackagesConfig=true` instructs MSBuild to
restore *legacy `packages.config`*-style NuGet references. The new test project
uses `<PackageReference>` + lockfile, which is restored unconditionally via
`/restore`. The flag is harmless for the SDK-style project but reads as if it
were intended for it. A future reader may think the test project needs it.

If the existing legacy projects in the solution (`UtinniCoreDotNet.csproj`,
`UtinniCoreDotNetGen.csproj`, `Launcher.vcxproj`, etc.) carry `packages.config`,
the flag is required *for them* — but that is not documented.

**Fix:** Either:
1. Add a one-line comment explaining which project(s) need this flag:

```yaml
- name: Build solution (Release|x86)
  # /p:RestorePackagesConfig=true is required because the legacy non-SDK csprojs
  # in the solution still use packages.config-style NuGet references; SDK-style
  # PackageReferences are restored unconditionally.
  run: msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true
```

2. Or, if no project actually uses `packages.config` (verify with
`Get-ChildItem -Recurse -Filter packages.config`), remove the flag entirely.

### IN-06: NuGet cache path uses ~ which can vary on Windows runners

**File:** `.github/workflows/ci.yml:30`

**Issue:** `path: ~/.nuget/packages` relies on `~` expansion in the
`actions/cache` action's path matcher. On Windows runners this expands to
`%USERPROFILE%\.nuget\packages`, which is correct, but `~` expansion across
shells (pwsh vs cmd vs node-internal) has historically been inconsistent.
GitHub's own documentation for caching NuGet on Windows uses the explicit form.

**Fix (optional, for portability/clarity):**

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ${{ runner.os == 'Windows' && '~\.nuget\packages' || '~/.nuget/packages' }}
    key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json', '**/*.csproj') }}
    restore-keys: |
      nuget-${{ runner.os }}-
```

Or, since this workflow is Windows-only, just `${env:USERPROFILE}\.nuget\packages`
hardcoded. The current value works in practice on `windows-2022`; this is purely a
defensive clarification.

---

_Reviewed: 2026-05-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
