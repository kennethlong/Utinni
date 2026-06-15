<#
.SYNOPSIS
    CPPS-01 clang-capability spike: tabulate each installed MSVC toolset's
    yvals_core.h clang-version gate against CppSharp's bundled clang, and
    print the mechanical negative-result conclusion.

.DESCRIPTION
    This is a REPORTING / TABULATION tool. It is read-only: it reads
    yvals_core.h from each installed MSVC toolset, extracts the
    `#if __clang_major__ < N` gate value, and tabulates N against the
    clang versions CppSharp ships (11 in the vendored 0.10.5 drop; 19 in
    the latest released CppSharp v1.2). It NEVER throws and NEVER modifies
    anything -- a missing toolset is reported as "(not found)", not an error.

    Mechanical conclusion: the vendored clang is 11 and the newest released
    clang is 19; the VS 2026 v145 (MSVC 14.5x) STL gate hard-requires clang
    20+. Since 11 < 20 AND 19 < 20, no released CppSharp can parse the v145
    STL -- which RE-SETS the phase acceptance from "retire the redirect" to
    "harden the redirect". The supported binding-generation config is the
    VS 2019 14.29 parser-include redirect (clang 11 explicitly accepted by
    the 14.29 STL gate).

    See docs/ai/cppsharp-v145-redirect.md for the supported-config writeup.

    PowerShell 5.1 safe. Toolset resolution mirrors the env-var / vswhere /
    default-path probe discipline in UtinniCoreDotNetGen/Program.cs (D-01
    reuse-don't-regress) -- NO MSVC install path is hard-coded.

.NOTES
    The vendored CppSharp's required-clang is 11; the latest released
    CppSharp v1.2 (2025-11-19) ships clang 19. Both are below the clang-20
    floor the v145 STL requires.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

# CppSharp clang capabilities (committed reference points -- not network-probed).
$VendoredCppSharpClang = 11   # external/CppSharp/lib (0.10.5, "based on LLVM 11.0.0")
$LatestReleasedCppSharpClang = 19   # CppSharp v1.2 (2025-11-19)

function Resolve-MsvcRoots {
    # Returns a list of "VC\Tools\MSVC" roots across every located VS install.
    # Resolution order mirrors Program.cs: env override -> vswhere -> default probe.
    $roots = New-Object System.Collections.Generic.List[string]

    # 1) Explicit env override for the redirect-target VS 2019 install.
    $envRoot = $env:UTINNI_VS2019_ROOT
    if ($envRoot -and (Test-Path $envRoot)) {
        $msvc = Join-Path $envRoot 'VC\Tools\MSVC'
        if (Test-Path $msvc) { [void]$roots.Add($msvc) }
    }

    # 2) vswhere discovery (canonical Microsoft mechanism). -products * spans
    #    BuildTools + Community/Pro/Enterprise + any prerelease/Insiders install.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        try {
            $installPaths = & $vswhere -prerelease -products * -property installationPath 2>$null
            foreach ($p in $installPaths) {
                $p = $p.Trim()
                if (-not $p) { continue }
                $msvc = Join-Path $p 'VC\Tools\MSVC'
                if ((Test-Path $msvc) -and -not $roots.Contains($msvc)) { [void]$roots.Add($msvc) }
            }
        } catch {
            # Fall through to the default-path probe; never throw.
        }
    }

    # 3) Default install-path fallback (VS 2019 redirect target + common VS roots).
    $defaultCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Community\VC\Tools\MSVC'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Professional\VC\Tools\MSVC'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Enterprise\VC\Tools\MSVC')
    )
    foreach ($c in $defaultCandidates) {
        if ((Test-Path $c) -and -not $roots.Contains($c)) { [void]$roots.Add($c) }
    }

    return $roots
}

function Get-YvalsClangGate {
    # Reads the `__clang_major__ < N` gate out of a toolset's yvals_core.h.
    # Returns the integer N, or $null when the toolset has no such gate / no header.
    param([string]$ToolsetDir)

    $header = Join-Path $ToolsetDir 'include\yvals_core.h'
    if (-not (Test-Path $header)) { return $null }

    # Match e.g. `#if __clang_major__ < 20` or `#if __clang_major__ < 11`.
    $match = Select-String -Path $header -Pattern '__clang_major__\s*<\s*(\d+)' -ErrorAction SilentlyContinue |
             Select-Object -First 1
    if (-not $match) { return $null }
    return [int]$match.Matches[0].Groups[1].Value
}

Write-Host ''
Write-Host '=== CPPS-01: CppSharp clang-capability spike (read-only report) ==='
Write-Host ''
Write-Host ('CppSharp clang capability: vendored 0.10.5 = clang {0}; latest released v1.2 = clang {1}.' -f $VendoredCppSharpClang, $LatestReleasedCppSharpClang)
Write-Host ''

$roots = Resolve-MsvcRoots
$rows = New-Object System.Collections.Generic.List[object]

foreach ($root in $roots) {
    Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $gate = Get-YvalsClangGate -ToolsetDir $_.FullName

        # WR-05: $null-on-the-left equality (PowerShell-recommended — avoids surprising
        # element-wise comparison if the right side is ever a collection). Compute the
        # report-column strings directly instead of relying on if-as-expression assignment.
        if ($null -ne $gate) {
            $requiredClang  = $gate
            $vendoredParses = if ($VendoredCppSharpClang -ge $gate) { 'YES' } else { 'NO' }
            $latestParses   = if ($LatestReleasedCppSharpClang -ge $gate) { 'YES' } else { 'NO' }
        } else {
            $requiredClang  = '(no gate)'
            $vendoredParses = 'n/a'
            $latestParses   = 'n/a'
        }

        $rows.Add([pscustomobject]@{
            MsvcVersion        = $_.Name
            RequiredClang      = $requiredClang
            VendoredParses     = $vendoredParses
            LatestReleaseParses = $latestParses
        })
    }
}

if ($rows.Count -eq 0) {
    Write-Host 'No MSVC toolsets located (env / vswhere / default-probe all empty). Report is empty but valid.'
} else {
    $rows |
        Sort-Object MsvcVersion |
        Format-Table -AutoSize MsvcVersion, RequiredClang,
            @{ Label = 'clang11 (vendored) parses?'; Expression = { $_.VendoredParses } },
            @{ Label = 'clang19 (latest rel) parses?'; Expression = { $_.LatestReleaseParses } } |
        Out-String | Write-Host
}

# Mechanical conclusion. The v145 (14.5x) STL gate is clang-20-or-newer; the
# redirect target (14.29) gate is clang-11-or-newer.
$v145Rows = $rows | Where-Object { $_.MsvcVersion -like '14.5*' -and $_.RequiredClang -ne '(no gate)' }
$redirectRows = $rows | Where-Object { $_.MsvcVersion -like '14.29*' -and $_.RequiredClang -ne '(no gate)' }

Write-Host '--- Conclusion ---'
if ($v145Rows) {
    $v145Gate = ($v145Rows | Select-Object -First 1).RequiredClang
    Write-Host ('v145 (MSVC 14.5x) STL requires clang {0}+. Vendored CppSharp clang {1} < {0}, and latest released CppSharp clang {2} < {0}.' -f $v145Gate, $VendoredCppSharpClang, $LatestReleasedCppSharpClang)
} else {
    Write-Host ('v145 (MSVC 14.5x) STL is documented to require clang 20+ (no 14.5x toolset located on this box to re-read). Vendored CppSharp clang {0} < 20, and latest released CppSharp clang {1} < 20.' -f $VendoredCppSharpClang, $LatestReleasedCppSharpClang)
}
Write-Host 'Therefore NO released CppSharp parses the v145 STL.'
if ($redirectRows) {
    $redirectGate = ($redirectRows | Select-Object -First 1).RequiredClang
    Write-Host ('The VS 2019 14.29 STL gate accepts clang {0}+ -- the vendored CppSharp clang {1} satisfies it. The 14.29 parser-include redirect is the SUPPORTED binding-generation config.' -f $redirectGate, $VendoredCppSharpClang)
} else {
    Write-Host 'The VS 2019 14.29 STL gate accepts clang 11+ (no 14.29 toolset located on this box to re-read) -- the vendored CppSharp clang 11 satisfies it. The 14.29 parser-include redirect is the SUPPORTED binding-generation config.'
}
Write-Host 'Phase acceptance is RE-SET from "retire the redirect" to "harden the redirect".'
Write-Host 'See docs/ai/cppsharp-v145-redirect.md for the supported-config writeup.'
Write-Host ''

exit 0
