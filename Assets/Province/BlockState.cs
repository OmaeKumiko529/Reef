using System.Collections.Generic;

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

        s.interestGroups = new Dictionary<string, float>();
        foreach (var id in GameContent.InterestGroupIds)
            s.interestGroups[id] = 1f / GameContent.InterestGroupIds.Length;

        s.ideologies = new Dictionary<string, float>();
        foreach (var id in GameContent.IdeologyIds)
            s.ideologies[id] = 1f / GameContent.IdeologyIds.Length;

        s.cultureId = string.IsNullOrEmpty(d.cultureId) ? GameContent.DefaultCultureId : d.cultureId;
        s.products = (d.products != null && d.products.Length > 0) ? d.products : GameContent.DefaultProducts;
        s.tradeRoutes = new List<string> { GameContent.DefaultTradeRoute };

        return s;
    }
}
