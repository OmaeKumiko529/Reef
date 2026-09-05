---
name: reef-heatmap
description: 更新「暗礁」项目活动热力图（飞书电子表格）。当需要刷新热力图、更新每日活跃数据、重新生成 GitHub 风格贡献热力图时使用。
---

# 暗礁 活动热力图更新

「暗礁」项目的 GitHub 风格贡献热力图，统计每日活跃度 = git 提交次数 + 飞书文档编辑次数，渲染为 7×N 的绿格矩阵。

## 目标表格

- 标题：`暗礁 活动热力图`
- URL：`https://icn64nguqe47.feishu.cn/sheets/ZMBVsLgpLhzLNgtGKevcLMAFnzc`
- spreadsheet token：`ZMBVsLgpLhzLNgtGKevcLMAFnzc`
- sheet_id：`e87fd2`（Sheet1）

## 人员身份

热力图按人拆分时的身份映射：

| 人 | 飞书 open_id | 历史 editor_id | 说明 |
|---|---|---|---|
| FishMoy（黄前不长酒梅子） | `ou_d5c023e7df5fd8dc976b1314ab82d64d` | `7501276975559589916` | 项目主理人，git 提交作者 |
| 米诺陶诺斯 | `ou_ce1ad5880944c7d2ab86cbb5cd9034c0` | `7672809211067108328` | 协作者（按策划部成员推断，待实名确认） |
| DeepSeek娘 | `ou_632cb27efc3f77a1a005e90bc60f9e5e` | `7680500222740499444` | AI 助手（lark-cli 的 user 身份） |

- git 作者名 `黄前不长酒梅子` = FishMoy（项目主理人，即用户本人）；git 提交目前全部归 FishMoy。
- `+history-list` 返回的 `editor_ids` 是数字 ID（非 open_id），按上表映射到人。
- 数字 editor_id 无法直接用 `contact +get-user` 反查（需 `contact:user.employee_id:readonly` scope），实名以人工确认为准。

## 数据源

### 1. git 提交（按日计数）

```powershell
git -C "F:\Projects\Reef" log --pretty="%ad" --date=short
```

每行一个日期 `YYYY-MM-DD`，按日计数即可。

### 2. 文档编辑（4 个 docx 的版本历史）

对下面每个 docx 调用 `docs +history-list` 并翻页（`has_more` / `page_token`），收集所有 `edit_time`：

| 文档 | token |
|---|---|
| 总纲 | `Qi1mwqQSHitfx4kaHWrcCgzPndd` |
| 创作规范与AI CLI事宜 | `W2xBweEs7ipxDOkRN8IcBg5KnUb` |
| 科技树设计 | `MaXPwxjUcicN78klN8Pcjgmpnqf` |
| 新成员索引 | `CEqaw7NaPiSx22kVPDJc0HGknBf` |

```powershell
lark-cli docs +history-list --doc <token> --page-size 20            # 首页
lark-cli docs +history-list --doc <token> --page-size 20 --page-token <token>   # 翻页
```

- `edit_time` 是 **UTC**（`...Z`），转北京时间需 `+8` 小时再取日期 `YYYY-MM-DD`。
- 只统计 docx；名词对照表 / type 对照表是电子表格，无逐日历史，**不计入**。

## 合并

```
total = git 提交数 + 文档历史快照数
```

## 布局（滚动 N 周，默认 4 周）

- 窗口：从「N 周前的周一」到「本周周日」。
- 第 1 行：A1 空，B1 起为各列**周起始日期**（`MM-DD`，加粗居中）。
- 第 2–8 行：A 列 = `周一`~`周日`（加粗），B 列起为当日 `total`，0 留空。
- 图例行：`图例` + 五档色块（`0` / `1-20` / `21-45` / `46-90` / `91+`）。
- 下方附明细表：`日期 / git 提交 / 文档编辑 / 总计`。

## 颜色（GitHub 五档）

| total | 背景色 | 文字色 |
|---|---|---|
| 0 | `#ebedf0` | — |
| 1–20 | `#9be9a8` | — |
| 21–45 | `#40c463` | — |
| 46–90 | `#30a14e` | `#ffffff` |
| 91+ | `#216e39` | `#ffffff` |

格内文字居中、垂直居中；列 B 起宽约 30px、行 2–8 高约 26px（近似方块）。

## 更新步骤

1. **取数**：git 日志 + 4 个 docx 的 `+history-list`（翻页取全），按日计数。
2. **合并**：`total = git + docs`，得到 `日期 → total` 映射。
3. **定窗口**：以今天为终点，算出滚动 N 周的周一~周日日期范围。
4. **生成**：按上文布局产出 values 二维数组（`--values` 风格）与 styles JSON（`--styles` 风格），为每个网格格上色、深色格白字。
5. **写回**，二选一：
   - **重建**（简单，链接会变）：
     ```powershell
     lark-cli sheets +workbook-create --title "暗礁 活动热力图" --values "@./hm_values.json" --styles "@./hm_styles.json"
     ```
   - **原地更新**（保留链接）：用 `+table-put --spreadsheet-token <token> --sheets ... --styles ...`（mode=overwrite）或 `+cells-set` + `+styles-put` 覆盖同张表；注意空值不回写时用 `+cells-clear`（high-risk，需 `--yes`）先清数据区。

## 注意

- 文档编辑数是**版本快照数**（非逐字编辑），量级远大于 git，图上颜色基本由文档编辑主导；git 只能从明细表分辨。
- 飞书无可订阅的「文档编辑」实时事件，只能定时轮询刷新，做不到实时。
