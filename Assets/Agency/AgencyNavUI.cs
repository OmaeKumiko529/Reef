using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class AgencyNavUI : MonoBehaviour
{
    static AgencyNavUI _instance;

    public static AgencyNavUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AgencyNavUI>();
                if (_instance == null)
                {
                    var go = new GameObject("AgencyNavUI");
                    _instance = go.AddComponent<AgencyNavUI>();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 入口数量：与 <see cref="UICopy.Dept.All"/> 的数组长度保持一致，但**不再写死**——
    /// 长度由文案表驱动（契约 2.3 禁止程序写死部门数量）。
    /// </summary>
    static int DeptCount => UICopy.Dept.All.Length;

    public Font font;
    public AudioClip uiClickClip;

    AudioSource audioSource;
    Canvas canvas;
    Image[] buttonBgs;
    Text[] buttonLabels;
    Text[] buttonNotes;
    UIButtonState[] buttonStates;   // 契约 70_ §6.0：五态（NavBar 模式，下沿 3px 朱砂选中条）

    /// <summary>部门浮层动画时长。L-07 删除死面板后暂无消费者，阶段 B 重建浮层时复用。</summary>
    public float fadeDuration = 0.2f;

    public void Setup(Font font, AudioClip clip)
    {
        this.font = font;
        this.uiClickClip = clip;
        EnsureAudio();
        EnsureUI();
    }

    void EnsureAudio()
    {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("AgencyNavCanvas", transform, UISorting.Hud);

        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.transform.SetParent(transform, false);
        }

        // 数组长度由文案表驱动（契约 2.3：禁止写死部门数量）
        buttonBgs = new Image[DeptCount];
        buttonLabels = new Text[DeptCount];
        buttonNotes = new Text[DeptCount];
        buttonStates = new UIButtonState[DeptCount];

        BuildNavBar(canvas.transform);
        // 契约 70_ §9.0 L-07：原 BuildPanels 的部门死面板已整体删除
        //（未开放部门恒不可点，浮层内容只是"开发中"承诺的重复）。
        // 未开放部门仍由 ShowPanel 拦截；已开放部门的浮层在阶段 B 用 UIPanelChrome 重建。
    }

    /// <summary>最后一个入口是「机构总览」聚合视图，本身不是部门：它承载各部门的等级与投入，恒可用。</summary>
    static bool IsOverview(int index) => index == DeptCount - 1;

    /// <summary>该部门是否已开放；文案表长度异常时按"未开放"处理（宁可不给入口，也不承诺不存在的系统）。</summary>
    static bool IsImplemented(int index)
    {
        var impl = UICopy.Dept.Implemented;
        return impl != null && index >= 0 && index < impl.Length && impl[index];
    }

    /// <summary>
    /// 该入口能不能点。部门入口看 <see cref="UICopy.Dept.Implemented"/>；
    /// 总览入口恒可用——它不承诺新系统，只是把已有部门的数据聚合展示，
    /// 而渗透等级唯一的提升入口就在这个面板里，禁用它会让玩家彻底无法升级渗透。
    /// </summary>
    static bool IsAvailable(int index) => IsOverview(index) || IsImplemented(index);

    void BuildNavBar(Transform parent)
    {
        // 契约 70_ §7.1：底栏 = Strip（纸底 + 单层 1px 框 + 投影）；布局组挂在 Content 上，
        // 装饰层是 Root 的兄弟节点、不参与排版，因此尺寸与 L-02 的 (0,0)-(1,0)/80 完全一致。
        var chrome = UIPanelChrome.CreateStretched(parent, "BottomNav", ChromeKind.Strip, null,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, UITheme.SafeBottom), Vector2.zero, UISorting.Hud);
        var barGo = chrome.Content.gameObject;

        var layout = barGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.spacing = UITheme.SpaceSm;
        layout.padding = new RectOffset(8, 8, 8, 8);

        for (int i = 0; i < DeptCount; i++)
        {
            int idx = i;
            string deptName = UICopy.Dept.All[i];

            var btnGo = new GameObject("Btn_" + deptName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(barGo.transform, false);

            var btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            buttonBgs[i] = btnGo.GetComponent<Image>();

            buttonLabels[i] = UIFactory.CreateStretchText("Label", btnGo.transform,
                UITheme.FontSubtitle, FontStyle.Normal, TextAnchor.MiddleCenter, font);
            buttonLabels[i].text = deptName;

            // 未开放部门：右下角「开发中」角标（契约 2.3 的 Common.InDevelopment）
            buttonNotes[i] = UIFactory.CreateText("Note", btnGo.transform,
                UITheme.FontCaption, FontStyle.Normal, TextAnchor.LowerRight, font);
            var nrt = buttonNotes[i].rectTransform;
            nrt.anchorMin = new Vector2(1f, 0f);
            nrt.anchorMax = new Vector2(1f, 0f);
            nrt.pivot = new Vector2(1f, 0f);
            nrt.sizeDelta = new Vector2(80f, 24f);
            nrt.anchoredPosition = new Vector2(-UITheme.SpaceSm, 6f);
            buttonNotes[i].text = UICopy.Common.InDevelopment;
            buttonNotes[i].color = UITheme.InkMuted;
            buttonNotes[i].gameObject.SetActive(!IsAvailable(i));

            // 契约 70_ §11.1 / 判据 P10「形态即承诺」：未开放入口 = 灰显 + 角标 + 朱砂斜章（三件套）
            if (!IsAvailable(i))
                UIPanelChrome.MakeSeal(btnGo.transform, UICopy.Common.NotImplemented,
                    UITheme.StampSize * 0.5f, new Vector2(28f, 0f));

            btn.onClick.AddListener(() => OnButtonClicked(idx));
            // 按下缩放由 UIButtonState 统一处理（0.98），不再叠加 AddPressScale，避免双重缩放

            // 契约 70_ §6.0：底栏入口显式用 NavBar 模式（选中条在下沿），并挂五态
            buttonStates[i] = UIButtonState.Apply(btn, ButtonMode.NavBar);
            ApplyDeptVisual(i, false);
        }
    }

    /// <summary>
    /// 一个入口的外观：选中态 / 未开放态 / 普通态。
    /// 未开放部门恒为灰（InkMuted）且不可交互，避免玩家点了没反应还以为卡了。
    /// </summary>
    void ApplyDeptVisual(int index, bool on)
    {
        bool open = IsAvailable(index);
        var btn = buttonBgs[index] != null ? buttonBgs[index].GetComponent<Button>() : null;
        if (btn != null) btn.interactable = open;

        // 颜色统一交给 UIButtonState 五态（停用态 = BtnDisabledBg / BtnDisabledText，等价原 Divider / InkMuted 的灰显意图）
        var state = buttonStates != null ? buttonStates[index] : null;
        if (state != null)
        {
            state.SetSelected(on && open);
            return;
        }

        // 兜底：五态组件缺失时退回直接染色
        if (!open)
        {
            buttonBgs[index].color = UITheme.Divider;
            buttonLabels[index].color = UITheme.InkMuted;
            return;
        }

        buttonBgs[index].color = on ? UITheme.SealRed : UITheme.PaperTop;
        buttonLabels[index].color = on ? Color.white : UITheme.InkPrimary;
    }

    public void ShowPanel(int index)
    {
        if (index < 0 || index >= DeptCount) return;
        // 未开放部门没有可展示的能力，不打开面板（按钮也已 interactable=false）
        if (!IsAvailable(index)) return;

        // 契约 70_ §9.0 L-07：部门浮层已删除，本轮此调用不产生可见内容。
        // 阶段 B 接入 UIPanelChrome 后在此重建部门浮层。
        // 注意 `Assets/Editor/EditorScenePreview.cs:214` 的 `ShowPanel(1)` 预览调用因此暂时无输出
        //（该 Editor 文件不在本任务写权限内，未改动）。
    }

    void UpdateButtons()
    {
        bool penActive = PenetrationManager.Instance.IsActive;
        bool agencyOpen = AgencyPanelUI.Instance.IsOpen;
        for (int i = 0; i < DeptCount; i++)
        {
            // 首尾两个入口分别由渗透模式 / 机构面板驱动；中间三个部门的浮层已随 L-07 删除，恒不点亮
            bool on = (i == 0) ? penActive : (i == DeptCount - 1) ? agencyOpen : false;
            ApplyDeptVisual(i, on);
        }
    }

    void AddPressScale(GameObject go)
    {
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => go.transform.localScale = Vector3.one * 0.96f);
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => go.transform.localScale = Vector3.one);
        trigger.triggers.Add(down);
        trigger.triggers.Add(up);
    }

    void OnButtonClicked(int index)
    {
        // 未开放部门：按钮已 interactable=false，这里是双保险
        if (!IsAvailable(index)) return;

        if (audioSource != null && uiClickClip != null) audioSource.PlayOneShot(uiClickClip);
        BlockManager.Instance.CloseBlock();

        if (index == 0)
        {
            AgencyPanelUI.Instance.SetOpen(false);
            PenetrationManager.Instance.ToggleActive();
            UpdateButtons();
            return;
        }

        if (index == DeptCount - 1)
        {
            PenetrationManager.Instance.SetActive(false);
            AgencyPanelUI.Instance.Toggle();
            UpdateButtons();
            return;
        }

        AgencyPanelUI.Instance.SetOpen(false);
        PenetrationManager.Instance.SetActive(false);
        ShowPanel(index);
    }
}
