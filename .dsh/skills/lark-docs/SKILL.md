---
name: lark-docs
description: 当用户提及「开发文档」「文档」「设计文档」「总纲」等词、需要查阅资料时使用——先在本项目本地查找，本地没有或过旧则改用 Lark CLI（lark-cli）在飞书搜索/读取。Lark CLI 权限对 AI 完整开放（可读可写），但任何改动操作必须先征得用户同意再执行。
whenToUse: 需要查阅暗礁项目的设计文档、总纲、名词对照表、type 对照表，或要把文档内容落成代码、核对代码与文档是否一致时。
metadata:
  requires:
    bins: ["lark-cli"]
---

# Lark 飞书文档访问

## 触发时机

1. 用户提到「开发文档 / 文档 / 设计文档 / 总纲」等词想查阅内容时，先在本地查找。
2. 若本地没有，或本地版本过旧，改用 Lark CLI 去飞书里找。

## 本地有什么（先查这里）

**本仓库目前不存放设计文档。** 原 `.opencode/skills/lark-docs/SKILL.md` 里写的 `Assets/Words/` 目录在仓库中**并不存在**（已核对 `git ls-files Assets`），不要在那里找。项目内唯一的本地文字资料是：

- `Assets/Resources/*.json`、`Assets/Province/ProvinceData.json` —— 运行期数据，不是设计文档；
- `.dsh/skills/*/SKILL.md` —— 本项目的 AI 技能说明。

真正的设计文档全部在飞书。所以绝大多数情况下应**直接走 Lark CLI**。

## Lark CLI 使用要点

- 命令名是 `lark-cli`（已全局安装，user/bot 身份均已认证，`lark-cli doctor` 可自查）。
- 常用命令：
  - 搜索文件：`lark-cli drive +search --query "关键词" --format table`
  - 读文档正文：`lark-cli docs +fetch --doc "<url或token>" --doc-format markdown`
  - 查帮助：`lark-cli <domain> --help`
  - 读内嵌操作指引：`lark-cli skills list` / `lark-cli skills read <name>`
- 本项目已知的飞书文档（完整索引见 `reef-creating-spec` skill）：
  - 总纲：`Qi1mwqQSHitfx4kaHWrcCgzPndd`（Wiki「暗礁 架空历史策略游戏总纲」）
  - 创作规范与AI CLI事宜：`W2xBweEs7ipxDOkRN8IcBg5KnUb`

## 身份与 scope（实测坑，先看这里）

`lark-cli auth status` 会返回两个身份，**权限并不对等**：

| 身份 | 能力 | 注意 |
|---|---|---|
| `--as bot` | 能读**文档**（`docs:document.content:read` 在 scope 内） | **缺 `sheets:spreadsheet:read`**，读电子表格会报 `app_scope_not_applied` |
| `--as user` | scope 完整（含 sheets） | **会过期**。过期后 `auth status` 显示 `missing (refresh token expired)`，此时 `--as user` 全部失败 |

- 默认 `defaultAs: auto`，会退到可用身份。所以「读不到表」和「读不到文档」是两个不同的故障，先看 `auth status` 再排查。
- 需要 user 身份时先 `lark-cli auth login` 补认证；**不要**把缺 scope 当成文档不存在。

## DSH 里已有更专门的 skill

本 skill 只负责「找到并读进来」这一步。具体领域操作请优先用 DSH 自带的更细的 skill：

| 需求 | 用哪个 |
|---|---|
| 读/写 docx、wiki 正文 | `lark-doc`（注意是单数，与本 skill 不同名） |
| 云空间文件搜索/上传/下载 | `lark-drive` |
| 读取表格内容 | `lark-sheets` |
| 认证、身份、缺 scope、`_notice` 信封 | `lark-shared` |
| 知识空间节点与成员 | `lark-wiki` |

## 权限与红线

- Lark CLI 对 AI **完整开放**，读取、编辑、创建、删除等全部能力都可用。
- 但**任何涉及改动**的操作（编辑 / 删除 / 创建 / 改名 / 移动 / 上传等）：
  1. 先向用户清楚说明将要做的改动；
  2. 等待用户确认；
  3. 再按用户的意见执行。
- 纯读取（搜索、`+fetch`）无需征询，可直接执行。
- 注意技术侧的另一条红线（来自《创作规范与AI CLI事宜》1.4.2）：飞书 bot 的后端跑在项目主理人的个人电脑上且**未放行写文件权限**，不要把「让飞书里的 agent 改代码」当作交付路径；代码走 GitHub。
