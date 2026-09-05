using System.Collections.Generic;
using UnityEngine;

public class BlockState
{
    public string provinceId;
    public float baseInfluence;
    public float penetrationBonus;
    public float eventInfluence;
    public Dictionary<string, float> interestGroups;
    public Dictionary<string, float> ideologies;
    public string cultureId;
    public string[] products;
    public List<string> tradeRoutes;

    public float influence => baseInfluence + penetrationBonus + eventInfluence;

    public static BlockState CreatePlaceholder(ProvinceData d)
    {
        var s = new BlockState();
        s.provinceId = d.id;
        s.baseInfluence = d.core ? 10f : 0f;
        s.penetrationBonus = 0f;
        s.eventInfluence = 0f;

        s.interestGroups = BuildWeights(d.initialIGroups, GameContent.InterestGroupIds);
        s.ideologies = BuildWeights(d.initialIdeologies, GameContent.IdeologyIds);

        s.cultureId = string.IsNullOrEmpty(d.cultureId) ? GameContent.DefaultCultureId : d.cultureId;
        s.products = (d.products != null && d.products.Length > 0) ? d.products : GameContent.DefaultProducts;
        s.tradeRoutes = new List<string> { GameContent.DefaultTradeRoute };

        return s;
    }

    /// <summary>
    /// 用开局配置（WeightEntry 列表）构建占比字典，权重归一化到总和为 1；
    /// 未配置或权重全为 0 时回退为按给定 ID 平均分配（总纲占位实现）。
    /// </summary>
    static Dictionary<string, float> BuildWeights(List<WeightEntry> entries, string[] fallbackIds)
    {
        var result = new Dictionary<string, float>();
        float sum = 0f;
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                float w = Mathf.Max(0f, e.weight);
                result[e.key] = w;
                sum += w;
            }
        }

        if (sum > 0f)
        {
            var keys = new List<string>(result.Keys);
            foreach (var k in keys) result[k] /= sum;
            return result;
        }

        result.Clear();
        foreach (var id in fallbackIds)
            result[id] = 1f / fallbackIds.Length;
        return result;
    }

    /// <summary>
    /// 零和比例转移：把 targetKey 的份额移动 delta，其余分量按当前占比等比例补偿，
    /// 保持字典总和恒为 1（即各占比之和恒为 100%）。
    /// </summary>
    public static void ShiftShare(Dictionary<string, float> shares, string targetKey, float delta)
    {
        if (shares == null || shares.Count == 0 || !shares.ContainsKey(targetKey)) return;

        float target = Mathf.Clamp01(shares[targetKey]);
        float newTarget = Mathf.Clamp01(target + delta);

        var others = new List<string>();
        float othersSum = 0f;
        foreach (var kv in shares)
        {
            if (kv.Key == targetKey) continue;
            others.Add(kv.Key);
            othersSum += kv.Value;
        }

        if (others.Count == 0)
        {
            shares[targetKey] = 1f;
            return;
        }

        if (othersSum <= 1e-6f)
        {
            float each = (1f - newTarget) / others.Count;
            foreach (var k in others) shares[k] = each;
        }
        else
        {
            float scale = (1f - newTarget) / othersSum;
            foreach (var k in others) shares[k] *= scale;
        }

        shares[targetKey] = newTarget;
    }
}
