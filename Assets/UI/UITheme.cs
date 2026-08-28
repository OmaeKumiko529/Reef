using UnityEngine;

public static class UITheme
{
    // 档案纸张浅色调
    public static readonly Color PaperBg     = new Color(0.906f, 0.871f, 0.784f, 1f);   // #E7DEC8 米灰旧纸
    public static readonly Color PaperTop    = new Color(0.941f, 0.914f, 0.839f, 1f);   // #F0E9D6 更浅纸
    public static readonly Color InkPrimary  = new Color(0.165f, 0.137f, 0.094f, 1f);   // #2A2318 深墨
    public static readonly Color InkSecondary= new Color(0.353f, 0.318f, 0.271f, 1f);   // #5A5145 灰棕
    public static readonly Color InkMuted    = new Color(0.541f, 0.502f, 0.447f, 1f);   // #8A8072 浅棕
    public static readonly Color SealRed     = new Color(0.627f, 0.188f, 0.094f, 1f);   // #A03018 朱砂印章红
    public static readonly Color AccentGold  = new Color(0.690f, 0.541f, 0.243f, 1f);   // #B08A3E 黄铜金
    public static readonly Color Divider     = new Color(0.788f, 0.749f, 0.651f, 1f);   // #C9BFA6 旧墨线
    public static readonly Color Redacted    = new Color(0.227f, 0.204f, 0.173f, 1f);   // #3A342C 迷雾遮蔽

    // 覆盖在彩色地图上的遮罩（保持暗色以突出纸面卡片）
    public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.55f);

    // 面板实底透明度（叠在地图上）
    public const float PanelAlpha = 0.97f;

    // 时长
    public const float FadeFast = 0.15f;
    public const float FadeNormal = 0.25f;
    public const float SlideNormal = 0.3f;
    public const float StampNormal = 0.28f;
    public const float TypeCharDelay = 0.02f;
    public const float CountUpNormal = 0.4f;

    // 缓动
    public static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    public static float EaseInOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
