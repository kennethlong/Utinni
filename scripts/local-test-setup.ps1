<#
.SYNOPSIS
Local-test setup for Utinni against a local SWGEmu Core3 server.

.DESCRIPTION
One-shot script that prepares a working `bin\Release\` for manual UAT runs:

  1. Verifies (or installs) DXSDK June 2010 — the d3dx9.h prerequisite that
     UtinniCore.vcxproj hard-codes.
  2. Locates MSBuild via vswhere and builds Utinni.sln Release|x86.
  3. Seeds bin\Release\utinni.cfg with the local Core3 login server settings
     IF the file is missing or blank. data\utinni.cfg stays blank to honor
     CON-D-01 (C-14 fix from Phase 02; verified by 02-VERIFICATION.md SC #3).
  4. Optionally launches bin\Release\Launcher.exe.

Idempotent: safe to re-run. Will not overwrite a populated
bin\Release\utinni.cfg unless `-Reseed` is passed.

Why the mtime dance: UtinniCore's post-build event copies data\* to bin\Release\
via `xcopy /d` (date-aware). Once bin\Release\utinni.cfg is newer than
data\utinni.cfg, future incremental builds leave the populated copy alone.
This script writes bin\Release\utinni.cfg AFTER the build so its mtime is
naturally newer than data\utinni.cfg.

.PARAMETER Launch
After build + seed, start bin\Release\Launcher.exe. The Launcher will pop a
GetOpenFileName dialog the first time if bin\Release\ut.ini's [Launcher]
fields are blank (the default); pick your SWGEmu client .exe and the
Launcher persists the choice into bin\Release\ut.ini for future runs.

.PARAMETER Reseed
Force-rewrite bin\Release\utinni.cfg's loginServer lines even if it already
has settings (e.g., to change ports).

.PARAMETER InstallDxsdk
If DXSDK June 2010 is missing, download and install it (~600 MB, requires
admin). Without this flag, the script prints install instructions and exits.
The install mirrors .github\workflows\ci.yml's "Install DirectX SDK" step.

.PARAMETER LoginAddress
Local Core3 login server address. Default: 127.0.0.1

.PARAMETER LoginPort
Local Core3 login server port. Default: 44453 (SWGEmu Core3 standard;
verify against your Core3 bin\conf\config.lua `LoginPort`).

.PARAMETER Configuration
Build configuration. Default: Release.

.EXAMPLE
.\scripts\local-test-setup.ps1
Standard run: verify DXSDK, build, seed bin\Release\utinni.cfg if needed.

.EXAMPLE
.\scripts\local-test-setup.ps1 -Launch
Build, seed, launch Launcher.exe.

.EXAMPLE
.\scripts\local-test-setup.ps1 -Reseed -LoginPort 44455
Force-rewrite the release cfg for a non-default Core3 port.

.EXAMPLE
.\scripts\local-test-setup.ps1 -InstallDxsdk
First-time setup: install DXSDK June 2010 before building.
#>

[CmdletBinding()]
param(
  [switch]$Launch,
  [switch]$Reseed,
  [switch]$InstallDxsdk,
  [string]$LoginAddress = '127.0.0.1',
  [int]$LoginPort = 44453,
  [ValidateSet('Debug','Release','RelWithDbgInfo')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Step($n, $total, $msg) {
  Write-Host ""
  Write-Host "[$n/$total] $msg" -ForegroundColor Cyan
}

Step 1 4 "Checking DXSDK June 2010..."
$dxsdkRoot = "C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)"
$dxsdkHeader = Join-Path $dxsdkRoot 'Include\d3dx9.h'
if (Test-Path $dxsdkHeader) {
  Write-Host "  OK: d3dx9.h found at $dxsdkHeader"
} elseif (-not $InstallDxsdk) {
  Write-Error @"
DXSDK June 2010 not found at $dxsdkRoot.

UtinniCore.vcxproj hard-codes the d3dx9.h include path; build will fail without it.

Re-run with -InstallDxsdk to auto-install (~600 MB, requires admin), or install
manually per .github\workflows\ci.yml lines 38-69:
  https://download.microsoft.com/download/a/e/7/ae743f1f-632b-4809-87a9-aa1bb3458e31/DXSDK_Jun10.exe

If installation fails with S1023, uninstall VC++ 2010 SP1 redistributables first
(see the CI workflow for the workaround).
"@
  exit 1
} else {
  Write-Host "  Installing DXSDK June 2010 (S1023 workaround applied)..."
  # S1023: DXSDK installer fails when newer VC++ 2010 SP1 runtimes are present.
  $vc2010 = @(
    '{1F1C2DFC-2D24-3E06-BCB8-725134ADF989}',  # x86 SP1
    '{1D8E6291-B0D5-35EC-8441-6616F567A0F7}'   # x64 SP1
  )
  foreach ($code in $vc2010) {
    Start-Process msiexec.exe -ArgumentList '/x', $code, '/quiet', '/norestart' -Wait -NoNewWindow
  }
  $url = 'https://download.microsoft.com/download/a/e/7/ae743f1f-632b-4809-87a9-aa1bb3458e31/DXSDK_Jun10.exe'
  $exe = Join-Path $env:TEMP 'DXSDK_Jun10.exe'
  Write-Host "  Downloading $url ..."
  Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
  Write-Host "  Running installer (unattended /U switch)..."
  Start-Process $exe -ArgumentList '/U' -Wait -NoNewWindow
  if (-not (Test-Path $dxsdkHeader)) {
    Write-Error "DXSDK install completed but d3dx9.h was not found at $dxsdkHeader. Manual investigation required."
    exit 1
  }
  Write-Host "  OK: DXSDK June 2010 installed."
}

Step 2 4 "Locating MSBuild..."
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
  Write-Error "vswhere.exe not found at $vswhere. Visual Studio 2019+ with C++ + .NET workloads required."
  exit 1
}
$msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
           Select-Object -First 1
if (-not $msbuild) {
  Write-Error "MSBuild not found via vswhere. Install the VS C++ workload (Microsoft.VisualStudio.Component.VC.Tools.x86.x64)."
  exit 1
}
Write-Host "  Using $msbuild"

Step 3 4 "Building Utinni.sln $Configuration|x86..."
& $msbuild Utinni.sln /m /restore /p:Configuration=$Configuration /p:Platform=x86 /v:minimal /nologo
$buildExit = $LASTEXITCODE
if ($buildExit -ne 0) {
  Write-Error "MSBuild failed with exit code $buildExit. See output above."
  exit $buildExit
}
Write-Host "  OK: Build succeeded."

Step 4 4 "Seeding bin\$Configuration\utinni.cfg..."
$srcCfg = Join-Path $repoRoot 'data\utinni.cfg'
$dstCfg = Join-Path $repoRoot "bin\$Configuration\utinni.cfg"

if (-not (Test-Path $dstCfg)) {
  Write-Error "Expected $dstCfg to exist after build (post-build xcopy of data\). Build may not have run UtinniCore's post-build event."
  exit 1
}

# CON-D-01 sanity: data\utinni.cfg must ship blank. If it isn't, that's a
# regression of C-14 from Phase 02 — warn loudly but don't auto-revert.
$srcContent = Get-Content -Raw $srcCfg
$srcHasLogin = $srcContent -match '(?m)^\s*loginServer(Port0|Address0)=\S' -ne $null
if ($srcContent -match '(?m)^\s*loginServerPort0=\S' -or $srcContent -match '(?m)^\s*loginServerAddress0=\S') {
  Write-Warning @"
data\utinni.cfg has non-blank login server settings. This violates CON-D-01
(C-14 fix verified by 02-VERIFICATION.md SC #3). The source-tree copy MUST
ship blank. Revert with: git restore data\utinni.cfg

Continuing to seed bin\$Configuration\utinni.cfg anyway, but PLEASE fix the
source before committing.
"@
}

$dstContent = Get-Content -Raw $dstCfg
$dstHasMyLogin = ($dstContent -match "(?m)^\s*loginServerPort0=$LoginPort\s*$") -and
                 ($dstContent -match "(?m)^\s*loginServerAddress0=$([regex]::Escape($LoginAddress))\s*$")
$dstHasBlankLogin = ($dstContent -match '(?m)^\s*loginServerPort0=\s*$') -and
                    ($dstContent -match '(?m)^\s*loginServerAddress0=\s*$')

if ($dstHasMyLogin -and -not $Reseed) {
  Write-Host "  OK: bin\$Configuration\utinni.cfg already has loginServer = $LoginAddress`:$LoginPort"
} elseif ($dstHasBlankLogin -or $Reseed) {
  # Replace the two login lines in-place. Preserves indentation, comments, line endings.
  $newContent = $dstContent -replace '(?m)^(\s*loginServerPort0=).*$', "`$1$LoginPort"
  $newContent = $newContent -replace '(?m)^(\s*loginServerAddress0=).*$', "`$1$LoginAddress"
  # Write as ASCII to match the original encoding (PowerShell 5.1 defaults
  # to UTF-16 LE BOM which would break C++ INI parsing).
  Set-Content -Path $dstCfg -Value $newContent -Encoding ascii -NoNewline
  # Bump mtime so subsequent `xcopy /d` from data\ skips us.
  (Get-Item $dstCfg).LastWriteTime = Get-Date
  Write-Host "  OK: Wrote loginServer = $LoginAddress`:$LoginPort to bin\$Configuration\utinni.cfg"
} else {
  Write-Warning @"
bin\$Configuration\utinni.cfg has loginServer settings that differ from
-LoginAddress=$LoginAddress / -LoginPort=$LoginPort. Leaving alone.
Pass -Reseed to overwrite.
"@
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
Write-Host "  bin\$Configuration\Launcher.exe is ready."

if ($Launch) {
  $launcher = Join-Path $repoRoot "bin\$Configuration\Launcher.exe"
  if (-not (Test-Path $launcher)) {
    Write-Error "Launcher.exe not found at $launcher (build did not produce expected output)"
    exit 1
  }
  Write-Host ""
  Write-Host "Launching $launcher ..."
  Start-Process $launcher -WorkingDirectory (Split-Path -Parent $launcher)
} else {
  Write-Host "  Re-run with -Launch to start it now, or invoke manually:"
  Write-Host "    bin\$Configuration\Launcher.exe"
}
