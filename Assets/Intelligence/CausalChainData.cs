[System.Serializable]
public class CausalChainData
{
    public string id;
    public string title;
    public string text;    // 揭示后的正文（可为空，由 set_chain_summary 动态更新）
    public bool locked;    // 初始是否处于因果迷雾（打码）
}

[System.Serializable]
public class CausalChainRoot
{
    public CausalChainData[] chains;
}
