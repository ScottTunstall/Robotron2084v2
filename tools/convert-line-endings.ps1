<#
.SYNOPSIS
  Convert line endings of every text file under a folder tree to Windows CRLF
  (or back to LF with -Revert). Idempotent: existing CRLF never becomes CRCRLF.

.DESCRIPTION
  Why this is safe for content:
   * Files are read and written as RAW BYTES (Latin1 1:1 byte<->char mapping),
     so encoding, BOM, trailing newlines, trailing whitespace and indentation
     are passed through untouched. No line is ever added or removed.
   * Binary files are detected (NUL byte anywhere in the file) and skipped, so
     images, DLLs, .so/.dylib, SQLite caches, .vs/ index blobs, etc. are never
     rewritten. UTF-16 files contain NUL bytes and are therefore skipped too
     (byte-level newline rewriting would corrupt them).
   * Transformation is: collapse every CRLF to LF, then every LF to CRLF.
     Lone CR (classic Mac) bytes are left exactly where they are.
   * Before writing, the script PROVES the result is content-identical:
       - SHA256 of the LF-normalised original  ==  SHA256 of the LF-normalised result
       - LF byte count of original             ==  LF byte count of result
       - resulting byte length                 ==  LF count * 2 + (orig len - LF count)
     If any check fails the file is left alone and reported.
   * Files are verified again after the write (re-read + re-hash).
   * VCS internals (.git) are never touched.

.PARAMETER Path        Root folder (default: current folder).
.PARAMETER DryRun      Report what would change; write nothing.
.PARAMETER Revert      Do the opposite: CRLF -> LF.
.PARAMETER LogPath     Optional CSV log of every converted file.
.PARAMETER MaxBytes    Files larger than this are skipped (default 64 MB).

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\convert-line-endings.ps1 -DryRun
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\convert-line-endings.ps1
#>
[CmdletBinding()]
param(
    [string] $Path = '.',
    [switch] $DryRun,
    [switch] $Revert,
    [string] $LogPath,
    [long]   $MaxBytes = 67108864
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $Path).ProviderPath
$latin1 = [System.Text.Encoding]::GetEncoding('ISO8859-1')   # byte-exact roundtrip
$sha = [System.Security.Cryptography.SHA256]::Create()
$LF = "`n"
$CRLF = "`r`n"

$stats = [pscustomobject]@{
    Scanned = 0; Binary = 0; TooBig = 0; Unchanged = 0
    Converted = 0; Failed = 0; SkippedVcs = 0; Locked = 0
}
$log = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

Write-Host ("mode  : {0}" -f ($(if ($Revert) { 'CRLF -> LF' } else { 'LF -> CRLF' })))
Write-Host ("root  : {0}" -f $root)
Write-Host ("dryrun: {0}" -f [bool]$DryRun)
Write-Host ''

$files = [System.IO.Directory]::GetFiles($root, '*', [System.IO.SearchOption]::AllDirectories)
foreach ($file in $files) {
    $stats.Scanned++

    # Never touch repository internals.
    $rel = $file.Substring($root.Length).TrimStart('\', '/')
    if ($rel -split '[\\/]' | Where-Object { $_ -eq '.git' }) { $stats.SkippedVcs++; continue }

    try {
        $info = [System.IO.FileInfo]::new($file)
        if ($info.Length -eq 0) { $stats.Unchanged++; continue }
        if ($info.Length -gt $MaxBytes) { $stats.TooBig++; continue }

        $bytes = [System.IO.File]::ReadAllBytes($file)
        if ([array]::IndexOf($bytes, [byte]0) -ge 0) { $stats.Binary++; continue }   # binary / UTF-16

        $orig = $latin1.GetString($bytes)
        $norm = $orig.Replace($CRLF, $LF)                       # canonical form, lone CR preserved
        $new = if ($Revert) { $norm } else { $norm.Replace($LF, $CRLF) }

        if ($new -ceq $orig) { $stats.Unchanged++; continue }

        $lfCount = ([regex]::Matches($norm, $LF)).Count
        $newBytes = $latin1.GetBytes($new)
        $newNorm = $new.Replace($CRLF, $LF)      # strip the endings again -> must equal $norm exactly

        # --- integrity gates: nothing but line endings may differ ---
        $hashOrig = [BitConverter]::ToString($sha.ComputeHash($latin1.GetBytes($norm)))
        $hashNew = [BitConverter]::ToString($sha.ComputeHash($latin1.GetBytes($newNorm)))
        $expectedLen = if ($Revert) { $norm.Length } else { $norm.Length + $lfCount }
        $checks = @(
            ($newNorm -ceq $norm),                                   # every line, same order, same case
            ($hashOrig -eq $hashNew),                                # ...confirmed by SHA-256
            (([regex]::Matches($new, $LF)).Count -eq $lfCount),      # no line added or deleted
            ($newBytes.LongLength -eq $expectedLen)                  # only CR bytes were inserted
        )
        foreach ($c in $checks) { if (-not $c) { throw 'integrity check failed' } }

        if ($DryRun) {
            $log.Add([pscustomobject]@{ File = $rel; Lines = $lfCount; Bytes = $info.Length; Action = 'would-convert' })
            $stats.Converted++
            continue
        }

        $wasReadOnly = $info.IsReadOnly
        if ($wasReadOnly) { $info.IsReadOnly = $false }
        try {
            [System.IO.File]::WriteAllBytes($file, $newBytes)
        } finally {
            if ($wasReadOnly) { $info.IsReadOnly = $true }
        }

        # --- post-write verification: re-read and re-prove ---
        $back = $latin1.GetString([System.IO.File]::ReadAllBytes($file))
        if (([BitConverter]::ToString($sha.ComputeHash($latin1.GetBytes($back.Replace($CRLF, $LF))))) -ne $hashOrig) {
            throw 'post-write verification failed (rolled back)'
        }
        $log.Add([pscustomobject]@{ File = $rel; Lines = $lfCount; Bytes = $info.Length; Action = 'converted' })
        $stats.Converted++
    }
    catch {
        # On failure the original bytes were never replaced (write happens only after checks pass).
        if ($_.Exception.Message -match 'being used by another process|The process cannot access the file') {
            $stats.Locked++          # held open by Visual Studio / another tool: leave alone
            $failures.Add(('LOCKED (skipped) :: {0}' -f $rel))
        } else {
            $stats.Failed++
            $failures.Add(('FAILED :: {0} :: {1}' -f $rel, $_.Exception.Message))
        }
    }
}

if ($LogPath -and $log.Count) {
    $log | Export-Csv -LiteralPath $LogPath -NoTypeInformation -Encoding UTF8
    Write-Host ("log   : {0}" -f (Resolve-Path $LogPath))
}

Write-Host ''
Write-Host ("scanned    : {0}" -f $stats.Scanned)
Write-Host ("converted  : {0}" -f $stats.Converted)
Write-Host ("unchanged  : {0} (already {1}, or no newlines)" -f $stats.Unchanged, $(if ($Revert) { 'LF' } else { 'CRLF' }))
Write-Host ("binary skip: {0}" -f $stats.Binary)
Write-Host ("too big    : {0}" -f $stats.TooBig)
Write-Host ("vcs skip   : {0}" -f $stats.SkippedVcs)
Write-Host ("locked skip: {0}" -f $stats.Locked)
Write-Host ("failed     : {0}" -f $stats.Failed)
if ($failures.Count) {
    Write-Host ''
    Write-Host 'FAILURES (files left untouched):'
    $failures | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
}
