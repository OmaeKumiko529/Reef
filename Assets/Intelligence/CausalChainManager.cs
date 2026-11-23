using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CausalChainManager : MonoBehaviour
{
    static CausalChainManager _instance;

    public static CausalChainManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CausalChainManager>();
                if (_instance == null)
                {
                    var go = new GameObject("CausalChainManager");
                    _instance = go.AddComponent<CausalChainManager>();
                }
            }
            return _instance;
        }
    }

    // 遮蔽态正文文案取自 UICopy（契约 2.3），保留旧名以免牵动调用点
    const string MistText = UICopy.Journal.Mist;

    // 日志几何（契约 70_ §9.0 L-06）：上留 104 = 顶栏占位 84 + 20 外边距；下留 100 = 底栏 80 + 20 间隙
    const float JournalTopInset = 104f;
    const float JournalBottomInset = 100f;
    const float JournalWidth = 380f;   // 宽度档 S（契约 70_ §8.0）

    public Font font;

    readonly Dictionary<string, CausalChainData> chains = new Dictionary<string, CausalChainData>();
    readonly List<string> journalOrder = new List<string>();
    readonly Dictionary<string, string> bodyOverride = new Dictionary<string, string>();

    Canvas canvas;
    PanelChrome chrome;
    Text countText;
    GameObject panelRoot;
    RectTransform panelRect;
    CanvasGroup group;
    RectTransform listRoot;
    readonly List<GameObject> entryCards = new List<GameObject>();
    Coroutine animRoutine;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        LoadChains();
    }

    void LoadChains()
    {
        var ta = Resources.Load<TextAsset>("CausalChains");
        if (ta == null)
        {
            Debug.LogWarning("未找到 Resources/CausalChains.json");
            return;
        }

        var root = JsonUtility.FromJson<CausalChainRoot>(ta.text);
        if (root == null || root.chains == null) return;

        foreach (var c in root.chains)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) continue;
            chains[c.id] = c;
        }

        Debug.Log("加载情报日志 " + chains.Count + " 条");
    }

    public void Setup(Font font)
    {
        this.font = font;
        EnsureUI();
    }

    public string GetTitle(string id)
    {
        return chains.TryGetValue(id, out var c) ? c.title : id;
    }

    public void Reveal(string chainId)
    {
        if (!chains.ContainsKey(chainId)) return;

        AddToJournal(chainId);
        EnsureUI();
        RebuildList();
        PlayReveal();
    }

    public void SetSummary(string chainId, string summary)
    {
        if (!chains.ContainsKey(chainId)) return;

        bodyOverride[chainId] = summary;
        AddToJournal(chainId);
        EnsureUI();
        RebuildList();
        PlayReveal();
    }

    void AddToJournal(string id)
    {
        if (!journalOrder.Contains(id)) journalOrder.Add(id);
    }

    bool IsRedacted(CausalChainData c)
    {
        return c.locked && !bodyOverride.ContainsKey(c.id);
    }

    string GetBody(CausalChainData c)
    {
        if (IsRedacted(c)) return MistText;
        if (bodyOverride.TryGetValue(c.id, out var ov) && !string.IsNullOrEmpty(ov)) return ov;
        return c.text;
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.transform.SetParent(transform, false);
        }

        canvas = UIFactory.CreateCanvas("JournalCanvas", transform, UISorting.Modal);

        // 契约 70_ §6.0/§7.1：Modal 形态（深墨反白标题栏 + 直角双层框 + 投影）；
        // L-06：**不挂 ContentSizeFitter**——面板高度固定 H−204，被内容撑开会盖住底栏
        chrome = UIPanelChrome.CreateStretched(canvas.transform, "Journal", ChromeKind.Modal, UICopy.Journal.Header,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(UITheme.WidthS, -(JournalTopInset + JournalBottomInset)),
            new Vector2(20f, -JournalTopInset), UISorting.Modal);
        panelRoot = chrome.Root.gameObject;
        panelRect = chrome.Root;
        group = chrome.Group;
        UIPanelChrome.SetEyebrow(chrome, UICopy.Journal.Eyebrow);

        // 内容区：布局组 + 计数行 + 卡片墙；超出由 RectMask2D 裁剪（高度固定 H−204）
        var listArea = chrome.Content;
        listArea.gameObject.AddComponent<RectMask2D>();

        var layout = listArea.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 契约 70_ §5.0：本卷收录 N 条（Journal.CountFormat）
        countText = UIFactory.CreateText("Count", listArea, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        countText.color = UITheme.InkMuted;
        countText.raycastTarget = false;
        countText.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.FontCaption;

        var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(listArea, false);
        listRoot = listGo.GetComponent<RectTransform>();
        var llayout = listGo.GetComponent<VerticalLayoutGroup>();
        llayout.spacing = 10f;
        llayout.childAlignment = TextAnchor.UpperLeft;
        llayout.childControlWidth = true;
        llayout.childControlHeight = false;
        llayout.childForceExpandWidth = true;
        llayout.childForceExpandHeight = false;
        var lfitter = listGo.GetComponent<ContentSizeFitter>();
        lfitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        lfitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        panelRoot.SetActive(false);
    }

    void RebuildList()
    {
        foreach (var card in entryCards) Destroy(card);
        entryCards.Clear();

        foreach (var id in journalOrder)
        {
            if (!chains.TryGetValue(id, out var c)) continue;
            entryCards.Add(CreateEntryCard(c, entryCards.Count));
        }

        if (countText != null)
            countText.text = string.Format(UICopy.Journal.CountFormat, entryCards.Count);
    }

    GameObject CreateEntryCard(CausalChainData c, int index)
    {
        var cardGo = new GameObject("Entry_" + c.id, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardGo.transform.SetParent(listRoot, false);
        var img = cardGo.GetComponent<Image>();
        img.color = UITheme.CardBg;                                        // 契约 §7.1：卡片底色
        UIPanelChrome.AddBorder(img.rectTransform, 0f, UITheme.CardBorderColor);   // 1px 卡片描边

        // 契约 70_ §11.1 线索卷宗：卡片左缘 1px 贯穿线 + 8×8 方形节点（都不是布局行）
        var threadGo = new GameObject("Thread", typeof(RectTransform), typeof(Image));
        threadGo.transform.SetParent(cardGo.transform, false);
        threadGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var threadRt = threadGo.GetComponent<RectTransform>();
        threadRt.anchorMin = new Vector2(0f, 0f);
        threadRt.anchorMax = new Vector2(0f, 1f);
        threadRt.pivot = new Vector2(0.5f, 0.5f);
        threadRt.sizeDelta = new Vector2(1f, 0f);
        threadRt.anchoredPosition = new Vector2(8f, 0f);
        var threadImg = threadGo.GetComponent<Image>();
        threadImg.color = UITheme.Divider;
        threadImg.raycastTarget = false;

        var nodeGo = new GameObject("Node", typeof(RectTransform), typeof(Image));
        nodeGo.transform.SetParent(cardGo.transform, false);
        nodeGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var nodeRt = nodeGo.GetComponent<RectTransform>();
        nodeRt.anchorMin = new Vector2(0f, 1f);
        nodeRt.anchorMax = new Vector2(0f, 1f);
        nodeRt.pivot = new Vector2(0.5f, 0.5f);
        nodeRt.sizeDelta = new Vector2(UITheme.BindingDotSize, UITheme.BindingDotSize);
        nodeRt.anchoredPosition = new Vector2(8f, -8f);
        var nodeImg = nodeGo.GetComponent<Image>();
        nodeImg.color = new Color(UITheme.Divider.r, UITheme.Divider.g, UITheme.Divider.b, 0.8f);
        nodeImg.raycastTarget = false;
        // 装订/线索节点：用素材的软边方块（缺素材时仍是纯色方块）
        UIArt.ApplySimple(nodeImg, UIArt.BindingDot);

        var layout = cardGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = cardGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bool redacted = IsRedacted(c);

        // 契约 70_ §5.0：卡片序号（Journal.CardNoFormat = "卷宗 {0:00}"）
        var no = UIFactory.CreateText("No", cardGo.transform, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        no.text = string.Format(UICopy.Journal.CardNoFormat, index + 1);
        no.color = UITheme.InkMuted;
        no.raycastTarget = false;
        no.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.FontCaption;

        var title = UIFactory.CreateText("Title", cardGo.transform, 22, FontStyle.Bold, TextAnchor.UpperLeft, font);
        // 总纲 §2.2.3.2.1.1 / 契约 70_ §11.1：雾中链的标题位就是「因果迷雾」本身，
        // 不印 c.title 也不用 MistSuffix 后缀（后者会半透露主题性标题）
        title.text = redacted ? UICopy.Journal.Mist : c.title;
        title.color = redacted ? UITheme.InkMuted : UITheme.InkPrimary;
        title.raycastTarget = false;

        if (redacted)
        {
            // 契约 70_ §11.1：锁定卡**不印正文**（修正原 Redacted 染色印字的违约做法），正文位置放 2~3 条打码条
            int bars = 2 + (index % 2);
            for (int i = 0; i < bars; i++)
                UIPanelChrome.AddRedactionBar(cardGo.transform, i == bars - 1 ? 0.62f : 1f);
        }
        else
        {
            var body = UIFactory.CreateText("Body", cardGo.transform, 20, FontStyle.Normal, TextAnchor.UpperLeft, font);
            body.text = GetBody(c);
            body.color = UITheme.InkSecondary;
            body.raycastTarget = false;
            var bf = body.gameObject.AddComponent<ContentSizeFitter>();
            bf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ble = body.gameObject.AddComponent<LayoutElement>();
            ble.minHeight = 24f;
        }

        return cardGo;
    }

    void PlayReveal()
    {
        if (panelRoot == null) return;
        if (animRoutine != null) StopCoroutine(animRoutine);

        panelRoot.SetActive(true);
        animRoutine = StartCoroutine(RevealRoutine());
    }

    System.Collections.IEnumerator RevealRoutine()
    {
        Vector2 target = panelRect.anchoredPosition;
        Vector2 from = target - new Vector2(panelRect.sizeDelta.x + 40f, 0f);
        group.alpha = 0f;
        panelRect.anchoredPosition = from;

        float t = 0f;
        while (t < UITheme.SlideNormal)
        {
            t += Time.deltaTime;
            float k = UITheme.EaseOutCubic(t / UITheme.SlideNormal);
            panelRect.anchoredPosition = Vector2.Lerp(from, target, k);
            group.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }
        panelRect.anchoredPosition = target;
        group.alpha = 1f;
        animRoutine = null;
    }
}
