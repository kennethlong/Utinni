#requires -Version 5.1
<#
audit-utinni-rvas.ps1

Cross-references every hardcoded SWGEmu RVA used by UtinniCore against the
bytes actually present at that address in the current SWGEmu.exe binary on
disk. Flags RVAs whose bytes don't look like a sensible function entry --
the kind of drift that turns a clean detour into a redirect to garbage.

Usage:
    .\scripts\audit-utinni-rvas.ps1
    .\scripts\audit-utinni-rvas.ps1 -ExePath D:\SWGEmu-Client\SWGEmu\SWGEmu.exe
    .\scripts\audit-utinni-rvas.ps1 -OutCsv .\rva-audit.csv
    .\scripts\audit-utinni-rvas.ps1 -OnlySuspicious   # only print drift candidates

Output columns:
  RVA          | as written in Utinni source
  Source       | file:line where it's hardcoded
  Symbol       | C++ variable/typedef name (best-effort regex extract)
  Padding      | the 4 bytes IMMEDIATELY BEFORE the RVA (alignment fill)
  Prologue     | the first 16 bytes AT the RVA
  Verdict      | OK | SUSPICIOUS | UNMAPPED
  Notes        | why we flagged it

A healthy hook target looks like:
  - Padded by CC CC CC CC (int 3 fill) or 90 90 90 90 (NOP fill) on the previous bytes.
  - First byte at the RVA is a common MSVC prologue: 55 (push ebp), 53/56/57 (push ebx/esi/edi),
    81 EC / 83 EC (sub esp, imm), 6A (push imm8), B8 (mov eax, imm), or a 5-byte hot-patch nop
    (8B FF / 90 90 90 90 90).

A SUSPICIOUS hook target:
  - No alignment padding before it (mid-function).
  - First byte is mid-instruction garbage (e.g. random ModR/M bytes, immediates).
  - Common red flag: EB FE (jmp self) -- meaning SWGEmu already patched it, like 0x0131DC7A.

UNMAPPED: the RVA falls outside any loaded PE section.
#>

[CmdletBinding()]
param(
    [string]$ExePath = 'D:\SWGEmu-Client\SWGEmu\SWGEmu.exe',
    [string]$UtinniRoot = (Join-Path $PSScriptRoot '..\UtinniCore'),
    [string]$OutCsv,
    [switch]$OnlySuspicious
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExePath))    { throw "ExePath not found: $ExePath" }
if (-not (Test-Path $UtinniRoot)) { throw "UtinniRoot not found: $UtinniRoot" }

# --- 1. Parse the PE header so we can map RVA -> file offset --------------

function Read-PESections {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $br    = New-Object System.IO.BinaryReader([System.IO.MemoryStream]::new($bytes))

    # DOS header: e_lfanew is at offset 0x3C
    $br.BaseStream.Position = 0x3C
    $peOffset = $br.ReadInt32()

    # PE signature ("PE\0\0")
    $br.BaseStream.Position = $peOffset
    $sig = $br.ReadUInt32()
    if ($sig -ne 0x00004550) { throw "Not a PE file: $Path" }

    # COFF header
    $machine              = $br.ReadUInt16()
    $numberOfSections     = $br.ReadUInt16()
    $br.ReadUInt32() | Out-Null   # TimeDateStamp
    $br.ReadUInt32() | Out-Null   # PointerToSymbolTable
    $br.ReadUInt32() | Out-Null   # NumberOfSymbols
    $sizeOfOptionalHeader = $br.ReadUInt16()
    $br.ReadUInt16() | Out-Null   # Characteristics

    # Optional header: read ImageBase
    $optStart = $br.BaseStream.Position
    $magic = $br.ReadUInt16()
    if ($magic -ne 0x010B) { throw "Expected PE32 (0x010B), got 0x{0:X4}" -f $magic }
    # Skip to ImageBase at +28 from optStart (for PE32)
    $br.BaseStream.Position = $optStart + 28
    $imageBase = $br.ReadUInt32()

    # Section table starts right after optional header
    $br.BaseStream.Position = $optStart + $sizeOfOptionalHeader

    $sections = @()
    for ($i = 0; $i -lt $numberOfSections; $i++) {
        $nameBytes  = $br.ReadBytes(8)
        $name       = ([System.Text.Encoding]::ASCII.GetString($nameBytes)).TrimEnd([char]0)
        $virtSize   = $br.ReadUInt32()
        $virtAddr   = $br.ReadUInt32()   # RVA of section
        $rawSize    = $br.ReadUInt32()
        $rawPtr     = $br.ReadUInt32()   # File offset of section
        $br.ReadUInt32() | Out-Null      # PointerToRelocations
        $br.ReadUInt32() | Out-Null      # PointerToLineNumbers
        $br.ReadUInt16() | Out-Null      # NumberOfRelocations
        $br.ReadUInt16() | Out-Null      # NumberOfLineNumbers
        $chars      = $br.ReadUInt32()

        $sections += [pscustomobject]@{
            Name      = $name
            VirtSize  = $virtSize
            VirtAddr  = $virtAddr           # relative to ImageBase
            RawSize   = $rawSize
            RawPtr    = $rawPtr
            Chars     = $chars
        }
    }

    return [pscustomobject]@{
        ImageBase = $imageBase
        Sections  = $sections
        Bytes     = $bytes
    }
}

function Convert-VAToFileOffset {
    param([uint32]$VA, $Pe)

    $rva = $VA - $Pe.ImageBase
    foreach ($s in $Pe.Sections) {
        $start = $s.VirtAddr
        $end   = $s.VirtAddr + [Math]::Max($s.VirtSize, $s.RawSize)
        if ($rva -ge $start -and $rva -lt $end) {
            return ($s.RawPtr + ($rva - $s.VirtAddr))
        }
    }
    return $null   # unmapped
}

# --- 2. Scrape every hardcoded RVA out of UtinniCore/swg/* ---------------

function Get-UtinniRVAs {
    param([string]$Root)

    $hits = @()
    $files = Get-ChildItem -Path $Root -Recurse -Include *.cpp,*.h -File `
                | Where-Object { $_.FullName -match '\\swg\\' }

    foreach ($f in $files) {
        $lineNo = 0
        foreach ($line in [System.IO.File]::ReadLines($f.FullName)) {
            $lineNo++
            # match cast-style:    SomeName name = (Type)0x00ABCDEF;
            # also catches:        constexpr swgptr foo = 0x00ABCDEF;
            # also catches:        memory::nopAddress(0x00ABCDEF, ...)
            $matches = [regex]::Matches($line, '0x00[0-9A-Fa-f]{6}')
            foreach ($m in $matches) {
                $va = [Convert]::ToUInt32($m.Value, 16)
                # Tiny addresses like 0x00000080 are usually constants, not RVAs.
                # Stick to plausible code-section range (0x00400000+).
                if ($va -lt 0x00400000) { continue }

                # Best-effort symbol extraction
                $symbol = ''
                $assign = [regex]::Match($line, '\b([A-Za-z_]\w*)\s*=\s*(?:\([^)]+\))?\s*0x')
                if ($assign.Success) { $symbol = $assign.Groups[1].Value }
                elseif ($line -match 'memory::') { $symbol = '(memory patch)' }

                $hits += [pscustomobject]@{
                    VA      = $va
                    RVA     = ('0x{0:X8}' -f $va)
                    Source  = '{0}:{1}' -f ($f.FullName.Substring($Root.Length+1)), $lineNo
                    Symbol  = $symbol
                    Line    = $line.Trim()
                }
            }
        }
    }
    # de-dupe by (RVA, Source) -- a single line can mention the same RVA twice
    return $hits | Group-Object { '{0}|{1}' -f $_.RVA, $_.Source } | ForEach-Object { $_.Group[0] }
}

# --- 3. Classify each RVA's bytes ----------------------------------------

function Classify-Prologue {
    param([byte[]]$Padding, [byte[]]$Prologue)

    $notes = New-Object System.Collections.Generic.List[string]
    $verdict = 'OK'

    # EB FE = SWGEmu jmp-self patch (like 0x0131DC7A)
    if ($Prologue.Length -ge 2 -and $Prologue[0] -eq 0xEB -and $Prologue[1] -eq 0xFE) {
        return @{ Verdict = 'SUSPICIOUS'; Notes = 'EB FE jmp-self -- SWGEmu runtime patch present' }
    }

    # Common MSVC function prologue first bytes
    $f0 = $Prologue[0]
    $f1 = if ($Prologue.Length -ge 2) { $Prologue[1] } else { 0 }
    $okFirst = @(0x55, 0x53, 0x56, 0x57, 0x83, 0x81, 0x6A, 0x68, 0xB8, 0xE9, 0xEB, 0xA1, 0x33, 0x8A, 0x8B)

    $looksLikeProlog = $okFirst -contains $f0
    # Hot-patch prologue: 8B FF (mov edi, edi)
    if ($f0 -eq 0x8B -and $f1 -eq 0xFF) { $looksLikeProlog = $true }

    if (-not $looksLikeProlog) {
        $notes.Add('First byte 0x{0:X2} is not a typical MSVC prologue opcode' -f $f0)
        $verdict = 'SUSPICIOUS'
    }

    # Padding check -- alignment fill before a function should be CC or 90
    if ($Padding -and $Padding.Length -ge 1) {
        $allFill = $true
        foreach ($b in $Padding) {
            if ($b -ne 0xCC -and $b -ne 0x90 -and $b -ne 0x00) { $allFill = $false; break }
        }
        if (-not $allFill) {
            $notes.Add('No alignment padding before RVA -- may be mid-function')
            if ($verdict -eq 'OK') { $verdict = 'SUSPICIOUS' }
        }
    }

    return @{ Verdict = $verdict; Notes = ($notes -join '; ') }
}

# --- 4. Run ---------------------------------------------------------------

Write-Host "Reading PE: $ExePath"
$pe = Read-PESections -Path $ExePath
Write-Host ("  ImageBase = 0x{0:X8}, {1} sections" -f $pe.ImageBase, $pe.Sections.Count)
foreach ($s in $pe.Sections) {
    Write-Host ("    {0,-8} VA=0x{1:X8} VirtSize=0x{2:X8} RawPtr=0x{3:X8}" `
                -f $s.Name, ($pe.ImageBase + $s.VirtAddr), $s.VirtSize, $s.RawPtr)
}

Write-Host "`nScraping Utinni source for hardcoded RVAs..."
$rvas = Get-UtinniRVAs -Root (Resolve-Path $UtinniRoot).Path
Write-Host ("  Found {0} unique RVA references." -f $rvas.Count)

# Spot-check the known halt address so we can confirm the script flags it.
$probe = [pscustomobject]@{
    VA     = 0x0131DC7A
    RVA    = '0x0131DC7A'
    Source = '(probe)'
    Symbol = '(known halt site)'
    Line   = 'EB FE patch site -- must report SUSPICIOUS'
}
$rvas = @($probe) + $rvas

$results = foreach ($r in $rvas) {
    $off = Convert-VAToFileOffset -VA $r.VA -Pe $pe
    if ($null -eq $off) {
        [pscustomobject]@{
            RVA      = $r.RVA
            Source   = $r.Source
            Symbol   = $r.Symbol
            Padding  = ''
            Prologue = ''
            Verdict  = 'UNMAPPED'
            Notes    = 'RVA outside all PE sections'
        }
        continue
    }

    # Read 4 bytes BEFORE (alignment fill) and 16 bytes AT the RVA
    $padStart = [Math]::Max(0, $off - 4)
    $padLen   = $off - $padStart
    $padding  = if ($padLen -gt 0) { $pe.Bytes[$padStart..($off-1)] } else { @() }
    $prologue = $pe.Bytes[$off..([Math]::Min($pe.Bytes.Length-1, $off+15))]

    $cls = Classify-Prologue -Padding $padding -Prologue $prologue

    [pscustomobject]@{
        RVA      = $r.RVA
        Source   = $r.Source
        Symbol   = $r.Symbol
        Padding  = ($padding  | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
        Prologue = ($prologue | ForEach-Object { '{0:X2}' -f $_ }) -join ' '
        Verdict  = $cls.Verdict
        Notes    = $cls.Notes
    }
}

if ($OnlySuspicious) {
    $results = $results | Where-Object { $_.Verdict -ne 'OK' }
}

# Sort by verdict (suspicious first), then by RVA
$results = $results | Sort-Object @{Expression='Verdict'; Descending=$true}, RVA

$results | Format-Table -AutoSize -Wrap

if ($OutCsv) {
    $results | Export-Csv -Path $OutCsv -NoTypeInformation -Encoding UTF8
    Write-Host "`nWrote CSV: $OutCsv"
}

# --- 5. Summary ---------------------------------------------------------------

$ok          = ($results | Where-Object Verdict -eq 'OK').Count
$suspicious  = ($results | Where-Object Verdict -eq 'SUSPICIOUS').Count
$unmapped    = ($results | Where-Object Verdict -eq 'UNMAPPED').Count

Write-Host ""
Write-Host ("Summary: {0} OK, {1} SUSPICIOUS, {2} UNMAPPED, {3} total" `
            -f $ok, $suspicious, $unmapped, $results.Count)

if ($suspicious -gt 0 -or $unmapped -gt 0) {
    Write-Host "`nNext: inspect SUSPICIOUS entries first. The probe row for 0x0131DC7A"
    Write-Host "should appear SUSPICIOUS with 'EB FE jmp-self' note -- that confirms the"
    Write-Host "classifier is working. Any OTHER suspicious entries are your drift candidates."
}
