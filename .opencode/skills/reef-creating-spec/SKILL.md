---
name: reef-creating-spec
description: 暗礁（Reef）项目统一创作规范与 AI CLI 使用规矩。任何与暗礁项目相关的工作（创作、程序开发、机制设计、美术、文档写作、飞书操作、AI CLI 使用等）开始前，必须先读取飞书文档《创作规范与AI CLI事宜》——这是强制前置步骤，不得跳过。
---

# 暗礁创作规范与 AI CLI 事宜（强制前置阅读）

> 本 skill 是「暗礁」项目的强制前置步骤。**在执行任何与暗礁项目相关的工作之前，必须先读取飞书文档《创作规范与AI CLI事宜》的最新内容，再动手。**

## 为什么必须先读

该文档统一了项目协作者在文档创作、程序开发、机制设计、美术设计与 AI Agent 辅助创作过程中的基本规范，是「信息结构与协作方式的统一参考标准」。跳过阅读会导致术语不统一、文档结构混乱、技术内容脱离实际项目，造成重复劳动与返工。

## 怎么读（最新内容为准）

文档内容会持续更新（含信息公示板、Token/成本统计），**每次开始工作前都要重新 fetch 最新版本**，不要依赖记忆或缓存：

```bash
lark-cli docs +fetch --doc "W2xBweEs7ipxDOkRN8IcBg5KnUb" --doc-format markdown
```

- 文档标题：《创作规范与AI CLI事宜》
- wiki 链接：`https://icn64nguqe47.feishu.cn/wiki/W2xBweEs7ipxDOkRN8IcBg5KnUb`
- token：`W2xBweEs7ipxDOkRN8IcBg5KnUb`

读完后，把其中与当前任务相关的规范真正应用到后续工作中，而不是只读不做。

## 文档要点速览（便于快速定位，细节以原文为准）

1. **1.2 Claude Code 八荣八耻**：适用于所有能读写项目文件的 AI Agent 的基本原则（内嵌表格，需另读对应 sheet）。
2. **1.3 AI 使用建议 / CLI 安装**：Node.js 装 `@larksuite/cli`、Unity CLI；基础 `.SKILL`（`reef-type-sync`、`lark-docs`、`unity-cli`）；图像生成式 AI 的使用红线。
3. **1.4 已配置好的 AI Agent（OpenCode Lark CLI）**：
   - 群聊通过 `@` 调用，私聊直接对话；
   - 修改文档时使用 **DeepSeek娘**（`--as user`）身份；
   - 后端运行在个人电脑，**未放行写文件权限**——技术侧不要用它做写操作，写代码/PR 走 GitHub。
4. **2.0 创作规范**：
   - **2.1 格式**：逐级标题带数字索引（`# 1.0`、`### 1.1.1`），标题层级与数字必须对应；文本用完整自然语句，禁止草稿式文段；技术内容加注解；代码块、`Ctrl+Shift+C` 关键字标记、加粗下划线标记机制关键字；生造词多时建「名词对照表」。
   - **2.2 内容**：程序/策划与文案/美术互不越权；技术文档严格基于源代码；文案美术文档控制字数用「一索引多子页」；用图标（❓❗‼️⁉️❌ / 🟢🔴🟠 / 🆕🆑）标记文档状态；遵循职责边界——他人核心设定不得未经沟通直接改写，小修（错别字/格式/失效链接）可直接修。

## 相关文档索引

- 总纲：`Qi1mwqQSHitfx4kaHWrcCgzPndd`
- 名词对照表：`HxqPwiuPFitbSxkJlOBcYlLRnNc`
- type 对照表：`Uz22w3Yp4i86q2koCJHchaSEnzb`
