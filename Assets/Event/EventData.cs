[System.Serializable]
public class ConditionData
{
    public string type;             // time_point / flag_compare
    public int year, month, day;    // time_point
    public string flag;             // flag_compare
    public string op;               // eq / ne / gt / ge / lt / le
    public float value;             // flag_compare
}

[System.Serializable]
public class EffectData
{
    public string type;             // add_influence / set_flag / reveal_causal_chain / add_ideology / add_interest_group / set_chain_summary / penetrate_block / set_ideology / set_country_name / set_leader
    public string blockId;          // add_influence / add_ideology / add_interest_group / penetrate_block
    public string flag;             // set_flag / add_ideology / add_interest_group
    public string chainId;          // reveal_causal_chain / set_chain_summary
    public string text;             // set_chain_summary / set_country_name
    public float value;             // add_influence / set_flag / add_ideology / add_interest_group
    public string ideologyId;       // set_ideology / set_leader
    public string name;             // set_leader
}

[System.Serializable]
public class EventOptionData
{
    public string text;
    public string hint;
    public EffectData[] effects;
}

[System.Serializable]
public class EventData
{
    public string id;
    public string title;
    public string story;
    public string description;
    public ConditionData[] trigger;
    public EventOptionData[] options;
}

[System.Serializable]
public class EventRoot
{
    public EventData[] events;
}
