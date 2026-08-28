using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PenetrationManager : MonoBehaviour
{
    static PenetrationManager _instance;

    public static PenetrationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PenetrationManager>();
                if (_instance == null)
                {
                    var go = new GameObject("PenetrationManager");
                    _instance = go.AddComponent<PenetrationManager>();
                }
            }
            return _instance;
        }
    }

    static readonly Color32 PenGreen = new Color32(70, 180, 95, 255);

    Texture2D idMap;
    ProvinceDatabase database;
    Material mapMaterial;
    Font font;

    readonly HashSet<string> penetrating = new HashSet<string>();
    readonly HashSet<string> decaying = new HashSet<string>();
    readonly HashSet<string> forced = new HashSet<string>();
    readonly Dictionary<Color32, string> colorToId = new Dictionary<Color32, string>();
    Texture2D penMap;
    bool colorCacheBuilt;

    public int infiltrationLevel = 1;
    public int MaxBlocks => (infiltrationLevel - 1) * 2 + 3;
    public float DailyBonus => infiltrationLevel * 0.2f;
    public float MaxBonus => infiltrationLevel * 10f;

    public const int MaxLevel = 5;

    public void SetLevel(int level)
    {
        infiltrationLevel = Mathf.Clamp(level, 1, MaxLevel);
    }

    public bool IsActive { get; private set; }

    GameObject hintRoot;
    CanvasGroup hintGroup;
    Coroutine hintRoutine;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        GameClock.OnDayPassed += OnDayPassed;
    }

    void OnDestroy()
    {
        GameClock.OnDayPassed -= OnDayPassed;
    }

    public void Setup(Texture2D map, ProvinceDatabase db, Material mat, Font font)
    {
        idMap = map;
        database = db;
        mapMaterial = mat;
        this.font = font;
        EnsureHint();
    }

    public bool IsPenetrating(string id) => penetrating.Contains(id);

    public IReadOnlyCollection<string> GetPenetratingIds() => penetrating;

    public void SetActive(bool value)
    {
        if (IsActive == value) return;
        IsActive = value;
        if (IsActive)
        {
            BuildColorCache();
            ApplyPenMap();
            ShowHint();
        }
        else
        {
            RestoreMap();
            HideHint();
        }
    }

    public void ToggleActive() => SetActive(!IsActive);

    public void Toggle(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (penetrating.Contains(id))
        {
            penetrating.Remove(id);
            forced.Remove(id);
            var st = BlockManager.Instance.GetState(id);
            if (st != null && st.penetrationBonus > 0f)
                decaying.Add(id);
        }
        else
        {
            if (penetrating.Count - forced.Count >= MaxBlocks) return;
            penetrating.Add(id);
            decaying.Remove(id);
        }

        ApplyPenMap();
    }

    public void ForcePenetrate(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (penetrating.Contains(id)) return;

        penetrating.Add(id);
        forced.Add(id);
        decaying.Remove(id);
        if (colorCacheBuilt) ApplyPenMap();
    }

    void OnDayPassed()
    {
        foreach (var id in penetrating)
        {
            var st = BlockManager.Instance.GetState(id);
            if (st == null) continue;
            st.penetrationBonus = Mathf.Min(st.penetrationBonus + DailyBonus, MaxBonus);
        }

        var done = new List<string>();
        foreach (var id in decaying)
        {
            var st = BlockManager.Instance.GetState(id);
            if (st == null)
            {
                done.Add(id);
                continue;
            }
            st.penetrationBonus = Mathf.Max(0f, st.penetrationBonus - 1f);
            if (st.penetrationBonus <= 0f) done.Add(id);
        }
        foreach (var id in done) decaying.Remove(id);
    }

    void BuildColorCache()
    {
        if (colorCacheBuilt) return;
        colorCacheBuilt = true;
        colorToId.Clear();

        if (idMap == null || database == null) return;

        var seen = new HashSet<Color32>();
        var pixels = idMap.GetPixels32();
        foreach (var c in pixels)
        {
            if (seen.Add(c) && database.TryGetByColor(c, out var p))
                colorToId[c] = p.id;
        }
    }

    void ApplyPenMap()
    {
        if (mapMaterial == null || idMap == null) return;

        if (penMap == null || penMap.width != idMap.width || penMap.height != idMap.height)
            penMap = new Texture2D(idMap.width, idMap.height, TextureFormat.RGBA32, false);

        var pixels = idMap.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = pixels[i];
            if (colorToId.TryGetValue(c, out var id))
                pixels[i] = penetrating.Contains(id) ? PenGreen : ToGray(c);
        }

        penMap.SetPixels32(pixels);
        penMap.Apply();
        mapMaterial.SetTexture("_BaseMap", penMap);
    }

    void RestoreMap()
    {
        if (mapMaterial != null && idMap != null)
            mapMaterial.SetTexture("_BaseMap", idMap);
    }

    static Color32 ToGray(Color32 c)
    {
        byte g = (byte)((c.r + c.g + c.b) / 3);
        return new Color32(g, g, g, 255);
    }

    void EnsureHint()
    {
        if (hintRoot != null) return;

        var canvasGo = new GameObject("PenetrationHintCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        hintRoot = new GameObject("Hint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        hintRoot.transform.SetParent(canvasGo.transform, false);
        var img = hintRoot.GetComponent<Image>();
        img.color = UITheme.PaperBg;
        hintGroup = hintRoot.GetComponent<CanvasGroup>();

        var rt = hintRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(520f, 60f);
        rt.anchoredPosition = new Vector2(-20f, 100f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(hintRoot.transform, false);
        var t = textGo.GetComponent<Text>();
        if (font != null) t.font = font;
        t.text = "点击区块以加入/退出需要渗透的区块";
        t.fontSize = 24;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = UITheme.InkPrimary;

        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        hintRoot.SetActive(false);
    }

    void ShowHint()
    {
        if (hintRoot == null) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoot.SetActive(true);
        hintRoutine = StartCoroutine(UIAnim.Fade(hintGroup, 1f, UITheme.FadeFast));
    }

    void HideHint()
    {
        if (hintRoot == null) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HideHintRoutine());
    }

    System.Collections.IEnumerator HideHintRoutine()
    {
        yield return UIAnim.Fade(hintGroup, 0f, UITheme.FadeFast);
        hintRoot.SetActive(false);
        hintRoutine = null;
    }
}
