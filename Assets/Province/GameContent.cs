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

    /// <summary>
    /// id → 玩家可见名称（利益集团 / 思潮 / 文化 / 产出）。
    /// ❗未命中一律返回 <see cref="UICopy.Common.Unknown"/>：绝不能把英文 id 漏给玩家
    /// （契约 2.3；`UICopy.FlagLabel` 依赖"返回值 == 入参 表示未命中"的旧约定已随之更新）。
    /// </summary>
    public static string Name(string id)
    {
        if (string.IsNullOrEmpty(id)) return UICopy.Common.Unknown;

        if (InterestGroupNames.TryGetValue(id, out var a)) return a;
        if (IdeologyNames.TryGetValue(id, out var b)) return b;
        if (CultureNames.TryGetValue(id, out var c)) return c;
        if (ProductNames.TryGetValue(id, out var d)) return d;
        return UICopy.Common.Unknown;
    }

    /// <summary>该 id 是否有登记的名称（用于需要区分"未命中"的调用方，例如 UICopy.FlagLabel）。</summary>
    public static bool TryGetName(string id, out string name)
    {
        if (!string.IsNullOrEmpty(id) &&
            (InterestGroupNames.TryGetValue(id, out name) ||
             IdeologyNames.TryGetValue(id, out name) ||
             CultureNames.TryGetValue(id, out name) ||
             ProductNames.TryGetValue(id, out name)))
            return true;

        name = UICopy.Common.Unknown;
        return false;
    }
}
