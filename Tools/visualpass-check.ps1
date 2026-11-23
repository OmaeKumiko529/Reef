<#
VisualPass acceptance assertions for the Reef project (read-only, no Unity needed).
Run after R3 implementation; every FAIL is a blocker for delivery.

Usage (PowerShell 5.1):
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\visualpass-check.ps1
  ... -SkipCompile     skip the C# compile gate (faster)

Exit code: 0 = all assertions passed, 1 = at least one failed.
#>
[CmdletBinding()]
param(
    [string]$Project = 'F:\Projects\Reef',
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
$script:fail = 0
$script:pass = 0

function Assert-True([string]$id, [string]$desc, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:pass++; Write-Host ("PASS  {0,-6} {1}" -f $id, $desc) -ForegroundColor Green }
    else {
        $script:fail++
        Write-Host ("FAIL  {0,-6} {1}" -f $id, $desc) -ForegroundColor Red
        if ($detail) { Write-Host ("             -> " + $detail) -ForegroundColor Yellow }
    }
}

function Get-Text([string]$rel) {
    $p = Join-Path $Project $rel
    if (-not (Test-Path $p)) { return '' }
    return [System.IO.File]::ReadAllText($p)
}

function Get-CsFiles { Get-ChildItem (Join-Path $Project 'Assets') -Recurse -Filter *.cs -File }
function Find-InCs([string]$pattern) {
    $hits = @()
    foreach ($f in Get-CsFiles) {
        $m = Select-String -Path $f.FullName -Pattern $pattern -ErrorAction SilentlyContinue
        foreach ($x in $m) {
            # 注释里的示例代码不算违规（例如「不再 mapMaterial.SetXxx()」这类文档注释）
            $t = $x.Line.TrimStart()
            if ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')) { continue }
            $hits += ("{0}:{1}" -f $f.FullName.Substring($Project.Length + 1), $x.LineNumber)
        }
    }
    return $hits
}

Write-Host "== Reef VisualPass acceptance check ==" -ForegroundColor Cyan
Write-Host ""

# --- A. compile gate --------------------------------------------------------
if ($SkipCompile) { Write-Host "SKIP  A      compile gate (skipped)" -ForegroundColor DarkGray }
else {
    $log = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Project 'Tools\compile-check.ps1') -Quiet 2>&1
    $code = $LASTEXITCODE
    Assert-True 'A' 'C# compile gate = 0 error' ($code -eq 0) (($log | Select-Object -Last 3) -join ' | ')
}

# --- B. baked selection state cleared + _IdMap declared ---------------------
$mat = Get-Text 'Assets\Map\MapMat.mat'
Assert-True 'B1' 'MapMat._HasSelection == 0' ($mat -match '(?m)^\s*-\s*_HasSelection:\s*0\s*$') 'still 1 (startup fake highlight)'
Assert-True 'B2' 'MapMat._SelectionBlend == 0' ($mat -match '(?m)^\s*-\s*_SelectionBlend:\s*0\s*$') 'still 1'
Assert-True 'B3' 'MapMat._SelectedId cleared' ($mat -match '(?m)^\s*-\s*_SelectedId:\s*\{r:\s*0,\s*g:\s*0,\s*b:\s*0,\s*a:\s*0\}') 'should be all zeros'
Assert-True 'B4' 'MapMat declares _IdMap' ($mat -match '(?m)^\s*-\s*_IdMap:') 'identity map property missing'

# --- C. no runtime writes to the shared material ---------------------------
$matWrites = Find-InCs 'mapMaterial\.Set|\.material\.Set|sharedMaterial\.Set'
Assert-True 'C' 'no runtime writes to shared material' ($matWrites.Count -eq 0) ($matWrites -join ', ')
$mpb = Find-InCs 'SetPropertyBlock'
Assert-True 'C2' 'MaterialPropertyBlock is used' ($mpb.Count -gt 0) 'no SetPropertyBlock call found'

# --- D. placeholder copy removed -------------------------------------------
$placeholders = @()
foreach ($ext in @('*.json', '*.cs', '*.asset')) {
    $files = Get-ChildItem (Join-Path $Project 'Assets') -Recurse -Filter $ext -File
    foreach ($f in $files) {
        # .cs 里 "Text"/"Story" 是 GameObject 名（非玩家可见），只在数据文件里查这两个
        $pat = if ($f.Extension -eq '.cs') { '"TestEvent|"OptionA"|"OptionB"|"HintA"|"HintB"|占位' } else { '"TestEvent|"Story|"Text|"OptionA"|"OptionB"|"HintA"|"HintB"|占位' }
        $m = Select-String -Path $f.FullName -Pattern $pat -CaseSensitive -ErrorAction SilentlyContinue
        foreach ($x in $m) {
            # 注释里的占位示例、开发者日志里的占位措辞都不算玩家可见文案
            $t = $x.Line.TrimStart()
            if ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')) { continue }
            if ($f.Extension -eq '.cs') {
                # .cs 只看代码文本：去掉行尾注释后再判定，避免 `const X = 1; // 顶栏占位` 这类误报
                $codeOnly = $x.Line -replace '//.*$', ''
                if ($codeOnly -notmatch $pat) { continue }
                if ($codeOnly -match 'Debug\.Log') { continue }
            }
            $placeholders += ("{0}:{1}: {2}" -f $f.Name, $x.LineNumber, $x.Line.Trim())
        }
    }
}
Assert-True 'D' 'no placeholder copy in Assets' ($placeholders.Count -eq 0) (($placeholders | Select-Object -First 6) -join ' ; ')

# --- E. UICopy exists and is consumed --------------------------------------
$uicopy = Get-Text 'Assets\UI\UICopy.cs'
Assert-True 'E1' 'Assets/UI/UICopy.cs exists' ($uicopy.Length -gt 0)
$users = Find-InCs 'UICopy\.'
Assert-True 'E2' 'UICopy consumed by >=3 files' ($users.Count -ge 3) ("consumers: " + (($users | ForEach-Object { $_.Split(':')[0] } | Sort-Object -Unique) -join ', '))
Assert-True 'E3' 'UICopy.FlagLabel present' ($uicopy -match 'FlagLabel')

# --- F. 13 provincial names are Chinese ------------------------------------
$json = Get-Text 'Assets\Province\ProvinceData.json'
$asset = Get-Text 'Assets\Province\ProvinceDatabase.asset'
Assert-True 'F0' 'province data files readable' (($json.Length -gt 500) -and ($asset.Length -gt 1000)) ("json={0}B asset={1}B" -f $json.Length, $asset.Length)
Assert-True 'F1' 'ProvinceData.json: no numeric displayName left' (-not ($json -match '"displayName"\s*:\s*"\d+')) 'some province still named by id'
Assert-True 'F2' 'ProvinceDatabase.asset: no numeric displayName left' (-not ($asset -match '(?m)displayName:\s*\d+\s*$')) 'runtime truth still numeric'
$cjk = [regex]::Matches($json, '"displayName"\s*:\s*"[^"]*[\u4e00-\u9fff][^"]*"').Count
Assert-True 'F3' 'ProvinceData.json has 13 Chinese names' ($cjk -eq 13) ("found $cjk")

# --- G. shader identity path ------------------------------------------------
$shader = Get-Text 'Assets\Province\ProvinceSelect.shader'
Assert-True 'G1' 'shader declares sampler_IdMap' ($shader -match 'sampler_IdMap|sampler\s*\(\s*_IdMap')
Assert-True 'G2' 'shader uses _IdMap_TexelSize' ($shader -match '_IdMap_TexelSize')
Assert-True 'G3' 'shader has _HoverId as Vector' ($shader -match '_HoverId\s*\(\s*"?' )

# --- H. the open scene was not touched -------------------------------------
Push-Location $Project
$eap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'   # git 的 autocrlf 警告走 stderr，不能让 $ErrorActionPreference='Stop' 把脚本打断
try {
    $changed = @(& git -c core.safecrlf=false diff --name-only 2>$null)
    $staged = @(& git -c core.safecrlf=false diff --cached --name-only 2>$null)
} finally {
    $ErrorActionPreference = $eap
    Pop-Location
}
$sceneTouched = @($changed + $staged) | Where-Object { $_ -match 'SampleScene\.unity' }
Assert-True 'H' 'SampleScene.unity untouched' ($sceneTouched.Count -eq 0) 'the editor has this scene open - revert it!'

# --- I. UI honesty: no fake department progress ----------------------------
$agencyPanel = Get-Text 'Assets\Agency\AgencyPanelUI.cs'
Assert-True 'I1' 'AgencyPanelUI no hardcoded new Text[4]' ((Find-InCs 'new\s+Text\s*\[\s*4\s*\]').Count -eq 0)
$navUsers = Find-InCs 'Dept\.Implemented'
Assert-True 'I2' 'UICopy.Dept.Implemented drives the nav' ($navUsers.Count -ge 1) 'not referenced anywhere'

# --- J. text no longer swallows map clicks ---------------------------------
$uifactory = Get-Text 'Assets\UI\UIFactory.cs'
Assert-True 'J1' 'UIFactory sets raycastTarget' ($uifactory -match 'raycastTarget')
$label = Get-Text 'Assets\Province\ProvinceLabelUI.cs'
Assert-True 'J2' 'province labels are no longer IMGUI' (-not ($label -match 'void\s+OnGUI'))
Assert-True 'J3' 'label Show/Hide contract kept' (($label -match 'Show\s*\(') -and ($label -match 'Hide\s*\('))

# --- K. 相机 / 材质隔离 / 反馈（R3 程序层，Lead 独立复核）-------------------
$cam = Get-Text 'Assets\Camera\CameraPanController.cs'
Assert-True 'K1' 'camera clears to UITheme.MapBackdrop' (($cam -match 'CameraClearFlags\.SolidColor') -and ($cam -match 'MapBackdrop')) 'clear flags not set at runtime'
Assert-True 'K2' 'zoom ceiling derives from fit size' ($cam -match 'maxZoomLimit\s*=\s*Mathf\.Max\([^;]*fitZoom[^;]*\*') 'ceiling not derived from fitZoom'
Assert-True 'K3' 'camera fit reserves HUD safe area' ($cam -match 'safeRatio|\b70\b') 'no HUD-safe fit'
Assert-True 'K8' 'no vestigial public maxZoom field' (-not ($cam -match '(?m)^\s*public\s+float\s+maxZoom\s*=')) 'dead Inspector field at 20f still present'
$click = Get-Text 'Assets\Province\ProvinceClickHandler.cs'
Assert-True 'K4' 'ClickHandler resets dirty MPB properties in Awake' (($click -match 'MaterialPropertyBlock') -and ($click -match '(?s)Awake\(\).{0,400}?ResetDirtyState') -and ($click -match '(?s)ResetDirtyState\(\).{0,400}?IdHasSelection')) 'no Awake reset'
$pen = Get-Text 'Assets\Agency\PenetrationManager.cs'
Assert-True 'K5' 'penetration limit click has visible feedback' ($pen -match 'limitFlash') 'limit click still silent'
Assert-True 'K9' 'penetration hint counts only player-chosen blocks' ($pen -match 'selected\s*=\s*penetrating\.Count\s*-\s*forced\.Count') 'hint can show 4/3 (forced blocks counted)'
$picker = Get-Text 'Assets\Province\ProvincePicker.cs'
# K10 asserts the WORKING binding, not the comment text: ProjectSettings activeInputHandler=1
# (Input System package only) means UnityEngine.Input.GetKeyDown throws at runtime.
# Strip line comments so the "do not use this" comment cannot satisfy or break the assertion.
$pickerCode = (($picker -split "`n" | ForEach-Object { $_ -replace '//.*$', '' }) -join "`n")
Assert-True 'K10' 'Esc clears the province selection' (($pickerCode -match 'Keyboard\.current\.escapeKey\.wasPressedThisFrame') -and ($pickerCode -match 'ClearSelection\(\)') -and (-not ($pickerCode -match 'Input\.GetKeyDown'))) 'Esc binding missing, or legacy Input API used (throws with the new Input System)'
Assert-True 'K6' 'UISorting layer table exists' ((Get-Text 'Assets\UI\UISorting.cs').Length -gt 0)
Assert-True 'K7' 'UIFactory sets matchWidthOrHeight' ($uifactory -match 'matchWidthOrHeight')

Write-Host ''
Write-Host ("== {0} passed, {1} failed ==" -f $script:pass, $script:fail) -ForegroundColor ($(if ($script:fail -gt 0) { 'Red' } else { 'Green' }))
if ($script:fail -gt 0) { exit 1 }
exit 0
