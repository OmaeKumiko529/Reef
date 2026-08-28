using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    const string BackgroundStage = "background";

    public Font font;

    readonly Dictionary<string, CausalChainData> chains = new Dictionary<string, CausalChainData>();
    readonly Dictionary<string, HashSet<string>> unlocked = new Dictionary<string, HashSet<string>>();
    readonly Dictionary<string, string> summaryOverride = new Dictionary<string, string>();
    string activeChainId;

    Canvas canvas;
    GameObject panelRoot;
    RectTransform panelRect;
    CanvasGroup group;
    Text titleText;
    Text contentText;
    Coroutine animRoutine;
    string currentSummary = "";

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

            var set = new HashSet<string>();
            if (c.segments != null)
                foreach (var s in c.segments)
                    if (s != null && !s.locked) set.Add(s.stage);
            unlocked[c.id] = set;
        }

        Debug.Log("加载因果链 " + chains.Count + " 条");
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
        if (!chains.TryGetValue(chainId, out var chain)) return;

        activeChainId = chainId;

        var set = unlocked.TryGetValue(chainId, out var existing) ? existing : new HashSet<string>();
        if (!unlocked.ContainsKey(chainId)) unlocked[chainId] = set;

        set.Add(BackgroundStage);
        PlayReveal();
    }

    public void UnlockSegment(string chainId, string stage)
    {
        if (!chains.ContainsKey(chainId) || string.IsNullOrEmpty(stage)) return;

        var set = unlocked.TryGetValue(chainId, out var existing) ? existing : new HashSet<string>();
        if (!unlocked.ContainsKey(chainId)) unlocked[chainId] = set;

        set.Add(stage);
        if (activeChainId == chainId) Refresh();
    }

    public void SetSummary(string chainId, string summary)
    {
        if (!chains.ContainsKey(chainId)) return;

        summaryOverride[chainId] = summary;
        if (activeChainId == chainId) Refresh();
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        var canvasGo = new GameObject("CausalChainCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var bg = panelRoot.GetComponent<Image>();
        bg.color = UITheme.PaperBg;
        group = panelRoot.GetComponent<CanvasGroup>();

        panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(400f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);

        var layout = panelRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 16);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelRoot.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleText = CreateText("Title", panelRoot.transform, 30, FontStyle.Bold, TextAnchor.MiddleLeft);
        titleText.color = UITheme.InkPrimary;
        var tle = titleText.gameObject.AddComponent<LayoutElement>();
        tle.preferredHeight = 44f;

        contentText = CreateText("Content", panelRoot.transform, 20, FontStyle.Normal, TextAnchor.UpperLeft);
        contentText.color = UITheme.InkSecondary;
        var cf = contentText.gameObject.AddComponent<ContentSizeFitter>();
        cf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var cle = contentText.gameObject.AddComponent<LayoutElement>();
        cle.minHeight = 28f;

        panelRoot.SetActive(false);
    }

    Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = UITheme.InkPrimary;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    string ComputeSummary(CausalChainData chain)
    {
        string summary = null;
        if (summaryOverride.TryGetValue(activeChainId, out var ov))
            summary = ov;

        if (string.IsNullOrEmpty(summary))
        {
            var set = unlocked.TryGetValue(activeChainId, out var u) ? u : null;
            if (chain.segments != null && set != null)
            {
                foreach (var seg in chain.segments)
                {
                    if (seg == null || !set.Contains(seg.stage)) continue;
                    if (!string.IsNullOrEmpty(seg.summary)) summary = seg.summary;
                }
            }
        }

        if (string.IsNullOrEmpty(summary))
            summary = "目前没有足够的情报表明这条因果链有价值...";

        return summary;
    }

    void PlayReveal()
    {
        if (panelRoot == null) return;
        if (!chains.TryGetValue(activeChainId, out var chain)) return;
        if (animRoutine != null) StopCoroutine(animRoutine);

        panelRoot.SetActive(true);
        titleText.text = chain.title;
        currentSummary = ComputeSummary(chain);
        contentText.text = "";

        animRoutine = StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
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

        if (contentText != null)
            yield return UIAnim.Typewriter(contentText, currentSummary, UITheme.TypeCharDelay);

        animRoutine = null;
    }

    void Refresh()
    {
        if (panelRoot == null) return;

        if (string.IsNullOrEmpty(activeChainId) || !chains.TryGetValue(activeChainId, out var chain))
        {
            panelRoot.SetActive(false);
            return;
        }

        panelRoot.SetActive(true);
        titleText.text = chain.title;
        currentSummary = ComputeSummary(chain);

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(TypeRoutine());
    }

    IEnumerator TypeRoutine()
    {
        yield return UIAnim.Typewriter(contentText, currentSummary, UITheme.TypeCharDelay);
        animRoutine = null;
    }
}
