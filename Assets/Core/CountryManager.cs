using UnityEngine;

public class CountryManager : MonoBehaviour
{
    static CountryManager _instance;

    public static CountryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CountryManager>();
                if (_instance == null)
                {
                    var go = new GameObject("CountryManager");
                    _instance = go.AddComponent<CountryManager>();
                }
            }
            return _instance;
        }
    }

    public static event System.Action OnCountryChanged;

    public CountryData country;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Load();
    }

    void Load()
    {
        var ta = Resources.Load<TextAsset>("CountryData");
        if (ta == null)
        {
            Debug.LogWarning("未找到 Resources/CountryData.json，使用占位数据");
            country = new CountryData
            {
                id = "lutetia",
                displayName = "卢泰西亚国",
                ideologyId = "democracy",
                leader = new LeaderData { name = "占位领导人", ideologyId = "democracy" }
            };
            return;
        }

        country = JsonUtility.FromJson<CountryData>(ta.text);
        Debug.Log("加载国家 " + (country != null ? country.displayName : "(空)"));
    }

    public void SetName(string name)
    {
        if (country == null || string.IsNullOrEmpty(name)) return;
        country.displayName = name;
        Notify();
    }

    public void SetIdeology(string ideologyId)
    {
        if (country == null || string.IsNullOrEmpty(ideologyId)) return;
        country.ideologyId = ideologyId;
        Notify();
    }

    public void SetLeader(string name, string ideologyId)
    {
        if (country == null || string.IsNullOrEmpty(name)) return;
        country.leader = new LeaderData { name = name, ideologyId = ideologyId };
        Notify();
    }

    void Notify()
    {
        OnCountryChanged?.Invoke();
    }
}
