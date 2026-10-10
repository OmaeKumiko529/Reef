---
name: reef-type-sync
description: 维护并同步「暗礁」事件系统的 type 注册表到飞书。当项目代码中事件系统的 type 部分（EventData.cs 的 type/op 注释、EventManager.cs 的 switch case、Events.json 的 type 字段）有任何改动时，必须同步更新飞书「暗礁 type 对照表」。
---

# 暗礁 type 注册表同步

## 触发时机

1. 用户改动（或要求改动）事件系统 type 相关代码时：
   - `Assets/Event/EventData.cs` 中 `ConditionData.type`、`ConditionData.op`、`EffectData.type` 的注释；
   - `Assets/Event/EventManager.cs` 中的 `case "..."` 分支；
   - `Assets/Resources/Events.json` 中的 `"type": "..."` 字段。
2. 用户明确说「同步 type 表」「更新 type 对照表」时。

改动完成后，**必须**把飞书「暗礁 type 对照表」同步到与代码一致。

> ⚠️ 本 skill 旧版（`.opencode/skills/reef-type-sync/`）的注册表**已过期**：它只列了 `add_influence / set_flag` 两种 effect，而代码里有 **10 种**，且引用的行号已经漂移。下表已按提交 `f2f44e2` 的源码重新核对——**以代码为准，下表只是快照**。

## type 的权威来源（代码）

事件系统的 type 是一组字符串字面量，共三类：

| 类别 | 字段 | 定义位置 | 取值 |
|---|---|---|---|
| 触发条件 | `ConditionData.type` | `Assets/Event/EventData.cs:4` | `time_point` / `flag_compare` |
| 比较操作符 | `ConditionData.op` | `Assets/Event/EventData.cs:7` | `eq` / `ne` / `gt` / `ge` / `lt` / `le` |
| 效果 | `EffectData.type` | `Assets/Event/EventData.cs:14` | 见下方 10 种 |

处理分支集中在 `Assets/Event/EventManager.cs`：

- `Evaluate`（`EventManager.cs:135-147`）—— 触发条件分支（`default: return false`）
- `Compare`（`EventManager.cs:149-161`）—— 比较操作符分支（`default: return false`）
- `ApplyEffect`（`EventManager.cs:197-241`）—— 效果分支

### 效果 effect 全表（10 种）

| type | 相关字段 | 处理位置 | Events.json 是否已有数据 |
|---|---|---|---|
| `add_influence` | `blockId`, `value` | `EventManager.cs:201-206` | ✅ |
| `set_flag` | `flag`, `value` | `:207-209` | ✅ |
| `reveal_causal_chain` | `chainId` | `:210-212` | ✅ |
| `add_ideology` | `blockId`, `flag`, `value` | `:213-218` | ✅ |
| `add_interest_group` | `blockId`, `flag`, `value` | `:219-224` | ✅ |
| `set_chain_summary` | `chainId`, `text` | `:225-227` | ✅ |
| `penetrate_block` | `blockId` | `:228-230` | ✅ |
| `set_ideology` | `ideologyId` | `:231-233` | ❌ 无数据 |
| `set_country_name` | `text` | `:234-236` | ❌ 无数据 |
| `set_leader` | `name`, `ideologyId` | `:237-239` | ❌ 无数据 |

### 读取代码时的注意点

- `ApplyEffect` 的 `switch` **没有 `default` 分支**，`Evaluate` / `Compare` 的 `default` 是 `return false`。也就是说 type 拼错会被**静默忽略**，不会报错也不会记日志。同步 type 表时如果发现表里有而代码里没有的项，先怀疑是代码写错还是表过期，必要时顺手给代码补 `default` 警告。
- `ConditionData` 的 `year/month/day` 只在 `time_point` 下有效；`flag`/`op`/`value` 只在 `flag_compare` 下有效。这些同结构字段的复用关系也要写进「相关字段」列。
- `flags` 字典里**未设置过的 flag 取值为 0**，所以 `flag_compare eq 0` 对任何没写过的 flag 都成立 —— 写表时可以顺带提醒策划这条语义。

## 飞书表位置

- 知识空间：策划部门（`space_id = 7504303201994473475`）
- 文档：**暗礁 type 对照表**
  - wiki 节点 token：`Uz22w3Yp4i86q2koCJHchaSEnzb`
  - 对象 token：`OzmGspaZLhufz0twnOLc6pWfngh`
  - 链接：https://icn64nguqe47.feishu.cn/wiki/Uz22w3Yp4i86q2koCJHchaSEnzb
  - 子表名：`type 对照表`

表结构（5 列）：

`类别 | type | 释义 | 相关字段 | 代码位置`

- 类别：触发条件 / 效果 / 比较操作符
- type：代码中的字符串字面量（如 `time_point`）
- 释义：该 type 的游戏含义
- 相关字段：该 type 生效时用到的同结构字段
- 代码位置：`文件名:行号`（定义 + 处理分支两处都标）

## 同步流程

1. 读取代码最新状态：`EventData.cs` 的注释、`EventManager.cs` 的 `case` 分支、`Events.json` 的实际 `type` 值。
2. 读取飞书表现状：
   ```powershell
   lark-cli sheets +csv-get --url "https://icn64nguqe47.feishu.cn/sheets/OzmGspaZLhufz0twnOLc6pWfngh" --sheet-name "type 对照表" --as user
   ```
   ⚠️ **读电子表格必须 `--as user`**：bot 身份没有 `sheets:spreadsheet:read` scope，会返回 `app_scope_not_applied`。若 user 身份过期（`lark-cli auth status` 显示 `missing (refresh token expired)`），先 `lark-cli auth login`。
3. 对比差异：新增 / 删除 / 改义的 type，逐条列出。
4. 用 `lark-cli sheets +csv-put`（全量覆盖）或 `+cells-set`（定点改）写回；表头固定为 `类别,type,释义,相关字段,代码位置`。
5. 回读校验，确认与代码一致。

## 权限与红线

- Lark CLI 对 AI 完整开放，但**任何写操作**（改表、建表、删行）执行前：
  1. 先向用户说明将要做的改动（新增/删除/修改了哪些 type）；
  2. 等待用户确认；
  3. 再执行。
- 纯读取（`+csv-get`）无需征询，可直接执行。
- 同步方向是「代码为源，飞书为镜像」：以代码为准更新飞书，不要反向修改代码。
