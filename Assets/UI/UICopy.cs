using System.Collections.Generic;

/// <summary>
/// 玩家可见文案的唯一来源（R3 冻结接口，见 Docs/VisualPass/00_方案与分工.md 2.3；
/// 第二轮 UI 成品化新增项见 Docs/VisualPass/70_UI成品化契约.md 5.0）。
/// 本文件只放常量与纯查表：不引用 Unity 运行时状态、不读写游戏数据、不产生副作用。
/// 规则：UI 代码里不再写玩家可见的字面量，一律从这里取；术语一律称「省份」。
/// </summary>
public static class UICopy
{
    public static class Common
    {
        /// <summary>未实装部门的按钮 / 状态用词（旧「未实装」已废弃）。</summary>
        public const string NotImplemented = "未开放";
        /// <summary>底栏未开放入口的角标。</summary>
        public const string InDevelopment  = "开发中";
        /// <summary>id 未命中的兜底，绝不把英文 id 漏给玩家（GameContent.Name() 用）。</summary>
        public const string Unknown        = "—";
        /// <summary>省份详情无数据。</summary>
        public const string EmptyProvince  = "暂无档案";
        /// <summary>情报日志为空（本轮无消费点，登记见 Docs/VisualPass/63_文案_UI成品化文案.md 1.11）。</summary>
        public const string EmptyJournal   = "暂无收录";
        /// <summary>②轮（70_ 5.0）：分区无条目时的占位，替代空行。消费方：省档「利益集团 / 政治思潮」两节。</summary>
        public const string NotRecorded    = "未见记载";
    }

    public static class TopBar
    {
        public const string Power  = "行政力 ";
        public const string Points = "情报点 ";
        /// <summary>②轮（70_ 5.0 正文 / R-04）：国名串 {0}=国名 {1}=意识形态；
        /// 栏宽降级（≥1200 三格 / ≥900 两格 / 否则只国名）由程序侧按阈值拼接。</summary>
        public const string CountryFormat = "{0} · {1}";
        /// <summary>R3b 补漏（70_ §5.0 常量表漏列）：顶栏三级降级的第三段，{0}=领导人名。
        /// 程序侧拼法：string.Format(CountryFormat, 国名, 意识形态) + " · " + string.Format(LeaderFormat, 名)。</summary>
        public const string LeaderFormat = "领导人 {0}";
        // 注：「正在研究：暂无（科技树开发中）」整格按策划备忘 3.3 删除，故此处不提供常量；
        // 若后续保留该格，请先通知文案补常量，不要在 UI 代码里写字面量。
    }

    public static class Clock
    {
        /// <summary>时间正在流逝时显示（点击后暂停）。</summary>
        public const string Pause  = "暂停";
        /// <summary>已暂停时显示（点击后继续）。原「开始」并入住这里；②轮裁决**不再新增** Clock.Start，
        /// Assets/Core/GameClock.cs:127 直接消费本常量（用词由「开始」改「继续」）。</summary>
        public const string Resume = "继续";
    }

    public static class Dept
    {
        /// <summary>底栏 5 入口与机构面板共用的全称，顺序与 AgencyNavUI.cs 的底栏一致。</summary>
        public static readonly string[] All = { "渗透处", "科技处", "内务处", "情报处", "机构" };

        /// <summary>是否已开放；程序据此灰显并挂 Common.InDevelopment 角标，禁止再写死数组长度。</summary>
        public static readonly bool[] Implemented = { true, false, false, false, false };

        /// <summary>底栏面板里每个入口的一句说明；未开放部门不做能力承诺。</summary>
        public static readonly string[] Desc =
        {
            "渗透处：执行渗透行动，扩大对省份的渗透范围。",
            "科技处：负责科技研发，" + Common.NotImplemented + "。",
            "内务处：负责内政督察，" + Common.NotImplemented + "。",
            "情报处：负责情报汇总，" + Common.NotImplemented + "。",
            "机构：查看各部门的投入等级与开放状态。"
        };

        /// <summary>②轮（70_ 5.0）：部门面板眉标。消费方：四个部门面板的标题栏上方小字。</summary>
        public const string Eyebrow = "机构";

        /// <summary>②轮（70_ 5.0）：部门面板档号角标，长度 = 4，按顺序对应 All[0..3]。
        /// ❗不要拿它的长度当部门计数（部门计数一律用 All.Length - 1）。</summary>
        public static readonly string[] FileNo =
        {
            "档号 渗-01", "档号 科-02", "档号 内-03", "档号 情-04"
        };
    }

    public static class Agency
    {
        public const string Title = "机构总览";
        /// <summary>机构面板渗透处数值行：{0}=等级 {1}=可渗透省数 {2}=每日加成 {3}=加成上限。</summary>
        public const string ValueFormat = "等级 {0} ｜ 覆盖 {1} 省 ｜ 日 +{2} ｜ 上限 +{3}";
        /// <summary>②轮（70_ 5.0）：副标题 {0}=在册部门数 {1}=已开放部门数。</summary>
        public const string CountFormat = "在册 {0} 处 · 已开放 {1} 处";
    }

    public static class Province
    {
        public const string Influence      = "影响力：";
        public const string InterestGroups = "利益集团：";
        public const string Ideologies     = "政治思潮：";
        public const string Products       = "主要产出物：";
        public const string Culture        = "主流文化：";
        /// <summary>该省已在渗透名单中时置顶的一行。</summary>
        public const string Penetrating    = "正在主动渗透";
        /// <summary>②轮（70_ 5.0）：详情面板眉标。消费方：详情面板标题栏上方小字。</summary>
        public const string Eyebrow        = "省情档案";
        /// <summary>②轮（70_ 5.0）：详情面板档号角注，{0}=ProvinceData.id（如 1008）。</summary>
        public const string FileNoFormat   = "编号 {0}";
    }

    public static class Journal
    {
        public const string Header = "情报日志";
        public const string Mist   = "因果迷雾";
        /// <summary>遮蔽态标题后缀（不要再在 UI 代码里拼「 · 迷雾」）。</summary>
        public const string MistSuffix = " · " + Mist;
        /// <summary>R4 补漏（70_ §15.2）选项 tooltip：揭示因果链（不点名，选择前不得泄露链名）。</summary>
        public const string ChainRevealHint = "揭示一条因果链";
        /// <summary>R4 补漏（70_ §15.2）选项 tooltip：推进因果链（同上，不点名）。</summary>
        public const string ChainSummaryHint = "推进一条因果链";
        /// <summary>②轮（70_ 5.0）：日志面板眉标。</summary>
        public const string Eyebrow = "卷宗";
        /// <summary>②轮（70_ 5.0）：日志计数，{0}=已收录条数。</summary>
        public const string CountFormat = "本卷收录 {0} 条";
        /// <summary>②轮（70_ 5.0）：卡片序号，{0}=序号（从 1 起，两位补零）。</summary>
        public const string CardNoFormat = "卷宗 {0:00}";
    }

    /// <summary>②轮（70_ 5.0）：事件卡「公文呈报」文案位。</summary>
    public static class Event
    {
        /// <summary>发文机关缺失时的眉标词。本轮事件 JSON 不提供 issuer 字段（契约 R-09），
        /// 故事件卡眉标固定用它；下一轮加了 issuer 再补 Event.IssuerFormat。</summary>
        public const string IssuerFallback = "呈报";
        /// <summary>事件卡日期：{0}=年 {1}=月 {2}=日（与 GameClock 的日期格式一致）。</summary>
        public const string DateFormat = "{0}年{1}月{2}日";
        /// <summary>选项区上方小标。</summary>
        public const string OptionLead = "批示";
    }

    public static class Penetration
    {
        /// <summary>渗透模式提示条：{0}=已选省数 {1}=可渗透上限；已达上限时必须可见反馈，不得静默 return。</summary>
        public const string Hint = "点击省份，加入或移出渗透名单（已选 {0}/{1}）";
        /// <summary>②轮（70_ 5.0）：达上限时**替换** Hint 的整句——
        /// 旧实现只把 Hint 染红加粗，玩家看不出"点不动"的原因。</summary>
        public const string HintFull = "渗透名额已满（{0}/{1}），点击省份可移出";
    }

    /// <summary>
    /// 纯标记（flag）的中文名。思潮 / 利益集团 / 文化 / 产出的名字不在这里重复登记，
    /// 一律交给 GameContent.Name()（唯一真源）；新增纯标记时才往这里加一行。
    /// </summary>
    static readonly Dictionary<string, string> FlagNames = new Dictionary<string, string>
    {
        { "revolution", "革命" }
    };

    /// <summary>
    /// 事件 tooltip 用的 flag / id 中文名纯查表：先查纯标记，再交给 GameContent.TryGetName()；
    /// 未命中一律返回 Common.Unknown，绝不返回英文 id。
    /// </summary>
    public static string FlagLabel(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return Common.Unknown;
        if (FlagNames.TryGetValue(flagId, out var flag)) return flag;
        return GameContent.TryGetName(flagId, out var name) ? name : Common.Unknown;
    }
}
