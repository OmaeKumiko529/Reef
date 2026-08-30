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

    const string MistText = "因果迷雾";

    public Font font;

    readonly Dictionary<string, CausalChainData> chains = new Dictionary<string, CausalChainData>();
    readonly List<string> journalOrder = new List<string>();
    readonly Dictionary<string, string> bodyOverride = new Dictionary<string, string>();

    Canvas canvas;
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

        canvas = UIFactory.CreateCanvas("JournalCanvas", transform);

        panelRoot = new GameObject("Journal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelRoot.transform.SetParent(canvas.transform, false);
        var bg = panelRoot.GetComponent<Image>();
        bg.color = UITheme.PaperBg;
        group = panelRoot.GetComponent<CanvasGroup>();

        panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(420f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);

        var layout = panelRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelRoot.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var header = UIFactory.CreateText("Header", panelRoot.transform, 26, FontStyle.Bold, TextAnchor.MiddleLeft, font);
        header.text = "情报日志";
        header.color = UITheme.SealRed;
        header.raycastTarget = false;
        var hle = header.gameObject.AddComponent<LayoutElement>();
        hle.preferredHeight = 32f;

        var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(panelRoot.transform, false);
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
            entryCards.Add(CreateEntryCard(c));
        }
    }

    GameObject CreateEntryCard(CausalChainData c)
    {
        var cardGo = new GameObject("Entry_" + c.id, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardGo.transform.SetParent(listRoot, false);
        var img = cardGo.GetComponent<Image>();
        img.color = UITheme.PaperTop;

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

        var title = UIFactory.CreateText("Title", cardGo.transform, 22, FontStyle.Bold, TextAnchor.UpperLeft, font);
        title.text = c.title + (redacted ? " · 迷雾" : "");
        title.color = redacted ? UITheme.InkMuted : UITheme.InkPrimary;
        title.raycastTarget = false;

        var body = UIFactory.CreateText("Body", cardGo.transform, 20, FontStyle.Normal, TextAnchor.UpperLeft, font);
        body.text = GetBody(c);
        body.color = redacted ? UITheme.Redacted : UITheme.InkSecondary;
        body.raycastTarget = false;
        var bf = body.gameObject.AddComponent<ContentSizeFitter>();
        bf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var ble = body.gameObject.AddComponent<LayoutElement>();
        ble.minHeight = 24f;

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
