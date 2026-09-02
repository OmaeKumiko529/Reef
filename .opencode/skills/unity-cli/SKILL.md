---
name: unity-cli
description: 涉及 Unity 项目操作（编译校验、生成构建、跑测试、批量导入、打包、执行编辑器自动化脚本等）时使用——优先通过 Unity CLI（Unity.exe -batchmode）实际运行，而不是只做静态代码分析。项目位于 F:\Projects\Reef（Unity 6000.5.1f1）。
---

# Unity CLI 操作

## 触发时机

任何需要真实 Unity 环境的操作，都应优先走 Unity CLI：

- 编译校验（改完 C# 后确认能编译过）
- 生成构建 / 打包（Android、Windows、iOS 等）
- 运行 EditMode / PlayMode 测试
- 批量导入资源、重新生成 Library
- 执行编辑器自动化脚本（`-executeMethod`）

## CLI 位置与调用

- 命令：`Unity.exe`（已加入用户 PATH，解析到 `D:\Unity Editor\6000.5.1f1\Editor\Unity.exe`，与 `ProjectSettings/ProjectVersion.txt` 的 `6000.5.1f1` 一致）。
- 若 PATH 未生效，用绝对路径：
  `& "D:\Unity Editor\6000.5.1f1\Editor\Unity.exe" <args>`
- 项目路径固定为 `F:\Projects\Reef`。

## 常用参数

| 参数 | 作用 |
|---|---|
| `-batchmode` | 无 UI 的批处理模式，必须配 `-logFile` 才有输出 |
| `-nographics` | 不初始化图形设备（编译/测试常用，构建时慎用） |
| `-quit` | 执行完退出编辑器 |
| `-projectPath <path>` | 指定项目路径 |
| `-logFile <path>` | 日志写到文件（`-logFile -` 输出到 stdout，PowerShell 下建议写文件） |
| `-executeMethod <Namespace.Class.Method>` | 执行静态方法（方法须 `public static void`） |
| `-runTests` | 运行测试，配合 `-testPlatform EditMode\|PlayMode` |
| `-testResults <path>` | 测试结果输出（xml/nunit） |
| `-buildTarget <name>` | 构建目标（`Android` / `Win64` / `StandaloneWindows64` 等） |

## 常用场景

编译校验（最快判断改动能编译过）：

```powershell
& "Unity.exe" -batchmode -nographics -quit `
  -projectPath "F:\Projects\Reef" `
  -logFile "F:\Projects\Reef\Logs\ci-build.log"
```

运行 EditMode 测试：

```powershell
& "Unity.exe" -batchmode -nographics -runTests -testPlatform EditMode `
  -testResults "F:\Projects\Reef\Logs\test-results.xml" `
  -projectPath "F:\Projects\Reef" `
  -logFile "F:\Projects\Reef\Logs\ci-test.log"
```

执行编辑器脚本（如自定义构建入口）：

```powershell
& "Unity.exe" -batchmode -quit -projectPath "F:\Projects\Reef" `
  -logFile "F:\Projects\Reef\Logs\ci-build.log" `
  -executeMethod "BuildScript.BuildAndroid"
```

## 结果判定

- 编译失败：日志含 `error CS` 与 `Compilation failed` / `Aborting batchmode due to failure`；退出码通常非 0。
- 测试结果看 `-testResults` 导出的 xml（`result="Failed"` / `test-run` 汇总）。
- batchmode 结束无 `error CS` 且正常退出即通过。

## 注意事项

- 每个 batchmode 进程会锁项目，避免并行跑多个 Unity 进程操作同一项目。
- 首次运行可能因资源导入较慢（本项目 `Library/` 已存在，通常较快）。
- `-executeMethod` 的目标方法必须是 `public static void`（无参或 Unity 支持的特有签名）。
- 输出大量时按 `-logFile` 文件回读定位，不要只看 stdout。
