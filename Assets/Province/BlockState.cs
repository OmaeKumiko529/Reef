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
}
