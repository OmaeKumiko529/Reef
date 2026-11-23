using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameClock : MonoBehaviour
{
    static GameClock _instance;

    public static GameClock Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameClock>();
                if (_instance == null)
                {
                    var go = new GameObject("GameClock");
                    _instance = go.AddComponent<GameClock>();
                }
            }
            return _instance;
        }
    }

    public static bool Paused { get; private set; }

    public static bool EventBlocked { get; private set; }

    public static event System.Action OnDayPassed;

    public static void PauseForEvent() { EventBlocked = true; }
    public static void ResumeFromEvent() { EventBlocked = false; }

    public int startYear = 1945;
    public int startMonth = 9;
    public int startDay = 2;
    public float daysPerSecond = 1f;

    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Day { get; private set; }

    Font font;
    Text dateText;
    Text pauseLabel;

    float accum;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        Year = startYear;
        Month = startMonth;
        Day = startDay;
    }

    public void Setup(Font font)
    {
        this.font = font;
        BuildUI();
        RefreshDate();
        RefreshPause();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!EventBlocked) TogglePause();
        }

        if (Paused || EventBlocked) return;

        accum += Time.deltaTime * daysPerSecond;
        while (accum >= 1f)
        {
            accum -= 1f;
            AdvanceOneDay();
            if (Paused || EventBlocked) break;
        }
    }

    void AdvanceOneDay()
    {
        Day++;
        if (Day > 31)
        {
            Day = 1;
            Month++;
        }
        if (Month > 12)
        {
            Month = 1;
            Year++;
        }
        RefreshDate();
        OnDayPassed?.Invoke();
    }

    public bool IsAtOrAfter(int y, int m, int d)
    {
        int cur = Year * 10000 + Month * 100 + Day;
        int target = y * 10000 + m * 100 + d;
        return cur >= target;
    }

    public void TogglePause()
    {
        Paused = !Paused;
        RefreshPause();
    }

    void RefreshDate()
    {
        if (dateText != null) dateText.text = Year + "年" + Month + "月" + Day + "日";
    }

    void RefreshPause()
    {
        // 契约 70_ §5.0：接线既有常量；用词由「开始」改「继续」
        if (pauseLabel != null) pauseLabel.text = Paused ? UICopy.Clock.Resume : UICopy.Clock.Pause;
    }

    void BuildUI()
    {
        // 层序：契约 70_ §10.0 把日期牌从默认 0 提到 Hud（与顶栏/底栏同层；同层内不依赖创建顺序）
        var canvas = UIFactory.CreateCanvas("GameClockCanvas", transform, UISorting.Hud);

        // 契约 70_ §7.1：日期牌 = Card（1px 单层框 + 投影，无标题栏），尺寸取 WidthXs 档
        var chrome = UIPanelChrome.Create(canvas.transform, "ClockPanel", ChromeKind.Card, null,
            new Vector2(UITheme.WidthXs, 110f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -20f), UISorting.Hud);
        var panelGo = chrome.Root.gameObject;

        var dateGo = new GameObject("Date", typeof(RectTransform), typeof(Text));
        dateGo.transform.SetParent(panelGo.transform, false);
        dateText = dateGo.GetComponent<Text>();
        if (font != null) dateText.font = font;
        dateText.fontSize = 30;
        dateText.fontStyle = FontStyle.Bold;
        dateText.alignment = TextAnchor.MiddleCenter;
        dateText.color = UITheme.InkPrimary;

        var drt = dateGo.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(1f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        // L-10：内边距与下方按钮的 20 对齐（原为 10），日期牌尺寸不动
        drt.offsetMin = new Vector2(20f, 55f);
        drt.offsetMax = new Vector2(-20f, 0f);

        var btnGo = new GameObject("PauseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(panelGo.transform, false);
        var btn = btnGo.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var btnImg = btnGo.GetComponent<Image>();
        btnImg.color = UITheme.PaperTop;

        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(20f, 10f);
        brt.offsetMax = new Vector2(-20f, 50f);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(btnGo.transform, false);
        pauseLabel = labelGo.GetComponent<Text>();
        if (font != null) pauseLabel.font = font;
        pauseLabel.fontSize = 22;
        pauseLabel.alignment = TextAnchor.MiddleCenter;
        pauseLabel.color = UITheme.InkPrimary;

        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(TogglePause);
        UIButtonState.Apply(btn, ButtonMode.Clock);   // 五态（契约 70_ §6.0）
    }
}
