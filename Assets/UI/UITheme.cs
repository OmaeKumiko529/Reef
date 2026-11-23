using UnityEngine;

public static class UITheme
{
    // 档案纸张浅色调
    public static readonly Color PaperBg     = new Color(0.906f, 0.871f, 0.784f, 1f);   // #E7DEC8 米灰旧纸
    public static readonly Color PaperTop    = new Color(0.941f, 0.914f, 0.839f, 1f);   // #F0E9D6 更浅纸
    public static readonly Color InkPrimary  = new Color(0.165f, 0.137f, 0.094f, 1f);   // #2A2318 深墨
    public static readonly Color InkSecondary= new Color(0.353f, 0.318f, 0.271f, 1f);   // #5A5145 灰棕
    public static readonly Color InkMuted    = new Color(0.541f, 0.502f, 0.447f, 1f);   // #8A8072 浅棕
    public static readonly Color SealRed     = new Color(0.627f, 0.188f, 0.094f, 1f);   // #A03018 朱砂印章红
    public static readonly Color AccentGold  = new Color(0.690f, 0.541f, 0.243f, 1f);   // #B08A3E 黄铜金
    public static readonly Color Divider     = new Color(0.788f, 0.749f, 0.651f, 1f);   // #C9BFA6 旧墨线
    public static readonly Color Redacted    = new Color(0.227f, 0.204f, 0.173f, 1f);   // #3A342C 迷雾遮蔽

    // 覆盖在彩色地图上的遮罩（保持暗色以突出纸面卡片）
    public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.55f);

    // 面板实底透明度（叠在地图上）
    public const float PanelAlpha = 0.97f;

    // ── 设计 token（R3 新增，供程序直接消费；只增不改，不影响既有引用）──────────
    // 间距梯度（4 基数）
    public const float SpaceXs = 4f;   public const float SpaceSm = 8f;    public const float SpaceMd = 12f;
    public const float SpaceLg = 16f;  public const float SpaceXl = 20f;   public const float Space2Xl = 30f;
    public const float Space3Xl = 40f;

    // 字号梯度（= 项目当前实际在用的全部字号，逐个替换不会产生视觉变化）
    public const int FontCaption = 18;  public const int FontSmall = 20;    public const int FontBody = 22;
    public const int FontButton = 24;   public const int FontSubtitle = 26; public const int FontClock = 30;
    public const int FontTitle = 32;    public const int FontDisplay = 34;

    // 面板（PanelRadius 本轮无处消费：项目零 Sprite，圆角需程序化生成 9-slice 贴图）
    public const float PanelRadius = 6f;  public const float PanelPad = 16f;
    public static readonly Color ShadowColor    = new Color(0.165f, 0.137f, 0.094f, 0.28f);
    public static readonly Vector2 ShadowOffset = new Vector2(0f, -3f);

    // 地图色调（与 MapMat.mat 的 _PaperWash/_PaperTint/_EdgeInk/_Vignette 目标值一一对应）
    public static readonly Color MapPaper = new Color(0.906f, 0.871f, 0.784f, 1f);    // #E7DEC8 同 PaperBg
    public const float MapPaperWash = 0.40f;  public const float MapPaperTint = 0.15f;
    public static readonly Color MapInk = new Color(0.165f, 0.137f, 0.094f, 1f);      // #2A2318 同 InkPrimary
    public const float MapInkAlpha = 0.55f;   public const float MapVignette = 0.35f;
    // 相机 Solid Color 背景（地图纸背）：暗于地图白场、深于面板纸底，地图才像摊在桌上的一页纸
    public static readonly Color MapBackdrop = new Color(0.706f, 0.678f, 0.616f, 1f); // #B4AD9D 灰褐纸背

    // 状态色：只留两枚（朱砂 / 黄铜），不引入红黄绿交通灯
    public static readonly Color StateHover    = new Color(0.690f, 0.541f, 0.243f, 0.22f); // AccentGold @22%
    public static readonly Color StateSelected = new Color(0.690f, 0.541f, 0.243f, 1f);   // AccentGold
    public static readonly Color StateAlert    = SealRed;                                  // 印章红即危险色

    // 时长
    public const float FadeFast = 0.15f;
    public const float FadeNormal = 0.25f;
    public const float SlideNormal = 0.3f;
    public const float StampNormal = 0.28f;
    public const float TypeCharDelay = 0.02f;
    public const float CountUpNormal = 0.4f;

    // 缓动
    public static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    public static float EaseInOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ══ 第二轮 UI 成品化（契约 70_ §4.0）新增 token，共 72 项，只增不改 ══════════════════
    // 69 项逐字来自 61_美术_UI装饰语言.md §9.0；3 项由 Lead 追加：TopBarLeftInset（契约 8.0）、
    // TopBarHeight（R-04）、JournalTopInset（L-06）。★ = 美术 P0 最小集（32 项）。
    // 整体回退：删除本区块即可 —— 既有 token 与既有调用点零影响，仅程序引用新名处会编译失败。

    // 网格与安全区
    public const float MarginScreen = 20f;   // ★ 屏幕外边距基准
    public const float SafeTop = 70f;        // ★ HUD 顶带（R-04 后顶栏实际占用 84 = 上边距 20 + 高 64）
    public const float SafeBottom = 80f;     // ★ 底栏
    public const float SafeInset = 20f;      // ★ 安全带内侧再收 20

    // 宽度档位与高度
    public const float WidthXs = 300f;           // 日期牌（特例）
    public const float WidthS = 380f;            // ★ 详情面板 / 日志 / 事件 tooltip
    public const float WidthM = 620f;            // ★ 浮层 / 渗透提示条
    public const float WidthL = 820f;            // ★ 机构总览
    public const float TopBarLeftInset = 20f;    // ★ 顶栏左内缩 = 屏幕外边距（左侧无构件；与右内缩成对）
    public const float TopBarRightInset = 340f;  // ★ 顶栏右内缩 = 避让右上角日期牌 300 + 2×20 间隙（1920−1580；日期牌实测 x∈[1600,1900]，见 61_ §6.0/§10.0）
    public const float TopBarHeight = 64f;       // 顶栏高（R-04）
    public const float JournalTopInset = 104f;   // 日志顶留（L-06）
    public const float TitleBarHeight = 56f;     // ★ 面板标题栏高

    // 内边距、行高与按钮高
    public const float PadPanel = 24f;          // ★ 面板
    public const float PadCard = 16f;           // ★ 卡片（值同既有 PanelPad）
    public const float PadRow = 8f;             // ★ 行
    public const float RowHeight = 56f;         // 常规行
    public const float RowHeightCompact = 44f;  // 紧凑行
    public const float BtnHeight = 40f;         // 标准按钮
    public const float BtnHeightSm = 32f;       // 小按钮

    // 框线、内高光与压痕
    public const float ChromeLineWidth = 1f;   // ★ 线宽
    public const float ChromeStrongLine = 2f;  // 强调线
    public const float ChromeDoubleGap = 2f;   // 双线间距
    public static readonly Color ChromeOuterColor = new Color(0.165f, 0.137f, 0.094f, 0.55f); // ★ 外框
    public static readonly Color ChromeInnerColor = new Color(0.788f, 0.749f, 0.651f, 0.90f); // ★ 内线
    public static readonly Color HighlightColor   = new Color(0.941f, 0.914f, 0.839f, 0.55f);  // ★ 内高光（顶 1px）
    public static readonly Color PressColor       = new Color(0.165f, 0.137f, 0.094f, 0.35f);  // ★ 压痕（底 1px）

    // 标题栏
    public static readonly Color TitleBarBg       = PaperTop;                                 // ★
    public static readonly Color TitleRuleColor   = new Color(0.165f, 0.137f, 0.094f, 0.85f); // ★ 下沿 2px
    public const float TitleRuleWidth = 2f;                                                   // ★
    public static readonly Color TitleAccentColor = SealRed;                                  // ★ 朱砂竖块
    public static readonly Vector2 TitleAccentSize = new Vector2(4f, 18f);                    // ★
    public const float TitleAccentInset = 12f;                                                // 竖块距左

    // 投影：本轮只做 UGUI Shadow 一级；下列三级柔和版为下一轮预留
    public const float ShadowLayer1Alpha = 0.16f;
    public const float ShadowLayer2Alpha = 0.10f;
    public const float ShadowLayer3Alpha = 0.06f;
    public const float ShadowLayerStep = 2f;

    // 按钮五态
    public static readonly Color BtnNormalBg        = PaperTop;                                // ★
    public static readonly Color BtnNormalBorder    = new Color(0.165f, 0.137f, 0.094f, 0.35f);
    public static readonly Color BtnHoverBg         = new Color(0.901f, 0.854f, 0.744f, 1f);   // ★
    public static readonly Color BtnHoverBorder     = AccentGold;
    public static readonly Color BtnPressedBg       = new Color(0.829f, 0.786f, 0.684f, 1f);   // ★
    public static readonly Color BtnSelectedBg      = PaperTop;                                // ★
    public static readonly Color BtnSelectedBarColor= SealRed;
    public const float BtnSelectedBarWidth = 3f;
    public static readonly Color RowSelectedBarColor= AccentGold;
    public static readonly Color BtnDisabledBg      = new Color(0.841f, 0.804f, 0.711f, 1f);   // ★
    public static readonly Color BtnDisabledText    = InkMuted;
    public static readonly Color BtnDisabledBorder  = Divider;

    // 卡片
    public static readonly Color CardBg          = PaperTop;  // ★
    public static readonly Color CardBorderColor = Divider;   // ★
    public const float CardGap = 8f;

    // 档号角标 / 领导人色签 / 打码条
    public static readonly Color DossierTagBg   = new Color(0.788f, 0.749f, 0.651f, 0.45f);
    public const float DossierTagHeight = 22f;
    public const float DossierTagPadX = 8f;
    public static readonly Color DossierTagText = InkSecondary;
    public static readonly Color LeaderDotColor = new Color(0.788f, 0.749f, 0.651f, 0.90f);
    public const float RedactedBarHeight = 16f;

    // 朱砂印章 / 装订方点
    public const float StampSize = 88f;
    public const float StampBorderWidth = 2f;
    public const float StampInset = 6f;
    public const float StampRotation = -8f;
    public const float StampAlpha = 0.85f;
    public const int StampFontSize = FontDisplay;
    public const int StampSubFontSize = FontSmall;
    public const float BindingDotSize = 4f;
    public const float BindingDotGap = 24f;
    public static readonly Color BindingDotColor = new Color(0.788f, 0.749f, 0.651f, 0.80f);

    // 地图标签
    public const float LabelSelectedAlpha = 0.97f;                                     // ★
    public const float LabelHoverAlpha = 0.90f;                                        // ★
    public static readonly Vector2 LabelHoverSize = new Vector2(260f, 40f);
    public const float LabelSelectedBarWidth = 4f;
}
