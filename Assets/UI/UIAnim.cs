using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class UIAnim
{
    /// <summary>
    /// 动画时长下限。原实现直接 `t/duration`：duration ≤ 0 时得到 NaN（`while (t &lt; 0)` 一次都不进，
    /// 但倒数第一行会把 NaN 写进 alpha/scale/position 且再也不会被修正）。
    /// 这里统一用"近似瞬时"替代 0，既保住收尾赋值又不产生 NaN。
    /// </summary>
    static float SafeDuration(float duration) => duration > 0.0001f ? duration : 0.0001f;

    public static IEnumerator Fade(CanvasGroup group, float to, float duration)
    {
        if (group == null) yield break;
        duration = SafeDuration(duration);
        float from = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, UITheme.EaseOutCubic(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    public static IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        if (rt == null) yield break;
        duration = SafeDuration(duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, to, UITheme.EaseOutCubic(t / duration));
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (target == null) yield break;
        duration = SafeDuration(duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(from, to, UITheme.EaseOutCubic(t / duration));
            yield return null;
        }
        target.localScale = to;
    }

    public static IEnumerator Stamp(Transform target, float duration)
    {
        if (target == null) yield break;
        duration = SafeDuration(duration);
        const float overshoot = 1.08f;
        target.localScale = Vector3.one * overshoot;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = UITheme.EaseOutBack(t / duration);
            target.localScale = Vector3.one * Mathf.Lerp(overshoot, 1f, k);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    public static IEnumerator Typewriter(Text text, string full, float perChar)
    {
        if (text == null) yield break;
        if (string.IsNullOrEmpty(full)) { text.text = full; yield break; }

        // perChar ≤ 0 会让 Mathf.FloorToInt(timer / perChar) 恒为极大/NaN，逐字循环永远走不完
        perChar = Mathf.Max(perChar, 0.0001f);

        text.text = "";
        float timer = 0f;
        int visible = 0;
        while (visible < full.Length)
        {
            timer += Time.deltaTime;
            int target = Mathf.Min(full.Length, Mathf.FloorToInt(timer / perChar));
            if (target > visible)
            {
                visible = target;
                text.text = full.Substring(0, visible);
            }
            yield return null;
        }
        text.text = full;
    }

    public static IEnumerator CountUp(Text text, string prefix, string suffix, int from, int to, float duration)
    {
        if (text == null) yield break;
        duration = SafeDuration(duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, UITheme.EaseOutCubic(t / duration)));
            text.text = prefix + v + suffix;
            yield return null;
        }
        text.text = prefix + to + suffix;
    }
}
