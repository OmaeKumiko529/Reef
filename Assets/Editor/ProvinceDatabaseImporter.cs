using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProvinceDatabaseImporter
{
    const string JsonPath = "Assets/Province/ProvinceData.json";
    const string DbPath = "Assets/Province/ProvinceDatabase.asset";

    [MenuItem("暗礁/从 JSON 导入省份数据")]
    public static void Import()
    {
        if (!File.Exists(JsonPath))
        {
            Debug.LogError("JSON 不存在: " + JsonPath);
            return;
        }

        var db = AssetDatabase.LoadAssetAtPath<ProvinceDatabase>(DbPath);
        if (db == null)
        {
            Debug.LogError("数据库不存在: " + DbPath);
            return;
        }

        var root = JsonUtility.FromJson<ProvinceJsonRoot>(File.ReadAllText(JsonPath));
        if (root == null || root.provinces == null)
        {
            Debug.LogError("JSON 解析失败");
            return;
        }

        db.provinces = new List<ProvinceData>();
        foreach (var j in root.provinces)
        {
            var p = new ProvinceData();
            p.id = j.id;
            p.displayName = j.displayName;
            p.mapColor = HexToColor(j.mapColor);
            p.cultureId = j.cultureId;
            p.core = j.core;
            p.products = j.products;
            p.initialIGroups = j.initialIGroups != null ? new List<WeightEntry>(j.initialIGroups) : new List<WeightEntry>();
            p.initialIdeologies = j.initialIdeologies != null ? new List<WeightEntry>(j.initialIdeologies) : new List<WeightEntry>();
            db.provinces.Add(p);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("已从 JSON 导入 " + root.provinces.Length + " 个省份");
    }

    static Color32 HexToColor(string hex)
    {
        hex = (hex ?? "").Trim().TrimStart('#');
        if (hex.Length < 6) return new Color32(0, 0, 0, 255);

        byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        return new Color32(r, g, b, 255);
    }

    [System.Serializable]
    class ProvinceJsonRoot
    {
        public ProvinceJsonData[] provinces;
    }

    [System.Serializable]
    class ProvinceJsonData
    {
        public string id;
        public string displayName;
        public string mapColor;
        public string cultureId;
        public bool core;
        public string[] products;
        public WeightEntry[] initialIGroups;
        public WeightEntry[] initialIdeologies;
    }
}
