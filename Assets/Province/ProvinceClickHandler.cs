using System.Collections;
using UnityEngine;

public class ProvinceClickHandler : MonoBehaviour
{
    public ProvinceDatabase database;
    public Material mapMaterial;
    public AudioClip tickClip;
    public AudioClip penetrationClip;
    public AudioClip uiClickClip;
    public AudioClip eventClip;

    public float fadeDuration = 0.3f;

    AudioSource audioSource;
    Coroutine fadeRoutine;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void HandleProvinceClicked(string provinceId)
    {
        if (database == null || mapMaterial == null) return;

        ProvinceData p = database.GetById(provinceId);
        if (p == null) return;

        // _SelectedId 已改为 Vector 属性，传原始字节值/255，与线性纹理采样值同源，
        // 避免 Color 属性在 Linear 项目下的自动 sRGB->Linear 转换导致匹配错位
        Vector4 id = new Vector4(
            p.mapColor.r / 255f,
            p.mapColor.g / 255f,
            p.mapColor.b / 255f,
            1f);
        mapMaterial.SetVector("_SelectedId", id); //内容高亮
        mapMaterial.SetFloat("_HasSelection", 1f); //描边开关

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

    public void ClearSelection()
    {
        if (mapMaterial == null) return;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        mapMaterial.SetFloat("_HasSelection", 0f);
        mapMaterial.SetFloat("_SelectionBlend", 0f);
    }

    IEnumerator FadeInSelection()
    {
        mapMaterial.SetFloat("_SelectionBlend", 0f);
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            mapMaterial.SetFloat("_SelectionBlend", Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        mapMaterial.SetFloat("_SelectionBlend", 1f);
        fadeRoutine = null;
    }
}