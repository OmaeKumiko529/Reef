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

    /// <summary>纹理由程序写入时用到的属性名（拼错不会报错，只会静默失效）。</summary>
    static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");

    Texture2D idMap;
    ProvinceDatabase database;
    Material mapMaterial;
    MeshRenderer mapRenderer;
    Font font;

    // 拿不到渲染器时只报一次错，避免每帧刷屏
    bool warnedNoRenderer;

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
    Text hintText;
    Coroutine hintRoutine;

    /// <summary>
    /// 渗透名单或上限发生变化时触发（参数：已选数量、上限）。
    /// 供底栏/面板刷新计数，不要再各自轮询 <see cref="GetPenetratingIds"/>。
    /// </summary>
    public event System.Action<int, int> OnStateChanged;

    /// <summary>已达上限、点击被拒绝的反馈计时（用于把提示条短暂刷成警示色）。</summary>
    float limitFlashUntil;

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

    /// <summary>旧签名（保留：调用方不必改）。渲染器由 <see cref="Setup(Texture2D,ProvinceDatabase,Material,MeshRenderer,Font)"/> 内部查找。</summary>
    public void Setup(Texture2D map, ProvinceDatabase db, Material mat, Font font)
    {
        Setup(map, db, mat, null, font);
    }

    public void Setup(Texture2D map, ProvinceDatabase db, Material mat, MeshRenderer renderer, Font font)
    {
        idMap = map;
        database = db;
        mapMaterial = mat;
        mapRenderer = renderer != null ? renderer : FindMapRenderer();
        this.font = font;
        EnsureHint();
        RefreshHint();
    }

    /// <summary>地图的网格渲染器（Map 物体上的那个）。</summary>
    static MeshRenderer FindMapRenderer()
    {
        var go = GameObject.Find("Map");
        return go != null ? go.GetComponent<MeshRenderer>() : null;
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
            // 达上限时不再静默 return：提示条闪一下警示色，让玩家知道"点不动是有原因的"
            if (penetrating.Count - forced.Count >= MaxBlocks)
            {
                limitFlashUntil = Time.time + 0.6f;
                RefreshHint();
                return;
            }
            penetrating.Add(id);
            decaying.Remove(id);
        }

        ApplyPenMap();
        RefreshHint();
    }

    public void ForcePenetrate(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (penetrating.Contains(id)) return;

        penetrating.Add(id);
        forced.Add(id);
        decaying.Remove(id);
        if (colorCacheBuilt) ApplyPenMap();
        RefreshHint();
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
        if (idMap == null || (mapMaterial == null && mapRenderer == null)) return;

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
        WriteBaseMap(penMap);
    }

    void RestoreMap()
    {
        WriteBaseMap(idMap);
    }

    /// <summary>
    /// 换掉 `_BaseMap`：必须走 MPB（契约 2.4），否则会脏化共享资产 `Assets/Map/MapMat.mat`。
    /// ❗MPB 是"读改写"：直接新建一个空块 SetPropertyBlock 会把点击高亮等其它属性一起清掉。
    /// </summary>
    void WriteBaseMap(Texture2D tex)
    {
        if (tex == null) return;

        // 拿不到渲染器时只报一次错：绝不退化成写共享材质资产（那会污染 MapMat.mat 的 _BaseMap 引用）
        if (mapRenderer == null)
        {
            if (!warnedNoRenderer)
            {
                warnedNoRenderer = true;
                Debug.LogError("PenetrationManager：地图上找不到 MeshRenderer，渗透模式的底图切换无法写入。" +
                               "不会退化为写共享材质 MapMat.mat（那会污染资产）。");
            }
            return;
        }

        var mpb = new MaterialPropertyBlock();
        mapRenderer.GetPropertyBlock(mpb);
        mpb.SetTexture(IdBaseMap, tex);
        mapRenderer.SetPropertyBlock(mpb);
    }

    static Color32 ToGray(Color32 c)
    {
        byte g = (byte)((c.r + c.g + c.b) / 3);
        return new Color32(g, g, g, 255);
    }

    void EnsureHint()
    {
        if (hintRoot != null) return;

        // 用 UIFactory 建：旧实现手写 Canvas 时漏了 GraphicRaycaster，
        // 而且 sortingOrder 默认 0，层序只能靠创建顺序
        var canvas = UIFactory.CreateCanvas("PenetrationHintCanvas", transform, UISorting.Hint);

        // 契约 70_ §9.0 L-02：移到底部居中（M 档 620×60，下边距 100 = 底栏 80 + 20 间隙）；
        // 原为右下锚点 (1,0)@(−20,100)，会与右侧详情面板的正文区相交。Strip 形态 = 无标题栏的外框+纸底。
        var chrome = UIPanelChrome.CreateStretched(canvas.transform, "Hint", ChromeKind.Strip, null,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(UITheme.WidthM, 60f), new Vector2(0f, 100f), UISorting.Hint);
        hintRoot = chrome.Root.gameObject;
        hintGroup = chrome.Group;

        hintText = UIFactory.CreateStretchText("Text", chrome.Content, UITheme.FontButton,
            FontStyle.Normal, TextAnchor.MiddleCenter, font);
        hintText.color = UITheme.InkPrimary;

        hintRoot.SetActive(false);
    }

    /// <summary>
    /// 刷新提示条文案：`已选 N/上限`（契约 2.3 `UICopy.Penetration.Hint`）。
    /// 达上限被拒时（<see cref="limitFlashUntil"/>）整条刷成警示色。
    /// </summary>
    void RefreshHint(bool notify = true)
    {
        // 口径必须与 Toggle() 的上限判据（penetrating.Count - forced.Count >= MaxBlocks）一致：
        // 事件强制渗透的省份（ForcePenetrate -> forced）不占玩家名额，若一并计数会出现「已选 4/3 且还能继续点」。
        int selected = penetrating.Count - forced.Count;
        int limit = MaxBlocks;

        if (hintText != null)
        {
            bool limitHit = Time.time < limitFlashUntil;
            // 达上限时换成「名额已满」文案（契约 70_ §5.0 Penetration.HintFull），并整条转警示色
            hintText.text = limitHit
                ? string.Format(UICopy.Penetration.HintFull, selected, limit)
                : string.Format(UICopy.Penetration.Hint, selected, limit);
            hintText.color = limitHit ? UITheme.StateAlert : UITheme.InkPrimary;
            hintText.fontStyle = limitHit ? FontStyle.Bold : FontStyle.Normal;
        }

        if (notify) OnStateChanged?.Invoke(selected, limit);
    }

    void ShowHint()
    {
        if (hintRoot == null) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        RefreshHint();
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
