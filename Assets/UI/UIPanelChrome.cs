using UnityEngine;
using UnityEngine.UI;

/// <summary>面板形态（契约 70_ §6.0）：决定是否有标题栏与内边距档位。</summary>
public enum ChromeKind { Panel, Modal, Card, Strip }

/// <summary>面板状态：正常 / 停用（未开放）/ 告警。</summary>
public enum ChromeState { Normal, Disabled, Alert }

/// <summary>
/// 一套面板外壳的引用集合（契约 70_ §6.0 冻结名单）。
/// 装饰只在创建时构建一次，运行期只改颜色与显隐；不生成任何 Texture2D / Sprite。
/// </summary>
public sealed class PanelChrome
{
    public RectTransform Root;       // 面板根
    public RectTransform Content;    // 内容区根（已扣好内边距）
    public Image Paper;              // 纸底（唯一的 raycastTarget = true 层）
    public Image Frame;              // 1px 外框 ChromeOuterColor
    public Image DoubleLine;         // 1px 内线 ChromeInnerColor（纸底外扩 3px）
    public RectTransform TitleBar;   // 标题栏（Panel / Modal 才有，Card / Strip 为 null）
    public Text Title;
    public Text Eyebrow;             // 眉标（标题栏内的小字，如「省情档案 / 机构 / 卷宗」）
    public Text DossierTag;          // 档号角标（文本一律取 UICopy）
    public Image Accent;             // 朱砂竖块 4×18
    public Image Stamp;              // 印章（默认隐藏）

    // 以下为状态切换需要的内部引用（契约名单之外，仅供 UIPanelChrome 使用）
    public Image PaperBack;          // 纸色垫底：把内线遮成 1px
    public Image Highlight;          // 顶部 1px 内高光
    public Image Press;              // 底部 1px 压痕
    public Image Rule;               // 标题栏下沿 2px
    public Image DossierTagBg;
    public CanvasGroup Group;

    internal Color paperColor = UITheme.PaperBg;
}

/// <summary>
/// 程序化面板外壳（第二轮 UI 成品化「公文呈报」视觉锤的公共基建）。
/// 8 层兄弟顺序（自底向上）：DoubleLine → PaperBack → Frame → Paper → Highlight/Press →
/// TitleBar → Content → DossierTag → Stamp；两种框线用"更宽的同色四边形 + 纸色遮罩"实现 1px 线，
/// 因此零额外贴图、零 Sprite、每面板固定 6~8 张纯色 Image。
/// </summary>
public static class UIPanelChrome
{
    const string TitleBarName = "TitleBar";
    const string ContentName = "Content";
    const string DossierName = "DossierTag";
    const string StampName = "Stamp";

    /// <summary>契约 70_ §6.0 冻结签名：单锚点面板（固定尺寸）。</summary>
    public static PanelChrome Create(Transform parent, string name, ChromeKind kind, string title,
        Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, int sortingOrder)
    {
        return CreateStretched(parent, name, kind, title,
            anchor, anchor, pivot, size, anchoredPos, sortingOrder);
    }

    /// <summary>拉伸锚点版本（详情面板 / 日志等需要随分辨率变高的面板用这个）。</summary>
    public static PanelChrome CreateStretched(Transform parent, string name, ChromeKind kind, string title,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos,
        int sortingOrder)
    {
        var host = ResolveHost(parent, name, sortingOrder);
        var c = new PanelChrome();

        var rootGo = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        rootGo.transform.SetParent(host, false);
        c.Root = rootGo.GetComponent<RectTransform>();
        c.Root.anchorMin = anchorMin;
        c.Root.anchorMax = anchorMax;
        c.Root.pivot = pivot;
        c.Root.sizeDelta = sizeDelta;
        c.Root.anchoredPosition = anchoredPos;
        c.Group = rootGo.GetComponent<CanvasGroup>();

        float line = UITheme.ChromeLineWidth;              // 1
        float innerOut = line + UITheme.ChromeDoubleGap + line;   // 外框 1 + 间距 2 + 内线 1 = 4px 外扩

        // ① 内线（最底）：外扩 4px 的四边形，被 ② 纸色垫底遮成 1px
        c.DoubleLine = AddQuad(c.Root, "DoubleLine", innerOut, UITheme.ChromeInnerColor);
        // ② 纸色垫底：外扩 3px，让 ① 只露出最外 1px
        c.PaperBack = AddQuad(c.Root, "PaperBack", innerOut - line, UITheme.PaperBg);
        // ③ 外框：外扩 1px，被 ④ 纸底遮成最外 1px
        c.Frame = AddQuad(c.Root, "Frame", line, UITheme.ChromeOuterColor);
        // ④ 纸底：唯一接收射线的层
        c.Paper = AddQuad(c.Root, "Paper", 0f, UITheme.PaperBg);
        c.Paper.raycastTarget = true;

        // ④′ 纸印素材（第三轮）：纸纹做旧边 + 双线框的墨线做旧。素材缺失时全部保持纯色四边形，
        // 观感回到第二轮的样子（优雅降级）。纸底/线环都是中性白贴图，颜色仍由上面的 UITheme token 决定，
        // 所以 SetState / 五态换色照旧生效。
        ApplyPaperArt(c);

        // ⑤ 内高光 / 压痕（纸底之上、1px）
        c.Highlight = AddEdgeLine(c.Root, "Highlight", true, UITheme.HighlightColor);
        c.Press = AddEdgeLine(c.Root, "Press", false, UITheme.PressColor);

        // 投影：UGUI Shadow，零额外 GameObject（契约 70_ §7.0）
        AddShadow(c.Paper);

        float pad = PadFor(kind);

        // ⑥ 标题栏（Panel / Modal）
        if (kind == ChromeKind.Panel || kind == ChromeKind.Modal)
        {
            bool modal = kind == ChromeKind.Modal;
            c.TitleBar = AddTitleBar(c.Root, title, UITheme.TitleBarHeight, modal);
            c.Title = c.TitleBar.Find("Title").GetComponent<Text>();
            c.Accent = c.TitleBar.Find("Accent").GetComponent<Image>();
            c.Rule = c.TitleBar.Find("Rule").GetComponent<Image>();
            // 标题栏纸板（压暗一档的纸 + 下沿直线），素材缺失时保持纯色
            UIArt.ApplySliced(c.TitleBar.GetComponent<Image>(), UIArt.TitlePlate);
        }

        // ⑦ 内容区：上边距 = 标题栏下沿 + 16（无标题栏时四周取内边距档）
        var contentGo = new GameObject(ContentName, typeof(RectTransform));
        contentGo.transform.SetParent(c.Root, false);
        c.Content = contentGo.GetComponent<RectTransform>();
        c.Content.anchorMin = Vector2.zero;
        c.Content.anchorMax = Vector2.one;
        c.Content.pivot = new Vector2(0.5f, 0.5f);
        float top = c.TitleBar != null ? UITheme.TitleBarHeight + UITheme.SpaceLg : pad;
        c.Content.offsetMin = new Vector2(pad, pad);
        c.Content.offsetMax = new Vector2(-pad, -top);

        // ⑧ 档号角标（默认隐藏，文本由调用方给）
        var tagGo = new GameObject(DossierName, typeof(RectTransform), typeof(Image));
        tagGo.transform.SetParent(c.Root, false);
        c.DossierTagBg = tagGo.GetComponent<Image>();
        c.DossierTagBg.color = UITheme.DossierTagBg;
        c.DossierTagBg.raycastTarget = false;
        UIArt.ApplySliced(c.DossierTagBg, UIArt.DossierPlate);
        var tagRt = tagGo.GetComponent<RectTransform>();
        tagRt.anchorMin = new Vector2(1f, 1f);
        tagRt.anchorMax = new Vector2(1f, 1f);
        tagRt.pivot = new Vector2(1f, 1f);
        tagRt.sizeDelta = new Vector2(120f, UITheme.DossierTagHeight);
        tagRt.anchoredPosition = new Vector2(-UITheme.SpaceSm, -(UITheme.SpaceSm + UITheme.DossierTagHeight));
        c.DossierTag = UIFactory.CreateText(DossierName + "Text", tagGo.transform,
            UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleCenter, null);
        c.DossierTag.color = UITheme.DossierTagText;
        UIFactory.Stretch(c.DossierTag.rectTransform, UITheme.DossierTagPadX);
        tagGo.SetActive(false);

        // ⑨ 印章（默认隐藏）
        var stampGo = new GameObject(StampName, typeof(RectTransform));
        stampGo.transform.SetParent(c.Root, false);
        var srt = stampGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(UITheme.StampSize, UITheme.StampSize);
        srt.anchoredPosition = Vector2.zero;
        srt.localRotation = Quaternion.Euler(0f, 0f, UITheme.StampRotation);
        // 方形双框印章：外框 2px + 内缩 6px 再一圈（不用贴图）
        AddBorder(srt, 0f, UITheme.SealRed);
        AddBorder(srt, UITheme.StampInset, UITheme.SealRed);
        var stampText = UIFactory.CreateStretchText(StampName + "Text", srt,
            UITheme.StampFontSize, FontStyle.Bold, TextAnchor.MiddleCenter, null);
        stampText.color = UITheme.SealRed;
        UIFactory.Stretch(stampText.rectTransform, UITheme.SpaceXs);

        // 印章本体不绘制，仅作句柄。❗句柄必须挂在**独立**的 GameObject 上：
        // UnityEngine.UI.Graphic 带 [DisallowMultipleComponent]（ugui Graphic.cs:17），
        // 在已有 Text 的物体上 AddComponent<Image>() 会返回 null（Unity 只记一条
        // "Can't add 'Image' ... because a 'Text' is already added"），紧接着的
        // c.Stamp.color 就抛 NullReferenceException。这段代码在**每个**面板创建时都会走到，
        // 所以一旦它是 null，整个 UI 都建不起来（ProvincePicker.Awake 直接中断）。
        var stampHandleGo = new GameObject(StampName + "Handle", typeof(RectTransform), typeof(Image));
        stampHandleGo.transform.SetParent(srt, false);
        var shrt = stampHandleGo.GetComponent<RectTransform>();
        shrt.anchorMin = Vector2.zero;
        shrt.anchorMax = Vector2.one;
        shrt.pivot = new Vector2(0.5f, 0.5f);
        shrt.offsetMin = Vector2.zero;
        shrt.offsetMax = Vector2.zero;
        c.Stamp = stampHandleGo.GetComponent<Image>();
        c.Stamp.color = new Color(1f, 1f, 1f, 0f);   // 印章本体不绘制，仅作句柄
        c.Stamp.raycastTarget = false;
        stampGo.SetActive(false);

        SetState(c, ChromeState.Normal);
        return c;
    }

    /// <summary>
    /// 纸印素材落位（第三轮）：纸底 → 做旧纸；外框/内线 → 做旧墨线环 / 细墨线环。
    /// 全部走 <see cref="UIArt.ApplySliced"/>，素材缺失时静默跳过（保持纯色外观）。
    /// </summary>
    static void ApplyPaperArt(PanelChrome c)
    {
        UIArt.ApplySliced(c.Paper, UIArt.Plate);
        UIArt.ApplySliced(c.Frame, UIArt.RuleRingWorn);
        UIArt.ApplySliced(c.DoubleLine, UIArt.RuleRing);
    }

    /// <summary>1px 描边（四边 Image）容器；返回容器以便改色/显隐（按钮与卡片用）。</summary>
    public static RectTransform AddBorder(Transform host, float inset, Color color)
    {
        var go = new GameObject("Border", typeof(RectTransform));
        go.transform.SetParent(host, false);

        // 布局组（Horizontal/VerticalLayoutGroup）下不能把装饰当子项参与排版
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-inset, -inset);
        rt.offsetMax = new Vector2(inset, inset);

        float w = UITheme.ChromeLineWidth;
        AddEdge(rt, "T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, w), color);
        AddEdge(rt, "B", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, w), color);
        AddEdge(rt, "L", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(w, 0f), color);
        AddEdge(rt, "R", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(w, 0f), color);
        return rt;
    }

    /// <summary>改一整条描边的颜色（四边一起）。</summary>
    public static void SetBorderColor(RectTransform border, Color color)
    {
        if (border == null) return;
        for (int i = 0; i < border.childCount; i++)
        {
            var img = border.GetChild(i).GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }

    /// <summary>标题栏：底 TitleBarBg + 下沿 2px 分隔线 + 左端 4×18 朱砂竖块 + 标题（FontTitle Bold 左对齐、左内边距 20）。</summary>
    public static RectTransform AddTitleBar(Transform host, string title, float height = UITheme.TitleBarHeight)
    {
        return AddTitleBar(host, title, height, false);
    }

    static RectTransform AddTitleBar(Transform host, string title, float height, bool modal)
    {
        var go = new GameObject(TitleBarName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;

        var bg = go.GetComponent<Image>();
        bg.color = modal ? UITheme.InkPrimary : UITheme.TitleBarBg;   // Modal 深墨反白
        bg.raycastTarget = false;

        // 左端朱砂竖块 4×18，距左 12
        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGo.transform.SetParent(go.transform, false);
        var art = accentGo.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0.5f);
        art.anchorMax = new Vector2(0f, 0.5f);
        art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = UITheme.TitleAccentSize;
        art.anchoredPosition = new Vector2(UITheme.TitleAccentInset, 0f);
        var accent = accentGo.GetComponent<Image>();
        accent.color = modal ? UITheme.PaperBg : UITheme.TitleAccentColor;
        accent.raycastTarget = false;

        // 下沿 2px 分隔线
        var rule = AddEdgeLine(rt, "Rule", false, UITheme.TitleRuleColor);
        var rrt = rule.rectTransform;
        rrt.anchorMin = new Vector2(0f, 0f);
        rrt.anchorMax = new Vector2(1f, 0f);
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.sizeDelta = new Vector2(0f, UITheme.TitleRuleWidth);
        rrt.anchoredPosition = Vector2.zero;

        var text = UIFactory.CreateText("Title", go.transform,
            UITheme.FontTitle, FontStyle.Bold, TextAnchor.MiddleLeft, null);
        text.text = title ?? string.Empty;
        text.color = modal ? UITheme.PaperBg : UITheme.InkPrimary;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(UITheme.SpaceXl, 0f);
        text.rectTransform.offsetMax = new Vector2(-UITheme.SpaceXl, 0f);
        return rt;
    }

    /// <summary>标题栏眉标（小字，如「省情档案 / 机构 / 卷宗」；文案一律取 UICopy）。传空串即隐藏并让标题恢复垂直居中。</summary>
    public static Text SetEyebrow(PanelChrome c, string text)
    {
        if (c == null || c.TitleBar == null) return null;

        if (c.Eyebrow == null)
        {
            c.Eyebrow = UIFactory.CreateText("Eyebrow", c.TitleBar,
                UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, null);
            c.Eyebrow.color = UITheme.InkMuted;
            var ert = c.Eyebrow.rectTransform;
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 1f);
            ert.sizeDelta = new Vector2(-2f * UITheme.SpaceXl, UITheme.FontCaption);
            ert.anchoredPosition = new Vector2(0f, -UITheme.SpaceXs);
            ert.SetAsLastSibling();
        }

        bool show = !string.IsNullOrEmpty(text);
        c.Eyebrow.gameObject.SetActive(show);
        if (c.Title != null)
        {
            // 有眉标时标题让出顶部一行，保持两者不重叠
            c.Title.alignment = TextAnchor.MiddleLeft;
            c.Title.rectTransform.offsetMin = new Vector2(UITheme.SpaceXl, 0f);
            c.Title.rectTransform.offsetMax = new Vector2(-UITheme.SpaceXl, show ? -UITheme.FontCaption : 0f);
        }
        if (show) c.Eyebrow.text = text;
        return c.Eyebrow;
    }

    /// <summary>
    /// 打码条（契约 70_ §11.1 线索卷宗）：锁定卡正文位置放 2~3 条 Redacted 横条，**不再用颜色印出正文**。
    /// 返回外层行容器（供父布局组撑行高），条长由 widthRatio 控制（0~1，模拟长短不一的墨条）。
    /// </summary>
    public static RectTransform AddRedactionBar(Transform parent, float widthRatio = 1f)
    {
        var rowGo = new GameObject("RedactedBar", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        var row = rowGo.GetComponent<RectTransform>();
        var le = rowGo.AddComponent<LayoutElement>();
        le.minHeight = UITheme.RedactedBarHeight;
        le.preferredHeight = UITheme.RedactedBarHeight;

        var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(row, false);
        var rt = barGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(Mathf.Clamp01(widthRatio), 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = barGo.GetComponent<Image>();
        img.color = UITheme.Redacted;
        img.raycastTarget = false;
        // 打码条素材：两端收笔、上缘有干墨感（9-slice 只切左右，因此 16px 高也能用）
        UIArt.ApplySliced(img, UIArt.RedactionBar);
        return row;
    }

    /// <summary>1px 分隔线（横跨父容器，距顶 topOffset）。</summary>
    public static Image AddDivider(Transform parent, float topOffset)
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, UITheme.ChromeLineWidth);
        rt.anchoredPosition = new Vector2(0f, -topOffset);
        var img = go.GetComponent<Image>();
        img.color = UITheme.Divider;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>UGUI 一级投影（契约 70_ §7.0 / §11.1：零额外 GameObject）。</summary>
    public static Shadow AddShadow(Graphic target)
    {
        if (target == null) return null;
        var sh = target.GetComponent<Shadow>();
        if (sh == null) sh = target.gameObject.AddComponent<Shadow>();
        sh.effectColor = UITheme.ShadowColor;
        sh.effectDistance = UITheme.ShadowOffset;
        sh.useGraphicAlpha = true;
        return sh;
    }

    /// <summary>写档号角标（空串则隐藏）。</summary>
    public static void SetDossierTag(PanelChrome c, string text)
    {
        if (c == null || c.DossierTag == null) return;
        bool show = !string.IsNullOrEmpty(text);
        if (c.DossierTagBg != null && c.DossierTagBg.transform.parent != null)
            c.DossierTagBg.transform.parent.gameObject.SetActive(show);
        if (show) c.DossierTag.text = text;
    }

    /// <summary>
    /// 独立印章（方形双框 + 朱砂字，-8°）：给非面板宿主（如底栏入口）盖「未开放」斜章（契约 70_ §11.1 / 判据 P10）。
    /// 生成路径与 <see cref="SetStamp"/> 同配方，零贴图。
    /// </summary>
    public static RectTransform MakeSeal(Transform host, string text, float size, Vector2 anchoredPos)
    {
        var go = new GameObject("Seal", typeof(RectTransform));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, UITheme.StampRotation);

        // 素材印章优先（横长方形，真实橡皮章形态）；无素材则走下面的程序化方框章
        var sealSprite = UIArt.SealFor(text);
        if (sealSprite != null)
        {
            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(rt, false);
            UIFactory.Stretch(artGo.GetComponent<RectTransform>());
            var aimg = artGo.GetComponent<Image>();
            aimg.raycastTarget = false;
            UIArt.ApplySimple(aimg, sealSprite);
            rt.sizeDelta = new Vector2(size * 2f, size * 2f / UIArt.SealAspect);
            return rt;
        }

        AddBorder(rt, 0f, UITheme.SealRed);
        AddBorder(rt, UITheme.StampInset, UITheme.SealRed);

        var t = UIFactory.CreateStretchText("Text", rt, UITheme.StampFontSize,
            FontStyle.Bold, TextAnchor.MiddleCenter, null);
        t.text = text;
        t.color = UITheme.SealRed;
        UIFactory.Stretch(t.rectTransform, UITheme.SpaceXs);
        return rt;
    }

    /// <summary>盖印章（方形双框 + 朱砂字，-8°）。</summary>
    public static void SetStamp(PanelChrome c, string text, bool show)
    {
        if (c == null || c.Stamp == null) return;
        var root = c.Stamp.transform.parent != null ? c.Stamp.transform.parent.gameObject : c.Stamp.gameObject;
        root.SetActive(show);
        if (!show) return;
        var t = root.GetComponentInChildren<Text>(true);

        // 素材印章优先：隐藏程序化双框与文字，句柄铺满并按素材长宽比拉长（横章）
        var sprite = UIArt.SealFor(text);
        if (sprite != null)
        {
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "Border") child.gameObject.SetActive(false);
            }
            if (t != null) t.gameObject.SetActive(false);
            UIArt.ApplySimple(c.Stamp, sprite);
            c.Stamp.color = Color.white;
            var srt = root.GetComponent<RectTransform>();
            if (srt != null) srt.sizeDelta = new Vector2(UITheme.StampSize * 2f, UITheme.StampSize * 2f / UIArt.SealAspect);
            return;
        }

        if (t != null) t.text = text ?? string.Empty;
    }

    /// <summary>状态：Normal / Disabled（未开放灰）/ Alert（朱砂框）。</summary>
    public static void SetState(PanelChrome c, ChromeState state)
    {
        if (c == null || c.Paper == null) return;

        switch (state)
        {
            case ChromeState.Disabled:
                c.paperColor = UITheme.BtnDisabledBg;
                c.Frame.color = UITheme.BtnDisabledBorder;
                c.DoubleLine.color = UITheme.BtnDisabledBorder;
                c.PaperBack.color = UITheme.BtnDisabledBg;
                if (c.Title != null) c.Title.color = UITheme.BtnDisabledText;
                if (c.Accent != null) c.Accent.color = UITheme.BtnDisabledBorder;
                if (c.DossierTag != null) c.DossierTag.color = UITheme.BtnDisabledText;
                break;
            case ChromeState.Alert:
                c.paperColor = UITheme.PaperBg;
                c.Frame.color = UITheme.StateAlert;
                c.DoubleLine.color = UITheme.ChromeInnerColor;
                c.PaperBack.color = UITheme.PaperBg;
                if (c.Title != null) c.Title.color = UITheme.InkPrimary;
                if (c.Accent != null) c.Accent.color = UITheme.StateAlert;
                break;
            default:
                c.paperColor = UITheme.PaperBg;
                c.Frame.color = UITheme.ChromeOuterColor;
                c.DoubleLine.color = UITheme.ChromeInnerColor;
                c.PaperBack.color = UITheme.PaperBg;
                if (c.Title != null) c.Title.color = UITheme.InkPrimary;
                if (c.Accent != null) c.Accent.color = UITheme.TitleAccentColor;
                if (c.DossierTag != null) c.DossierTag.color = UITheme.DossierTagText;
                break;
        }

        c.Paper.color = c.paperColor;
    }

    // ── 内部工具 ──────────────────────────────────────────────────────────────

    static float PadFor(ChromeKind kind)
    {
        switch (kind)
        {
            case ChromeKind.Card: return UITheme.PadCard;
            case ChromeKind.Strip: return UITheme.PadRow;
            default: return UITheme.PadPanel;
        }
    }

    /// <summary>取宿主：父级已是某个面板画布就直接用；否则按层序新建一个画布。</summary>
    static Transform ResolveHost(Transform parent, string name, int sortingOrder)
    {
        if (parent != null && parent.GetComponent<Canvas>() != null)
        {
            var canvas = parent.GetComponent<Canvas>();
            canvas.sortingOrder = sortingOrder;   // 层序显式来自 UISorting（契约 70_ §10.0）
            return parent;
        }
        var made = UIFactory.CreateCanvas(name + "Canvas", parent, sortingOrder);
        return made.transform;
    }

    /// <summary>外扩 inset 的纯色四边形（同色叠加 + 纸色遮罩即得 1px 框线）。</summary>
    static Image AddQuad(RectTransform parent, string name, float inset, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-inset, -inset);
        rt.offsetMax = new Vector2(inset, inset);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>父容器（未外扩）内的 1px 水平线：top 贴顶、否则贴底。</summary>
    static Image AddEdgeLine(RectTransform parent, string name, bool top, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, UITheme.ChromeLineWidth);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Image AddEdge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
