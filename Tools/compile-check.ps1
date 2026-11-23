<#
Static compile gate for the Reef Unity project (no Unity process required).
--------------------------------------------------------------------
How it works: reuse Unity's own generated csc response files
(Library/Bee/artifacts/*/Assembly-CSharp.rsp) for every compiler switch and
assembly reference, then recompile all C# under Assets with the Roslyn csc.dll
that ships with the dotnet SDK. This catches syntax errors, API misuse,
name collisions and missing references WITHOUT launching Unity.

Usage (Windows PowerShell 5.1, execution policy bypass needed):
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\compile-check.ps1
  ... -RuntimeOnly     only the runtime assembly
  ... -Quiet           only the summary and the error list

Exit code: 0 = pass, 1 = compile errors, 2 = environment problem.
Artifacts go to Tools\.compilecheck\ (never Library\, which the running
editor owns).
#>
[CmdletBinding()]
param(
    [string]$Project = 'F:\Projects\Reef',
    [switch]$RuntimeOnly,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
function Info($m) { if (-not $Quiet) { Write-Host $m } }
function Die($m, $code) { Write-Host "COMPILE GATE FAILED: $m" -ForegroundColor Red; exit $code }

# --- 1. locate Roslyn csc.dll from the dotnet SDK ---------------------------
$sdkRoot = 'C:\Program Files\dotnet\sdk'
$csc = Get-ChildItem $sdkRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'Roslyn\bincore\csc.dll' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { Die 'Roslyn csc.dll not found under the dotnet SDK' 2 }

# --- 2. locate the response files Unity generated ---------------------------
$dag = Get-ChildItem (Join-Path $Project 'Library\Bee\artifacts') -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'Assembly-CSharp.rsp') } | Select-Object -First 1
if (-not $dag) { Die 'Assembly-CSharp.rsp not found - compile once inside the editor first' 2 }

# one directory per invocation: several teammates may run this gate at the same time
$outDir = Join-Path $Project ('Tools\.compilecheck\' + $PID)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$logPath = Join-Path $outDir 'compile.log'
$noBom = New-Object System.Text.UTF8Encoding($false)

function Get-UnitySwitches([string]$rspPath) {
    # keep Unity's switches and references, drop output/doc/analyzer plumbing
    Get-Content $rspPath |
        Where-Object { $_ -match '^\s*[-/]' } |
        Where-Object { $_ -notmatch '^\s*[-/](out|refout|doc|additionalfile|analyzer|errorlog|resource|linkresource|ruleset|warnaserror)' } |
        # drop references to previous build outputs: this run recompiles them and
        # passes them explicitly, otherwise CS1704 duplicate-assembly errors
        Where-Object { $_ -notmatch 'Assembly-CSharp(-Editor)?\.dll' }
}

function Get-Sources([string]$root, [bool]$editorAssembly) {
    $all = Get-ChildItem $root -Recurse -Filter *.cs -File
    if ($editorAssembly) { $all | Where-Object { $_.FullName -match '\\Editor\\' } | ForEach-Object { $_.FullName } }
    else { $all | Where-Object { $_.FullName -notmatch '\\Editor\\' } | ForEach-Object { $_.FullName } }
}

function Invoke-Pass([string]$name, [string]$srcRsp, [string]$outDll, [string[]]$sources, [string[]]$extraRefs) {
    if ($sources.Count -eq 0) { Info "[$name] no sources, skipped"; return @{ Name = $name; Errors = 0; Warnings = 0; ErrorLines = @(); Exit = 0 } }
    $rsp = Join-Path $outDir "$name.rsp"
    $lines = @(Get-UnitySwitches $srcRsp)
    $lines += "-out:`"$outDll`""
    foreach ($r in $extraRefs) { $lines += "-r:`"$r`"" }
    $lines += ($sources | ForEach-Object { '"' + $_ + '"' })
    [System.IO.File]::WriteAllLines($rsp, [string[]]$lines, $noBom)

    Info "[$name] $($sources.Count) source files -> compiling..."
    Push-Location $Project
    try {
        $raw = & dotnet $csc "@$rsp" 2>&1
        $code = $LASTEXITCODE
    } finally { Pop-Location }

    $errors = @($raw | Where-Object { $_ -match ': error CS' })
    $warns = @($raw | Where-Object { $_ -match ': warning CS' })
    Add-Content -Path $logPath -Value "===== $name (exit=$code) ====="
    Add-Content -Path $logPath -Value ($raw | Out-String)
    return @{ Name = $name; Errors = $errors.Count; Warnings = $warns.Count; ErrorLines = $errors; Exit = $code }
}

Remove-Item $logPath -ErrorAction SilentlyContinue
Info "csc      : $csc"
Info "unity rsp: $($dag.FullName)"
Info "output   : $outDir"

$runtimeDll = Join-Path $outDir 'Assembly-CSharp.dll'
$results = @()
$results += Invoke-Pass 'Assembly-CSharp' (Join-Path $dag.FullName 'Assembly-CSharp.rsp') $runtimeDll (Get-Sources (Join-Path $Project 'Assets') $false) @()
if (-not $RuntimeOnly) {
    $editorRsp = Join-Path $dag.FullName 'Assembly-CSharp-Editor.rsp'
    if (Test-Path $editorRsp) {
        $results += Invoke-Pass 'Assembly-CSharp-Editor' $editorRsp (Join-Path $outDir 'Assembly-CSharp-Editor.dll') (Get-Sources (Join-Path $Project 'Assets') $true) @($runtimeDll)
    } else { Info 'Assembly-CSharp-Editor.rsp not found, skipping editor assembly' }
}

$totalErr = 0; $totalWarn = 0
foreach ($r in $results) {
    if ($r -isnot [hashtable]) { Write-Host "DEBUG unexpected result element: $($r.GetType().FullName) -> $r" -ForegroundColor Magenta; continue }
    $totalErr += [int]$r.Errors
    $totalWarn += [int]$r.Warnings
}
Write-Host ''
foreach ($r in $results) {
    $color = if ($r.Errors -gt 0) { 'Red' } else { 'Green' }
    Write-Host ("{0,-26} errors={1,-4} warnings={2,-4} exit={3}" -f $r.Name, $r.Errors, $r.Warnings, $r.Exit) -ForegroundColor $color
}
if ($totalErr -gt 0) {
    Write-Host ''
    Write-Host '--- compile errors (first 40) ---' -ForegroundColor Red
    $results | ForEach-Object { $_.ErrorLines } | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
    Write-Host ''
    Write-Host "full log: $logPath" -ForegroundColor Yellow
    exit 1
}
Write-Host ''
Write-Host "COMPILE GATE PASSED: 0 error, $totalWarn warning(s). log: $logPath" -ForegroundColor Green
exit 0
