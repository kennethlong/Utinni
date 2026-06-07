# Plan 14-04 Task 4 — structural verifier for MCP-SECURITY.md (Consensus #10: the doc verify
# must NOT be shallow — require DISTINCT T-14-NN ids AND the required headings/substrings, not a
# bare string count). PowerShell 5.1-safe (the self-hosted CI runner has 5.1 only). Exits 0 only
# if every check passes; otherwise prints what is missing and exits 1.
$ErrorActionPreference = 'Stop'

$doc = Join-Path $PSScriptRoot 'MCP-SECURITY.md'
if (-not (Test-Path $doc)) {
    Write-Host "FAIL: MCP-SECURITY.md not found at $doc"
    exit 1
}

$text = Get-Content -LiteralPath $doc -Raw
$missing = @()

# (1) >= 10 DISTINCT T-14-NN threat ids.
$ids = [System.Collections.Generic.HashSet[string]]::new()
foreach ($m in [regex]::Matches($text, 'T-14-\d+')) { [void]$ids.Add($m.Value) }
$distinct = $ids.Count
if ($distinct -lt 10) {
    $missing += "at least 10 distinct T-14-NN ids (found $distinct)"
}

# (2) Required headings / substrings (literal). These prove the 07-SECURITY.md structure + the
#     review-mandated content (5-layer model, apply-save ZERO-verbs exception, advisory caveat).
$required = @(
    '## Trust Boundaries',
    '## Threat Register',
    '## Accepted Risks',
    '5-layer',
    'apply-save',
    'advisory'
)
foreach ($r in $required) {
    if ($text.IndexOf($r, [System.StringComparison]::Ordinal) -lt 0) {
        $missing += "required substring: '$r'"
    }
}

if ($missing.Count -gt 0) {
    Write-Host "FAIL: MCP-SECURITY.md is missing:"
    foreach ($x in $missing) { Write-Host "  - $x" }
    exit 1
}

Write-Host "PASS: MCP-SECURITY.md structural verify ($distinct distinct T-14 ids; all required headings/substrings present)."
exit 0
