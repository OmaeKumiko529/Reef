---
name: reef-skill-sync
description: 同步「暗礁」项目内两份 AI skill 目录（.dsh/skills 与 .opencode/skills）。当两份 skill 出现分叉、修改了某个 skill、新加 skill、或需要确认两边是否一致时使用——按"哪个新用哪个"合并，把较新一侧的内容覆盖到较旧一侧。
---

# 暗礁 skill 双目录同步

## 为什么有两份

本项目的 AI skill 要同时给两个工具用，因此仓库里存在两份目录：

| 工具 | 目录 | 加载方式 | frontmatter 支持 |
|---|---|---|---|
| **DSH** | `.dsh/skills/<name>/SKILL.md` | 项目根 rank 100，自动发现，改完热更新 | `name`、`description`、可选 `whenToUse` / `metadata` / `disable-model-invocation` / `user-invocable` |
| **opencode** | `.opencode/skills/<name>/SKILL.md` | opencode 自身加载 | 只保证 `name`、`description` |

两份会各自被改，于是分叉。**本规则：同名的两个 skill，谁的文件更新（mtime 更大），就以谁为准，把内容覆盖到另一侧。**

## frontmatter 差异是设计如此，不算分叉

`.dsh` 侧允许 DSH 专有键，`.opencode` 侧不保证能解析它们。所以比较和写入都先做一次归一化：

- **比较时先剥掉** `whenToUse`、`metadata`、`disable-model-invocation`、`user-invocable` 这四个键（连同它们的缩进子行）再比对；
- **从 DSH 写到 opencode 时也剥掉**，保证 opencode 拿到的永远是干净的 `name` + `description` + 正文。

只要"剥掉专有键之后两边一致"，就算已同步，不需要动。

## 同步脚本

把下面整段存成 UTF-8（无 BOM 或带 BOM 均可）的临时 `.ps1` 再运行。**脚本刻意只用 ASCII**：这台机器上是 Windows PowerShell 5.1，它按 ANSI 读取 `.ps1`，脚本里只要有中文就会被解码成乱码、甚至吃掉字符串的引号导致语法错误。想加中文提示，就存成 **UTF-8 带 BOM**。

```powershell
[CmdletBinding()]
param(
  [string]$ProjectRoot,
  [switch]$DryRun,
  [ValidateSet('none','dsh','opencode')][string]$TieBreak = 'none'
)
$ErrorActionPreference = 'Stop'

if (-not $ProjectRoot) {
  $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
  if (-not $ProjectRoot) { throw 'Cannot locate project root: run inside the repo, or pass -ProjectRoot' }
}
$ProjectRoot = ($ProjectRoot -replace '/', '\').TrimEnd('\')
$DshDir = Join-Path $ProjectRoot '.dsh\skills'
$OpcDir = Join-Path $ProjectRoot '.opencode\skills'
foreach ($d in @($DshDir, $OpcDir)) { if (-not (Test-Path $d)) { throw "Missing directory: $d" } }

$DshOnlyKeys = @('whenToUse', 'metadata', 'disable-model-invocation', 'user-invocable')

function Get-NormalizedText([string]$Path) {
  if (-not (Test-Path $Path)) { return $null }
  $lines = [System.IO.File]::ReadAllLines($Path)
  $out = New-Object System.Collections.Generic.List[string]
  $inFm = $false; $fmDone = $false; $skipIndented = $false
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $ln = $lines[$i]
    if (-not $fmDone -and $i -eq 0 -and $ln.Trim() -eq '---') { $inFm = $true; $out.Add($ln); continue }
    if ($inFm) {
      if ($ln.Trim() -eq '---') { $inFm = $false; $fmDone = $true; $skipIndented = $false; $out.Add($ln); continue }
      if ($skipIndented -and $ln -match '^\s+\S') { continue }
      $skipIndented = $false
      $m = [regex]::Match($ln, '^([A-Za-z0-9_-]+)\s*:')
      if ($m.Success -and ($DshOnlyKeys -contains $m.Groups[1].Value)) { $skipIndented = $true; continue }
      $out.Add($ln); continue
    }
    $out.Add($ln)
  }
  return ((($out | ForEach-Object { $_.TrimEnd() }) -join "`n").TrimEnd())
}

function Get-BundleStamp([string]$Dir) {
  if (-not (Test-Path $Dir)) { return $null }
  $f = @(Get-ChildItem $Dir -Recurse -File -ErrorAction SilentlyContinue)
  if ($f.Count -eq 0) { return $null }
  return ($f | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

function Get-ExtraFiles([string]$Dir) {
  if (-not (Test-Path $Dir)) { return @() }
  return @(Get-ChildItem $Dir -Recurse -File -ErrorAction SilentlyContinue |
           Where-Object { $_.Name -ne 'SKILL.md' } |
           ForEach-Object { $_.FullName.Substring($Dir.Length).TrimStart('\') })
}

$backupRoot = Join-Path $env:TEMP ('reef-skill-sync-backup\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$rows = New-Object System.Collections.Generic.List[object]
$copied = 0

function Write-Skill([string]$TargetFile, [string]$Text) {
  if ($DryRun) { return }
  if (Test-Path $TargetFile) {
    $rel = $TargetFile.Substring($ProjectRoot.Length).TrimStart('\')
    $bak = Join-Path $backupRoot ($rel -replace '[\\/]', '__')
    New-Item -ItemType Directory -Force -Path (Split-Path $bak) | Out-Null
    Copy-Item -LiteralPath $TargetFile -Destination $bak -Force
  } else {
    New-Item -ItemType Directory -Force -Path (Split-Path $TargetFile) | Out-Null
  }
  [System.IO.File]::WriteAllText($TargetFile, $Text.TrimEnd() + "`n", (New-Object System.Text.UTF8Encoding($false)))
}

$names = @()
$names += @(Get-ChildItem $DshDir -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
$names += @(Get-ChildItem $OpcDir -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
$names = @($names | Sort-Object -Unique)

foreach ($n in $names) {
  $dDir = Join-Path $DshDir $n; $oDir = Join-Path $OpcDir $n
  $dFile = Join-Path $dDir 'SKILL.md'; $oFile = Join-Path $oDir 'SKILL.md'
  $dTxt = Get-NormalizedText $dFile
  $oTxt = Get-NormalizedText $oFile
  $status = ''; $action = 'none'; $winner = ''

  if ($null -eq $dTxt -and $null -eq $oTxt) { $status = 'empty dir, skipped' }
  elseif ($null -eq $dTxt) { $winner = 'opencode'; $action = 'create-dsh'; $status = 'only in opencode -> create in .dsh' }
  elseif ($null -eq $oTxt) { $winner = 'dsh'; $action = 'create-opencode'; $status = 'only in .dsh -> create in .opencode' }
  elseif ($dTxt -eq $oTxt) { $status = 'in sync (normalized equal)' }
  else {
    $dS = Get-BundleStamp $dDir; $oS = Get-BundleStamp $oDir
    if ($null -eq $dS -or $null -eq $oS) { $status = 'no timestamp, skipped' }
    elseif ($dS -gt $oS) { $winner = 'dsh'; $action = 'overwrite-opencode'; $status = '.dsh newer -> overwrite .opencode' }
    elseif ($oS -gt $dS) { $winner = 'opencode'; $action = 'overwrite-dsh'; $status = 'opencode newer -> overwrite .dsh' }
    else {
      if ($TieBreak -eq 'none') { $status = '[!] same mtime, different content -> ambiguous, skipped (use -TieBreak dsh|opencode)' }
      elseif ($TieBreak -eq 'dsh') { $winner = 'dsh'; $status = 'same mtime -> took .dsh (-TieBreak)' }
      else { $winner = 'opencode'; $status = 'same mtime -> took opencode (-TieBreak)' }
    }
  }

  $extra = @()
  $extra += @(Get-ExtraFiles $dDir | ForEach-Object { ".dsh$_" })
  $extra += @(Get-ExtraFiles $oDir | ForEach-Object { ".opencode$_" })

  if ($winner -eq 'dsh') {
    Write-Skill $oFile $dTxt
    $copied++
  } elseif ($winner -eq 'opencode') {
    Write-Skill $dFile $oTxt
    $copied++
  }

  $rows.Add([pscustomobject]@{
    skill  = $n
    action = $action
    result = $status
    extras = if ($extra.Count) { ($extra -join ', ') } else { '' }
  })
}

Write-Host ("Project root: {0}" -f $ProjectRoot)
Write-Host ("Mode        : {0}" -f $(if ($DryRun) { 'DryRun (no write)' } else { 'APPLY' }))
Write-Host ("Backup      : {0}" -f $(if ($DryRun -or $copied -eq 0) { '(none needed)' } else { $backupRoot }))
Write-Host ''
$rows | Format-Table -AutoSize -Wrap
$need = @($rows | Where-Object { $_.action -ne 'none' }).Count
Write-Host ("Skills needing action: {0} / {1}" -f $need, $rows.Count)
$ex = @($rows | Where-Object { $_.extras -ne '' })
if ($ex.Count) {
  Write-Host ''
  Write-Host 'NOTE: these bundles contain files besides SKILL.md; this script only syncs SKILL.md (extras neither copied nor deleted):'
  $ex | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.skill, $_.extras) }
}
```

## 用法

本机是 **Windows PowerShell 5.1，没有 `pwsh`**，且默认执行策略禁止未签名脚本，所以必须带 `-ExecutionPolicy Bypass`：

```powershell
# 先看差异，不写任何文件
powershell -NoProfile -ExecutionPolicy Bypass -File .\sync-skill-dirs.ps1 -DryRun

# 实际同步
powershell -NoProfile -ExecutionPolicy Bypass -File .\sync-skill-dirs.ps1
```

已经在 PowerShell 会话里时，也可以直接 `& .\sync-skill-dirs.ps1 -DryRun`（若报未签名，改走上面的 `-ExecutionPolicy Bypass` 形式）。

### 在 DSH 沙箱里跑会被拒写 `.opencode`（实测）

AI 用 DSH 的 `pwsh` 工具（沙箱模式 `workspace-write`）跑本脚本时，**读两边、写 `.dsh` 都正常，但写 `.opencode\skills\` 会报"访问被拒绝"**。原因不是 DSH 的策略层，而是 Windows 层：工作区根目录 `F:\Projects\Reef` 上有针对当前沙箱能力的显式写授权 ACE，而 `.opencode` 目录只有沙箱能力 SID 与 Authenticated Users 的授权，DSH 受限的 pwsh token 落不到有效写权限上。

两种可行做法：

1. **放开权限单次运行**（推荐，最省事）——AI 用 `sandbox_permissions: danger-full-access` 重跑同一条命令；
2. **不在沙箱里跑**，由人自己的终端执行；
3. 或者干脆让 AI 用 `write` / `edit` 工具逐个落盘（fs 工具不走 pwsh 沙箱，能正常写 `.opencode`）。

**判据**：脚本跑完如果打印 `Skills needing action: 0 / N` 且全是 `in sync`，才算真的收敛了。只看到 `DryRun` 列表不代表已写入。

## 安全约定

- **覆盖前一定备份**：被覆盖的文件原样拷到 `%TEMP%\reef-skill-sync-backup\<时间戳>\`，路径会打印出来。
- **只复制，不删除**。只在某一侧存在的 skill 不会自动删；包里的非 `SKILL.md` 文件会被报告但不处理。
- **mtime 相同但内容不同 → 判为歧义、跳过、报告**，绝不猜。要强行指定用 `-TieBreak dsh` 或 `-TieBreak opencode`。
- **mtime 是启发式，不是权威**：`git clone` / `git checkout` / `git stash` 会把 mtime 刷成检出时刻，此时两边可能都是"同一秒"，脚本会报歧义。真正权威的历史是 **git**——所以改完 skill **请提交**，别只靠 mtime 判新旧。
- 写完（UTF-8 无 BOM）后**回读校验**：再跑一次 `-DryRun`，应当全部显示 `in sync`。

## 加新 skill 时

在**任意一侧**建好 `<name>/SKILL.md`，跑一次同步即可补到另一侧。`.dsh` 侧的 `whenToUse` / `metadata` 不会带到 `.opencode`，这是有意的。
