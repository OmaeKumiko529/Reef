using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EventManager : MonoBehaviour
{
    static EventManager _instance;

    public static EventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EventManager>();
                if (_instance == null)
                {
                    var go = new GameObject("EventManager");
                    _instance = go.AddComponent<EventManager>();
                }
            }
            return _instance;
        }
    }

    public Font font;
    public AudioClip optionClip;
    public AudioClip eventClip;

    AudioSource audioSource;

    readonly List<EventData> events = new List<EventData>();
    readonly HashSet<string> triggered = new HashSet<string>();
    readonly Dictionary<string, float> flags = new Dictionary<string, float>();

    EventData pendingEvent;

    Canvas canvas;
    GameObject eventRoot;
    Text titleText;
    Text eventEyebrow;   // 契约 70_ §5.0：事件卡眉标（本轮无 issuer 字段 → Event.IssuerFallback）
    Text eventDate;      // 契约 70_ §5.0：事件卡日期（Event.DateFormat）
    Text descText;
    Text storyText;
    RectTransform optionsContainer;
    readonly List<GameObject> optionButtons = new List<GameObject>();

    GameObject tooltipRoot;
    RectTransform tooltipRect;
    Text tooltipText;
    Coroutine hoverRoutine;
    public float hoverDelay = 0.5f;

    CanvasGroup maskGroup;
    RectTransform panelRect;
    Coroutine eventRoutine;
    readonly List<CanvasGroup> optionGroups = new List<CanvasGroup>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        LoadEvents();
        GameClock.OnDayPassed += OnDayPassed;
    }

    void OnDestroy()
    {
        GameClock.OnDayPassed -= OnDayPassed;
    }

    public void Setup(Font font, AudioClip optionClip, AudioClip eventClip)
    {
        this.font = font;
        this.optionClip = optionClip;
        this.eventClip = eventClip;
        Debug.Log("[EventManager] Setup optionClip=" + (optionClip != null) + " eventClip=" + (eventClip != null));
        EnsureAudio();
        EnsureUI();
    }

    void EnsureAudio()
    {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void LoadEvents()
    {
        var ta = Resources.Load<TextAsset>("Events");
        if (ta == null)
        {
            Debug.LogWarning("未找到 Resources/Events.json");
            return;
        }

        var root = JsonUtility.FromJson<EventRoot>(ta.text);
        if (root != null && root.events != null)
            events.AddRange(root.events);

        Debug.Log("加载事件 " + events.Count + " 个");
    }

    void OnDayPassed()
    {
        if (pendingEvent != null) return;

        foreach (var e in events)
        {
            if (triggered.Contains(e.id)) continue;
            if (EvaluateAll(e.trigger))
            {
                Trigger(e);
                break;
            }
        }
    }

    bool EvaluateAll(ConditionData[] conditions)
    {
        if (conditions == null || conditions.Length == 0) return true;
        foreach (var c in conditions)
            if (!Evaluate(c)) return false;
        return true;
    }

    bool Evaluate(ConditionData c)
    {
        if (c == null) return true;
        switch (c.type)
        {
            case "time_point":
                return GameClock.Instance.IsAtOrAfter(c.year, c.month, c.day);
            case "flag_compare":
                return Compare(GetFlag(c.flag), c.op, c.value);
            default:
                return false;
        }
    }

    bool Compare(float a, string op, float b)
    {
        switch (op)
        {
            case "eq": return Mathf.Approximately(a, b);
            case "ne": return !Mathf.Approximately(a, b);
            case "gt": return a > b;
            case "ge": return a >= b;
            case "lt": return a < b;
            case "le": return a <= b;
            default: return false;
        }
    }

    float GetFlag(string name)
    {
        flags.TryGetValue(name, out var v);
        return v;
    }

    void Trigger(EventData e)
    {
        pendingEvent = e;
        triggered.Add(e.id);
        if (audioSource != null && eventClip != null) audioSource.PlayOneShot(eventClip);
        else Debug.LogWarning("[EventManager] 事件音效缺失 audioSource=" + (audioSource != null) + " eventClip=" + (eventClip != null));
        ShowEvent(e);
        GameClock.PauseForEvent();
    }

    void OnOptionChosen(EventOptionData opt)
    {
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }
        HideTooltip();
        if (audioSource != null && optionClip != null) audioSource.PlayOneShot(optionClip);

        if (opt.effects != null)
            foreach (var fx in opt.effects) ApplyEffect(fx);

        pendingEvent = null;
        HideEvent();
        GameClock.ResumeFromEvent();
    }

    void ApplyEffect(EffectData fx)
    {
        switch (fx.type)
        {
            case "add_influence":
            {
                var st = BlockManager.Instance.GetState(fx.blockId);
                if (st != null) st.eventInfluence += fx.value;
                break;
            }
            case "set_flag":
                flags[fx.flag] = fx.value;
                break;
            case "reveal_causal_chain":
                CausalChainManager.Instance.Reveal(fx.chainId);
                break;
            case "add_ideology":
            {
                var st = BlockManager.Instance.GetState(fx.blockId);
                if (st != null) BlockState.ShiftShare(st.ideologies, fx.flag, fx.value);
                break;
            }
            case "add_interest_group":
            {
                var st = BlockManager.Instance.GetState(fx.blockId);
                if (st != null) BlockState.ShiftShare(st.interestGroups, fx.flag, fx.value);
                break;
            }
            case "set_chain_summary":
                CausalChainManager.Instance.SetSummary(fx.chainId, fx.text);
                break;
            case "penetrate_block":
                PenetrationManager.Instance.ForcePenetrate(fx.blockId);
                break;
            case "set_ideology":
                CountryManager.Instance.SetIdeology(fx.ideologyId);
                break;
            case "set_country_name":
                CountryManager.Instance.SetName(fx.text);
                break;
            case "set_leader":
                CountryManager.Instance.SetLeader(fx.name, fx.ideologyId);
                break;
        }
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        // 层序：契约 70_ §10.0 L-03。原实现写死 100（= Hud），会被顶栏/底栏压住，模态遮罩失效
        canvas = UIFactory.CreateCanvas("EventCanvas", transform, UISorting.Event);

        var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        maskGo.transform.SetParent(canvas.transform, false);
        var maskImg = maskGo.GetComponent<Image>();
        maskImg.color = UITheme.Scrim;
        maskGroup = maskGo.GetComponent<CanvasGroup>();
        var mrt = maskGo.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = Vector2.zero;
        mrt.offsetMax = Vector2.zero;
        eventRoot = maskGo;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGo.transform.SetParent(maskGo.transform, false);
        var pimg = panelGo.GetComponent<Image>();
        pimg.color = UITheme.PaperBg;
        var prt = panelGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(UITheme.WidthM, 100f);
        prt.anchoredPosition = Vector2.zero;
        panelRect = prt;

        // 契约 70_ §7.1：Modal 补外框 + 投影；面板高度由 ContentSizeFitter 决定，因此只加装饰、不动布局
        UIPanelChrome.AddBorder(prt, 0f, UITheme.ChromeOuterColor);
        UIPanelChrome.AddShadow(pimg);

        var playout = panelGo.GetComponent<VerticalLayoutGroup>();
        playout.padding = new RectOffset(30, 30, 24, 24);
        playout.spacing = 12f;
        playout.childAlignment = TextAnchor.UpperLeft;
        playout.childControlWidth = true;
        playout.childControlHeight = false;
        playout.childForceExpandWidth = true;
        playout.childForceExpandHeight = false;

        var pfitter = panelGo.GetComponent<ContentSizeFitter>();
        pfitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        pfitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 契约 70_ §5.0：事件卡眉标（「呈报」）+ 日期行，都在标题之上
        eventEyebrow = UIFactory.CreateText("Eyebrow", panelGo.transform, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        eventEyebrow.text = UICopy.Event.IssuerFallback;
        eventEyebrow.color = UITheme.InkMuted;
        eventEyebrow.gameObject.AddComponent<LayoutElement>().minHeight = UITheme.FontCaption;

        eventDate = UIFactory.CreateText("Date", panelGo.transform, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        eventDate.color = UITheme.InkMuted;
        eventDate.gameObject.AddComponent<LayoutElement>().minHeight = UITheme.FontCaption;

        titleText = UIFactory.CreateText("Title", panelGo.transform, 34, FontStyle.Bold, TextAnchor.MiddleLeft, font);
        MakeAutoHeight(titleText, 44f);

        storyText = UIFactory.CreateText("Story", panelGo.transform, 20, FontStyle.Italic, TextAnchor.UpperLeft, font);
        storyText.color = UITheme.InkSecondary;
        MakeAutoHeight(storyText, 20f);

        descText = UIFactory.CreateText("Desc", panelGo.transform, 24, FontStyle.Normal, TextAnchor.UpperLeft, font);
        MakeAutoHeight(descText, 30f);

        // 契约 70_ §5.0：选项区小标（常量 Event.OptionLead），18px 墨淡色，参与面板纵向布局
        var optLead = UIFactory.CreateText("OptionLead", panelGo.transform, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        optLead.text = UICopy.Event.OptionLead;
        optLead.color = UITheme.InkMuted;
        optLead.gameObject.AddComponent<LayoutElement>().minHeight = UITheme.FontCaption;

        var optsGo = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        optsGo.transform.SetParent(panelGo.transform, false);
        optionsContainer = optsGo.GetComponent<RectTransform>();

        var olayout = optsGo.GetComponent<VerticalLayoutGroup>();
        olayout.childAlignment = TextAnchor.UpperCenter;
        olayout.childControlWidth = true;
        olayout.childControlHeight = true;      // L-12：原 false → 选项行高度由行内自适应结果决定（允许 2 行）
        olayout.childForceExpandWidth = true;
        olayout.childForceExpandHeight = false;
        olayout.spacing = 10f;

        var ofitter = optsGo.GetComponent<ContentSizeFitter>();
        ofitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 契约 70_ §6.0/§7.1：tooltip = Card 固定 S 档 380×120（原 ContentSizeFitter 会随文字长高，与固定档位冲突）
        var tooltipChrome = UIPanelChrome.Create(canvas.transform, "Tooltip", ChromeKind.Card, null,
            new Vector2(UITheme.WidthS, 120f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, UISorting.Event);
        tooltipRoot = tooltipChrome.Root.gameObject;
        tooltipRect = tooltipChrome.Root;   // 锚点保持默认中心，UpdateTooltipPosition 仍按画布局部坐标摆位

        var ttipTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        ttipTextGo.transform.SetParent(tooltipChrome.Content, false);
        tooltipText = ttipTextGo.GetComponent<Text>();
        if (font != null) tooltipText.font = font;
        tooltipText.fontSize = UITheme.FontSmall;
        tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.color = UITheme.InkPrimary;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltipText.verticalOverflow = VerticalWrapMode.Truncate;
        tooltipText.raycastTarget = false;
        UIFactory.Stretch(tooltipText.rectTransform);

        tooltipRoot.SetActive(false);

        eventRoot.SetActive(false);
    }

    void MakeAutoHeight(Text t, float minHeight)
    {
        var fitter = t.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
    }

    // 选项行的内边距与最小高度（判据 P7：点击目标 ≥44×44；间距只取 16/8 两档，契约 70_ §4.0）。
    static readonly float OptionPadX = UITheme.SpaceLg;
    static readonly float OptionPadY = UITheme.SpaceSm;
    const float OptionRowMinHeight = 44f;

    /// <summary>
    /// 选项行高。❗容器 <c>Options</c> 的 VerticalLayoutGroup( childControlHeight = true ) 是
    /// 按每行自己的 <c>LayoutElement.preferredHeight</c> 排版的，而行的标签随后会被
    /// <see cref="UIButtonState"/> 搬进 Visual 层（该层 ignoreLayout），容器**量不到**标签 —— 所以
    /// "行内 VerticalLayoutGroup + ContentSizeFitter 自适应"是量不出来的，高度必须在建行时算准。
    /// 24pt 中文按"字宽 = 字号"估算：可用宽 = M 档面板宽 − 左右内边距。
    /// </summary>
    static float OptionRowHeight(string text)
    {
        float usable = UITheme.WidthM - 2f * OptionPadX;
        int perLine = Mathf.Max(1, Mathf.FloorToInt(usable / UITheme.FontButton));
        int chars = text != null ? text.Length : 0;
        int lines = Mathf.Max(1, Mathf.CeilToInt((float)chars / perLine));
        return Mathf.Max(OptionRowMinHeight, lines * (UITheme.FontButton + 4f) + 2f * OptionPadY);
    }

    GameObject CreateOptionButton(EventOptionData opt, System.Action onClick)
    {
        var go = new GameObject("OptionBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(optionsContainer, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = OptionRowHeight(opt.text);
        le.preferredHeight = le.minHeight;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        optionGroups.Add(group);
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var img = go.GetComponent<Image>();
        img.color = UITheme.PaperTop;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var t = labelGo.GetComponent<Text>();
        // 字体兜底：font 为空时 UGUI 的 Text.OnPopulateMesh 直接 return，一个字都不画
        t.font = font != null ? font : UIFactory.DefaultFont;
        // ❗契约 70_ §5.0 的 Event.OptionLead(「批示」) 是**选项区上方**的 18px 小标（见上方 optLead），
        //   不是每行前缀。原先每行拼 "批示：xxx" 既与上方小标重复，又多占 3 字宽导致提前折行。
        t.text = opt.text;
        t.fontSize = UITheme.FontButton;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = UITheme.InkPrimary;
        // 行高已按行数留足；Wrap + Overflow 保证极端文案也不被裁（判据 P4）
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(OptionPadX, 0f);
        lrt.offsetMax = new Vector2(-OptionPadX, 0f);

        // 契约 70_ §6.0：选项行显式用 Option 模式（悬停左缘 3px 黄铜条 + 底色三档）
        UIButtonState.Apply(btn, ButtonMode.Option);

        btn.onClick.AddListener(() => onClick());

        var trigger = go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => OnOptionEnter(opt));
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => OnOptionExit());
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);

        return go;
    }

    void ShowEvent(EventData e)
    {
        if (eventRoutine != null) StopCoroutine(eventRoutine);

        titleText.text = e.title;
        descText.text = e.description;
        storyText.text = "";
        // 契约 70_ §5.0：事件卡日期取当前游戏日期（Event.DateFormat = "{0}年{1}月{2}日"）
        var clock = GameClock.Instance;
        if (eventDate != null && clock != null)
            eventDate.text = string.Format(UICopy.Event.DateFormat, clock.Year, clock.Month, clock.Day);

        foreach (var b in optionButtons) Destroy(b);
        optionButtons.Clear();
        optionGroups.Clear();

        if (e.options != null)
        {
            foreach (var opt in e.options)
            {
                EventOptionData captured = opt;
                optionButtons.Add(CreateOptionButton(opt, () => OnOptionChosen(captured)));
            }
        }

        eventRoot.SetActive(true);
        if (maskGroup != null) maskGroup.alpha = 0f;
        eventRoutine = StartCoroutine(PlayEventIntro(e.story));
    }

    System.Collections.IEnumerator PlayEventIntro(string story)
    {
        yield return UIAnim.Fade(maskGroup, 1f, UITheme.FadeNormal);
        yield return UIAnim.Stamp(panelRect, UITheme.StampNormal);
        if (storyText != null)
            yield return UIAnim.Typewriter(storyText, story, UITheme.TypeCharDelay);
        foreach (var g in optionGroups)
        {
            g.blocksRaycasts = true;
            yield return UIAnim.Fade(g, 1f, UITheme.FadeFast);
        }
        eventRoutine = null;
    }

    void HideEvent()
    {
        if (eventRoot == null) return;
        if (eventRoutine != null)
        {
            StopCoroutine(eventRoutine);
            eventRoutine = null;
        }
        eventRoutine = StartCoroutine(HideEventRoutine());
    }

    System.Collections.IEnumerator HideEventRoutine()
    {
        yield return UIAnim.Fade(maskGroup, 0f, UITheme.FadeFast);
        eventRoot.SetActive(false);
        eventRoutine = null;
    }

    void OnOptionEnter(EventOptionData opt)
    {
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverDelay(opt));
    }

    void OnOptionExit()
    {
        if (hoverRoutine != null)
        {
            StopCoroutine(hoverRoutine);
            hoverRoutine = null;
        }
        HideTooltip();
    }

    System.Collections.IEnumerator HoverDelay(EventOptionData opt)
    {
        yield return new WaitForSeconds(hoverDelay);
        ShowTooltip(opt);
        hoverRoutine = null;
    }

    void ShowTooltip(EventOptionData opt)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(opt.hint))
            sb.AppendLine(opt.hint);
        if (opt.effects != null)
            foreach (var fx in opt.effects)
                sb.AppendLine(DescribeEffect(fx));
        tooltipText.text = sb.ToString();
        tooltipRoot.SetActive(true);
        UpdateTooltipPosition();
    }

    void HideTooltip()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    void Update()
    {
        if (tooltipRoot != null && tooltipRoot.activeSelf)
            UpdateTooltipPosition();
    }

    void UpdateTooltipPosition()
    {
        if (Mouse.current == null) return;
        var rect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (rect == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Mouse.current.position.ReadValue(), null, out local)) return;

        // 契约 70_ §9.0 L-11：出屏钳制。tooltip 的 pivot = (0,1)，anchoredPosition 即其"左上角"；
        // 画布局部坐标以画布中心为原点，故可用区 = x∈[20, W−20]、y∈[100, H−90]，右/下再扣掉 tooltip 自身尺寸。
        var area = rect.rect;
        float tw = tooltipRect.rect.width > 1f ? tooltipRect.rect.width : 380f;
        float th = tooltipRect.rect.height > 1f ? tooltipRect.rect.height : 120f;

        Vector2 pos = local + new Vector2(16f, -16f);
        float minX = area.xMin + 20f, maxX = area.xMax - 20f - tw;
        float minY = area.yMin + 100f + th, maxY = area.yMax - 90f;
        pos.x = Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX));
        pos.y = Mathf.Clamp(pos.y, Mathf.Min(minY, maxY), maxY);
        tooltipRect.anchoredPosition = pos;
    }

    string DescribeEffect(EffectData fx)
    {
        switch (fx.type)
        {
            case "add_influence":
                string name = GetBlockName(fx.blockId);
                string sign = fx.value >= 0f ? "+" : "";
                return "影响力 " + sign + fx.value.ToString("0.##") + "（" + name + "）";
            case "set_flag":
                return "设置标记 " + UICopy.FlagLabel(fx.flag) + " = " + fx.value.ToString("0.##");
            case "reveal_causal_chain":
                // 契约 70_ §15.2：选择前的 tooltip 不得点名因果链（雾中不可读）。
                // 链名只允许出现在情报日志内部渲染，故这里返回通用提示串。
                return UICopy.Journal.ChainRevealHint;
            case "add_ideology":
                return "政治思潮 " + UICopy.FlagLabel(fx.flag) + " " + (fx.value >= 0f ? "+" : "") + (fx.value * 100f).ToString("0.##") + "%";
            case "add_interest_group":
                return "利益集团 " + UICopy.FlagLabel(fx.flag) + " " + (fx.value >= 0f ? "+" : "") + (fx.value * 100f).ToString("0.##") + "%";
            case "set_chain_summary":
                // 契约 70_ §15.2：同上，选择前不揭示具体链名
                return UICopy.Journal.ChainSummaryHint;
            case "penetrate_block":
                return "将 " + GetBlockName(fx.blockId) + " 加入渗透列表";
            case "set_ideology":
                return "转变国家意识形态为「" + GameContent.Name(fx.ideologyId) + "」（政权更迭）";
            case "set_country_name":
                return "将国名改为「" + fx.text + "」";
            case "set_leader":
                return "更换领导人为「" + fx.name + "」";
            default:
                // 兜底不把英文 type 直接甩给玩家（新 effect 类型接入前不显示原始 id）
                return UICopy.Common.Unknown;
        }
    }

    string GetBlockName(string id)
    {
        var p = BlockManager.Instance.GetProvince(id);
        return p != null ? p.displayName : UICopy.Common.Unknown;
    }
}
