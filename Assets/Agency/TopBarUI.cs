using UnityEngine;
using UnityEngine.UI;

public class TopBarUI : MonoBehaviour
{
    static TopBarUI _instance;

    public static TopBarUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TopBarUI>();
                if (_instance == null)
                {
                    var go = new GameObject("TopBarUI");
                    _instance = go.AddComponent<TopBarUI>();
                }
            }
            return _instance;
        }
    }

    public int administrativePower = 200;   // 内务处一级初始
    public int intelligencePoints = 50;     // 情报处一级初始

    public Font font;

    Canvas canvas;
    Text powerText;
    Text pointsText;
    Text techText;
    Coroutine powerRoutine;
    Coroutine pointsRoutine;

    public void Setup(Font font)
    {
        this.font = font;
        EnsureUI();
        Refresh();
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("TopBarCanvas", transform);

        var panelGo = new GameObject("TopBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        panelGo.transform.SetParent(canvas.transform, false);
        var img = panelGo.GetComponent<Image>();
        img.color = UITheme.PaperBg;

        var rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(720f, 50f);
        rt.anchoredPosition = new Vector2(0f, -20f);

        var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 0, 0);
        layout.spacing = 32f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        powerText = CreateLabel("Power", panelGo.transform);
        pointsText = CreateLabel("Points", panelGo.transform);
        techText = CreateLabel("Tech", panelGo.transform);
        techText.color = UITheme.InkMuted;
    }

    Text CreateLabel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 22;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = UITheme.InkPrimary;
        return t;
    }

    public void Refresh()
    {
        if (powerText == null) return;
        powerText.text = "行政力 " + administrativePower;
        pointsText.text = "情报点 " + intelligencePoints;
        techText.text = "正在研究：暂无（科技树开发中）";
    }

    public void AddAdministrativePower(int amount)
    {
        int from = administrativePower;
        administrativePower += amount;
        if (powerRoutine != null) StopCoroutine(powerRoutine);
        powerRoutine = StartCoroutine(UIAnim.CountUp(powerText, "行政力 ", "", from, administrativePower, UITheme.CountUpNormal));
    }

    public void AddIntelligencePoints(int amount)
    {
        int from = intelligencePoints;
        intelligencePoints += amount;
        if (pointsRoutine != null) StopCoroutine(pointsRoutine);
        pointsRoutine = StartCoroutine(UIAnim.CountUp(pointsText, "情报点 ", "", from, intelligencePoints, UITheme.CountUpNormal));
    }
}
