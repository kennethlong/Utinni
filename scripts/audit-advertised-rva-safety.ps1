#requires -Version 5.1
<#
audit-advertised-rva-safety.ps1  --  WS-3 static RVA-safety audit (SOURCE-ONLY, CI gate)

The authoritative net for the "hardcoded SWGEmu RVA reached on the advertised DX11 client"
crash class (e.g. generateHighestId -> worldSnapshotReaderWriter::openFile). Unlike
audit-utinni-rvas.ps1 (which cross-references a SWGEmu.exe BINARY on disk for byte drift),
this is PURE SOURCE analysis: no binary, no egress, deterministic -- so it runs in CI on the
self-hosted runner like the C++23-header gate in ci.yml.

WHAT IT DOES
  1. Scans UtinniCore/swg/**/*.{cpp,h} for hardcoded RVA literals (0x00400000-0x02000000) and
     memory:: sites, with // and /* */ comments stripped (so RVAs cited in prose don't count).
  2. Classifies each site on TWO axes (crew review 2026-06-25 -- patch/data are NOT the same as call):
       CALL   -- function-pointer cast `(pFn)0x...;`            (a slot that's CALLed)
       DATA   -- data-pointer cast / memory::read|write of 0x.. (a global the code reads/writes)
       PATCH  -- memory::nop|createJMP|patchAddress             (a code-mutation at 0x..)
       OTHER  -- anything else matching the literal regex
  3. Cross-maps each CALL symbol against the resolver's s_bindings[] (endpoints_bindings.cpp):
     BOUND   = the slot is overwritten by the resolver on the advertised client (safe there);
     UNBOUND = a raw literal the resolver never touches (garbage on advertised unless guarded).
  3b. GUARD INVARIANT (Goal A+): every Node*-producing function body in scene/world_snapshot.cpp
     must open with the offlineSnapshotUnavailable() gate (the reader/writer singleton is a raw
     SWGEmu RVA; see the section-2b comment). Fails CHECK/Report mode on an ungated body.
  4. RATCHET: compares the live site set against a committed BASELINE (the allowlist). The baseline
     grandfathers the ~320 pre-WS-3 literals (each row carries a Reason). CI FAILS if a NEW site
     appears that is not in the baseline -- forcing every new RVA to be consciously reviewed:
     guard it on isAdvertisedClient() (so it can't fire on advertised) or add a baseline row with a
     Reason. Sites in the baseline but gone from the tree are reported as prunable (never fail).

USAGE
    .\scripts\audit-advertised-rva-safety.ps1                 # CHECK mode (CI): exit 1 on new sites
    .\scripts\audit-advertised-rva-safety.ps1 -Report         # print the full classified inventory
    .\scripts\audit-advertised-rva-safety.ps1 -UpdateBaseline # regenerate the baseline from the tree

EXIT CODES:  0 = clean (no new sites);  1 = new unbaselined site(s) found / bad args.

The baseline lives at scripts/advertised-rva-baseline.tsv (TAB-separated: Key, Class, Bound, Reason).
The Key is file|RVA|symbol -- deliberately LINE-NUMBER-FREE so refactors that move code don't churn it.
#>

[CmdletBinding()]
param(
    [string]$SwgRoot = (Join-Path $PSScriptRoot '..\UtinniCore\swg'),
    [string]$BindingsFile = (Join-Path $PSScriptRoot '..\UtinniCore\swg\endpoints_bindings.cpp'),
    [string]$Baseline = (Join-Path $PSScriptRoot 'advertised-rva-baseline.tsv'),
    [switch]$Report,
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SwgRoot)) { throw "SwgRoot not found: $SwgRoot" }

$SwgRootFull = (Resolve-Path $SwgRoot).Path

# --- comment stripping (so RVAs cited in // and /* */ prose are NOT scanned) ----------------
# Returns the code-only text of a file as an array of lines (block-comment state carried across lines).
function Get-CodeLines {
    param([string]$Path)
    $out = New-Object System.Collections.Generic.List[string]
    $inBlock = $false
    foreach ($raw in [System.IO.File]::ReadLines($Path)) {
        $sb = New-Object System.Text.StringBuilder
        $i = 0
        $n = $raw.Length
        while ($i -lt $n) {
            if ($inBlock) {
                if ($i + 1 -lt $n -and $raw[$i] -eq '*' -and $raw[$i + 1] -eq '/') { $inBlock = $false; $i += 2 }
                else { $i++ }
                continue
            }
            if ($i + 1 -lt $n -and $raw[$i] -eq '/' -and $raw[$i + 1] -eq '/') { break } # rest of line is a comment
            if ($i + 1 -lt $n -and $raw[$i] -eq '/' -and $raw[$i + 1] -eq '*') { $inBlock = $true; $i += 2; continue }
            [void]$sb.Append($raw[$i]); $i++
        }
        $out.Add($sb.ToString())
    }
    return $out
}

# --- 1. parse the resolver's s_bindings[] for the set of BOUND symbol leaf-names --------------
# Rows look like:  {"group::name", (void**)&swg::sub::leafName},
# We capture leafName (the consumer slot the resolver overwrites). Best-effort cross-map key.
function Get-BoundLeafNames {
    param([string]$Path)
    $names = @{}
    if (-not (Test-Path $Path)) { return $names }
    $code = Get-CodeLines -Path $Path
    foreach ($line in $code) {
        $m = [regex]::Match($line, '\(void\*\*\)\s*&\s*(?:swg::)?[\w:]*?(\w+)\s*\}')
        if ($m.Success) { $names[$m.Groups[1].Value] = $true }
    }
    return $names
}

$boundLeaves = Get-BoundLeafNames -Path $BindingsFile

# --- 2. scan + classify ----------------------------------------------------------------------
function Classify-Site {
    param([string]$Line)
    if ($Line -match 'memory::(nopAddress|nop|createJMP|patchAddress)\b') { return 'PATCH' }
    if ($Line -match 'memory::(write|set|copy)\b') { return 'PATCH' }   # writes/mutations
    if ($Line -match 'memory::read\b') { return 'DATA' }
    # function-pointer typedef cast assignment: `pFoo foo = (pFoo)0x..;`  (the pFn typedef starts with p + Cap or is a using-alias)
    if ($Line -match '=\s*\(\s*p[A-Z]\w*\s*\)\s*0x') { return 'CALL' }
    if ($Line -match '\(\s*\w*[Ff]n\w*\s*\)\s*0x') { return 'CALL' }
    # data-pointer cast: `(SomeType*)0x..` or `*(T*)0x..` or `(Type*)0x..`
    if ($Line -match '\(\s*[\w:<>\s]+\*+\s*\)\s*0x') { return 'DATA' }
    if ($Line -match '\*\s*\(\s*[\w:<>\s]+\*+\s*\)\s*\(?\s*0x') { return 'DATA' }
    return 'OTHER'
}

# best-effort symbol: `Type symbol = (...)0x..`  OR  the leaf of a `swg::a::b(` call near the literal
function Get-SiteSymbol {
    param([string]$Line)
    $m = [regex]::Match($Line, '\b([A-Za-z_]\w*)\s*=\s*(?:\([^)]*\))?\s*0x')
    if ($m.Success) { return $m.Groups[1].Value }
    return ''
}

$files = Get-ChildItem -Path $SwgRootFull -Recurse -Include *.cpp, *.h -File
$sites = @{}   # key -> object

foreach ($f in $files) {
    $rel = $f.FullName.Substring($SwgRootFull.Length + 1)
    $code = Get-CodeLines -Path $f.FullName
    $lineNo = 0
    foreach ($line in $code) {
        $lineNo++
        $matches = [regex]::Matches($line, '0x[0-9A-Fa-f]{6,8}')
        foreach ($mm in $matches) {
            $val = 0
            if (-not [uint32]::TryParse($mm.Value.Substring(2), [System.Globalization.NumberStyles]::HexNumber, $null, [ref]$val)) { continue }
            # plausible SWG image range; below 0x400000 = ordinary constants, above 0x2000000 = not an RVA here.
            if ($val -lt 0x00400000 -or $val -gt 0x02000000) { continue }
            $rva = ('0x{0:X8}' -f $val)
            $sym = Get-SiteSymbol -Line $line
            $cls = Classify-Site -Line $line
            $bound = if ($cls -eq 'CALL' -and $sym -and $boundLeaves.ContainsKey($sym)) { 'BOUND' } else { '' }
            $key = '{0}|{1}|{2}' -f $rel, $rva, $sym
            if (-not $sites.ContainsKey($key)) {
                $sites[$key] = [pscustomobject]@{
                    Key = $key; File = $rel; RVA = $rva; Symbol = $sym
                    Class = $cls; Bound = $bound; Line = $lineNo; Text = $line.Trim()
                }
            }
        }
    }
}

$liveKeys = $sites.Keys | Sort-Object

# --- 2b. world_snapshot Node*-guard invariant (Goal A+ prerequisite, 2026-07-09) --------------
# The offline WorldSnapshotReaderWriter singleton is a hardcoded SWGEmu RVA (0x1913E94), so every
# raw nodeList/children walk in world_snapshot.cpp is garbage on the advertised client. Those
# walks were unreachable there only while the player's lookAt target resolved null (wave 1); the
# v16 lookAt-target accessor removes that property. Invariant: every Node*-producing function
# BODY in world_snapshot.cpp opens with the offlineSnapshotUnavailable() gate, before any member
# touch. Auto-catches future Node*-returning additions to the file.
$wsFile = Join-Path $SwgRootFull 'scene\world_snapshot.cpp'
$guardViolations = @()
if (Test-Path $wsFile) {
    $wsCode = Get-CodeLines -Path $wsFile
    for ($li = 0; $li -lt $wsCode.Count; $li++) {
        if ($wsCode[$li] -notmatch '^WorldSnapshotReaderWriter::Node\*\s+\S+\(') { continue }
        $defLine = $li + 1  # 1-based for the report
        # scan forward for the opening brace, then the first non-empty code line of the body
        $j = $li
        while ($j -lt $wsCode.Count -and $wsCode[$j] -notmatch '\{') { $j++ }
        $j++
        while ($j -lt $wsCode.Count -and $wsCode[$j].Trim() -eq '') { $j++ }
        if ($j -ge $wsCode.Count -or $wsCode[$j] -notmatch 'if\s*\(\s*offlineSnapshotUnavailable\s*\(\s*\)\s*\)') {
            $guardViolations += ('{0}: {1}' -f $defLine, $wsCode[$li].Trim())
        }
    }
} else {
    $guardViolations += 'scene\world_snapshot.cpp not found (invariant target missing)'
}

if ($guardViolations.Count -gt 0 -and -not $UpdateBaseline) {
    Write-Host ''
    Write-Host ("FAIL: {0} Node*-producing function(s) in world_snapshot.cpp do NOT open with the" -f $guardViolations.Count)
    Write-Host '      offlineSnapshotUnavailable() gate (raw 0x1913E94 walk reachable on advertised):'
    foreach ($v in $guardViolations) { Write-Host "    - $v" }
    Write-Host '  Fix: make `if (offlineSnapshotUnavailable()) return nullptr;` the first statement of the body.'
    exit 1
}

# --- 3. report mode --------------------------------------------------------------------------
if ($Report) {
    $sites.Values | Sort-Object Class, File, RVA |
        Format-Table File, RVA, Class, Bound, Symbol, @{N = 'Ln'; E = { $_.Line } } -AutoSize -Wrap
    $byClass = $sites.Values | Group-Object Class | Sort-Object Name
    Write-Host ''
    foreach ($g in $byClass) { Write-Host ("  {0,-6} {1}" -f $g.Name, $g.Count) }
    Write-Host ("  TOTAL  {0} sites in {1} files" -f $sites.Count, $files.Count)
    return
}

# --- 4. baseline update mode -----------------------------------------------------------------
if ($UpdateBaseline) {
    # PRESERVE existing Reason annotations for keys that still exist (manual reasons must survive a
    # regen -- crew review: reasons are durable, not wiped). Only genuinely NEW keys get the default.
    $existingReason = @{}
    if (Test-Path $Baseline) {
        foreach ($line in [System.IO.File]::ReadLines($Baseline)) {
            if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
            $parts = $line.Split("`t")
            if ($parts.Count -ge 4) { $existingReason[$parts[0]] = $parts[3] }
        }
    }
    $rows = New-Object System.Collections.Generic.List[string]
    $rows.Add("# WS-3 advertised-client RVA-safety baseline (allowlist). Maintained by audit-advertised-rva-safety.ps1.")
    $rows.Add("# Each row grandfathers a hardcoded RVA / memory site so CI fails only on NEW ones. Columns:")
    $rows.Add("#   Key (file|RVA|symbol)  <TAB>  Class  <TAB>  Bound  <TAB>  Reason")
    $rows.Add("# A new site must be EITHER guarded on isAdvertisedClient() (so it can't fire on advertised)")
    $rows.Add("# OR added here with a Reason. -UpdateBaseline PRESERVES existing Reasons; only new keys get")
    $rows.Add("# the grandfathered default. Bound is a best-effort leaf-name heuristic (may false-positive on")
    $rows.Add("# shared leaf names, e.g. objectTemplate::createObject vs worldsnapshot::createObject) -- it is")
    $rows.Add("# INFORMATIONAL ONLY; the ratchet gates purely on new-vs-baseline keys, never on Bound/Class.")
    foreach ($k in $liveKeys) {
        $s = $sites[$k]
        if ($existingReason.ContainsKey($k)) { $reason = $existingReason[$k] }
        else { $reason = 'grandfathered pre-WS-3 inventory ({0})' -f $s.Class }
        $rows.Add(("{0}`t{1}`t{2}`t{3}" -f $s.Key, $s.Class, $s.Bound, $reason))
    }
    [System.IO.File]::WriteAllLines($Baseline, $rows, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("Wrote baseline: {0} ({1} sites)" -f $Baseline, $liveKeys.Count)
    return
}

# --- 5. CHECK mode (CI gate) -----------------------------------------------------------------
if (-not (Test-Path $Baseline)) {
    throw "Baseline not found: $Baseline. Run with -UpdateBaseline to generate it (and commit it)."
}

$baseKeys = @{}
foreach ($line in [System.IO.File]::ReadLines($Baseline)) {
    if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }
    $key = $line.Split("`t")[0]
    $baseKeys[$key] = $true
}

$new = @()
foreach ($k in $liveKeys) { if (-not $baseKeys.ContainsKey($k)) { $new += $sites[$k] } }

$removed = @()
foreach ($k in $baseKeys.Keys) { if (-not $sites.ContainsKey($k)) { $removed += $k } }

if ($removed.Count -gt 0) {
    Write-Host ("::notice::{0} baseline RVA site(s) no longer in the tree (prunable via -UpdateBaseline):" -f $removed.Count)
    $removed | Sort-Object | ForEach-Object { Write-Host "    - $_" }
}

if ($new.Count -gt 0) {
    Write-Host ""
    Write-Host ("FAIL: {0} NEW hardcoded RVA / memory site(s) not in the baseline:" -f $new.Count)
    foreach ($s in ($new | Sort-Object Class, File, RVA)) {
        Write-Host ("    [{0}{1}] {2}:{3}  {4}  {5}" -f $s.Class, ($(if ($s.Bound) { '/' + $s.Bound } else { '' })), $s.File, $s.Line, $s.RVA, $s.Symbol)
        Write-Host ("        {0}" -f $s.Text)
    }
    Write-Host ""
    Write-Host "Each NEW site on the advertised DX11 client hits a SWGEmu literal that is GARBAGE there."
    Write-Host "Resolve EACH by one of:"
    Write-Host "  1. Guard its function on swg::endpoints::isAdvertisedClient() (degrade to no-op on advertised), OR"
    Write-Host "  2. If it is genuinely advertised-safe or SWGEmu-only-unreachable, add it to"
    Write-Host "     scripts/advertised-rva-baseline.tsv with a specific Reason (run -UpdateBaseline, then"
    Write-Host "     edit the Reason column to justify it -- do NOT leave the grandfathered default)."
    exit 1
}

Write-Host ("advertised-RVA-safety audit passed: {0} sites, all baselined ({1} files scanned)." -f $sites.Count, $files.Count)
exit 0
