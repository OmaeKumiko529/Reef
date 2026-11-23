using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BlockDetailPanel : MonoBehaviour
{
    const float TopReserve = 140f;      // 上留 140：日期牌底边（H−130）+ 10（契约 70_ §9.0 L-01，保持）
    const float BottomReserve = 100f;   // 下留 100：底栏 80 + 20 间隙（契约 70_ §9.0 L-01，原为 0 → 面板不再压底栏）

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
    PanelChrome chrome;
    CanvasGroup contentGroup;
    Coroutine animRoutine;
    string currentId;
    ProvinceData currentProvince;

    public void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("BlockDetailCanvas", transform, UISorting.Panel);

        // 契约 70_ §6.0/§7.1：Panel 形态（纸底 + 直角双层框 + 标题栏 + 投影）
        // 尺寸与 L-01 逐字一致：380 × −(TopReserve 140 + BottomReserve 100) @ (−20,−20)
        chrome = UIPanelChrome.CreateStretched(canvas.transform, "Panel", ChromeKind.Panel, null,
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(UITheme.WidthS, -(TopReserve + BottomReserve)), new Vector2(-20f, -20f), UISorting.Panel);
        panelRoot = chrome.Root.gameObject;
        panelRect = chrome.Root;
        group = chrome.Group;

        // 标题与内容几何改由 chrome 的标题栏 / 内容区统一管理（不再手写 20/−80 与 20/−100）
        titleText = chrome.Title;
        UIPanelChrome.SetEyebrow(chrome, UICopy.Province.Eyebrow);
        titleGroup = titleText.gameObject.AddComponent<CanvasGroup>();

        contentText = UIFactory.CreateText("Content", chrome.Content, UITheme.FontBody, FontStyle.Normal, TextAnchor.UpperLeft, font);
        contentGroup = contentText.gameObject.AddComponent<CanvasGroup>();
        UIFactory.Stretch(contentText.rectTransform);

        panelRoot.SetActive(false);
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
            UIPanelChrome.SetDossierTag(chrome, string.Format(UICopy.Province.FileNoFormat, province.id));
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

        // 契约 70_ §15.1：静止位在协程入口取一次，动画只做相对位移。
        // （旧版用手算的垂直偏移与绝对终值复位面板，覆盖了 chrome 建出的 (-20,-20)，
        //   导致打开省份后右缘贴屏、底边落进底栏 30px —— 即 U-09 的错位真因。）
        Vector2 rest = panelRect.anchoredPosition;
        float width = panelRect.sizeDelta.x;
        Vector2 off = rest + new Vector2(width, 0f);
        panelRect.anchoredPosition = off;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float e = UITheme.EaseOutCubic(t / slideDuration);
            group.alpha = e;
            panelRect.anchoredPosition = Vector2.Lerp(off, rest, e);
            yield return null;
        }

        group.alpha = 1f;
        panelRect.anchoredPosition = rest;
        animRoutine = null;
    }

    System.Collections.IEnumerator SlideOutRoutine()
    {
        // 契约 70_ §15.1：从静止位滑出到 rest + width；协程结束必须写回 rest，否则下一次滑入的起点会漂移。
        Vector2 rest = panelRect.anchoredPosition;
        float width = panelRect.sizeDelta.x;
        Vector2 off = rest + new Vector2(width, 0f);
        panelRect.anchoredPosition = rest;

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float e = UITheme.EaseOutCubic(t / slideDuration);
            group.alpha = 1f - e;
            panelRect.anchoredPosition = Vector2.Lerp(rest, off, e);
            yield return null;
        }

        panelRect.anchoredPosition = rest;
        group.alpha = 1f;
        panelRoot.SetActive(false);
        animRoutine = null;
    }

    System.Collections.IEnumerator CrossFadeRoutine(ProvinceData province, BlockState state)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float e = UITheme.EaseOutCubic(t / fadeDuration);
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
            float e = UITheme.EaseOutCubic(t / fadeDuration);
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
        if (state == null) return UICopy.Common.EmptyProvince;

        var sb = new StringBuilder();
        if (province != null && PenetrationManager.Instance.IsPenetrating(province.id))
            sb.AppendLine(UICopy.Province.Penetrating);

        sb.AppendLine(UICopy.Province.Influence + state.influence.ToString("0.##"));
        sb.AppendLine();
        sb.AppendLine(UICopy.Province.InterestGroups);
        foreach (var id in GameContent.InterestGroupIds)
        {
            if (state.interestGroups.TryGetValue(id, out var w))
                sb.AppendLine("  " + GameContent.Name(id) + "  " + (w * 100f).ToString("0") + "%");
        }
        sb.AppendLine();
        sb.AppendLine(UICopy.Province.Ideologies);
        foreach (var id in GameContent.IdeologyIds)
        {
            if (state.ideologies.TryGetValue(id, out var w))
                sb.AppendLine("  " + GameContent.Name(id) + "  " + (w * 100f).ToString("0") + "%");
        }
        sb.AppendLine();
        sb.Append(UICopy.Province.Products);
        sb.AppendLine(string.Join("、", System.Array.ConvertAll(state.products, p => GameContent.Name(p))));
        sb.Append(UICopy.Province.Culture);
        sb.AppendLine(GameContent.Name(state.cultureId));
        return sb.ToString();
    }
}
