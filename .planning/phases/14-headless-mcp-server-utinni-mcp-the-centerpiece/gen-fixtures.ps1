# One-off fixture generator for Plan 14-04 (NOT part of CI; kept in the phase dir as the
# generator-of-record provenance trail). Loads the already-built net472 fixture builders and
# emits the minimal binary fixtures the net10 RoundTripTests consume. Run once locally:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/14-.../gen-fixtures.ps1
# Re-run only if a builder changes; the committed bytes are the source of truth thereafter.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$bin  = Join-Path $repo 'Utinni.Cli.Tests\bin\Release\net472'
$fix  = Join-Path $repo 'Utinni.Mcp.Tests\Fixtures'
New-Item -ItemType Directory -Force $fix | Out-Null

$asmCore  = [System.Reflection.Assembly]::LoadFrom((Join-Path $bin 'UtinniCoreDotNet.dll'))
$asmTests = [System.Reflection.Assembly]::LoadFrom((Join-Path $bin 'Utinni.Cli.Tests.dll'))

function Emit($name, $bytes) {
    [System.IO.File]::WriteAllBytes((Join-Path $fix $name), $bytes)
    Write-Host ("{0}: {1} bytes" -f $name, $bytes.Length)
}

# --- sample.tab : DTII datatable with editable cells (BuildV1AllTypes) ---
$dt = $asmTests.GetType('Utinni.Cli.Tests.Infrastructure.DataTableFixtureBuilder')
Emit 'sample.tab' ($dt.GetMethod('BuildV1AllTypes').Invoke($null, $null))

# --- sample.stf : string table with known keys (alpha/beta/gamma) for edit + read-back ---
$stf = $asmTests.GetType('Utinni.Cli.Tests.Infrastructure.StringTableFixtureBuilder')
Emit 'sample.stf' ($stf.GetMethod('BuildV1MultiEntry').Invoke($null, $null))

# --- sample.iff : a plain IFF FORM with a mutable leaf (use the IffBuilder) ---
$iffb = $asmTests.GetType('Utinni.Cli.Tests.Infrastructure.IffBuilder')
$leaf = $iffb.GetMethod('Leaf').Invoke($null, @('DATA', [byte[]]@(1,2,3,4)))
$form = $iffb.GetMethod('Form').Invoke($null, @('TEST', [byte[][]]@(,$leaf)))
Emit 'sample.iff' $form

# --- sample_ot.iff : minimal object template (FORM SHOT) with local overrides ---
# Built directly off the public MutableIffNode / MutableIffDocument / IffWriter API (mirrors the
# BuildTemplate shape in ApplySaveOtCommandTests). hitPoints/armor/scale int params, DERV base.
$nodeT = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.MutableIffNode')
$docT  = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.MutableIffDocument')
$wrT   = $asmCore.GetType('UtinniCoreDotNet.Formats.Iff.IffWriter')

function Int32Le([int]$v) { return [byte[]]@(($v -band 0xFF), (($v -shr 8) -band 0xFF), (($v -shr 16) -band 0xFF), (($v -shr 24) -band 0xFF)) }
function IntValueRegion([int]$v) {
    $ms = New-Object System.IO.MemoryStream
    $ms.WriteByte(1); $ms.WriteByte([byte][char]' ')
    $le = Int32Le $v; $ms.Write($le, 0, $le.Length)
    return $ms.ToArray()
}
function EncodeParam([string]$name, [byte[]]$valueRegion) {
    $ms = New-Object System.IO.MemoryStream
    $n = [System.Text.Encoding]::ASCII.GetBytes($name)
    $ms.Write($n, 0, $n.Length); $ms.WriteByte(0)
    if ($valueRegion -and $valueRegion.Length -gt 0) { $ms.Write($valueRegion, 0, $valueRegion.Length) }
    return $ms.ToArray()
}

$root = $nodeT.GetMethod('NewContainer').Invoke($null, @('FORM', 'SHOT'))
$derv = $nodeT.GetMethod('AddContainer').Invoke($root, @('FORM', 'DERV'))
$baseName = 'object/base.iff'
$bms = New-Object System.IO.MemoryStream
$bn = [System.Text.Encoding]::ASCII.GetBytes($baseName)
$bms.Write($bn, 0, $bn.Length); $bms.WriteByte(0)
[void]$nodeT.GetMethod('AddLeaf').Invoke($derv, @('XXXX', [byte[]]$bms.ToArray()))

$verForm = $nodeT.GetMethod('AddContainer').Invoke($root, @('FORM', '0000'))
[void]$nodeT.GetMethod('AddLeaf').Invoke($verForm, @('PCNT', [byte[]](Int32Le 3)))
[void]$nodeT.GetMethod('AddLeaf').Invoke($verForm, @('XXXX', [byte[]](EncodeParam 'hitPoints' ([byte[]](IntValueRegion 100)))))
[void]$nodeT.GetMethod('AddLeaf').Invoke($verForm, @('XXXX', [byte[]](EncodeParam 'armor'     ([byte[]](IntValueRegion 5)))))
[void]$nodeT.GetMethod('AddLeaf').Invoke($verForm, @('XXXX', [byte[]](EncodeParam 'scale'     ([byte[]](IntValueRegion 1)))))

$doc = [System.Activator]::CreateInstance($docT, @($root))
$otBytes = $wrT.GetMethod('Write', [type[]]@($docT)).Invoke($null, @($doc))
Emit 'sample_ot.iff' $otBytes

# --- sample_v0006.tre : SUPPORTED, NON-encrypted size-first v0006 archive ---
# (NOT EERT/"6000" which is the V6000 encrypted-class header.) Replicates TreFixtureBuilder's
# private BuildSizeFirstArchive("0006", ...) path verbatim — header tag "EERT"+"0006", 24-byte
# size-first records, one stored + one raw-deflate payload.
function NameBlock([string[]]$names) {
    $ms = New-Object System.IO.MemoryStream; $offs = @(); $cum = 0
    foreach ($n in $names) {
        $offs += $cum
        $nb = [System.Text.Encoding]::ASCII.GetBytes($n)
        $ms.Write($nb, 0, $nb.Length); $ms.WriteByte(0)
        $cum += $nb.Length + 1
    }
    return ,@($ms.ToArray(), $offs)
}
function RawDeflate([byte[]]$data) {
    $ms = New-Object System.IO.MemoryStream
    $ds = New-Object System.IO.Compression.DeflateStream($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
    $ds.Write($data, 0, $data.Length); $ds.Dispose()
    return $ms.ToArray()
}
function WI32([System.IO.Stream]$s, [int]$v) { $b = [System.BitConverter]::GetBytes($v); $s.Write($b, 0, 4) }

$names = @('object/tangible/foo.iff', 'string/en/bar.stf')
$payloads = @(
    [System.Text.Encoding]::ASCII.GetBytes('v0006 readable payload alpha -> object/tangible/foo.iff'),
    [System.Text.Encoding]::ASCII.GetBytes('v0006 readable payload beta  -> string/en/bar.stf')
)
$compression = @(1, 0) # entry0 raw-deflate, entry1 stored/none
$nbRes = NameBlock $names
$nameBlock = $nbRes[0]; $nameOffsets = $nbRes[1]
$n = $names.Length
$stored = @()
for ($i = 0; $i -lt $n; $i++) {
    if ($compression[$i] -eq 1) { $s = [byte[]](RawDeflate $payloads[$i]) } else { $s = [byte[]]$payloads[$i] }
    $stored += ,$s
}
$tocSize = $n * 24
$payloadStart = 36 + $tocSize + $nameBlock.Length
$tocMs = New-Object System.IO.MemoryStream
$pcur = $payloadStart
for ($i = 0; $i -lt $n; $i++) {
    WI32 $tocMs $payloads[$i].Length      # uncompressedSize
    WI32 $tocMs $pcur                      # offset
    WI32 $tocMs $compression[$i]           # compression
    WI32 $tocMs $stored[$i].Length         # compressedSize
    WI32 $tocMs 0                          # checksum
    WI32 $tocMs $nameOffsets[$i]           # nameOffset
    $pcur += $stored[$i].Length
}
$toc = $tocMs.ToArray()
$ms = New-Object System.IO.MemoryStream
$ms.Write([System.Text.Encoding]::ASCII.GetBytes('EERT'), 0, 4)
$ms.Write([System.Text.Encoding]::ASCII.GetBytes('0006'), 0, 4)
WI32 $ms $n                               # recordCount
WI32 $ms 36                               # infoOffset
WI32 $ms 0                                # infoCompression
WI32 $ms $tocSize                         # infoCompressedSize
WI32 $ms 0                                # nameCompression
WI32 $ms $nameBlock.Length                # nameCompressedSize
WI32 $ms $nameBlock.Length                # nameSize
$ms.Write($toc, 0, $toc.Length)
$ms.Write($nameBlock, 0, $nameBlock.Length)
for ($i = 0; $i -lt $n; $i++) { $ms.Write($stored[$i], 0, $stored[$i].Length) }
Emit 'sample_v0006.tre' $ms.ToArray()

# --- sample.tdf : minimal text template-definition for get_template_schema ---
$tdf = @'
# Minimal authored .tdf fixture (Plan 14-04). Mirrors Utinni.Cli.Tests Fixtures/tdf/mintest.tdf.
# Exercises the compile-definition verb + the ParamType/ListType schema vocabulary.
clientpath foo
compilerpath bar
id TEST
version 0
	int hitPoints
	float scale
	bool wantSawDust
	string objectName
	list int slots
'@
[System.IO.File]::WriteAllText((Join-Path $fix 'sample.tdf'), $tdf)
Write-Host 'sample.tdf written (text)'

Write-Host 'ALL FIXTURES EMITTED'
