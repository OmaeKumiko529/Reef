/// <summary>
/// UI 层序常量（画布 sortingOrder）。
/// 背景：项目里 9 处 Canvas 原先全部用默认 sortingOrder=0，层序完全由创建顺序决定——
/// 谁先被 Setup 谁在下，加一个新面板就会把已有层级关系打乱，因此这里显式登记层。
/// 数值留出间隔，便于后续插入（例如面板内的浮层）。
/// </summary>
public static class UISorting
{
    /// <summary>
    /// 默认值，仅供 <see cref="UIFactory.CreateCanvas"/> 的缺省参数使用。
    /// ❗第二轮 UI 成品化（契约 70_ §10.0）起，**每一个画布都必须显式指定层**，
    /// 不得再依赖这个 0——否则层序又会被创建顺序决定（判据 P5 / U-20）。
    /// </summary>
    public const int Default = 0;

    /// <summary>常驻 HUD：顶栏、底栏、日期牌（日期牌原为默认 0，契约 70_ §10.0 提为 Hud）。</summary>
    public const int Hud = 100;

    /// <summary>省名标签：压在地图上，但不能盖住任何面板与提示。</summary>
    public const int Label = 200;

    /// <summary>渗透等模式的提示条。</summary>
    public const int Hint = 300;

    /// <summary>省详情面板、机构总览等大面板。</summary>
    public const int Panel = 400;

    /// <summary>模态：情报日志（因果链）。</summary>
    public const int Modal = 500;

    /// <summary>最上层：全屏事件演出（含遮罩），必须压住一切——原实现写死 100，会被顶栏压住。</summary>
    public const int Event = 900;
}
