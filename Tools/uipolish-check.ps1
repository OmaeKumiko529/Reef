# Tools\uipolish-check.ps1
# Round 2 (UI polish) acceptance assertions for the "Reef" Unity project.
# Read-only: it never writes into Assets/ or Library/. ASCII only on purpose
# (Windows PowerShell 5.1 mis-decodes non-ASCII .ps1 files without a BOM).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\uipolish-check.ps1 [-Quiet]
param([switch]$Quiet)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot

$script:pass = 0
$script:fail = 0
$script:rows = New-Object System.Collections.ArrayList

function Add-Result {
    param([string]$Id, [string]$Desc, [bool]$Ok, [string]$Detail)
    $tag = 'FAIL'
    if ($Ok) { $tag = 'PASS'; $script:pass++ } else { $script:fail++ }
    [void]$script:rows.Add([pscustomobject]@{ Id = $Id; Ok = $Ok; Desc = $Desc; Detail = $Detail })
    Write-Host ("[{0}] {1} {2} {3}" -f $tag, $Id, $Desc, $Detail)
}

function Read-Text {
    param([string]$Rel)
    $p = Join-Path $root $Rel
    if (-not (Test-Path -LiteralPath $p)) { return $null }
    try { return [System.IO.File]::ReadAllText($p) } catch { return $null }
}

function Strip-Comments {
    param([string]$T)
    if ([string]::IsNullOrEmpty($T)) { return '' }
    $lines = $T -split "\r?\n"
    $out = foreach ($l in $lines) { $l -replace '//.*$', '' }
    return ($out -join "`n")
}

function Has-Pat {
    param([string]$T, [string]$Pat)
    if ([string]::IsNullOrEmpty($T)) { return $false }
    return [bool]([regex]::IsMatch($T, $Pat))
}

function Count-Pat {
    param([string]$T, [string]$Pat)
    if ([string]::IsNullOrEmpty($T)) { return 0 }
    return ([regex]::Matches($T, $Pat)).Count
}

function Missing-Pat {
    param([string]$T, [string[]]$Pats)
    $missing = @()
    foreach ($p in $Pats) { if (-not (Has-Pat $T $p)) { $missing += $p } }
    return $missing
}

$uiTheme   = Read-Text 'Assets\UI\UITheme.cs'
$uiCopy    = Strip-Comments (Read-Text 'Assets\UI\UICopy.cs')
$chrome    = Strip-Comments (Read-Text 'Assets\UI\UIPanelChrome.cs')
$btnState  = Strip-Comments (Read-Text 'Assets\UI\UIButtonState.cs')
$sorting   = Read-Text 'Assets\UI\UISorting.cs'
$topBar    = Strip-Comments (Read-Text 'Assets\Agency\TopBarUI.cs')
$navUI     = Strip-Comments (Read-Text 'Assets\Agency\AgencyNavUI.cs')
$agencyUI  = Strip-Comments (Read-Text 'Assets\Agency\AgencyPanelUI.cs')
$penMan    = Strip-Comments (Read-Text 'Assets\Agency\PenetrationManager.cs')
$detail    = Strip-Comments (Read-Text 'Assets\Province\BlockDetailPanel.cs')
$journal   = Strip-Comments (Read-Text 'Assets\Intelligence\CausalChainManager.cs')
$eventMgr  = Strip-Comments (Read-Text 'Assets\Event\EventManager.cs')
$clock     = Strip-Comments (Read-Text 'Assets\Core\GameClock.cs')
$camPan    = Strip-Comments (Read-Text 'Assets\Camera\CameraPanController.cs')

# ---------- A. compile gate ----------
$gateOutput = ''
try {
    $gateOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Tools\compile-check.ps1') -Quiet 2>&1 | Out-String
} catch { $gateOutput = "EXCEPTION: " + $_.Exception.Message }
$gateOk = (Has-Pat $gateOutput '0 error') -and (Has-Pat $gateOutput '11 warning')
$gateLines = @($gateOutput -split "\r?\n" | Where-Object { $_.Trim() -ne '' })
$gateLast = ''
if ($gateLines.Count -gt 0) { $gateLast = $gateLines[$gateLines.Count - 1] }
Add-Result 'A1' 'compile gate 0 error / 11 warning' $gateOk $gateLast

# ---------- B. chrome infrastructure ----------
$b1 = (Has-Pat $chrome 'class\s+PanelChrome') -and (Has-Pat $chrome 'static\s+PanelChrome\s+Create\(') -and (Has-Pat $chrome 'enum\s+ChromeKind') -and (Has-Pat $chrome 'enum\s+ChromeState')
Add-Result 'B1' 'UIPanelChrome.cs exists with PanelChrome + Create()' $b1 ''

$b2 = (Has-Pat $btnState 'class\s+UIButtonState') -and (Has-Pat $btnState 'static\s+UIButtonState\s+Apply\(') -and (Has-Pat $btnState 'enum\s+ButtonMode')
Add-Result 'B2' 'UIButtonState.cs exists with UIButtonState + Apply()' $b2 ''

# frozen draw order = runtime creation order (later sibling draws on top)
$layerAnchors = @(
    'AddQuad(c.Root, "DoubleLine"',
    'AddQuad(c.Root, "PaperBack"',
    'AddQuad(c.Root, "Frame"',
    'AddQuad(c.Root, "Paper"',
    'AddEdgeLine(c.Root, "Highlight"',
    'AddEdgeLine(c.Root, "Press"',
    'AddTitleBar(c.Root',
    'new GameObject(ContentName',
    'new GameObject(DossierName',
    'new GameObject(StampName'
)
$orderOk = $false
$orderDetail = 'CreateStretched body not found'
$iStart = $chrome.IndexOf('static PanelChrome CreateStretched(')
if ($iStart -ge 0) {
    $body = $chrome.Substring($iStart)
    $last = -1
    $ok = $true
    $seq = @()
    $missing = @()
    foreach ($a in $layerAnchors) {
        $i = $body.IndexOf($a)
        if ($i -lt 0) { $missing += $a; continue }
        $seq += ('' + $i)
        if ($i -lt $last) { $ok = $false }
        $last = $i
    }
    $orderOk = $ok -and ($missing.Count -eq 0)
    if ($missing.Count -gt 0) {
        $orderDetail = 'missing anchors: ' + ($missing -join ' | ')
    } else {
        $orderDetail = 'creation offsets: ' + ($seq -join ' < ')
    }
}
Add-Result 'B3' 'chrome layer creation order = frozen 8-layer order' $orderOk $orderDetail

$rtFalse = Count-Pat $chrome 'raycastTarget\s*=\s*false'
Add-Result 'B4' 'chrome decorations non-interactive (raycastTarget=false >= 3)' ($rtFalse -ge 3) ('count=' + $rtFalse)

$fiveStates = Missing-Pat $btnState @('IPointerEnterHandler', 'IPointerDownHandler', 'ISelectHandler', 'Normal', 'Disabled')
Add-Result 'B5' 'UIButtonState covers hover/press/select states' ($fiveStates.Count -eq 0) ('missing=' + ($fiveStates -join ','))

# ---------- C. no textures / sprites ----------
# PenetrationManager.cs is excluded on purpose: its pen-map Texture2D is pre-existing
# (review item P0-5, explicitly out of scope for round 2) and is guarded by a size check.
$uiFiles = @('Assets\UI\UIPanelChrome.cs', 'Assets\UI\UIButtonState.cs', 'Assets\UI\UIFactory.cs',
    'Assets\Agency\TopBarUI.cs', 'Assets\Agency\AgencyNavUI.cs', 'Assets\Agency\AgencyPanelUI.cs',
    'Assets\Province\BlockDetailPanel.cs', 'Assets\Province\ProvinceLabelUI.cs',
    'Assets\Intelligence\CausalChainManager.cs', 'Assets\Event\EventManager.cs', 'Assets\Core\GameClock.cs')
$texHits = @()
foreach ($f in $uiFiles) {
    $t = Strip-Comments (Read-Text $f)
    $c = (Count-Pat $t 'new\s+Texture2D\s*\(') + (Count-Pat $t 'Sprite\.Create\s*\(')
    if ($c -gt 0) { $texHits += ($f + '=' + $c) }
}
Add-Result 'C1' 'no runtime Texture2D / Sprite.Create in UI files' ($texHits.Count -eq 0) ($texHits -join ' ')

$penTex = Count-Pat $penMan 'new\s+Texture2D\s*\('
$penGuard = Has-Pat $penMan 'if\s*\(\s*penMap\s*==\s*null'
$penInUpdate = Has-Pat $penMan 'void\s+Update\s*\([^)]*\)\s*\{[^}]{0,600}new\s+Texture2D'
Add-Result 'C2' 'pre-existing pen-map texture stays cached (P0-5, out of scope)' `
    (($penTex -eq 1) -and $penGuard -and (-not $penInUpdate)) `
    ('count=' + $penTex + ' sizeGuard=' + $penGuard + ' inUpdate=' + $penInUpdate)

# ---------- D. UITheme tokens ----------
$tokens = @('MarginScreen', 'SafeTop', 'SafeBottom', 'SafeInset', 'WidthS', 'WidthM', 'WidthL',
    'TopBarLeftInset', 'TopBarRightInset', 'TitleBarHeight', 'PadPanel', 'PadCard', 'PadRow',
    'ChromeLineWidth', 'ChromeOuterColor', 'ChromeInnerColor', 'HighlightColor', 'PressColor',
    'TitleBarBg', 'TitleRuleColor', 'TitleRuleWidth', 'TitleAccentColor', 'TitleAccentSize',
    'BtnNormalBg', 'BtnHoverBg', 'BtnPressedBg', 'BtnSelectedBg', 'BtnDisabledBg',
    'CardBg', 'CardBorderColor', 'LabelSelectedAlpha', 'LabelHoverAlpha',
    'TopBarHeight', 'JournalTopInset')
$missingTokens = Missing-Pat $uiTheme $tokens
Add-Result 'D1' 'UITheme has all 34 P0 tokens' ($missingTokens.Count -eq 0) ('missing=' + ($missingTokens -join ','))

$radiusHits = Count-Pat $uiTheme '(?m)^\s*public\s+const\s+float\s+PanelRadius'
Add-Result 'D2' 'PanelRadius kept (not deleted)' ($radiusHits -eq 1) ('count=' + $radiusHits)

$tokenValues = @(
    @('MarginScreen=20f', 'MarginScreen\s*=\s*20f'),
    @('SafeTop=70f', 'SafeTop\s*=\s*70f'),
    @('SafeBottom=80f', 'SafeBottom\s*=\s*80f'),
    @('SafeInset=20f', 'SafeInset\s*=\s*20f'),
    @('WidthXs=300f', 'WidthXs\s*=\s*300f'),
    @('WidthS=380f', 'WidthS\s*=\s*380f'),
    @('WidthM=620f', 'WidthM\s*=\s*620f'),
    @('WidthL=820f', 'WidthL\s*=\s*820f'),
    @('TopBarLeftInset=20f', 'TopBarLeftInset\s*=\s*20f'),
    @('TopBarRightInset=340f', 'TopBarRightInset\s*=\s*340f'),
    @('TopBarHeight=64f', 'TopBarHeight\s*=\s*64f'),
    @('JournalTopInset=104f', 'JournalTopInset\s*=\s*104f'),
    @('TitleBarHeight=56f', 'TitleBarHeight\s*=\s*56f'),
    @('PadPanel=24f', 'PadPanel\s*=\s*24f'),
    @('PadCard=16f', 'PadCard\s*=\s*16f'),
    @('PadRow=8f', 'PadRow\s*=\s*8f'),
    @('ChromeLineWidth=1f', 'ChromeLineWidth\s*=\s*1f'),
    @('TitleRuleWidth=2f', 'TitleRuleWidth\s*=\s*2f'),
    @('LabelSelectedAlpha=0.97f', 'LabelSelectedAlpha\s*=\s*0\.97f'),
    @('LabelHoverAlpha=0.90f', 'LabelHoverAlpha\s*=\s*0\.90f')
)
$badTokenValues = @()
foreach ($pair in $tokenValues) {
    if (-not (Has-Pat $uiTheme $pair[1])) { $badTokenValues += $pair[0] }
}
Add-Result 'D3' 'UITheme token values match the frozen numbers' ($badTokenValues.Count -eq 0) ('mismatch=' + ($badTokenValues -join ','))

# ---------- E. UICopy new constants (13) ----------
$copyNames = @('NotRecorded', 'Eyebrow', 'FileNoFormat', 'FileNo', 'CountFormat', 'CardNoFormat',
    'IssuerFallback', 'DateFormat', 'OptionLead', 'HintFull', 'LeaderFormat', 'CountryFormat',
    'ChainRevealHint', 'ChainSummaryHint')
$missingCopy = Missing-Pat $uiCopy $copyNames
Add-Result 'E1' 'UICopy has round-2 member names' ($missingCopy.Count -eq 0) ('missing=' + ($missingCopy -join ','))

$copyValues = @(
    @('NotRecorded', '\u672a\u89c1\u8bb0\u8f7d'),
    @('Province.Eyebrow', '\u7701\u60c5\u6863\u6848'),
    @('Journal.Eyebrow', '\u5377\u5b97'),
    @('Event.IssuerFallback', '\u5448\u62a5'),
    @('Event.OptionLead', '\u6279\u793a'),
    @('Penetration.HintFull', '\u6e17\u900f\u540d\u989d\u5df2\u6ee1'),
    @('Dept.FileNo', '\u6863\u53f7'),
    @('Agency.CountFormat', '\u5728\u518c'),
    @('Journal.CountFormat', '\u672c\u5377\u6536\u5f55'),
    @('Province.FileNoFormat', '\u7f16\u53f7'),
    @('TopBar.LeaderFormat', '\u9886\u5bfc\u4eba \{0\}'),
    @('Journal.ChainRevealHint', '\u63ed\u793a\u4e00\u6761\u56e0\u679c\u94fe'),
    @('Journal.ChainSummaryHint', '\u63a8\u8fdb\u4e00\u6761\u56e0\u679c\u94fe')
)
$badValues = @()
foreach ($pair in $copyValues) {
    if (-not (Has-Pat $uiCopy $pair[1])) { $badValues += $pair[0] }
}
Add-Result 'E2' 'UICopy values match frozen strings' ($badValues.Count -eq 0) ('missing=' + ($badValues -join ','))

# ---------- F. chrome consumption ----------
$consumers = @('Assets\Agency\TopBarUI.cs', 'Assets\Agency\AgencyNavUI.cs', 'Assets\Agency\AgencyPanelUI.cs',
    'Assets\Agency\PenetrationManager.cs', 'Assets\Province\BlockDetailPanel.cs',
    'Assets\Intelligence\CausalChainManager.cs', 'Assets\Event\EventManager.cs', 'Assets\Core\GameClock.cs')
$consumerHits = @()
$btnApply = @()
foreach ($f in $consumers) {
    $t = Strip-Comments (Read-Text $f)
    if (Has-Pat $t 'UIPanelChrome\.Create(Stretched)?\(') { $consumerHits += (Split-Path -Leaf $f) }
    if (Has-Pat $t 'UIButtonState') { $btnApply += (Split-Path -Leaf $f) }
}
Add-Result 'F1' 'UIPanelChrome.Create used by >= 6 panels' ($consumerHits.Count -ge 6) ('files=' + $consumerHits.Count + ' [' + ($consumerHits -join ',') + ']')
Add-Result 'F2' 'UIButtonState applied by >= 3 panels' ($btnApply.Count -ge 3) ('[' + ($btnApply -join ',') + ']')

# ---------- G. sorting orders ----------
Add-Result 'G1' 'clock plate uses UISorting.Hud' (Has-Pat $clock 'UISorting\.Hud') ''
Add-Result 'G2' 'detail panel uses UISorting.Panel' (Has-Pat $detail 'UISorting\.Panel') ''
Add-Result 'G3' 'journal uses UISorting.Modal' (Has-Pat $journal 'UISorting\.(Modal|Journal)') ''
Add-Result 'G4' 'event canvas uses UISorting.Event' (Has-Pat $eventMgr 'UISorting\.Event') ''

# ---------- H. misalignment fixes ----------
$h1a = (Has-Pat $topBar 'TopBarLeftInset') -and (Has-Pat $topBar 'TopBarRightInset')
$h1b = (Has-Pat $topBar 'TopBarHeight') -or (Has-Pat $topBar '64f\s*,\s*64f') -or (Has-Pat $topBar ',\s*64f\)')
$h1c = Has-Pat $camPan 'topReservePixels\s*=\s*84'
Add-Result 'H1' 'L-05 top bar insets/height + topReservePixels 84' ($h1a -and $h1b -and $h1c) ('insets=' + $h1a + ' height=' + $h1b + ' cam84=' + $h1c)

$nav620 = Count-Pat $navUI '620'
Add-Result 'H2' 'L-07 dead 620x420 nav panel removed' ($nav620 -eq 0) ('count620=' + $nav620)

$h3a = (Has-Pat $detail '-\s*240') -or ((Has-Pat $detail 'BottomReserve') -and (Has-Pat $detail '140') -and (Has-Pat $detail '100'))
$h3b = (Has-Pat $detail '380') -or (Has-Pat $detail 'WidthS')
Add-Result 'H3' 'L-01 detail panel reserve 240 / width S(380)' ($h3a -and $h3b) ('reserve240=' + $h3a + ' w380=' + $h3b)

$h4a = (Has-Pat $penMan '620') -or (Has-Pat $penMan 'WidthM')
$h4b = Has-Pat $penMan 'new\s+Vector2\s*\(\s*0f?\s*,\s*100'
Add-Result 'H4' 'L-02 hint strip bottom-centred 620x60 @ (0,100)' ($h4a -and $h4b) ('w620=' + $h4a + ' pos=' + $h4b)

Add-Result 'H5' 'L-11 event tooltip clamped' (Has-Pat $eventMgr 'Mathf\.Clamp') ''

$h6a = Has-Pat $journal '380'
$h6b = Has-Pat $journal 'RectMask2D'
Add-Result 'H6' 'L-06 journal 380 + RectMask2D' ($h6a -and $h6b) ('w380=' + $h6a + ' mask=' + $h6b)

Add-Result 'H7' 'H-08 redaction bars instead of printed text' (Has-Pat $journal 'Redacted') ''

# ---------- I. UICopy wiring ----------
Add-Result 'I1' 'GameClock wires UICopy.Clock' (Has-Pat $clock 'UICopy\.Clock') ''

$i2a = Has-Pat $topBar 'UICopy\.TopBar'
$i2b = Has-Pat $topBar 'LeaderFormat'
$i2c = -not (Has-Pat $topBar '"\u9886\u5bfc\u4eba')
Add-Result 'I2' 'TopBar wires UICopy LeaderFormat (no raw leader literal)' ($i2a -and $i2b -and $i2c) ('uiCopy=' + $i2a + ' leaderFormat=' + $i2b + ' rawLeaderLiteral=' + (-not $i2c))

$i3a = -not (Has-Pat $navUI '\u5f00\u53d1\u4e2d')
$i3b = -not (Has-Pat $agencyUI '\u5f00\u53d1\u4e2d')
Add-Result 'I3' 'nav/agency panels take in-development text from UICopy' ($i3a -and $i3b) ('navRaw=' + (-not $i3a) + ' agencyRaw=' + (-not $i3b))

# ---------- J. scope discipline ----------
$sceneDiff = ''
try { $sceneDiff = & git -C $root status --porcelain -- Assets/Scenes/SampleScene.unity 2>$null | Out-String } catch { $sceneDiff = '' }
Add-Result 'J1' 'SampleScene.unity untouched' ([string]::IsNullOrWhiteSpace($sceneDiff)) $sceneDiff.Trim()
Add-Result 'J2' 'UISorting documents Modal layer' (Has-Pat $sorting 'Modal') ''

# ---------- N. R4b fixes (contract 70_ 15.0) ----------
# N1: the detail drawer must animate from the rest position captured at runtime,
#     never from hand-computed coordinates (that is what broke U-09).
$n1rest = Has-Pat $detail 'rest\s*=\s*panelRect\.anchoredPosition'
$n1noY = -not (Has-Pat $detail 'y\s*=\s*-TopReserve')
$n1noZero = -not (Has-Pat $detail 'new\s+Vector2\s*\(\s*0f?\s*,')
Add-Result 'N1' 'U-09 detail slide animates from captured rest position' ($n1rest -and $n1noY -and $n1noZero) ('rest=' + $n1rest + ' noTopReserve=' + $n1noY + ' noZeroX=' + $n1noZero)

# N2: the pre-choice option tooltip must not leak the causal-chain title.
$n2noTitle = -not (Has-Pat $eventMgr 'GetTitle\s*\(')
$n2hints = (Has-Pat $eventMgr 'ChainRevealHint') -and (Has-Pat $eventMgr 'ChainSummaryHint')
Add-Result 'N2' 'option tooltip does not name the chain before reveal' ($n2noTitle -and $n2hints) ('noGetTitle=' + $n2noTitle + ' usesHints=' + $n2hints)

# N3: the two frozen chain-hint constants exist and the +/- click target is >= 44px (P7).
$n3consts = (Has-Pat $uiCopy 'ChainRevealHint') -and (Has-Pat $uiCopy 'ChainSummaryHint')
$n3btn = Has-Pat $agencyUI 'preferredHeight\s*=\s*44f'
Add-Result 'N3' 'chain-hint constants present + 44px click target' ($n3consts -and $n3btn) ('consts=' + $n3consts + ' btn44=' + $n3btn)

# ---------- N4/N5: runtime NRE guards (real crash found by the user in Play mode) ----------
# UnityEngine.UI.Graphic carries [DisallowMultipleComponent] (ugui Graphic.cs:17): adding an
# Image/Text onto a GameObject that already has a Graphic returns null, and the next dereference
# throws NullReferenceException at runtime. Every Graphic must be created through
# `new GameObject(name, typeof(X))`. Comments are stripped first (they may mention the pattern).
$n4hits = @()
foreach ($f in (Get-ChildItem (Join-Path $root 'Assets') -Recurse -Filter '*.cs' -File)) {
    $t = Strip-Comments ([System.IO.File]::ReadAllText($f.FullName))
    if (Has-Pat $t 'AddComponent<(Image|Text|RawImage|Graphic)>') { $n4hits += $f.Name }
}
Add-Result 'N4' 'no AddComponent<Graphic subtype> in Assets' ($n4hits.Count -eq 0) (($n4hits | Select-Object -First 4) -join ' ; ')

# N5: the stamp handle must live on its own GameObject (that is exactly the N4 hotfix).
$n5handle = Has-Pat $chrome 'StampName \+ "Handle"'
$n5noadd = -not (Has-Pat $chrome 'AddComponent<Image>')
Add-Result 'N5' 'chrome stamp handle is its own GameObject' ($n5handle -and $n5noadd) ('handle=' + $n5handle + ' noAddComponent=' + $n5noadd)

# ---------- N6: runtime default-font fallback ----------
# UGUI paints NOTHING when Text.font is null (Text.OnPopulateMesh returns early) and reports no
# error, so every runtime-created Text needs a fallback font. Chrome-internal texts (title bar /
# eyebrow / dossier tag / stamp) pass no font at all.
$uiFactorySrc = Strip-Comments (Read-Text 'Assets\UI\UIFactory.cs')
$pickerSrc = Strip-Comments (Read-Text 'Assets\Province\ProvincePicker.cs')
$n6a = (Has-Pat $uiFactorySrc 'public static Font DefaultFont') -and (Has-Pat $uiFactorySrc 'font = DefaultFont')
$n6b = Has-Pat $pickerSrc 'UIFactory\.DefaultFont = labelFont'
Add-Result 'N6' 'default-font fallback wired (UIFactory + ProvincePicker)' ($n6a -and $n6b) ('factory=' + $n6a + ' picker=' + $n6b)

# ---------- N7/N8: SelectedBar must survive the "AddComponent runs Awake synchronously" trap ----------
# UIButtonState.Apply calls AddComponent<UIButtonState>(), which runs Awake->Bind synchronously while
# `mode` is still the default (Action); Bind sets bound=true, so the later Bind() is a no-op and the
# NavBar/Option selected bar would never be built -> bottom-bar / option-row selection invisible.
$btnSrc = Strip-Comments (Read-Text 'Assets\UI\UIButtonState.cs')
$n7calls = ([regex]::Matches($btnSrc, 'EnsureSelectedBar\(\)')).Count
$n7seq = Has-Pat $btnSrc 's\.mode = mode;\s*\r?\n\s*s\.Bind\(\);\s*\r?\n\s*s\.EnsureSelectedBar\(\);'
$n7 = ($n7calls -ge 3) -and (Has-Pat $btnSrc 'void EnsureSelectedBar\(\)') -and $n7seq
Add-Result 'N7' 'EnsureSelectedBar re-called after mode is set (Apply)' $n7 ('calls=' + $n7calls + ' seq=' + $n7seq)
$n8 = Has-Pat $btnSrc 'selected && \(mode == ButtonMode\.NavBar \|\| mode == ButtonMode\.Option\)'
Add-Result 'N8' 'selected bar gated on NavBar/Option mode' $n8 ''

# ---------- N9: Visual layer must be excluded from the button's own LayoutGroup ----------
# A LayoutGroup on the *button itself* (the event option row used to have a VerticalLayoutGroup)
# treats the direct child Visual as a layout row: with childControlHeight=true and no ILayoutElement
# on Visual its height collapses to 0, so the reparented label collapses with it -> "option text
# misaligned". ignoreLayout is the documented cure and also stops the layout pass from overwriting
# the 1px hover lift (visual.anchoredPosition).
$n9 = Has-Pat $btnSrc 'AddComponent<LayoutElement>\(\)\.ignoreLayout = true'
Add-Result 'N9' 'Visual layer uses LayoutElement.ignoreLayout' $n9 ''

# ---------- N10: option row height is computed at build time, no per-row copy prefix ----------
# The Options container's VerticalLayoutGroup measures each row by its own
# LayoutElement.preferredHeight, but the row label is reparented into the (ignored) Visual layer, so
# the container can never measure the label: an in-row VerticalLayoutGroup + ContentSizeFitter is
# structurally unable to auto-size the row. Height must be computed from the copy length instead.
# Also: Event.OptionLead("<pi-shi>") is the section eyebrow ABOVE the options, not a per-row prefix.
# NOTE: this file must stay pure ASCII. A CJK char immediately before a closing quote makes GBK/ANSI
# code pages swallow that quote (trail-byte pairing -> "string is missing the terminator"), which is
# exactly how the assertion below broke. Compose the CJK colon from its code point instead of typing it.
$evtSrc = Strip-Comments (Read-Text 'Assets\Event\EventManager.cs')
$n10pref1 = Has-Pat $evtSrc 'preferredHeight = OptionRowHeight\('
$n10pref2 = Has-Pat $evtSrc 'le\.preferredHeight = le\.minHeight'
# NOTE: never write `Has-Pat $src 'x' -or (Has-Pat ...)` inline - PowerShell parses that `-or`
# as a parameter of Has-Pat, so the second test is silently dropped. Precompute into variables.
$n10a = $n10pref1 -or $n10pref2
$n10b = Has-Pat $evtSrc 'static float OptionRowHeight\(string text\)'
$n10c = -not (Has-Pat $evtSrc ('OptionLead \+ "' + [char]0xFF1A + "'"))
$n10 = $n10a -and $n10b -and $n10c
Add-Result 'N10' 'option row height computed in code, no duplicated prefix' $n10 ('pref=' + $n10a + ' helper=' + $n10b + ' noPrefix=' + $n10c)

# ---------- H1/H2/H3: the asset layer (added after an independent review found this gate was blind to it) ----------
# Before this block, all 75 assertions only read .cs source + compile + git diff: a missing PNG, a wrong
# spriteBorder, a Clamp wrap mode on a tiling overlay or an overlay that ate clicks all still passed.
# H1  generator asset list == PNGs on disk, and every name UIArt.cs asks for exists on disk.
# H2  every .meta is Sprite(2D/UI) / FullRect mesh / no mipmap, and border+wrap match the generator.
# H3  the screen overlay canvas keeps sortingOrder >= 50 and never takes raycasts.
$artDir = Join-Path $root 'Assets\Resources\UIArt'
$genSrc = Strip-Comments (Read-Text 'Tools\gen-uiart.py')
$uiArtSrc = Strip-Comments (Read-Text 'Assets\UI\UIArt.cs')

$genNames = @()
if ($genSrc) {
    $genNames = @([regex]::Matches($genSrc, 'add\("([A-Za-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}
$diskNames = @()
if (Test-Path -LiteralPath $artDir) {
    $diskNames = @(Get-ChildItem -LiteralPath $artDir -Filter *.png | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } | Sort-Object -Unique)
}
$h1a = ($genNames.Count -ge 10) -and (($genNames -join ',') -eq ($diskNames -join ','))
Add-Result 'H1a' 'generator asset list equals PNGs on disk' $h1a ($genNames.Count.ToString() + ' gen / ' + $diskNames.Count.ToString() + ' png')

$refNames = @()
if ($uiArtSrc) {
    $refNames = @([regex]::Matches($uiArtSrc, '(?:Get|TextureOf)\("([A-Za-z0-9_]+)"\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}
$missingRef = @($refNames | Where-Object { $diskNames -notcontains $_ })
$h1b = ($refNames.Count -ge 10) -and ($missingRef.Count -eq 0)
Add-Result 'H1b' 'every sprite name referenced by UIArt.cs exists on disk' $h1b ($refNames.Count.ToString() + ' refs, missing=' + ($missingRef -join ','))

$metaBad = New-Object System.Collections.ArrayList
# V2-2 (65_ review): the expected borders are PINNED to the design contract here on purpose. Comparing
# "meta == generator" alone would still pass if the generator and the metas drifted together.
# Names not listed are unsliced: (0, 0, 0, 0).
$expectedBorder = @{
    'paper_plate_9s'    = '(18, 18, 18, 18)'
    'titleplate_9s'     = '(20, 0, 20, 0)'
    'rule_ring_9s'      = '(8, 8, 8, 8)'
    'rule_ring_worn_9s' = '(8, 8, 8, 8)'
    'btn_plate_9s'      = '(12, 12, 12, 12)'
    'redaction_bar_9s'  = '(12, 0, 12, 0)'
    'dossier_plate_9s'  = '(12, 0, 12, 0)'
}
# V2-1: sRGB / compression / max size are load-bearing too (a BC-compressed plate shows artefacts).
$metaNeedles = @('textureType: 8', 'spriteMode: 1', 'enableMipMap: 0', 'spriteMeshType: 0',
                 'alphaIsTransparency: 1', 'sRGBTexture: 1', 'textureCompression: 0',
                 'maxTextureSize: 256')
foreach ($n in $diskNames) {
    $mp = Join-Path $artDir ($n + '.png.meta')
    if (-not (Test-Path -LiteralPath $mp)) { [void]$metaBad.Add($n + ':no-meta'); continue }
    $mt = [System.IO.File]::ReadAllText($mp)
    foreach ($needle in $metaNeedles) {
        if ($mt -notmatch [regex]::Escape($needle)) { [void]$metaBad.Add($n + ':' + $needle) }
    }
    $wantBorder = '(0, 0, 0, 0)'
    if ($expectedBorder.ContainsKey($n)) { $wantBorder = $expectedBorder[$n] }
    $bn = [regex]::Match($wantBorder, '\((\d+), (\d+), (\d+), (\d+)\)')
    $wantMeta = 'spriteBorder: {x: ' + $bn.Groups[1].Value + ', y: ' + $bn.Groups[2].Value + ', z: ' + $bn.Groups[3].Value + ', w: ' + $bn.Groups[4].Value + '}'
    if ($mt -notmatch [regex]::Escape($wantMeta)) { [void]$metaBad.Add($n + ':border!=pinned') }
    # NOTE: Regex.Escape already escapes the parentheses of "(18, 18, 18, 18)" - do not wrap it in another \( \).
    $gcall = [regex]::Match($genSrc, 'add\("' + [regex]::Escape($n) + '", ' + [regex]::Escape($wantBorder))
    if (-not $gcall.Success) { [void]$metaBad.Add($n + ':border!=generator') }
    $wantWrap = 1
    if ($genSrc -match ('WRAP_REPEAT = \{[^}]*"' + [regex]::Escape($n) + '"')) { $wantWrap = 0 }
    if ($mt -notmatch ('wrapU: ' + $wantWrap)) { [void]$metaBad.Add($n + ':wrapU!=' + $wantWrap) }
    if ($mt -notmatch ('wrapV: ' + $wantWrap)) { [void]$metaBad.Add($n + ':wrapV!=' + $wantWrap) }
}
Add-Result 'H2' 'every .meta: sprite/FullRect/no-mipmap/compression + pinned border & wrap' ($metaBad.Count -eq 0) (($metaBad | Select-Object -First 4) -join ' ')

$h3sortOk = $false
$h3sortVal = 'none'
$h3sort = [regex]::Match($uiArtSrc, 'sortingOrder = (\d+)')
if ($h3sort.Success) {
    $h3sortVal = $h3sort.Groups[1].Value
    if ([int]$h3sortVal -ge 50) { $h3sortOk = $true }
}
$h3ray = ([regex]::Matches($uiArtSrc, 'raycastTarget = false')).Count
# Two statement sites are the contract: one inside the tile loop (covers both tile Raw/Images) and one
# for the vignette. Assert >= 2, plus the CanvasGroup guards.
$h3group = Has-Pat $uiArtSrc 'blocksRaycasts = false'
$h3inter = Has-Pat $uiArtSrc 'interactable = false'
# V2-3: counting the "false" sites alone is a blind spot - a NEW overlay graphic that set
# raycastTarget = true would pass. The overlay builder must never turn raycasts ON.
$h3rayOn = ([regex]::Matches($uiArtSrc, 'raycastTarget = true')).Count
$h3 = $h3sortOk -and ($h3ray -ge 2) -and ($h3rayOn -eq 0) -and $h3group -and $h3inter
Add-Result 'H3' 'overlay canvas: sortingOrder >= 50, raycastTarget=false xN (never true), group blocks/interactable off' $h3 ('sort=' + $h3sortVal + ' rayOff=' + $h3ray + ' rayOn=' + $h3rayOn + ' block=' + $h3group + ' interactable=' + $h3inter)

# ---------- summary ----------
Write-Host ''
Write-Host ("UIPOLISH-CHECK: " + $script:pass + " passed / " + $script:fail + " failed")
foreach ($r in $script:rows) {
    if (-not $r.Ok) { Write-Host ("  FAILED " + $r.Id + ": " + $r.Desc + " " + $r.Detail) }
}
if ($script:fail -gt 0) { exit 1 }
exit 0
