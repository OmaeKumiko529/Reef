using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ProvinceDatabase", menuName = "暗礁/省份数据库")]
public class ProvinceDatabase : ScriptableObject
{
    public List<ProvinceData> provinces = new List<ProvinceData>();

    //运行时用的查找表，相当于Map<"r,g,b", Province>
    Dictionary<Color32, ProvinceData> colorMap;

    public void BuildLookup()
    {
        colorMap = new Dictionary<Color32, ProvinceData>();
        foreach (var p in provinces)
        {
            if (!colorMap.ContainsKey(p.mapColor))
                colorMap.Add(p.mapColor, p);
            else
                Debug.LogWarning($"颜色代码{p.mapColor} | 省份{p.id}");
        }
    }

    public bool TryGetByColor(Color32 color, out ProvinceData province)
    {
        if (colorMap == null) BuildLookup();

        // 最近颜色容差匹配，覆盖导入/保存带来的 1~2 字节误差
        ProvinceData best = null;
        int bestDist = int.MaxValue;
        foreach (var p in provinces)
        {
            int dr = p.mapColor.r - color.r;
            int dg = p.mapColor.g - color.g;
            int db = p.mapColor.b - color.b;
            int dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = p;
            }
        }

        // 白色背景(255,255,255)等到最近省份的距离会很大，予以排除
        if (best != null && bestDist <= 30)
        {
            province = best;
            return true;
        }

        province = null;
        return false;
    }

    public ProvinceData GetById(string id)
    {
        foreach (var p in provinces)
            if (p.id == id) return p;
        return null;
    }
}