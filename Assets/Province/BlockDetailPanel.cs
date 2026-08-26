using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BlockDetailPanel : MonoBehaviour
{
    const float TopReserve = 140f;

    public Font font;
    public float slideDuration = 0.25f;
    public float fadeDuration = 0.15f;

    Canvas canvas;
    GameObject panelRoot;
    RectTransform panelRect;
    CanvasGroup group;
    Text titleText;
    Text contentText;
    CanvasGroup titleGroup;
    CanvasGroup contentGroup;
    Coroutine animRoutine;
    string currentId;
    ProvinceData currentProvince;

    public void EnsureUI()
    {
        if (canvas != null) return;

        var canvasGo = new GameObject("BlockDetailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var bg = panelRoot.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.09f, 0.13f, 0.96f);
        group = panelRoot.GetComponent<CanvasGroup>();

        panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(380f, -TopReserve);
        panelRect.anchoredPosition = new Vector2(0f, -TopReserve * 0.5f);

        titleText = CreateText("Title", panelRoot.transform, 32, FontStyle.Bold, TextAnchor.MiddleLeft);
        titleGroup = titleText.GetComponent<CanvasGroup>();
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(20f, -80f);
        trt.offsetMax = new Vector2(-20f, 0f);

        contentText = CreateText("Content", panelRoot.transform, 22, FontStyle.Normal, TextAnchor.UpperLeft);
        contentGroup = contentText.GetComponent<CanvasGroup>();
        var crt = contentText.rectTransform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(20f, 20f);
        crt.offsetMax = new Vector2(-20f, -100f);

        panelRoot.SetActive(false);
    }

    Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public void Show(ProvinceData province, BlockState state)
    {
        if (canvas == null) EnsureUI();
        if (province == null)
        {
            Hide();
            return;
        }

        if (!panelRoot.activeSelf)
        {
            currentId = province.id;
            currentProvince = province;
            titleText.text = province.displayName;
            contentText.text = BuildContent(province, state);
            PlaySlideIn();
        }
        else if (currentId == province.id)
        {
            currentId = null;
            PlaySlideOut();
        }
        else
        {
            currentId = province.id;
            currentProvince = province;
            CrossFade(province, state);
        }
    }

    public void Hide()
    {
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (animRoutine != null) return;
        if (panelRoot == null || !panelRoot.activeSelf || string.IsNullOrEmpty(currentId)) return;

        var state = BlockManager.Instance.GetState(currentId);
        contentText.text = BuildContent(currentProvince, state);
    }

    void PlaySlideIn()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(SlideInRoutine());
    }

    void PlaySlideOut()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(SlideOutRoutine());
    }

    void CrossFade(ProvinceData province, BlockState state)
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(CrossFadeRoutine(province, state));
    }

    System.Collections.IEnumerator SlideInRoutine()
    {
        panelRoot.SetActive(true);
        group.alpha = 0f;
        float y = -TopReserve * 0.5f;
        panelRect.anchoredPosition = new Vector2(panelRect.sizeDelta.x, y);

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            group.alpha = e;
            panelRect.anchoredPosition = new Vector2(panelRect.sizeDelta.x * (1f - e), y);
            yield return null;
        }

        group.alpha = 1f;
        panelRect.anchoredPosition = new Vector2(0f, y);
        animRoutine = null;
    }

    System.Collections.IEnumerator SlideOutRoutine()
    {
        float w = panelRect.sizeDelta.x;
        float y = -TopReserve * 0.5f;
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideDuration));
            group.alpha = 1f - e;
            panelRect.anchoredPosition = new Vector2(w * e, y);
            yield return null;
        }

        panelRoot.SetActive(false);
        group.alpha = 1f;
        panelRect.anchoredPosition = new Vector2(0f, y);
        animRoutine = null;
    }

    System.Collections.IEnumerator CrossFadeRoutine(ProvinceData province, BlockState state)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration));
            float a = 1f - e;
            titleGroup.alpha = a;
            contentGroup.alpha = a;
            yield return null;
        }

        titleText.text = province.displayName;
        contentText.text = BuildContent(province, state);

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration));
            titleGroup.alpha = e;
            contentGroup.alpha = e;
            yield return null;
        }

        titleGroup.alpha = 1f;
        contentGroup.alpha = 1f;
        animRoutine = null;
    }

    string BuildContent(ProvinceData province, BlockState state)
    {
        if (state == null) return "无数据";

        var sb = new StringBuilder();
        if (province != null && PenetrationManager.Instance.IsPenetrating(province.id))
            sb.AppendLine("正在主动渗透");

        sb.AppendLine("影响力: " + state.influence.ToString("0.##"));
        sb.AppendLine();
        sb.AppendLine("利益集团:");
        foreach (var id in GameContent.InterestGroupIds)
        {
            if (state.interestGroups.TryGetValue(id, out var w))
                sb.AppendLine("  " + GameContent.Name(id) + "  " + (w * 100f).ToString("0") + "%");
        }
        sb.AppendLine();
        sb.AppendLine("政治思潮:");
        foreach (var id in GameContent.IdeologyIds)
        {
            if (state.ideologies.TryGetValue(id, out var w))
                sb.AppendLine("  " + GameContent.Name(id) + "  " + (w * 100f).ToString("0") + "%");
        }
        sb.AppendLine();
        sb.Append("主要产出物: ");
        sb.AppendLine(string.Join("、", System.Array.ConvertAll(state.products, p => GameContent.Name(p))));
        sb.Append("主流文化: ");
        sb.AppendLine(GameContent.Name(state.cultureId));
        return sb.ToString();
    }
}
