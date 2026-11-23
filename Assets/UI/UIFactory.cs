using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时 UI 构建的公共辅助：集中创建 Canvas / Text / Button，避免各管理器重复实现。
/// 创建出来的纯装饰性 Text 一律 raycastTarget = false，避免文字挡住地图点击
/// （需要挡住点击的由面板自身的 Image 承担，见 00_方案与分工.md 2.5）。
/// </summary>
public static class UIFactory
{
    // 画布参考分辨率：所有布局常量按 1920x1080 书写
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    /// <summary>画布缩放模式：0 = 按宽度匹配，1 = 按高度匹配，0.5 = 两者折中（防极端宽高比下布局崩坏）。</summary>
    public const float MatchWidthOrHeight = 0.5f;

    // 仅供排版用的参考像素值，不是设计系统本身
    public const float DefaultTextWidth = 200f;

    /// <summary>
    /// 运行期缺省字体。❗必须由引导方尽早赋值（<c>ProvincePicker.Awake</c> 第一步就设）：
    /// 运行期用 <c>new GameObject(...)</c> / <c>AddComponent</c> 建出来的 Text，其 font 默认是
    /// <c>null</c>，而 UGUI 的 <c>Text.OnPopulateMesh</c> 在 <c>font == null</c> 时直接 return
    /// ——文字**一帧都不会画**，而且不报任何错。面板 chrome 的内建文字（标题栏/眉标/档号角标/
    /// 印章）都不带字体参数，全靠这个兜底；另外任何一次显式传字体的调用都会把第一个非空字体
    /// 记下来作为二次兜底（调用顺序早于 ProvincePicker 的场合）。
    /// </summary>
    public static Font DefaultFont;

    public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder = UISorting.Default)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        // 之前没设过这一项，默认 0（纯按宽匹配）：16:10 / 21:9 下纵向布局会溢出
        scaler.matchWidthOrHeight = MatchWidthOrHeight;
        return canvas;
    }

    /// <param name="overflow">
    /// 纵向溢出模式。默认 <see cref="VerticalWrapMode.Truncate"/>（契约 70_ §6.0/§9.0 L-12）：
    /// 文本超出容器时截断，而不是像原来那样溢出到面板外（判据 P4）。
    /// 需要"允许溢出"的调用方显式传 <see cref="VerticalWrapMode.Overflow"/>。
    /// </param>
    public static Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor align, Font font = null,
        VerticalWrapMode overflow = VerticalWrapMode.Truncate)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        // 字体解析顺序：调用方显式给的 → 第一个见过的非空字体 → UIFactory.DefaultFont
        if (font != null)
        {
            if (DefaultFont == null) DefaultFont = font;
        }
        else
        {
            font = DefaultFont;
        }
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = UITheme.InkPrimary;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = overflow;

        // 文字只负责显示：不参与射线检测，否则"点到字"会挡住地图/按钮
        t.raycastTarget = false;
        return t;
    }

    /// <summary>创建一个贴满父容器的文字（按钮标签 / 提示条正文的常见形态）。</summary>
    public static Text CreateStretchText(string name, Transform parent, int size, FontStyle style, TextAnchor align, Font font = null)
    {
        var t = CreateText(name, parent, size, style, align, font);
        Stretch(t.rectTransform);
        return t;
    }

    /// <summary>把 RectTransform 拉伸至贴满父容器（offsetMin/Max 归零）。</summary>
    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>面板底：米色实底 + 可选 CanvasGroup（配合 UIAnim.Fade 做淡入淡出）。</summary>
    public static Image CreatePanel(string name, Transform parent, Color bg, bool withGroup = false)
    {
        var types = withGroup
            ? new System.Type[] { typeof(RectTransform), typeof(Image), typeof(CanvasGroup) }
            : new System.Type[] { typeof(RectTransform), typeof(Image) };

        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;
        return img;
    }

    /// <summary>
    /// 锚定在某条边上的面板形态：pivot 贴边，便于做滑入/滑出。
    /// </summary>
    public static RectTransform AnchorTo(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 offset)
    {
        if (rt == null) return null;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        return rt;
    }

    public static Button CreateButton(string name, Transform parent, string label, Font font, int fontSize,
        Color bg, Color textColor, System.Action onClick = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var bgImg = go.GetComponent<Image>();
        bgImg.color = bg;
        // 压印按钮板（中性白素材）：五态换色仍由 UIButtonState 改 color 实现；素材缺失则保持纯色
        UIArt.ApplySliced(bgImg, UIArt.ButtonPlate);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        CreateStretchText("Label", go.transform, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, font).color = textColor;

        // 契约 70_ §6.0：工厂创建的按钮默认挂五态（Action）；调用方可用 UIButtonState.Apply 覆盖模式
        UIButtonState.Apply(btn, ButtonMode.Action);
        return btn;
    }
}
