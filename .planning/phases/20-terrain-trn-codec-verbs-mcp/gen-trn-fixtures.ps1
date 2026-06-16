# One-off TGEN (.trn) fixture generator for Plan 20-04 (NOT part of CI; kept in the phase dir as the
# generator-of-record provenance trail). The net10 Utinni.Mcp.Tests project cannot reference the net472
# TgenFixtureSynthesizer (cross-TFM), so the bytes are GENERATED ONCE here from the shipped x86
# UtinniCoreDotNet.dll IFF writer and committed under Utinni.Mcp.Tests/Fixtures/trn/. Run once locally
# (under 32-bit PowerShell so the x86 UtinniCoreDotNet.dll loads):
#   & 'C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass `
#       -File .planning/phases/20-terrain-trn-codec-verbs-mcp/gen-trn-fixtures.ps1
# Re-run only if the synthesizer's TGEN shape changes; the committed bytes are the source of truth.
#
# The fixtures mirror TgenFixtureSynthesizer.WithTypedTag("BREC", <version>): a FORM TGEN whose single
# LYRS->LAYR holds one typed BREC boundary form. BREC is the ONE observed lineage divergence
# (TgenEraVersions: SWGEmu 0002 / Infinity 0003), so the low/high fixtures genuinely differ in version
# AND payload (the high arm carries the feather fields), exercising both lineages end-to-end.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$bin  = Join-Path $repo 'Utinni.Cli\bin\x86\Release\net472'
if (-not (Test-Path (Join-Path $bin 'UtinniCoreDotNet.dll'))) {
    $bin = Join-Path $repo 'Utinni.Cli\bin\Release\net472'
}
$fix  = Join-Path $repo 'Utinni.Mcp.Tests\Fixtures\trn'
New-Item -ItemType Directory -Force $fix | Out-Null

$asmCore = [System.Reflection.Assembly]::LoadFrom((Join-Path $bin 'UtinniCoreDotNet.dll'))
$nodeT   = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.MutableIffNode')
$docT    = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.MutableIffDocument')
$wrT     = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.IffWriter')

function Emit($name, $bytes) {
    [System.IO.File]::WriteAllBytes((Join-Path $fix $name), $bytes)
    Write-Host ("{0}: {1} bytes" -f $name, $bytes.Length)
}
function Int32Le([int]$v) { return [byte[]]@(($v -band 0xFF), (($v -shr 8) -band 0xFF), (($v -shr 16) -band 0xFF), (($v -shr 24) -band 0xFF)) }
function FloatLe([single]$v) {
    $b = [System.BitConverter]::GetBytes([single]$v)
    if (-not [System.BitConverter]::IsLittleEndian) { [System.Array]::Reverse($b) }
    return [byte[]]$b
}
function Concat([byte[][]]$parts) {
    $ms = New-Object System.IO.MemoryStream
    foreach ($p in $parts) { if ($p -and $p.Length -gt 0) { $ms.Write($p, 0, $p.Length) } }
    return [byte[]]$ms.ToArray()
}
function AddContainer($parent, $type, $sub) { return $nodeT.GetMethod('AddContainer').Invoke($parent, @($type, $sub)) }
function AddLeaf($parent, $type, [byte[]]$data) { [void]$nodeT.GetMethod('AddLeaf').Invoke($parent, @($type, [byte[]]$data)) }

# Build a FORM TGEN -> FORM 0000 -> FORM LYRS -> FORM 0003 (LAYR version) -> FORM BREC -> FORM <ver> -> DATA
function BuildBrecTgen([string]$brecVersion, [byte[]]$payload) {
    $tgen = $nodeT.GetMethod('NewContainer').Invoke($null, @('FORM', 'TGEN'))
    $body = AddContainer $tgen 'FORM' '0000'
    $lyrs = AddContainer $body 'FORM' 'LYRS'
    $layr = AddContainer $lyrs 'FORM' '0003'   # LAYR observed version (SWGEmu == Infinity)
    $brec = AddContainer $layr 'FORM' 'BREC'
    $ver  = AddContainer $brec 'FORM' $brecVersion
    AddLeaf $ver 'DATA' $payload
    $doc = [System.Activator]::CreateInstance($docT, @($tgen))
    return [byte[]]$wrT.GetMethod('Write', [type[]]@($docT)).Invoke($null, @($doc))
}

# Low arm (SWGEmu BREC 0002): [x0][z0][x1][z1] — no feather.
$lowPayload  = Concat @((FloatLe 0.0), (FloatLe 0.0), (FloatLe 10.0), (FloatLe 10.0))
# High arm (Infinity BREC 0003): the low payload + feather fn (int32) + feather distance (float).
$highPayload = Concat @($lowPayload, (Int32Le 0), (FloatLe 0.2))

Emit 'terrain_low.trn'  (BuildBrecTgen '0002' $lowPayload)
Emit 'terrain_high.trn' (BuildBrecTgen '0003' $highPayload)

# Malformed: a non-IFF / non-TGEN blob — decode-iff must reject it (exit 2), surfacing a tool error (#5).
Emit 'terrain_malformed.trn' ([byte[]]@(0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07))

Write-Host 'Done. Validate each via: utinni-cli decode-iff <fixture> (low/high => type:terrain; malformed => exit 2).'
