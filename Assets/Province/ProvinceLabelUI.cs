using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 省名标签：UGUI 实现，跟随相机投影到屏幕，只显示当前选中/悬停的省份名。
/// 契约（不要改）：<see cref="Show(Vector3,string)"/> 与 <see cref="Hide()"/>。
/// 旧实现走 OnGUI（每帧 Layout + Repaint 两趟 IMGUI + 每帧 Camera.main 查找），
/// 现在改为常驻 Canvas，每帧只在位置真正变化时写 RectTransform。
/// </summary>
public class ProvinceLabelUI : MonoBehaviour
{
    /// <summary>标签块尺寸（画布参考像素，1920x1080 基准）。</summary>
    public static readonly Vector2 LabelSize = new Vector2(300f, 46f);

    /// <summary>文字风格：深墨压纸底，避免彩色地图上白字糊掉（取自 UITheme 设计 token）。</summary>
    public static readonly Color BoxColor = new Color(UITheme.PaperBg.r, UITheme.PaperBg.g, UITheme.PaperBg.b, 0.94f);
    public static readonly Color TextColor = UITheme.InkPrimary;
    public static readonly Color OutlineColor = new Color(UITheme.PaperTop.r, UITheme.PaperTop.g, UITheme.PaperTop.b, 0.75f);

    public const int LabelFontSize = UITheme.FontSubtitle;

    /// <summary>用于显示名称的字体（可显示中文，例如 Noto Serif SC）。</summary>
    public Font font;

    // 要显示文字的世界坐标（省份重心），每帧投影到屏幕，随摄像机移动
    Vector3 worldPos;
    string displayText = "";
    bool visible;

    Canvas canvas;
    RectTransform boxRect;
    Text label;
    Camera cam;

    // 上一帧写入的位置，用于跳过无意义的 RectTransform 写入（避免触发布局重建）
    Vector2 lastLocal;
    bool hasLastLocal;

    void Awake()
    {
        cam = Camera.main;
    }

    void EnsureUI()
    {
        if (canvas != null) return;

        canvas = UIFactory.CreateCanvas("ProvinceLabelCanvas", transform, UISorting.Label);

        // 纸底：浅色实底保证压在深色省份上也读得清；raycastTarget=false 是硬要求——
        // 标签跟着光标走，只要它挡射线，指针下的地图就再也点不动
        var box = UIFactory.CreatePanel("LabelBox", canvas.transform, BoxColor);
        box.raycastTarget = false;
        boxRect = box.rectTransform;

        // 先固定成"以画布中心为锚点的定尺寸块"，再改 sizeDelta；
        // 顺序反了会得到"满屏尺寸 + 300x46 偏移"的错误尺寸
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = LabelSize;

        // 文字贴满纸底；CreateText 已置 raycastTarget=false，这里再保一次险
        label = UIFactory.CreateStretchText("Label", boxRect, LabelFontSize, FontStyle.Bold,
            TextAnchor.MiddleCenter, font);
        label.color = TextColor;
        label.raycastTarget = false;

        // 浅色描边保证压在深色省份上也能读清（旧实现为避免重影干脆去掉了阴影，
        // 但代价是文字落在同色地块上完全不可读）
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        label.text = "";
        SetVisible(false);
    }

    /// <summary>
    /// 在世界坐标处显示文本（会投影到屏幕坐标）。
    /// </summary>
    public void Show(Vector3 worldPos, string text)
    {
        this.worldPos = worldPos;
        displayText = text ?? "";
        visible = true;
        hasLastLocal = false; // 强制下一帧重算位置
    }

    public void Hide()
    {
        visible = false;
    }

    public void SetFont(Font f)
    {
        font = f;
        if (label != null && f != null) label.font = f;
    }

    void LateUpdate()
    {
        if (!visible || string.IsNullOrEmpty(displayText))
        {
            SetVisible(false);
            return;
        }

        EnsureUI();
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        if (screen.z <= 0f)
        {
            SetVisible(false); // 重心转到相机背后
            return;
        }

        // 屏幕边缘裁剪：投影点太靠边时隐藏，避免半个标签贴在屏幕上
        float halfW = LabelSize.x * 0.5f;
        float halfH = LabelSize.y * 0.5f;
        if (screen.x < halfW || screen.x > Screen.width - halfW ||
            screen.y < halfH || screen.y > Screen.height - halfH)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        label.text = displayText;

        // Overlay 画布没有相机，必须传 null
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, new Vector2(screen.x, screen.y), null, out Vector2 local))
            return;

        if (!hasLastLocal || (local - lastLocal).sqrMagnitude > 0.25f)
        {
            boxRect.anchoredPosition = local;
            lastLocal = local;
            hasLastLocal = true;
        }
    }

    void SetVisible(bool value)
    {
        // 只开关标签块本身，不动 Canvas：Canvas 反复启停会重建整个批处理
        if (boxRect == null) return;
        if (boxRect.gameObject.activeSelf != value)
            boxRect.gameObject.SetActive(value);
    }
}
