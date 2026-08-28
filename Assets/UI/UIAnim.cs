using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class UIAnim
{
    public static IEnumerator Fade(CanvasGroup group, float to, float duration)
    {
        if (group == null) yield break;
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
