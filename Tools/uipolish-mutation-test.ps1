# uipolish-mutation-test.ps1 - proves the H-block assertions actually FAIL when the asset layer breaks.
#
# Why this exists: an independent review (Docs/VisualPass/65_ asset-integration review, section V2) showed
# that counting assertions is not evidence. Each fault below is injected into the working tree, the gate is run,
# and the tree is restored and hash-verified. Expected: baseline 45/0, then one FAIL per injected fault, then 45/0.
# This script is pure ASCII on purpose: a no-BOM .ps1 with CJK text is a landmine on Chinese Windows (see
# contract 17.5 - GBK trail-byte pairing eats the character after a CJK byte).
#
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\uipolish-mutation-test.ps1
# Do NOT run this while a Unity import / build is in flight - for ~2 minutes the tree holds one fault.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gate = Join-Path $root 'Tools\uipolish-check.ps1'
$files = @(
    'Assets\Resources\UIArt\vignette.png.meta',
    'Tools\gen-uiart.py',
    'Assets\UI\UIArt.cs'
)
$backup = Join-Path $env:TEMP ('uiart-mutation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup | Out-Null
$hashBefore = @{}
foreach ($f in $files) {
    $src = Join-Path $root $f
    Copy-Item -LiteralPath $src -Destination (Join-Path $backup ([System.IO.Path]::GetFileName($f))) -Force
    $hashBefore[$f] = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
}

function Restore-All {
    foreach ($f in $files) {
        Copy-Item -LiteralPath (Join-Path $backup ([System.IO.Path]::GetFileName($f))) -Destination (Join-Path $root $f) -Force
    }
}

function Run-Gate([string] $label) {
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $gate 2>&1
    Write-Output ("[gate] " + $label + " -> " + ($out | Select-String -Pattern 'UIPOLISH-CHECK:').Line)
    foreach ($x in @($out | Select-String -Pattern '^\[FAIL\]')) { Write-Output ("        " + $x.Line) }
}

function Patch-File([string] $rel, [string] $from, [string] $to) {
    $p = Join-Path $root $rel
    $t = [System.IO.File]::ReadAllText($p)
    if (-not $t.Contains($from)) { throw ("mutation anchor not found in " + $rel + ": " + $from) }
    [System.IO.File]::WriteAllText($p, $t.Replace($from, $to))
}

try {
    Run-Gate 'baseline (expect 45 passed / 0 failed)'

    # V2-1: a meta drifts on an import field the old gate never looked at -> H2 must fail
    Patch-File 'Assets\Resources\UIArt\vignette.png.meta' 'textureCompression: 0' 'textureCompression: 1'
    Run-Gate 'A meta textureCompression 0->1 (expect H2 FAIL)'
    Restore-All

    # V2-2: generator and meta drift apart -> H2 must fail on the pinned border
    Patch-File 'Tools\gen-uiart.py' '"paper_plate_9s", (18, 18, 18, 18)' '"paper_plate_9s", (2, 18, 18, 18)'
    Run-Gate 'B generator paper border 18->2 (expect H2 FAIL)'
    Restore-All

    # V2-3: an overlay graphic starts eating clicks -> H3 must fail
    Patch-File 'Assets\UI\UIArt.cs' 'raycastTarget = false;' 'raycastTarget = true;'
    Run-Gate 'C raycastTarget false->true (expect H3 FAIL)'
    Restore-All

    Run-Gate 'restored (expect 45 passed / 0 failed)'
} finally {
    Restore-All
    $bad = @()
    foreach ($f in $files) {
        $h = (Get-FileHash -LiteralPath (Join-Path $root $f) -Algorithm SHA256).Hash
        if ($h -ne $hashBefore[$f]) { $bad += $f }
    }
    Write-Output ("[restore-verify] " + $(if ($bad.Count -eq 0) { 'all files byte-identical to backup' } else { 'MISMATCH: ' + ($bad -join ', ') }))
    Remove-Item -LiteralPath $backup -Recurse -Force
}
