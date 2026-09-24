#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the mechanical convention scans of docs/coding-standards.md ("Mechanical scans").

.DESCRIPTION
    Every scan below flags something the codebase has decided not to have. The script prints one
    line per hit and exits 1 when there is any, so it can be wired into a review or a task.

    STR-3  more than one top-level type in a .cs file (one type per file, notes §121).
    NUM-2  the arcade clock written as a literal: a timer that adds 5 per port tick or compares
           against 6 for a ROM frame, or a name that calls the clock unit a "fifth" or a "sixth"
           (Core/ArcadeClock is the one definition).

.EXAMPLE
    ./tools/lint-conventions.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

# obj/ and bin/ hold generated sources (AssemblyInfo, the .g.cs files), not hand-written code.
$sourceFiles = Get-ChildItem -Path 'src', 'tests' -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

$hits = [System.Collections.Generic.List[string]]::new()

# ---- STR-3: one top-level type per file -------------------------------------
# A top-level declaration starts in the first column; a nested one is indented.
$typePattern = '^(public|internal)\s+((sealed|abstract|static|readonly|partial|file)\s+)*(class|record|enum|interface|struct)\b'
foreach ($file in $sourceFiles)
{
    $declarations = @(Select-String -LiteralPath $file.FullName -Pattern $typePattern)
    if ($declarations.Count -gt 1)
    {
        $relative = Resolve-Path -LiteralPath $file.FullName -Relative
        $hits.Add("STR-3 $relative holds $($declarations.Count) top-level types")
    }
}

# ---- NUM-2: the clock written as a literal ----------------------------------
# 5 is one port tick's worth of clock units and 6 is one ROM frame's; both live on ArcadeClock.
$clockPattern = '(\+=|-=) 5;|\* 6\b|>= 6\b|< 6\b|= 6;|SixthsPer|Fifths|_sixths|_fifths'
foreach ($file in $sourceFiles)
{
    foreach ($match in @(Select-String -LiteralPath $file.FullName -Pattern $clockPattern))
    {
        $relative = Resolve-Path -LiteralPath $file.FullName -Relative
        $hits.Add("NUM-2 ${relative}:$($match.LineNumber) $($match.Line.Trim())")
    }
}

if ($hits.Count -eq 0)
{
    Write-Host 'lint-conventions: no hits.'
    exit 0
}

$hits | Sort-Object | ForEach-Object { Write-Host $_ }
Write-Host "lint-conventions: $($hits.Count) hit(s)."
exit 1
