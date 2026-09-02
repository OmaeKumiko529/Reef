---
name: feishu-agent
description: 维护与启停「暗礁」飞书 bot 守护进程（feishu-agent.mjs）。当需要启动、停止、重启、检查状态、排障或修改该 Node 进程及其 lark-cli/opencode 依赖时使用。
---

# 飞书 Bot 守护进程维护

「暗礁」飞书 bot 是一个常驻 Node 脚本 `feishu-agent.mjs`（项目根目录），它订阅飞书 IM 消息事件，为每条消息 fork 一个受限 opencode agent 去理解意图、查资料、并以 DeepSeek娘 用户身份回消息。

## 架构速览

```
飞书云端 ──事件流──> lark-cli(本地总线 daemon) ──NDJSON──> feishu-agent.mjs ──spawn──> opencode ──lark-cli──> 飞书云端(回复)
```

| 角色 | 位置 / 版本 |
|---|---|
| 脚本 | `F:\Projects\Reef\feishu-agent.mjs` |
| node | v24.16.0（`node`） |
| lark-cli | `C:\Users\ZAIza\AppData\Roaming\npm\node_modules\@larksuite\cli\bin\lark-cli.exe`（v1.0.92） |
| opencode | `C:\Users\ZAIza\AppData\Roaming\npm\node_modules\opencode-ai\bin\opencode.exe`（v1.18.25） |
| agent 定义 | `.opencode\agent\reef-feishu.md` |
| 模型 | `deepseek/deepseek-v4-pro` |

脚本内硬编码配置（改动需重启生效）：`BOT_OPEN_ID`、`MODEL`、`DOC_TOKEN`，以及两个可执行程序路径。

## 前置条件

启动前确认两项认证均可用：

```bash
lark-cli auth status      # bot: ready；user: DeepSeek娘（可 needs_refresh，会自动续）
lark-cli doctor           # 全绿/仅 warn 即可；bot + user 至少一项可用
```

- `event consume` 用 **bot** 身份收事件（`--as bot`）。
- AI 正式回复用 **user** 身份（`--as user`，需 `im:message.send_as_user` scope）。
- 脚本级技术消息（回执/失败兜底）用 **bot** 身份（`--as bot`），不依赖 user 身份。
- 两项缺一都会导致收不到消息或发不出去，先 `lark-cli auth login` 补齐。

## 消息反馈机制

脚本内置两处自动化消息，保证飞书侧始终有反馈：

| 时机 | 身份 | 内容 |
|---|---|---|
| 收到消息、开始处理前 | bot | `收到，正在处理…` |
| opencode 退出码非 0（无余额/服务异常/崩溃） | bot | `抱歉，处理失败：模型服务暂时不可用（可能额度不足或网络异常），请稍后重试。` |

- 这两条由 `replyBot()` 直发，**不依赖模型**，速度快且必定发出。
- 正常时 AI 最终回复仍由 opencode 以 DeepSeek娘（user）发出，与上述 bot 消息互不冲突。
- 若用户收到回执但迟迟没有最终回复，通常是 opencode 已成功启动、正在长时间思考，或 opencode 退出码为 0 但未实际发出消息（罕见）。

## 启动

在项目目录下（脚本用 `__dirname` 定位项目根，必须 `workdir=F:\Projects\Reef`）：

**前台（推荐调试用，日志打印到终端）：**
```powershell
node feishu-agent.mjs
```

**后台 + 日志落盘（推荐常驻用）：**
```powershell
Start-Process node -ArgumentList "feishu-agent.mjs" `
  -WorkingDirectory "F:\Projects\Reef" `
  -RedirectStandardError "F:\Projects\Reef\feishu-agent.err.log" `
  -RedirectStandardOutput "F:\Projects\Reef\feishu-agent.out.log" `
  -WindowStyle Hidden
```

启动成功标志：stderr 出现 `[feishu-agent] listening (im.message.receive_v1, bot identity)`。

## 检查状态

```powershell
# 1. 进程是否在跑（node + 子进程 lark-cli event consume）
Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*feishu-agent*' } | Select-Object ProcessId,CommandLine

# 2. 事件总线 daemon 状态
lark-cli event status --json

# 3. 认证/连通性
lark-cli doctor
```

## 停止

脚本捕获 SIGINT/SIGTERM，会优雅关闭 `event consume` 子进程再退出。**不要 `kill -9`**（会跳过服务端订阅清理）。

```powershell
# 前台：Ctrl+C

# 后台：按 PID 优雅停止
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*feishu-agent*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId }
```

## 日志与排障

所有运行日志走 stderr，前缀 `[feishu-agent]`。关键行：

| 日志 | 含义 |
|---|---|
| `listening (...) bot identity` | 订阅就绪 |
| `queued <message_id> (<chat_type>)` | 收到一条待回复消息 |
| `handling <message_id>...` | 开始 fork opencode 处理 |
| `opencode exit=<code> ...` | opencode 完成（0 正常；非 0 会触发失败兜底消息） |
| `reply failed: <err>` | 回执/兜底消息发送失败 |
| `continue session <sid> ...` | 复用历史会话 |
| `consume exited code=...` | 事件流断开，脚本随之退出 |

常见问题：

- **收不到消息**：bot 身份认证失效（`auth status`）、`BOT_OPEN_ID` 不对、`event consume` 订阅被占（已有另一个进程在跑，`event status` 查）。仅 `kill -9` 遗留会导致 "subscription already exists"，用 `lark-cli event stop --force` 清理。
- **发不出消息**：user 身份 `needs_refresh` 未自动续 → `lark-cli auth login`；DeepSeek娘 不在目标群（报 "not in chat"）→ 拉它进群。
- **lark-cli/opencode 版本更新**：`lark-cli update`；opencode 走 `npm update -g opencode-ai`。升级后重启进程。

## 修改脚本后

`feishu-agent.mjs` 是纯 Node 脚本，无热重载：改任何逻辑或配置（模型、bot open_id、文档 token、命令路径）后必须**重启进程**生效。改动前先 `lark-cli auth status` 确认认证不依赖脚本内任何状态。
