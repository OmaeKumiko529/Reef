using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 素材装载器（第三轮「纸印素材」）：读 Assets/Resources/UIArt/*.png。
///
/// 纪律（契约 70_ §3.0 写权限矩阵的补充条目）：
/// <list type="bullet">
/// <item>贴图一律是「中性白 + alpha」：颜色仍由 UITheme 的 token 决定，
/// 所以 <see cref="Graphic.color"/> 的五态换色、ChromeState 换色全部照旧生效。</item>
/// <item>找不到素材时全部返回 null，调用方保持原来的纯色外观 —— 删掉整个 UIArt 目录
/// 游戏仍能跑，只是回到第二轮的样子（优雅降级，不抛异常、不报错）。</item>
/// <item>只读不写：本类不生成任何 Texture2D / Sprite。</item>
/// </list>
/// </summary>
public static class UIArt
{
    public const string Folder = "UIArt/";

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();
    static bool warnedMissing;

    // ── 素材表（名字即文件名，见 Tools/gen-uiart.py 的 ASSETS 清单）────────────
    public static Sprite Plate           { get { return Get("paper_plate_9s"); } }        // 纸底（9-slice 18）
    public static Sprite TitlePlate      { get { return Get("titleplate_9s"); } }         // 标题栏纸板（9-slice 20,0,20,0）
    public static Sprite RuleRing        { get { return Get("rule_ring_9s"); } }          // 1px 细墨线环（9-slice 8）
    public static Sprite RuleRingWorn    { get { return Get("rule_ring_worn_9s"); } }     // 1px 做旧墨线环（9-slice 8）
    public static Sprite ButtonPlate     { get { return Get("btn_plate_9s"); } }          // 按钮压印板（9-slice 12）
    public static Sprite RedactionBar    { get { return Get("redaction_bar_9s"); } }      // 打码条（9-slice 12,0,12,0）
    public static Sprite DossierPlate    { get { return Get("dossier_plate_9s"); } }      // 档号角标纸片（9-slice 12,0,12,0）
    public static Sprite BindingDot      { get { return Get("binding_dot"); } }           // 装订/线索节点
    public static Sprite GrainTile       { get { return Get("grain_tile"); } }            // 颗粒叠层（可平铺）
    public static Sprite HalftoneTile    { get { return Get("halftone_tile"); } }         // 网点叠层（可平铺）
    public static Sprite Vignette        { get { return Get("vignette"); } }              // 暗角
    public static Sprite StampOpen       { get { return Get("stamp_weikaifang"); } }      // 「未开放」朱砂章
    public static Sprite StampPenetrate  { get { return Get("stamp_shentou"); } }         // 「已渗透」朱砂章
    public static Sprite StampSecret     { get { return Get("stamp_jimi"); } }            // 「机密」朱砂章

    /// <summary>取 Sprite（带缓存）。缺失时返回 null —— 调用方必须容错。</summary>
    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Sprite sp;
        if (Cache.TryGetValue(name, out sp)) return sp;
        sp = Resources.Load<Sprite>(Folder + name);
        if (sp == null && !warnedMissing)
        {
            warnedMissing = true;
            Debug.LogWarning("[Reef] UIArt 素材缺失（首个：" + Folder + name +
                "）：界面回退到纯色外观。请确认 Assets/Resources/UIArt/ 下的 PNG 已按 Sprite (2D and UI) 导入。");
        }
        Cache[name] = sp;
        return sp;
    }

    /// <summary>取贴图（平铺叠层用 RawImage 时直接要 Texture）。</summary>
    public static Texture2D TextureOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Texture2D tex;
        if (TexCache.TryGetValue(name, out tex)) return tex;
        var sp = Get(name);
        tex = sp != null ? sp.texture : null;
        TexCache[name] = tex;
        return tex;
    }

    /// <summary>把 9-slice 贴图挂到 Image 上（保持原有 color 不变）。素材缺失则什么都不做。</summary>
    public static bool ApplySliced(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return false;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.preserveAspect = false;
        return true;
    }

    /// <summary>把整幅贴图挂到 Image 上（保持原有 color 不变）。素材缺失则什么都不做。</summary>
    public static bool ApplySimple(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return false;
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        return true;
    }

    /// <summary>按印章文案取对应素材（无对应素材返回 null，调用方回退到程序化方框章）。</summary>
    public static Sprite SealFor(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text == UICopy.Common.NotImplemented) return StampOpen;      // 「未开放」
        if (text == "已渗透") return StampPenetrate;
        if (text == "机密") return StampSecret;
        return null;
    }

    /// <summary>印章素材的宽高比（176×72），用于把方形章座改成合适的长宽。</summary>
    public const float SealAspect = 176f / 72f;

    /// <summary>
    /// 屏幕叠层：网点 + 颗粒 + 暗角（画布 sortingOrder = 50，位于地图之上、HUD(100) 之下）。
    /// 全部 raycastTarget = false；素材缺失则整块不建。由 ProvincePicker.Setup 调用一次。
    /// </summary>
    public static GameObject BuildScreenOverlays(Transform parent)
    {
        var halftone = HalftoneTile;
        var grain = GrainTile;
        var vignette = Vignette;
        if (halftone == null && grain == null && vignette == null) return null;

        var canvas = UIFactory.CreateCanvas("ArtOverlay", parent, 50);
        canvas.sortingOrder = 50;
        var root = canvas.transform;
        var group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        AddTiled(root, "Halftone", halftone, 64f, 1f);
        AddTiled(root, "Grain", grain, 256f, 1f);
        if (vignette != null)
        {
            var go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-40f, -40f);   // 略微外扩，暗角压在屏幕边缘
            rt.offsetMax = new Vector2(40f, 40f);
            var img = go.GetComponent<Image>();
            img.sprite = vignette;
            img.type = Image.Type.Simple;
            img.color = new Color(1f, 1f, 1f, 0.85f);
            img.raycastTarget = false;
        }
        return canvas.gameObject;
    }

    static void AddTiled(Transform parent, string name, Sprite sprite, float tilePixels, float alpha)
    {
        if (sprite == null) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var raw = go.GetComponent<RawImage>();
        raw.texture = sprite.texture;
        raw.color = new Color(1f, 1f, 1f, alpha);
        raw.raycastTarget = false;
        // uvRect 决定平铺密度：按「参考分辨率 1920×1080 / tilePixels」算，分辨率变化时由
        // CanvasScaler 缩放整块画布，密度观感不变。
        raw.uvRect = new Rect(0f, 0f, 1920f / tilePixels, 1080f / tilePixels);
    }
}
