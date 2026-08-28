[System.Serializable]
public class CausalSegment
{
    public string stage;   // background / trigger / ferment / outcome / impact
    public string name;    // 背景 / 导火索 / 发酵 / 结局 / 影响
    public string hint;    // 根本原因 / 直接原因 / 过程 / 结果 / 诱因
    public string text;    // 解锁后的详细文本（可选）
    public string summary; // 侧栏显示的简短阶段描述
    public bool locked;    // 初始是否为因果迷雾
}

[System.Serializable]
public class CausalChainData
{
    public string id;
    public string title;
    public CausalSegment[] segments;
}

[System.Serializable]
public class CausalChainRoot
{
    public CausalChainData[] chains;
}
