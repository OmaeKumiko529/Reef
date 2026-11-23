param(
  [string]$PreviewState = "-",
  [string]$OutPath = "F:\Projects\Reef\Logs\ui_play.png",
  [string]$LogPath = "F:\Projects\Reef\Logs\preview.log",
  [int]$Frames = 10,
  [int]$TimeoutSec = 300
)

$unity = "D:\Unity Editor\6000.5.1f1\Editor\Unity.exe"
Remove-Item $OutPath, $LogPath -ErrorAction SilentlyContinue

$unityArgs = @(
  "-projectPath", "F:\Projects\Reef",
  "-executeMethod", "EditorScenePreview.CapturePreview",
  "-out", $OutPath,
  "-frames", "$Frames",
  "-screen-width", "1920", "-screen-height", "1080",
  "-logFile", $LogPath
)
if ($PreviewState -and $PreviewState.Trim() -ne "-") { $unityArgs += @("-state", $PreviewState.Trim()) }

Write-Host "[runner] launching unity (state='$PreviewState')"
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
Write-Host "[runner] pid=$($p.Id)"

$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
  Start-Sleep -Seconds 3
  if ((Test-Path $OutPath) -and ((Get-Item $OutPath).Length -gt 1000)) {
    Start-Sleep -Seconds 2
    Write-Host "[runner] OK png=$((Get-Item $OutPath).Length) bytes in $([int]$sw.Elapsed.TotalSeconds)s"
    break
  }
  if ($p.HasExited) { Write-Host "[runner] unity exited early, code=$($p.ExitCode)"; break }
}
if (-not (Test-Path $OutPath)) { Write-Host "[runner] FAILED: no screenshot after $TimeoutSec s" }

# 收尾：不给 Unity 留后台进程
Get-Process Unity -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "[runner] done"
