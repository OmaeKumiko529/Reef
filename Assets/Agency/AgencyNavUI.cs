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

    static readonly string[] DeptNames = { "渗透", "科技", "内务", "情报", "机构" };
    static readonly string[] DeptDescriptions =
    {
        "渗透处：执行渗透行动，提高对非核心区块的渗透力。",
        "科技处：负责科技研发，影响我方与敌方的破译难度。",
        "内务处：督察政府，提高行政效率。",
        "情报处：处理情报并汇总报告，产出情报点。",
        "机构：查看各部门投入等级。"
    };

    public Font font;
    public AudioClip uiClickClip;

    AudioSource audioSource;
    Canvas canvas;
    GameObject[] panels = new GameObject[5];
    CanvasGroup[] panelGroups = new CanvasGroup[5];
    Image[] buttonBgs = new Image[5];
    Text[] buttonLabels = new Text[5];
    Coroutine animRoutine;
    int currentIndex = -1;

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

        canvas = UIFactory.CreateCanvas("AgencyNavCanvas", transform);

        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.transform.SetParent(transform, false);
        }

        BuildNavBar(canvas.transform);
        BuildPanels(canvas.transform);
    }

    void BuildNavBar(Transform parent)
    {
        var barGo = new GameObject("BottomNav", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(parent, false);

        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0f);
        barRt.anchorMax = new Vector2(1f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.sizeDelta = new Vector2(0f, 80f);
        barRt.anchoredPosition = Vector2.zero;

        var barImg = barGo.GetComponent<Image>();
        barImg.color = UITheme.PaperBg;

        var layout = barGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);

        for (int i = 0; i < DeptNames.Length; i++)
        {
            int idx = i;
            var btnGo = new GameObject("Btn_" + DeptNames[i], typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(barGo.transform, false);

            var btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var bg = btnGo.GetComponent<Image>();
            bg.color = UITheme.PaperTop;
            buttonBgs[i] = bg;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var t = textGo.GetComponent<Text>();
            if (font != null) t.font = font;
            t.text = DeptNames[i];
            t.fontSize = 26;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = UITheme.InkPrimary;
            buttonLabels[i] = t;

            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(() => OnButtonClicked(idx));
            AddPressScale(btnGo);
        }
    }

    void BuildPanels(Transform parent)
    {
        for (int i = 0; i < DeptNames.Length; i++)
        {
            var panelGo = new GameObject("Panel_" + DeptNames[i], typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelGo.transform.SetParent(parent, false);
            panelGroups[i] = panelGo.GetComponent<CanvasGroup>();

            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 420f);
            rt.anchoredPosition = new Vector2(-40f, 40f);

            var img = panelGo.GetComponent<Image>();
            img.color = UITheme.PaperBg;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var title = titleGo.GetComponent<Text>();
            if (font != null) title.font = font;
            title.text = DeptNames[i];
            title.fontSize = 34;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = UITheme.InkPrimary;

            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(30f, -70f);
            trt.offsetMax = new Vector2(-30f, 0f);

            var descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(panelGo.transform, false);
            var desc = descGo.GetComponent<Text>();
            if (font != null) desc.font = font;
            desc.text = DeptDescriptions[i];
            desc.fontSize = 24;
            desc.alignment = TextAnchor.UpperLeft;
            desc.color = UITheme.InkSecondary;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;

            var drt = descGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.offsetMin = new Vector2(30f, 30f);
            drt.offsetMax = new Vector2(-30f, -90f);

            panelGo.SetActive(false);
            panels[i] = panelGo;
        }
    }

    public void ShowPanel(int index)
    {
        if (index < 0 || index >= DeptNames.Length) return;

        if (currentIndex == -1)
        {
            currentIndex = index;
            PlayOpen(index);
        }
        else if (currentIndex == index)
        {
            PlayClose(index);
            currentIndex = -1;
        }
        else
        {
            int old = currentIndex;
            currentIndex = index;
            PlaySwitch(old, index);
        }

        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool penActive = PenetrationManager.Instance.IsActive;
        bool agencyOpen = AgencyPanelUI.Instance.IsOpen;
        for (int i = 0; i < DeptNames.Length; i++)
        {
            bool on = (i == 0) ? penActive : (i == 4) ? agencyOpen : (i == currentIndex);
            buttonBgs[i].color = on ? UITheme.SealRed : UITheme.PaperTop;
            buttonLabels[i].color = on ? Color.white : UITheme.InkPrimary;
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
        if (audioSource != null && uiClickClip != null) audioSource.PlayOneShot(uiClickClip);
        BlockManager.Instance.CloseBlock();

        if (index == 0)
        {
            AgencyPanelUI.Instance.SetOpen(false);
            PenetrationManager.Instance.ToggleActive();
            if (PenetrationManager.Instance.IsActive)
                CloseAllPanelsImmediate();
            UpdateButtons();
            return;
        }

        if (index == 4)
        {
            PenetrationManager.Instance.SetActive(false);
            CloseAllPanelsImmediate();
            AgencyPanelUI.Instance.Toggle();
            UpdateButtons();
            return;
        }

        AgencyPanelUI.Instance.SetOpen(false);
        PenetrationManager.Instance.SetActive(false);
        ShowPanel(index);
    }

    void CloseAllPanelsImmediate()
    {
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
        for (int i = 0; i < DeptNames.Length; i++)
            panels[i].SetActive(false);
        currentIndex = -1;
    }

    void PlayOpen(int index)
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(OpenRoutine(index));
    }

    void PlayClose(int index)
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(CloseRoutine(index));
    }

    void PlaySwitch(int oldIndex, int newIndex)
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(SwitchRoutine(oldIndex, newIndex));
    }

    System.Collections.IEnumerator OpenRoutine(int index)
    {
        yield return OpenAnim(index);
        animRoutine = null;
    }

    System.Collections.IEnumerator CloseRoutine(int index)
    {
        yield return UIAnim.Fade(panelGroups[index], 0f, fadeDuration);
        panels[index].SetActive(false);
        animRoutine = null;
    }

    System.Collections.IEnumerator SwitchRoutine(int oldIndex, int newIndex)
    {
        yield return UIAnim.Fade(panelGroups[oldIndex], 0f, fadeDuration);
        panels[oldIndex].SetActive(false);
        yield return OpenAnim(newIndex);
        animRoutine = null;
    }

    System.Collections.IEnumerator OpenAnim(int index)
    {
        panels[index].SetActive(true);
        panelGroups[index].alpha = 0f;
        panels[index].transform.localScale = Vector3.one * 0.95f;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = UITheme.EaseOutCubic(t / fadeDuration);
            panelGroups[index].alpha = Mathf.Lerp(0f, 1f, k);
            panels[index].transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
            yield return null;
        }
        panelGroups[index].alpha = 1f;
        panels[index].transform.localScale = Vector3.one;
    }
}
