[System.Serializable]
public class LeaderData
{
    public string name;         // 领导人姓名
    public string ideologyId;   // 领导人个人倾向
}

[System.Serializable]
public class CountryData
{
    public string id;           // 国家ID
    public string displayName;  // 国名
    public string ideologyId;   // 官方意识形态
    public LeaderData leader;   // 领导人
}
