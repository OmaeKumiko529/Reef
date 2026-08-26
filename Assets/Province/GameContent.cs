using System.Collections.Generic;

public static class GameContent
{
    public static readonly string[] InterestGroupIds =
        { "knowledge", "industry", "politics", "military", "labor" };

    public static readonly string[] IdeologyIds =
        { "democracy", "socialism", "fascism" };

    public const string DefaultCultureId = "culture_lutetian";
    public const string DefaultTradeRoute = "route_capital_port";

    public static readonly string[] DefaultProducts = { "grain", "coal", "steel" };

    static readonly Dictionary<string, string> InterestGroupNames = new Dictionary<string, string>
    {
        { "knowledge", "知识界" },
        { "industry", "工商界" },
        { "politics", "政治界" },
        { "military", "军队" },
        { "labor", "劳工界" }
    };

    static readonly Dictionary<string, string> IdeologyNames = new Dictionary<string, string>
    {
        { "democracy", "民主主义" },
        { "socialism", "社会主义" },
        { "fascism", "法西斯主义" }
    };

    static readonly Dictionary<string, string> CultureNames = new Dictionary<string, string>
    {
        { "culture_lutetian", "卢泰西亚" }
    };

    static readonly Dictionary<string, string> ProductNames = new Dictionary<string, string>
    {
        { "grain", "粮食" },
        { "coal", "煤" },
        { "steel", "钢" }
    };

    public static string Name(string id)
    {
        if (InterestGroupNames.TryGetValue(id, out var a)) return a;
        if (IdeologyNames.TryGetValue(id, out var b)) return b;
        if (CultureNames.TryGetValue(id, out var c)) return c;
        if (ProductNames.TryGetValue(id, out var d)) return d;
        return id;
    }
}
