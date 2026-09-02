---
name: lark-docs
description: 当用户提及「开发文档」「文档」「设计文档」「总纲」等词、需要查阅资料时使用——先在本项目本地查找，本地没有或过旧则改用 Lark CLI（lark-cli）在飞书搜索/读取。Lark CLI 权限对 AI 完整开放（可读可写），但任何改动操作必须先征得用户同意再执行。
---

# Lark 飞书文档访问

## 触发时机

1. 用户提到「开发文档 / 文档 / 设计文档 / 总纲」等词想查阅内容时，先在本地查找
   （如 `Assets/Words/` 下的 `.md`）。
2. 若本地没有，或本地版本过旧，改用 Lark CLI 去飞书里找。

## Lark CLI 使用要点

- 命令名是 `lark-cli`（已全局安装，user/bot 身份均已认证，`lark-cli doctor` 可自查）。
- 常用命令：
  - 搜索文件：`lark-cli drive +search --query "关键词" --format table`
  - 读文档正文：`lark-cli docs +fetch --doc "<url或token>" --doc-format markdown`
  - 查帮助：`lark-cli <domain> --help`
  - 读内嵌操作指引：`lark-cli skills list` / `lark-cli skills read <name>`
- 本项目已知的飞书文档：Wiki「暗礁 架空历史策略游戏总纲」
  （token `Qi1mwqQSHitfx4kaHWrcCgzPndd`）。

## 权限与红线

- Lark CLI 对 AI **完整开放**，读取、编辑、创建、删除等全部能力都可用。
- 但**任何涉及改动**的操作（编辑 / 删除 / 创建 / 改名 / 移动 / 上传等）：
  1. 先向用户清楚说明将要做的改动；
  2. 等待用户确认；
  3. 再按用户的意见执行。
- 纯读取（搜索、`+fetch`）无需征询，可直接执行。
