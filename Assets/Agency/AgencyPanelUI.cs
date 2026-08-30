using UnityEngine;
using UnityEngine.UI;

public class AgencyPanelUI : MonoBehaviour
{
    static AgencyPanelUI _instance;

    public static AgencyPanelUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AgencyPanelUI>();
                if (_instance == null)
                {
                    var go = new GameObject("AgencyPanelUI");
                    _instance = go.AddComponent<AgencyPanelUI>();
                }
            }
            return _instance;
        }
    }

    static readonly string[] DeptNames = { "渗透处", "科技处", "内务处", "情报处" };
    const int LevelCount = 5;

    static readonly Color FilledColor = UITheme.SealRed;
    static readonly Color EmptyColor = UITheme.Divider;

    public Font font;

    Canvas canvas;
    GameObject panelRoot;
    CanvasGroup group;
    Coroutine animRoutine;

    readonly Text[] valueTexts = new Text[4];
    readonly Image[][] levelCells = new Image[4][];

    public bool IsOpen { get; private set; }

    public void Setup(Font font)
    {
        this.font = font;
        EnsureUI();
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("AgencyPanelCanvas", transform);

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelRoot.transform.SetParent(canvas.transform, false);
        var img = panelRoot.GetComponent<Image>();
        img.color = UITheme.PaperBg;
        group = panelRoot.GetComponent<CanvasGroup>();

        var rt = panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(820f, 0f);
        rt.anchoredPosition = Vector2.zero;

        var layout = panelRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 24, 24);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelRoot.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(panelRoot.transform, false);
        var title = titleGo.GetComponent<Text>();
        if (font != null) title.font = font;
        title.text = "机构";
        title.fontSize = 34;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = UITheme.InkPrimary;
        var titleLE = titleGo.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 50f;

        for (int d = 0; d < DeptNames.Length; d++)
            BuildRow(panelRoot.transform, d);

        panelRoot.SetActive(false);
    }

    void BuildRow(Transform parent, int dept)
    {
        var rowGo = new GameObject("Row_" + DeptNames[dept], typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(parent, false);
        var rl = rowGo.GetComponent<HorizontalLayoutGroup>();
        rl.spacing = 14f;
        rl.childAlignment = TextAnchor.MiddleLeft;
        rl.childControlWidth = true;
        rl.childControlHeight = true;
        rl.childForceExpandWidth = true;
        rl.childForceExpandHeight = false;
        var rowLE = rowGo.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 56f;

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(rowGo.transform, false);
        var name = nameGo.GetComponent<Text>();
        if (font != null) name.font = font;
        name.text = DeptNames[dept];
        name.fontSize = 26;
        name.fontStyle = FontStyle.Bold;
        name.alignment = TextAnchor.MiddleLeft;
        name.color = UITheme.InkPrimary;
        var nameLE = nameGo.AddComponent<LayoutElement>();
        nameLE.preferredWidth = 110f;
        nameLE.flexibleWidth = 0f;

        var barGo = new GameObject("Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        barGo.transform.SetParent(rowGo.transform, false);
        var bl = barGo.GetComponent<HorizontalLayoutGroup>();
        bl.spacing = 6f;
        bl.childAlignment = TextAnchor.MiddleLeft;
        bl.childControlWidth = true;
        bl.childControlHeight = false;
        bl.childForceExpandWidth = false;
        bl.childForceExpandHeight = false;
        var barLE = barGo.AddComponent<LayoutElement>();
        barLE.preferredWidth = 170f;
        barLE.preferredHeight = 24f;
        barLE.flexibleWidth = 0f;

        levelCells[dept] = new Image[LevelCount];
        for (int i = 0; i < LevelCount; i++)
        {
            var cellGo = new GameObject("Cell" + i, typeof(RectTransform), typeof(Image));
            cellGo.transform.SetParent(barGo.transform, false);
            var cell = cellGo.GetComponent<Image>();
            cell.color = EmptyColor;
            levelCells[dept][i] = cell;
            var cellLE = cellGo.AddComponent<LayoutElement>();
            cellLE.preferredWidth = 28f;
            cellLE.preferredHeight = 24f;
        }

        var valGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
        valGo.transform.SetParent(rowGo.transform, false);
        var val = valGo.GetComponent<Text>();
        if (font != null) val.font = font;
        val.fontSize = 18;
        val.alignment = TextAnchor.MiddleLeft;
        val.color = UITheme.InkSecondary;
        val.horizontalOverflow = HorizontalWrapMode.Wrap;
        val.verticalOverflow = VerticalWrapMode.Overflow;
        valueTexts[dept] = val;
        var valLE = valGo.AddComponent<LayoutElement>();
        valLE.preferredWidth = 200f;
        valLE.flexibleWidth = 1f;

        int captured = dept;
        CreateLevelButton(rowGo.transform, "-", () => OnDowngrade(captured));
        CreateLevelButton(rowGo.transform, "+", () => OnUpgrade(captured));
    }

    GameObject CreateLevelButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        var btnImg = btnGo.GetComponent<Image>();
        btnImg.color = UITheme.PaperTop;
        var btn = btnGo.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var btnLE = btnGo.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 44f;
        btnLE.preferredHeight = 40f;
        btnLE.flexibleWidth = 0f;

        var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lblGo.transform.SetParent(btnGo.transform, false);
        var lbl = lblGo.GetComponent<Text>();
        if (font != null) lbl.font = font;
        lbl.text = label;
        lbl.fontSize = 26;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.color = UITheme.InkPrimary;
        var lblRT = lbl.rectTransform;
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        btn.onClick.AddListener(onClick);
        return btnGo;
    }

    int GetLevel(int dept)
    {
        if (dept == 0) return PenetrationManager.Instance.infiltrationLevel;
        return 1;
    }

    string GetValueText(int dept)
    {
        if (dept == 0)
        {
            var p = PenetrationManager.Instance;
            return "等级 " + p.infiltrationLevel + " ｜ 区块 " + p.MaxBlocks + " ｜ 日 +" + p.DailyBonus.ToString("0.#") + " ｜ 上限 +" + p.MaxBonus.ToString("0");
        }
        return "未实装";
    }

    void OnUpgrade(int dept)
    {
        if (dept != 0) return;
        int cur = PenetrationManager.Instance.infiltrationLevel;
        if (cur >= LevelCount) return;
        PenetrationManager.Instance.SetLevel(cur + 1);
        Refresh();
    }

    void OnDowngrade(int dept)
    {
        if (dept != 0) return;
        int cur = PenetrationManager.Instance.infiltrationLevel;
        if (cur <= 1) return;
        PenetrationManager.Instance.SetLevel(cur - 1);
        Refresh();
    }

    public void Toggle() { SetOpen(!IsOpen); }

    public void SetOpen(bool open)
    {
        if (canvas == null) EnsureUI();
        if (open == IsOpen) return;

        IsOpen = open;
        if (animRoutine != null) StopCoroutine(animRoutine);

        if (open)
        {
            panelRoot.SetActive(true);
            Refresh();
            for (int i = 0; i < LevelCount; i++)
                levelCells[0][i].color = EmptyColor;
            animRoutine = StartCoroutine(OpenRoutine());
        }
        else
        {
            animRoutine = StartCoroutine(CloseRoutine());
        }
    }

    System.Collections.IEnumerator OpenRoutine()
    {
        group.alpha = 0f;
        panelRoot.transform.localScale = Vector3.one * 0.95f;

        float t = 0f;
        while (t < UITheme.FadeNormal)
        {
            t += Time.deltaTime;
            float k = UITheme.EaseOutCubic(t / UITheme.FadeNormal);
            group.alpha = Mathf.Lerp(0f, 1f, k);
            panelRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        panelRoot.transform.localScale = Vector3.one;

        int penLevel = GetLevel(0);
        for (int i = 0; i < penLevel; i++)
        {
            levelCells[0][i].color = FilledColor;
            yield return new WaitForSeconds(0.05f);
        }

        animRoutine = null;
    }

    System.Collections.IEnumerator CloseRoutine()
    {
        yield return UIAnim.Fade(group, 0f, UITheme.FadeFast);
        panelRoot.SetActive(false);
        animRoutine = null;
    }

    void Refresh()
    {
        for (int d = 0; d < DeptNames.Length; d++)
        {
            int lvl = GetLevel(d);
            for (int i = 0; i < LevelCount; i++)
                levelCells[d][i].color = (i < lvl) ? FilledColor : EmptyColor;
            valueTexts[d].text = GetValueText(d);
        }
    }
}
