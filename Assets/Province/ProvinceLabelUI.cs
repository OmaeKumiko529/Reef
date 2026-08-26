using UnityEngine;

/// <summary>
/// 用 Unity 自带 OnGUI 在屏幕上绘制省份名称。
/// 不依赖 Canvas / RectTransform / EventSystem，坐标即屏幕像素，最稳。
/// </summary>
public class ProvinceLabelUI : MonoBehaviour
{
    string displayText = "";
    bool visible = false;

    // 要显示文字的世界坐标（省份重心），每帧投影到屏幕，随摄像机移动
    Vector3 worldPos;

    // 用于显示名称的字体（可显示中文，例如 Noto Serif SC）
    public Font font;

    GUIStyle textStyle;

    void EnsureStyles()
    {
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 30;
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;
        }

        // 每次绘制前同步字体：运行中修改 font 也能立即生效，未指定时回退到默认字体
        Font target = font != null ? font : GUI.skin.label.font;
        if (textStyle.font != target)
            textStyle.font = target;
    }

    /// <summary>
    /// 在世界坐标处显示文本（会投影到屏幕坐标）。
    /// </summary>
    public void Show(Vector3 worldPos, string text)
    {
        this.worldPos = worldPos;
        displayText = text;
        visible = true;
    }

    public void Hide()
    {
        visible = false;
    }

    void OnGUI()
    {
        if (!visible || string.IsNullOrEmpty(displayText)) return;
        EnsureStyles();

        // 每帧重新把世界坐标投影到屏幕，摄像机平移时文字会跟随省份重心移动
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        if (screen.z < 0f) return; // 重心转到相机背后时不绘制

        // GUI 坐标：左上角原点、y 向下。屏幕坐标：左下角原点、y 向上。做一次转换。
        float cx = screen.x;
        float cy = Screen.height - screen.y;

        float w = 260f;
        float h = 40f;
        float x = cx - w * 0.5f;
        float y = cy - h * 0.5f; // 文本块中心对齐重心投影点，无额外偏移

        // 只绘制一次文字，去掉偏移阴影，避免出现错位的重影
        GUI.Label(new Rect(x, y, w, h), displayText, textStyle);
    }
}