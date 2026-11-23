using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>按钮使用场景（契约 70_ §6.0）：不同场景的选中条位置不同。</summary>
public enum ButtonMode { NavBar, Option, Action, Clock }

/// <summary>
/// 按钮五态（普通 / 悬停 / 按下 / 选中 / 停用）的唯一实现（契约 70_ §6.0、判据 P6/U-14）。
/// 结构：按钮本体上的 Image 只当命中区（颜色被盖住），真正的底色放在子物体 "Visual" 上——
/// 这样即使按钮被 HorizontalLayoutGroup 等布局控制，"悬停上浮 1px" 也能生效。
/// 零贴图、零 Sprite；状态切换只改颜色与 anchoredPosition，不产生分配。
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class UIButtonState : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    public ButtonMode mode = ButtonMode.Action;

    Button button;
    Image hit;
    RectTransform visual;
    Image visualBg;
    RectTransform border;
    Image bar;
    Text label;

    bool hover, pressed, selected, bound;
    bool lastInteractable = true;
    Vector2 visualBase;

    /// <summary>由调用方设置选中态（底栏入口 / 列表行）。</summary>
    public bool Selected => selected;

    /// <summary>给已有按钮挂上五态；重复调用只更新模式。返回组件本身。</summary>
    public static UIButtonState Apply(Button b, ButtonMode mode)
    {
        if (b == null) return null;
        var s = b.GetComponent<UIButtonState>();
        // ❗AddComponent 会**同步**跑 Awake→Bind，那一刻 mode 还是默认值 Action，
        //   NavBar/Option 的选中条分支会被跳过、bound 又被置 true（Bind 的守卫）。
        //   所以设完 mode 之后必须再 EnsureSelectedBar() 补建一次，否则底栏「当前是渗透
        //   还是机构模式」与事件选项行的选中态会整体不可见（BtnSelectedBg 与常态近同色）。
        if (s == null) s = b.gameObject.AddComponent<UIButtonState>();
        s.mode = mode;
        s.Bind();
        s.EnsureSelectedBar();
        s.Refresh();
        return s;
    }

    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        Refresh();
    }

    void Awake() { Bind(); }

    void Bind()
    {
        if (bound) return;
        bound = true;

        button = GetComponent<Button>();
        hit = GetComponent<Image>();
        if (hit != null) hit.raycastTarget = true;   // 命中区必须收射线

        // ① 视觉层：独立子物体，让"上浮 1px"不受父级布局组管束
        var visualGo = new GameObject("Visual", typeof(RectTransform), typeof(Image));
        visualGo.transform.SetParent(transform, false);
        // ❗必须 ignoreLayout：按钮**自身**挂布局组时（事件选项行的 VerticalLayoutGroup），
        //   组会把直接子物体当排版行接管尺寸。Visual 没有 ILayoutElement，会被压成 0 高度，
        //   搬进来的标签跟着塌陷/错位（"悬停上浮 1px"也会被组的排版覆盖掉）。
        //   底栏按钮不受影响，因为布局组挂在底栏上、按钮才是它的子物体。
        visualGo.AddComponent<LayoutElement>().ignoreLayout = true;
        visual = visualGo.GetComponent<RectTransform>();
        UIFactory.Stretch(visual);
        visualBase = visual.anchoredPosition;
        visualBg = visualGo.GetComponent<Image>();
        visualBg.raycastTarget = false;
        visualBg.color = UITheme.BtnNormalBg;

        // ② 1px 描边（判据 P8：不允许裸色块）
        border = UIPanelChrome.AddBorder(visual, 0f, UITheme.BtnNormalBorder);

        // ③ 选中条（见 EnsureSelectedBar：Apply 里 AddComponent 会**同步**跑一次 Bind，
        //    那一刻 mode 还是默认的 Action，建不出条；设完 mode 后必须再补调一次）
        EnsureSelectedBar();

        // ④ 已有子物体（标签等）搬进视觉层：跟着一起上浮，且颜色由本组件统一接管
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child == visual) continue;
            child.SetParent(visual, false);
        }
        label = GetComponentInChildren<Text>(true);
    }

    /// <summary>
    /// 选中条：底栏入口（NavBar）走「下沿 3px 朱砂」，选项行/列表行（Option）走「左缘 3px 黄铜」。
    /// ❗必须可重入：<see cref="Apply"/> 里的 <c>AddComponent</c> 会**同步**触发 <c>Awake→Bind</c>，
    /// 那一刻 <c>mode</c> 还是默认的 <c>Action</c>（NavBar/Option 分支被跳过）且 <c>bound</c> 立刻置 true，
    /// 之后 <c>s.mode = mode; s.Bind();</c> 会被 <c>if (bound) return;</c> 整段挡掉 ⇒ 条永远不会被建出来。
    /// 现状是 Apply 在设完 mode 之后再调一次本方法补建。
    /// </summary>
    void EnsureSelectedBar()
    {
        if (visual == null || bar != null) return;
        if (mode != ButtonMode.NavBar && mode != ButtonMode.Option) return;

        var barGo = new GameObject("SelectedBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(visual, false);
        var brt = barGo.GetComponent<RectTransform>();
        if (mode == ButtonMode.NavBar)
        {
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, UITheme.BtnSelectedBarWidth);
        }
        else
        {
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(UITheme.BtnSelectedBarWidth, 0f);
        }
        brt.anchoredPosition = Vector2.zero;
        bar = barGo.GetComponent<Image>();
        bar.color = mode == ButtonMode.NavBar ? UITheme.BtnSelectedBarColor : UITheme.RowSelectedBarColor;
        bar.raycastTarget = false;
        // 按钮可能在设 mode 之前就已处于选中态（列表行复用），建完立刻按状态显形
        bar.gameObject.SetActive(selected && (button == null || button.interactable));
    }

    void Update()
    {
        // 调用方可能随时改 button.interactable（未开放部门），这里轮询一次布尔比较即可
        if (button == null) return;
        if (button.interactable == lastInteractable) return;
        if (!button.interactable) { hover = false; pressed = false; }
        Refresh();
    }

    void Refresh()
    {
        if (visualBg == null) return;

        bool interactable = button == null || button.interactable;
        lastInteractable = interactable;

        Color bg;
        Color borderColor;
        Color textColor = UITheme.InkPrimary;

        if (!interactable)
        {
            bg = UITheme.BtnDisabledBg;
            borderColor = UITheme.BtnDisabledBorder;
            textColor = UITheme.BtnDisabledText;
        }
        else if (pressed)
        {
            bg = UITheme.BtnPressedBg;
            borderColor = UITheme.BtnHoverBorder;
        }
        else if (hover)
        {
            bg = UITheme.BtnHoverBg;
            borderColor = UITheme.BtnHoverBorder;
        }
        else if (selected)
        {
            bg = UITheme.BtnSelectedBg;
            borderColor = UITheme.BtnNormalBorder;
        }
        else
        {
            bg = UITheme.BtnNormalBg;
            borderColor = UITheme.BtnNormalBorder;
        }

        visualBg.color = bg;
        UIPanelChrome.SetBorderColor(border, borderColor);
        if (label != null) label.color = textColor;
        if (bar != null) bar.gameObject.SetActive(interactable && selected && (mode == ButtonMode.NavBar || mode == ButtonMode.Option));

        // U-14：悬停上浮 1px、按下缩到 0.98
        visual.anchoredPosition = visualBase + new Vector2(0f, (interactable && hover && !pressed) ? 1f : 0f);
        visual.localScale = (interactable && pressed) ? Vector3.one * 0.98f : Vector3.one;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { pressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { pressed = false; Refresh(); }
    public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
}
