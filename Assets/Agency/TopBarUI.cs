using UnityEngine;
using UnityEngine.UI;

public class TopBarUI : MonoBehaviour
{
    static TopBarUI _instance;

    public static TopBarUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TopBarUI>();
                if (_instance == null)
                {
                    var go = new GameObject("TopBarUI");
                    _instance = go.AddComponent<TopBarUI>();
                }
            }
            return _instance;
        }
    }

    public int administrativePower = 200;   // 内务处一级初始
    public int intelligencePoints = 50;     // 情报处一级初始

    public Font font;

    // ===== 契约 70_ §8.0 / §9.0 L-05（冻结值，1920×1080 参考像素；单源取 token）=====
    const float BarHeight = UITheme.TopBarHeight;       // 64：HUD 顶部占位 = 20 + 64 = 84（与 CameraPanController.topReservePixels 成对）
    const float TopInset = 20f;
    const float LeftInset = UITheme.TopBarLeftInset;    // 20：屏幕外边距（左侧无构件）
    const float RightInset = UITheme.TopBarRightInset;  // 340：避让右上日期牌 300 + 2×20 间隙
    const float ValueCellWidth = 220f;  // 两个数值各自固定宽，右对齐成组
    // 国名串三级降级阈值，按"栏宽 = W − 20 − 340"判定（L-05 / §8.0）
    const float WidthFull = 1200f;      // ≥1200：{国名} · {意识形态} · 领导人 {名}
    const float WidthMedium = 900f;     // ≥900：{国名} · {意识形态}；再窄只留 {国名}

    Canvas canvas;
    Text countryText;
    Text powerText;
    Text pointsText;
    RectTransform barRect;
    Coroutine powerRoutine;
    Coroutine pointsRoutine;
    float lastBarWidth = -1f;

    void OnEnable() { CountryManager.OnCountryChanged += Refresh; }
    void OnDisable() { CountryManager.OnCountryChanged -= Refresh; }

    public void Setup(Font font)
    {
        this.font = font;
        EnsureUI();
        Refresh();
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("TopBarCanvas", transform, UISorting.Hud);

        var panelGo = new GameObject("TopBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        panelGo.transform.SetParent(canvas.transform, false);
        var img = panelGo.GetComponent<Image>();
        img.color = UITheme.PaperBg;

        // L-05：左右同时 stretch（左 20 = 屏幕外边距 / 右 340 = 避让右上角日期牌），高度 64，上边距 20
        // → 栏宽 = W − 360：1920→1560、1600→1240、1440→1080、1280→920、1200→840
        // （国名串再按 WidthFull=1200 / WidthMedium=900 的栏宽阈值降级，见 ComposeCountryLine）
        barRect = panelGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(-(LeftInset + RightInset), BarHeight);
        barRect.anchoredPosition = new Vector2((LeftInset - RightInset) * 0.5f, -TopInset);

        // 契约 70_ §7.1：Strip 形态 = 纸底 + 单层 1px 框 + 投影（无标题栏；尺寸不变）
        UIPanelChrome.AddBorder(barRect, 0f, UITheme.ChromeOuterColor);
        UIPanelChrome.AddShadow(img);

        var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 0, 0);
        layout.spacing = 32f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;   // 宽度改为 LayoutElement 决定：国名弹性、数值固定 220
        layout.childForceExpandHeight = true;

        countryText = CreateLabel("Country", panelGo.transform, TextAnchor.MiddleLeft);
        countryText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        powerText = CreateLabel("Power", panelGo.transform, TextAnchor.MiddleRight);
        powerText.gameObject.AddComponent<LayoutElement>().preferredWidth = ValueCellWidth;

        pointsText = CreateLabel("Points", panelGo.transform, TextAnchor.MiddleRight);
        pointsText.gameObject.AddComponent<LayoutElement>().preferredWidth = ValueCellWidth;
        // 「正在研究：暂无（科技树开发中）」整格已按策划备忘 3.3 删除（契约 2.3 也不提供该文案常量）；
        // 科技树实装后重新加回这一格与对应常量。
    }

    Text CreateLabel(string name, Transform parent, TextAnchor align)
    {
        // 走工厂：字号用 token、默认 Truncate（L-12）、raycastTarget=false
        return UIFactory.CreateText(name, parent, UITheme.FontBody, FontStyle.Normal, align, font);
    }

    void Update()
    {
        // 分辨率变化 → 栏宽变化 → 国名串详略要跟着变（三级降级）。
        // 每帧只比一个 float，无分配、无布局重建。
        if (barRect == null || countryText == null) return;
        float w = barRect.rect.width;
        if (Mathf.Abs(w - lastBarWidth) > 1f)
        {
            lastBarWidth = w;
            RefreshCountry();
        }
    }

    public void Refresh()
    {
        if (powerText == null) return;
        powerText.text = UICopy.TopBar.Power + administrativePower;
        pointsText.text = UICopy.TopBar.Points + intelligencePoints;
        RefreshCountry();
    }

    void RefreshCountry()
    {
        var c = CountryManager.Instance.country;
        if (c == null || countryText == null) return;

        string ideology = GameContent.Name(c.ideologyId);
        string leader = c.leader != null ? c.leader.name : "";
        countryText.text = ComposeCountryLine(c.displayName, ideology, leader);
    }

    /// <summary>
    /// 契约 70_ §8.0：按顶栏实际可用宽决定国名串的详略，三级降级、永不换行（截断由工厂默认的 Truncate 保证）。
    /// 首帧布局尚未刷新时 barRect.rect.width 可能为 0，此时按屏幕宽推算兜底。
    /// </summary>
    string ComposeCountryLine(string displayName, string ideology, string leader)
    {
        float w = barRect != null ? barRect.rect.width : 0f;
        if (w <= 1f) w = Screen.width - LeftInset - RightInset;

        // 两级式用 UICopy.TopBar.CountryFormat = "{0} · {1}"（契约 §5.0）、三级式再接 UICopy.TopBar.LeaderFormat = "领导人 {0}"
        string twoPart = string.Format(UICopy.TopBar.CountryFormat, displayName, ideology);
        if (w >= WidthFull) return twoPart + " · " + string.Format(UICopy.TopBar.LeaderFormat, leader);
        if (w >= WidthMedium) return twoPart;
        return displayName;
    }

    public void AddAdministrativePower(int amount)
    {
        int from = administrativePower;
        administrativePower += amount;
        if (powerRoutine != null) StopCoroutine(powerRoutine);
        powerRoutine = StartCoroutine(UIAnim.CountUp(powerText, UICopy.TopBar.Power, "", from, administrativePower, UITheme.CountUpNormal));
    }

    public void AddIntelligencePoints(int amount)
    {
        int from = intelligencePoints;
        intelligencePoints += amount;
        if (pointsRoutine != null) StopCoroutine(pointsRoutine);
        pointsRoutine = StartCoroutine(UIAnim.CountUp(pointsText, UICopy.TopBar.Points, "", from, intelligencePoints, UITheme.CountUpNormal));
    }
}
