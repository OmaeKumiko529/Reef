using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「机构总览」面板：逐行展示各部门的投入等级。
/// 行数、部门名、是否已开放全部由 <see cref="UICopy.Dept"/> 驱动，
/// 未开放部门只显示「未开放」状态与空格子，绝不显示假的 1/5 进度（契约 2.6）。
/// </summary>
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

    /// <summary>
    /// 部门行数：<see cref="UICopy.Dept.All"/> 的末项是「机构总览」入口本身而不是部门，故减一。
    /// 不再写死 4（契约 2.3：部门数量由文案表驱动）。
    /// </summary>
    static int DepartmentCount => Mathf.Max(0, UICopy.Dept.All.Length - 1);

    const int LevelCount = 5;

    static readonly Color FilledColor = UITheme.SealRed;
    static readonly Color EmptyColor = UITheme.Divider;

    /// <summary>该部门是否已开放；文案表长度异常时按"未开放"处理。</summary>
    static bool IsImplemented(int dept)
    {
        var impl = UICopy.Dept.Implemented;
        return impl != null && dept >= 0 && dept < impl.Length && impl[dept];
    }

    public Font font;

    Canvas canvas;
    PanelChrome chrome;
    Text countText;
    GameObject panelRoot;
    CanvasGroup group;
    Coroutine animRoutine;

    // 长度在 EnsureUI() 里按 DepartmentCount 分配（原来写死 new Text[4] / new Image[4][]）
    Text[] valueTexts;
    Image[][] levelCells;

    public bool IsOpen { get; private set; }

    public void Setup(Font font)
    {
        this.font = font;
        EnsureUI();
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        int rows = DepartmentCount;
        valueTexts = new Text[rows];
        levelCells = new Image[rows][];

        canvas = UIFactory.CreateCanvas("AgencyPanelCanvas", transform, UISorting.Panel);

        // 契约 70_ §6.0/§7.1：Panel 形态（L 档 820 宽 + 直角双层框 + 铅字标题栏 + 朱砂竖块 + 投影）
        chrome = UIPanelChrome.Create(canvas.transform, "Panel", ChromeKind.Panel, UICopy.Agency.Title,
            new Vector2(UITheme.WidthL, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, UISorting.Panel);
        panelRoot = chrome.Root.gameObject;
        group = chrome.Group;
        UIPanelChrome.SetEyebrow(chrome, UICopy.Dept.Eyebrow);

        // chrome 的装饰层（框线/纸底/标题栏/档号/印章）不能参与下面的垂直布局，
        // 否则会被当成布局行、把标题栏挤下去
        foreach (Transform child in chrome.Root)
        {
            var cle = child.GetComponent<LayoutElement>();
            if (cle == null) cle = child.gameObject.AddComponent<LayoutElement>();
            cle.ignoreLayout = true;
        }

        var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, (int)(UITheme.TitleBarHeight + UITheme.SpaceLg), 24);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 契约 70_ §5.0：在册 N 处 · 已开放 M 处（Agency.CountFormat）
        int openCount = 0;
        for (int i = 0; i < rows; i++)
            if (IsImplemented(i)) openCount++;

        countText = UIFactory.CreateText("Count", panelRoot.transform,
            UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        countText.text = string.Format(UICopy.Agency.CountFormat, rows, openCount);
        countText.color = UITheme.InkMuted;
        countText.gameObject.AddComponent<LayoutElement>().preferredHeight = UITheme.FontCaption;

        for (int d = 0; d < rows; d++)
            BuildRow(panelRoot.transform, d);

        panelRoot.SetActive(false);
    }

    void BuildRow(Transform parent, int dept)
    {
        bool implemented = IsImplemented(dept);
        string deptName = UICopy.Dept.All[dept];

        var rowGo = new GameObject("Row_" + deptName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
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

        var name = UIFactory.CreateText("Name", rowGo.transform,
            UITheme.FontSubtitle, FontStyle.Bold, TextAnchor.MiddleLeft, font);
        name.text = deptName;
        // 未开放部门的名字压暗，与「未开放」状态一致
        name.color = implemented ? UITheme.InkPrimary : UITheme.InkMuted;
        var nameLE = name.gameObject.AddComponent<LayoutElement>();
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
            cell.raycastTarget = false; // 纯装饰格子，不参与射线检测
            levelCells[dept][i] = cell;
            var cellLE = cellGo.AddComponent<LayoutElement>();
            cellLE.preferredWidth = 28f;
            cellLE.preferredHeight = 24f;
        }

        var val = UIFactory.CreateText("Value", rowGo.transform,
            UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        val.color = implemented ? UITheme.InkSecondary : UITheme.InkMuted;
        valueTexts[dept] = val;
        var valLE = val.gameObject.AddComponent<LayoutElement>();
        valLE.preferredWidth = 200f;
        valLE.flexibleWidth = 1f;

        if (!implemented)
        {
            // 未开放部门：不生成 +/- 按钮（原本点了也没用），状态词用文案表的「未开放」
            val.text = UICopy.Common.NotImplemented;
            return;
        }

        CreateLevelButton(rowGo.transform, "-", () => OnDowngrade(dept));
        CreateLevelButton(rowGo.transform, "+", () => OnUpgrade(dept));
    }

    GameObject CreateLevelButton(Transform parent, string label, System.Action onClick)
    {
        var btn = UIFactory.CreateButton("Btn_" + label, parent, label, font,
            UITheme.FontSubtitle, UITheme.PaperTop, UITheme.InkPrimary, onClick);
        var btnLE = btn.gameObject.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 44f;
        btnLE.preferredHeight = 44f;   // 契约 70_ §15.3（P7）：点击目标 ≥44×44
        btnLE.flexibleWidth = 0f;
        return btn.gameObject;
    }

    /// <summary>
    /// 该部门的当前等级。目前只有渗透处（下标 0）有真实等级来源；
    /// 未开放部门一律返回 0，让等级格全空——原来的 <c>return 1</c> 会让未实装部门常亮第一格，
    /// 看起来像已经投过一级（假进度）。
    /// </summary>
    int GetLevel(int dept)
    {
        if (!IsImplemented(dept)) return 0;
        if (dept == 0) return PenetrationManager.Instance.infiltrationLevel;
        return 0;
    }

    string GetValueText(int dept)
    {
        if (!IsImplemented(dept)) return UICopy.Common.NotImplemented;
        if (dept == 0)
        {
            var p = PenetrationManager.Instance;
            return string.Format(UICopy.Agency.ValueFormat,
                p.infiltrationLevel, p.MaxBlocks,
                p.DailyBonus.ToString("0.#"), p.MaxBonus.ToString("0"));
        }
        return UICopy.Common.NotImplemented;
    }

    void OnUpgrade(int dept)
    {
        if (dept != 0 || !IsImplemented(dept)) return;
        int cur = PenetrationManager.Instance.infiltrationLevel;
        if (cur >= LevelCount) return;
        PenetrationManager.Instance.SetLevel(cur + 1);
        Refresh();
    }

    void OnDowngrade(int dept)
    {
        if (dept != 0 || !IsImplemented(dept)) return;
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
            // 渗透行的等级格从空开始，由 OpenRoutine 逐格点亮
            if (DepartmentCount > 0 && levelCells[0] != null)
            {
                for (int i = 0; i < LevelCount; i++)
                    levelCells[0][i].color = EmptyColor;
            }
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

        if (DepartmentCount > 0 && levelCells[0] != null)
        {
            int penLevel = GetLevel(0);
            for (int i = 0; i < penLevel && i < LevelCount; i++)
            {
                levelCells[0][i].color = FilledColor;
                yield return new WaitForSeconds(0.05f);
            }
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
        for (int d = 0; d < valueTexts.Length; d++)
        {
            int lvl = GetLevel(d);
            for (int i = 0; i < LevelCount; i++)
                levelCells[d][i].color = (i < lvl) ? FilledColor : EmptyColor;
            valueTexts[d].text = GetValueText(d);
        }
    }
}
