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
        }
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("EventCanvas", transform, 100);

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
        prt.sizeDelta = new Vector2(700f, 100f);
        prt.anchoredPosition = Vector2.zero;
        panelRect = prt;

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

        titleText = UIFactory.CreateText("Title", panelGo.transform, 34, FontStyle.Bold, TextAnchor.MiddleLeft, font);
        MakeAutoHeight(titleText, 44f);

        storyText = UIFactory.CreateText("Story", panelGo.transform, 20, FontStyle.Italic, TextAnchor.UpperLeft, font);
        storyText.color = UITheme.InkSecondary;
        MakeAutoHeight(storyText, 20f);

        descText = UIFactory.CreateText("Desc", panelGo.transform, 24, FontStyle.Normal, TextAnchor.UpperLeft, font);
        MakeAutoHeight(descText, 30f);

        var optsGo = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        optsGo.transform.SetParent(panelGo.transform, false);
        optionsContainer = optsGo.GetComponent<RectTransform>();

        var olayout = optsGo.GetComponent<VerticalLayoutGroup>();
        olayout.childAlignment = TextAnchor.UpperCenter;
        olayout.childControlWidth = true;
        olayout.childControlHeight = false;
        olayout.childForceExpandWidth = true;
        olayout.childForceExpandHeight = false;
        olayout.spacing = 10f;

        var ofitter = optsGo.GetComponent<ContentSizeFitter>();
        ofitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tooltipRoot = new GameObject("Tooltip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tooltipRoot.transform.SetParent(canvas.transform, false);
        var ttipImg = tooltipRoot.GetComponent<Image>();
        ttipImg.color = UITheme.PaperTop;
        tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.sizeDelta = new Vector2(360f, 120f);

        var tlayout = tooltipRoot.GetComponent<VerticalLayoutGroup>();
        tlayout.padding = new RectOffset(12, 12, 8, 8);
        tlayout.childControlWidth = true;
        tlayout.childControlHeight = true;
        tlayout.childForceExpandWidth = true;
        tlayout.childForceExpandHeight = false;

        var tfitter = tooltipRoot.GetComponent<ContentSizeFitter>();
        tfitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tfitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tle = tooltipRoot.AddComponent<LayoutElement>();
        tle.minHeight = 40f;

        var ttipTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        ttipTextGo.transform.SetParent(tooltipRoot.transform, false);
        tooltipText = ttipTextGo.GetComponent<Text>();
        if (font != null) tooltipText.font = font;
        tooltipText.fontSize = 20;
        tooltipText.alignment = TextAnchor.UpperLeft;
        tooltipText.color = UITheme.InkPrimary;
        tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tooltipText.verticalOverflow = VerticalWrapMode.Truncate;
        var t4 = tooltipText.rectTransform;
        t4.anchorMin = Vector2.zero;
        t4.anchorMax = Vector2.one;
        t4.offsetMin = Vector2.zero;
        t4.offsetMax = Vector2.zero;

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

    GameObject CreateOptionButton(EventOptionData opt, System.Action onClick)
    {
        var go = new GameObject("OptionBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(optionsContainer, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;
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
        if (font != null) t.font = font;
        t.text = opt.text;
        t.fontSize = 24;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = UITheme.InkPrimary;

        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

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
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Mouse.current.position.ReadValue(), null, out local))
            tooltipRect.anchoredPosition = local + new Vector2(16f, -16f);
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
                return "设置标记 " + fx.flag + " = " + fx.value.ToString("0.##");
            case "reveal_causal_chain":
                return "揭示因果链「" + CausalChainManager.Instance.GetTitle(fx.chainId) + "」";
            case "add_ideology":
                return "政治思潮 " + fx.flag + " " + (fx.value >= 0f ? "+" : "") + (fx.value * 100f).ToString("0.##") + "%";
            case "add_interest_group":
                return "利益集团 " + fx.flag + " " + (fx.value >= 0f ? "+" : "") + (fx.value * 100f).ToString("0.##") + "%";
            case "set_chain_summary":
                return "更新因果链「" + CausalChainManager.Instance.GetTitle(fx.chainId) + "」的描述";
            case "penetrate_block":
                return "将 " + GetBlockName(fx.blockId) + " 加入渗透列表";
            default:
                return fx.type;
        }
    }

    string GetBlockName(string id)
    {
        var p = BlockManager.Instance.GetProvince(id);
        return p != null ? p.displayName : id;
    }
}
