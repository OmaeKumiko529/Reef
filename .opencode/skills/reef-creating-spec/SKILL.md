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

```powershell
lark-cli docs +fetch --doc "W2xBweEs7ipxDOkRN8IcBg5KnUb" --doc-format markdown
```

- 文档标题：《创作规范与AI CLI事宜》
- wiki 链接：`https://icn64nguqe47.feishu.cn/wiki/W2xBweEs7ipxDOkRN8IcBg5KnUb`
- token：`W2xBweEs7ipxDOkRN8IcBg5KnUb`

读完后，把其中与当前任务相关的规范真正应用到后续工作中，而不是只读不做。

## 文档要点速览（便于快速定位，细节以原文为准）

1. **1.2 Claude Code 八荣八耻**：适用于所有能读写项目文件的 AI Agent 的基本原则。原文在飞书电子表格里，下面是 2026-10-11 读取的快照（表格为真源，有疑问就重读）：

   | # | 荣（应当） | 耻（避免） | 核心约束 |
   |---|---|---|---|
   | 1 | 查档求证 Verify Before Assuming | 臆猜接口 Guessing Interfaces | API / 参数以实际代码为准，不凭记忆猜测 |
   | 2 | 对齐需求 Align Before Building | 模糊开工 Vague Kickoffs | 需求不清时确认，不自行脑补 |
   | 3 | 请示规则 Ask About Business Rules | 脑补业务 Fabricating Domain Logic | 业务逻辑向人确认，不自行假设 |
   | 4 | 复用存量 Reuse Existing Code | 新增冗余 Adding Redundancy | 先搜已有实现，不重复造轮子 |
   | 5 | 完备测例 Complete Test Cases | 省略校验 Skipping Edge Cases | 边界 / 错误 / 空值全量覆盖，不只测顺路 |
   | 6 | 恪守规范 Follow Conventions | 乱改架构 Arbitrary Refactoring | 遵循项目既有约定，不凭个人偏好重构 |
   | 7 | 坦诚存疑 Admit Uncertainty | 不懂装懂 Pretending to Know | 不确定就说不确定，不编造答案 |
   | 8 | 分步迭代 Iterate Incrementally | 批量乱改 Bulk Chaotic Changes | 小步可验证，不一口气改多个模块 |

   重读命令：
   ```powershell
   lark-cli sheets +csv-get --spreadsheet-token "HozAsvza9hv8NhtHgI9cdqGkn2Y" --sheet-id "xvkcen" --as user
   ```
   注意：**必须 `--as user`**，bot 身份没有 `sheets:spreadsheet:read` scope，会报 `app_scope_not_applied`。若 user 身份过期（`lark-cli auth status` 显示 `missing (refresh token expired)`）就先 `lark-cli auth login --domain all --no-wait --json`（详见 `lark-shared` skill 的 split-flow）。
2. **1.3 AI 使用建议 / CLI 安装**：Node.js 装 `@larksuite/cli`、Unity CLI；基础 `.SKILL`（`reef-type-sync`、`lark-docs`、`unity-cli` —— 这三份被内嵌在文档里，是官方认定的基础技能）；图像生成式 AI 的使用红线。
3. **1.3.2 加载方式**：*「请优先按照所使用 AI 工具的官方方式加载 `.SKILL` 文件。如果你不清楚具体的加载方式，可以向 AI 询问当前工具支持的 Skill 安装或加载方法。」* —— 也就是说加载器由所用 AI 工具决定；本项目 skill 已按 **DSH 的文件系统 skill 规范**安装在 `<项目根>/.dsh/skills/<name>/SKILL.md`（本仓库即 `F:\Projects\Reef\.dsh\skills\`）。
4. **1.4 已配置好的 AI Agent（OpenCode Lark CLI）**：
   - 群聊通过 `@` 调用，私聊直接对话；
   - 修改文档时使用 **DeepSeek娘**（`--as user`）身份；
   - 后端运行在个人电脑，**未放行写文件权限**——技术侧不要用它做写操作，写代码/PR 走 GitHub：
     `https://github.com/OmaeKumiko529/Reef`（该页面同时负责维护项目 `.SKILL` 文件）。
5. **2.0 创作规范**：
   - **2.1 格式**：逐级标题带数字索引（`# 1.0`、`### 1.1.1`），标题层级与数字必须对应；文本用完整自然语句，禁止草稿式文段；技术内容加注解；代码块、`Ctrl+Shift+C` 关键字标记、加粗下划线标记机制关键字；生造词多时建「名词对照表」。
   - **2.2 内容**：程序/策划与文案/美术互不越权；技术文档严格基于源代码；文案美术文档控制字数用「一索引多子页」；用图标（❓❗‼️⁉️❌ / 🟢🔴🟠 / 🆕🆑）标记文档状态；遵循职责边界——他人核心设定不得未经沟通直接改写，小修（错别字/格式/失效链接）可直接修。
6. **Token 纪律**：文档的信息公示板会公开累计 Token 与成本，并明确要求「请节约 Token」。写文档、读文档、跑 agent 时都按此约束自己。

## 相关文档索引

- 总纲：`Qi1mwqQSHitfx4kaHWrcCgzPndd`
- 创作规范与AI CLI事宜：`W2xBweEs7ipxDOkRN8IcBg5KnUb`
- 名词对照表：`HxqPwiuPFitbSxkJlOBcYlLRnNc`
- type 对照表：`Uz22w3Yp4i86q2koCJHchaSEnzb`
- 科技树设计：`MaXPwxjUcicN78klN8Pcjgmpnqf`
- 八荣八耻电子表格：spreadsheet `HozAsvza9hv8NhtHgI9cdqGkn2Y`，sheet `xvkcen`

## 本仓库 skill 清单

本项目的 skill 全部位于 `.dsh/skills/`，DSH 会自动发现（新增/改名无需重启）：

| skill | 用途 |
|---|---|
| `reef-creating-spec` | 本文件——强制前置阅读与规范索引 |
| `unity-cli` | 用 Unity CLI 做编译校验/测试/构建 |
| `lark-docs` | 查飞书文档的入口与读写红线 |
| `reef-type-sync` | 事件系统 type 注册表与飞书表的同步 |
| `reef-changelog` | 生成更新日志的格式规则 |
| `reef-heatmap` | 刷新飞书活动热力图 |
| `reef-chat-reply` | 飞书群聊回复的身份规则 |
| `feishu-agent` | 飞书 bot 守护进程的启停与排障 |
