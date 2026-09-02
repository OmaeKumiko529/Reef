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

## type 的权威来源（代码）

事件系统的 type 是一组字符串字面量，共三类：

| 类别 | 字段 | 定义位置 | 取值 |
|---|---|---|---|
| 触发条件 | `ConditionData.type` | `Assets/Event/EventData.cs:4` | `time_point` / `flag_compare` |
| 比较操作符 | `ConditionData.op` | `Assets/Event/EventData.cs:7` | `eq` / `ne` / `gt` / `ge` / `lt` / `le` |
| 效果 | `EffectData.type` | `Assets/Event/EventData.cs:14` | `add_influence` / `set_flag` |

处理分支集中在 `Assets/Event/EventManager.cs`：

- `Evaluate`（`EventManager.cs:130-142`）—— 触发条件分支
- `Compare`（`EventManager.cs:144-156`）—— 比较操作符分支
- `ApplyEffect`（`EventManager.cs:192-204`）—— 效果分支

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
   ```bash
   lark-cli sheets +csv-get --url "https://icn64nguqe47.feishu.cn/sheets/OzmGspaZLhufz0twnOLc6pWfngh" --sheet-name "type 对照表" --as user
   ```
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
