<#
.SYNOPSIS
  Phase 12 / AUTH-01 D-09 byte-exact smoke harness for the revived SOE build CLIs.

.DESCRIPTION
  Runs a lifted build tool against a real maintainer-supplied SOURCE asset and
  compares its output byte-for-byte (SHA256 Get-FileHash) to a known-good
  REFERENCE produced by the original SOE toolchain. There is NO structural /
  round-trip / normalized fallback (D-09): a mismatch exits non-zero and dumps
  BOTH byte streams next to the reference for triage (mirroring
  Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs).

  This harness is reference-pair-driven. As of 12-03 the repo contains NO
  source -> known-good pair for any of the three tools (see
  tools/DEPENDENCY-MANIFEST.md "Byte-exact status" gate-findings). Supply a pair
  via the parameters below to activate a real byte-exact gate for that tool.

  TreeFileBuilder note: the .tre reference MUST be version 0005/0006 (the format
  this 2002-era tool emits). Restoration v6000 .tre files (e.g. D:\Sample-TRE-Files)
  are a newer, encrypted format this tool cannot produce or read -- they are NOT
  valid references (documented gate-finding).

.PARAMETER Tool
  Path to the lifted *_d.exe (TreeFileBuilder / TemplateCompiler / TemplateDefinitionCompiler).

.PARAMETER ToolArgs
  The full argument vector that produces the output, e.g.
  @('-r','build.rsp','out.tre') or @('-compile','input.tpf').

.PARAMETER Output
  Path the tool writes (compared against -Reference).

.PARAMETER Reference
  Path to the known-good reference artifact from the original SOE toolchain.

.PARAMETER NormalizeBanner
  TemplateDefinitionCompiler ONLY: a regex whose matches are stripped from BOTH
  output and reference before compare, to absorb the generated-C++ header banner's
  embedded __DATE__/time/absolute-path (Pitfall 6). Use the NARROWEST regex the
  maintainer approves -- nothing broader. Omit for binary (.tre/.iff) tools.

.EXAMPLE
  ./byte-exact-smoke.ps1 -Tool ..\src\compile\win32\TemplateCompiler\Debug\TemplateCompiler_d.exe `
                         -ToolArgs @('-compile','sample.tpf') -Output sample.iff -Reference known_good.iff
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string]   $Tool,
  [Parameter(Mandatory)] [string[]] $ToolArgs,
  [Parameter(Mandatory)] [string]   $Output,
  [Parameter(Mandatory)] [string]   $Reference,
  [string] $NormalizeBanner
)

$ErrorActionPreference = 'Stop'

foreach ($p in @(@{n='Tool';v=$Tool}, @{n='Reference';v=$Reference})) {
  if (-not (Test-Path $p.v)) { Write-Error "byte-exact smoke: missing $($p.n): $($p.v)"; exit 2 }
}

# Run the tool that produces $Output.
& $Tool @ToolArgs
if ($LASTEXITCODE -ne 0) { Write-Error "byte-exact smoke: tool exited $LASTEXITCODE"; exit 1 }
if (-not (Test-Path $Output)) { Write-Error "byte-exact smoke: tool produced no output at $Output"; exit 1 }

function Get-CompareHash([string]$path) {
  if ($NormalizeBanner) {
    # Text path (generated C++): strip ONLY the approved banner regex, then hash.
    $text = [System.IO.File]::ReadAllText($path)
    $text = [regex]::Replace($text, $NormalizeBanner, '')
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    return [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')
  }
  return (Get-FileHash $path -Algorithm SHA256).Hash
}

$refHash = Get-CompareHash $Reference
$gotHash = Get-CompareHash $Output

if ($refHash -ne $gotHash) {
  # D-09: no fallback. Dump both streams beside the reference for triage.
  $dir = Split-Path -Parent $Reference
  Copy-Item $Output  (Join-Path $dir 'byte-exact-ACTUAL') -Force
  Copy-Item $Reference (Join-Path $dir 'byte-exact-EXPECTED') -Force
  Write-Error "BYTE-EXACT FAIL (D-09 gate finding): $Output != $Reference`n  expected $refHash`n  actual   $gotHash`n  dumped ACTUAL/EXPECTED to $dir"
  exit 1
}

Write-Host "BYTE-EXACT PASS: $Output == $Reference ($refHash)"
exit 0
