# Reef - HLSL compile check for Assets/Province/ProvinceSelect.shader
#
# Why: this session cannot start Unity (the user's editor holds Temp/UnityLockfile),
# so the shader could not be compiled by Unity itself. The Windows SDK DirectX
# Shader Compiler (dxc) can compile the vertex and fragment entry points with the
# real URP includes, which catches syntax / semantic / cbuffer mistakes.
#
# Requires: Windows SDK with dxc.exe (default path below).
# Artifacts (safe to delete; regenerated on demand):
#   Tools/.shadercheck/inc               (~78 MB copy of the URP include trees)
#   Tools/.shadercheck/probe.hlsl        (extracted HLSL block)
#   Tools/.shadercheck/{vert,frag}.dxil  (compile output)
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\shader-check.ps1

param(
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Dxc = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\dxc.exe'
)

$ErrorActionPreference = 'Stop'
$shaderRel = 'Assets\Province\ProvinceSelect.shader'
$work      = Join-Path $PSScriptRoot '.shadercheck'
$incRoot   = Join-Path $work 'inc\Packages'

Write-Host "== Reef shader check: $shaderRel ==" -ForegroundColor Cyan

if (-not (Test-Path $Dxc)) { Write-Host "SKIP: dxc not found at $Dxc" -ForegroundColor Yellow; exit 2 }
$shaderPath = Join-Path $Project $shaderRel
if (-not (Test-Path $shaderPath)) { Write-Host "FAIL: $shaderRel missing" -ForegroundColor Red; exit 1 }

# --- 1. extract the HLSLPROGRAM block ---------------------------------------
$sh = [System.IO.File]::ReadAllText($shaderPath, [System.Text.Encoding]::UTF8)
$s = $sh.IndexOf('HLSLPROGRAM'); $e = $sh.IndexOf('ENDHLSL')
if ($s -lt 0 -or $e -le $s) { Write-Host "FAIL: no HLSLPROGRAM/ENDHLSL block" -ForegroundColor Red; exit 1 }
$hlsl = $sh.Substring($s + 11, $e - $s - 11)
# dxc does not know Unity's #pragma vertex/fragment
$hlsl = ($hlsl -split "`n" | Where-Object { $_ -notmatch '^\s*#pragma\s+(vertex|fragment)' }) -join "`n"
New-Item -ItemType Directory -Force -Path $work | Out-Null
[System.IO.File]::WriteAllText((Join-Path $work 'probe.hlsl'), $hlsl, (New-Object System.Text.UTF8Encoding($false)))

# --- 2. materialise the virtual 'Packages/...' include tree -----------------
# The shader includes "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl",
# a path that only exists inside Unity's virtual file system -> copy the real
# package folders from Library/PackageCache into a shim directory.
$pkgCache = Join-Path $Project 'Library\PackageCache'
if (-not (Test-Path $incRoot)) {
    Write-Host "preparing include shim (first run, ~78 MB) ..."
    New-Item -ItemType Directory -Force -Path $incRoot | Out-Null
    foreach ($p in @('com.unity.render-pipelines.core', 'com.unity.render-pipelines.universal',
                     'com.unity.render-pipelines.universal-config', 'com.unity.shadergraph')) {
        $src = Get-ChildItem $pkgCache -Directory -Filter "$p@*" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $src) { Write-Host "FAIL: package $p not in Library/PackageCache" -ForegroundColor Red; exit 1 }
        $dst = Join-Path $incRoot $p
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        robocopy $src.FullName $dst /E /NFL /NDL /NJH /NJS /NP /XD .git Tests Documentation Samples~ | Out-Null
    }
}

# --- 3. compile both entry points -------------------------------------------
$fail = 0
foreach ($stage in @(@('vs_6_0', 'vert'), @('ps_6_0', 'frag'))) {
    $profile = $stage[0]; $entry = $stage[1]
    $out = Join-Path $work "$entry.dxil"
    $log = & $Dxc -T $profile -E $entry -D SHADER_API_D3D11=1 -I (Join-Path $work 'inc') -Fo $out (Join-Path $work 'probe.hlsl') 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host ("PASS  {0,-4} {1} ({2} bytes)" -f $entry, $profile, (Get-Item $out).Length) -ForegroundColor Green
    } else {
        $fail++
        Write-Host ("FAIL  {0,-4} {1}" -f $entry, $profile) -ForegroundColor Red
        $log | Select-Object -First 12 | ForEach-Object { Write-Host "      $_" }
    }
}

Write-Host ''
if ($fail -gt 0) { Write-Host "== SHADER CHECK FAILED ($fail entry point(s)) ==" -ForegroundColor Red; exit 1 }
Write-Host "== SHADER CHECK PASSED (vert + frag, SHADER_API_D3D11) ==" -ForegroundColor Green
exit 0
