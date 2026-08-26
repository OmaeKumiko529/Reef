using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ProvinceData
{
    public string id; //程序ID
    public string displayName; //显示名称
    public Color32 mapColor; //RGB值

    public string cultureId; //主流文化ID
    public string[] products; //主要产出物（商品ID）
    public bool core = true; //是否核心区块
    public List<WeightEntry> initialIGroups; //开局利益集团占比
    public List<WeightEntry> initialIdeologies; //开局政治思潮占比
}