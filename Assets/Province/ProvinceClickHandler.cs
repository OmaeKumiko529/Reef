using System.Collections;
using UnityEngine;

/// <summary>
/// 省份点击的音效与选中高亮。
///
/// R3 变更（契约 00_方案与分工.md 2.4）：材质写入一律走 MaterialPropertyBlock，
/// 不再 `mapMaterial.SetXxx()` —— 那会脏化共享资产 `Assets/Map/MapMat.mat`
/// （现状：该 .mat 里烘焙着 `_HasSelection=1 / _SelectionBlend=1 / _SelectedId=1008号色`，
/// 导致开局自带一个假高亮）。同时 Awake 复位一次脏值，防用户在 Inspector 存过状态。
///
/// ❗注意：MPB 不能"清空"，只能覆盖。所以 Awake 复位只覆盖了 shader 会读的那几个开关；
/// `.mat` 里烘焙的数值仍需由美术在 T6 清零（两条互为保险，见契约 2.4）。
/// </summary>
public class ProvinceClickHandler : MonoBehaviour
{
    public ProvinceDatabase database;
    public Material mapMaterial;
    public AudioClip tickClip;
    public AudioClip penetrationClip;
    public AudioClip uiClickClip;
    public AudioClip eventClip;

    public float fadeDuration = 0.3f;

    // 属性名走 Shader.PropertyToID：拼错属性名时 MPB 会静默忽略，不会报错
    static readonly int IdSelectedId = Shader.PropertyToID("_SelectedId");
    static readonly int IdHasSelection = Shader.PropertyToID("_HasSelection");
    static readonly int IdSelectionBlend = Shader.PropertyToID("_SelectionBlend");
    static readonly int IdHoverId = Shader.PropertyToID("_HoverId");
    static readonly int IdHoverStrength = Shader.PropertyToID("_HoverStrength");

    AudioSource audioSource;
    Coroutine fadeRoutine;

    MaterialPropertyBlock mpb;
    MeshRenderer mapRenderer;

    /// <summary>渲染器上的属性块（读改写模式，不能新建后直接 SetPropertyBlock，否则会覆盖掉渗透模式写入的 _BaseMap）。</summary>
    MaterialPropertyBlock Block
    {
        get
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            return mpb;
        }
    }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        mpb = new MaterialPropertyBlock();
        mapRenderer = ResolveMapRenderer();
        ResetDirtyState();
    }

    /// <summary>Map 物体的渲染器（与 <see cref="ProvincePicker"/> 使用同一个）。</summary>
    public MeshRenderer MapRenderer => mapRenderer != null ? mapRenderer : (mapRenderer = ResolveMapRenderer());

    MeshRenderer ResolveMapRenderer()
    {
        // 本组件挂在 Map 物体上，普通网格渲染器就在同一个 GameObject 上
        var r = GetComponent<MeshRenderer>();
        if (r != null) return r;
        return GetComponentInParent<MeshRenderer>();
    }

    /// <summary>清掉可能被 Inspector 存下的选中/悬停状态（契约 2.4）。</summary>
    void ResetDirtyState()
    {
        WriteVector(IdSelectedId, Vector4.zero);
        WriteFloat(IdHasSelection, 0f);
        WriteFloat(IdSelectionBlend, 0f);
        WriteFloat(IdHoverStrength, 0f);
    }

    // -------------------------------------------------------------- 材质写入

    // 拿不到渲染器时只报一次错：绝不退化成写共享材质资产（MapMat.mat 的脏值就是这么来的）
    bool warnedNoRenderer;

    void WarnNoRenderer()
    {
        if (warnedNoRenderer) return;
        warnedNoRenderer = true;
        Debug.LogError("ProvinceClickHandler：地图上找不到 MeshRenderer，选中/悬停反馈无法写入。" +
                       "不会退化为写共享材质 MapMat.mat（那会污染资产）。");
    }

    void WriteVector(int id, Vector4 value)
    {
        var r = MapRenderer;
        if (r == null)
        {
            WarnNoRenderer();
            return;
        }

        r.GetPropertyBlock(Block);
        Block.SetVector(id, value);
        r.SetPropertyBlock(Block);
    }

    void WriteFloat(int id, float value)
    {
        var r = MapRenderer;
        if (r == null)
        {
            WarnNoRenderer();
            return;
        }

        r.GetPropertyBlock(Block);
        Block.SetFloat(id, value);
        r.SetPropertyBlock(Block);
    }

    // -------------------------------------------------------------- 选中

    public void HandleProvinceClicked(string provinceId)
    {
        if (database == null || (mapMaterial == null && mapRenderer == null)) return;

        ProvinceData p = database.GetById(provinceId);
        if (p == null) return;

        // _SelectedId 是 Vector 属性，传原始字节值/255，与线性纹理采样值同源，
        // 避免 Color 属性在 Linear 项目下的自动 sRGB->Linear 转换导致匹配错位
        Vector4 id = new Vector4(
            p.mapColor.r / 255f,
            p.mapColor.g / 255f,
            p.mapColor.b / 255f,
            1f);
        WriteVector(IdSelectedId, id);   // 内容高亮
        WriteFloat(IdHasSelection, 1f);  // 描边开关

        // 播放点击音效
        if (tickClip != null)
            audioSource.PlayOneShot(tickClip);

        // 重新开始渐入动画
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeInSelection());
    }

    public void PlayPenetrationTick()
    {
        if (penetrationClip != null)
            audioSource.PlayOneShot(penetrationClip);
    }

    /// <summary>
    /// 悬停高亮：写 `_HoverId / _HoverStrength`（契约 2.1 新增属性）。
    /// ❗不修改真正的选中态 `_SelectedId / _HasSelection`，两者互不抢占；
    /// hovered 传 null 表示移出地图，清掉悬停。
    /// </summary>
    public void SetHover(ProvinceData hovered)
    {
        if (mapMaterial == null && mapRenderer == null) return;

        if (hovered == null)
        {
            WriteFloat(IdHoverStrength, 0f);
            return;
        }

        WriteVector(IdHoverId, new Vector4(
            hovered.mapColor.r / 255f,
            hovered.mapColor.g / 255f,
            hovered.mapColor.b / 255f,
            1f));
        WriteFloat(IdHoverStrength, 1f);
    }

    public void ClearSelection()
    {
        if (mapMaterial == null && mapRenderer == null) return;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        WriteFloat(IdHasSelection, 0f);
        WriteFloat(IdSelectionBlend, 0f);
    }

    IEnumerator FadeInSelection()
    {
        // 每帧写 MPB：读改写模式，因此不会覆盖渗透模式写入的 _BaseMap
        WriteFloat(IdSelectionBlend, 0f);
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            WriteFloat(IdSelectionBlend, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        WriteFloat(IdSelectionBlend, 1f);
        fadeRoutine = null;
    }
}
