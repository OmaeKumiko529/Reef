using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ProvincePicker : MonoBehaviour
{
    [Header("引用")]
    public Texture2D idMap; //填色图
    public ProvinceDatabase database; //省份数据
    public Camera cam; //摄像
    public ProvinceLabelUI labelUI; //省名悬浮文本框（留空会自动创建）
    public Font labelFont; //显示名称用的字体（可显示中文）

    [Header("点到省份时")]
    public UnityEvent<string> onProvinceClicked; //参数是省份ID

    // 省份颜色匹配容差（与 ProvinceDatabase.TryGetByColor 保持一致）
    const int MatchToleranceSq = 30;

    ProvinceClickHandler clickHandler;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (database != null) database.BuildLookup();
        if (labelUI == null) labelUI = gameObject.AddComponent<ProvinceLabelUI>();
        if (labelUI != null) labelUI.font = labelFont;

        clickHandler = GetComponent<ProvinceClickHandler>();
        PenetrationManager.Instance.Setup(idMap, database, clickHandler != null ? clickHandler.mapMaterial : null, labelFont);

        BlockManager.Instance.Setup(database, labelFont);
        AgencyNavUI.Instance.Setup(labelFont, clickHandler != null ? clickHandler.uiClickClip : null);
        AgencyPanelUI.Instance.Setup(labelFont);
        GameClock.Instance.Setup(labelFont);
        EventManager.Instance.Setup(labelFont, clickHandler != null ? clickHandler.uiClickClip : null, clickHandler != null ? clickHandler.eventClip : null);
        CausalChainManager.Instance.Setup(labelFont);
        TopBarUI.Instance.Setup(labelFont);
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (PenetrationManager.Instance.IsActive && labelUI != null) labelUI.Hide();
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsPointerOverUI()) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        TryPick(mousePos);
    }

    bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;

        var ped = new PointerEventData(es);
        ped.position = Mouse.current.position.ReadValue();

        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        return results.Count > 0;
    }

    void TryPick(Vector3 screenPos)
    {
        if (idMap == null || database == null)
        {
            Debug.LogError("需求idMap和database");
            return;
        }

        if (cam == null) cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            labelUI.Hide();
            return;
        }

        //没点到带MeshCollider的地图就忽略
        if (hit.collider == null || hit.collider.GetComponent<MeshCollider>() == null)
        {
            labelUI.Hide();
            return;
        }

        Vector2 uv = hit.textureCoord;
        int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * idMap.width), 0, idMap.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * idMap.height), 0, idMap.height - 1);

        Color32 picked = idMap.GetPixel(x, y);

        if (database.TryGetByColor(picked, out ProvinceData province))
        {
            if (PenetrationManager.Instance.IsActive)
            {
                if (clickHandler != null) clickHandler.PlayPenetrationTick();
                PenetrationManager.Instance.Toggle(province.id);
                return;
            }

            Debug.Log($"province.displayName:{province.displayName} | id:{province.id} | RGB:({picked.r},{picked.g},{picked.b})");

            // 文字定位到省份重心
            Vector3 centroid = ComputeCentroid(province, hit);
            labelUI.Show(centroid, province.displayName);

            onProvinceClicked?.Invoke(province.id);

            BlockManager.Instance.OpenBlock(province.id);
        }
        else
        {
            Debug.Log($"未登记RGB:({picked.r},{picked.g},{picked.b})");
            labelUI.Hide();
        }
    }

    /// <summary>
    /// 计算省份在 ID 图中的像素重心，并转换为世界坐标。
    /// </summary>
    Vector3 ComputeCentroid(ProvinceData province, RaycastHit hit)
    {
        MeshCollider mc = hit.collider as MeshCollider;
        if (mc == null || mc.sharedMesh == null)
            return hit.point;

        Color32 target = province.mapColor;

        Vector2 uvSum = Vector2.zero;
        int count = 0;

        // 一次性读取所有像素，避免逐像素 GetPixel 的低效
        Color32[] pixels = idMap.GetPixels32();
        for (int py = 0; py < idMap.height; py++)
        {
            for (int px = 0; px < idMap.width; px++)
            {
                Color32 c = pixels[py * idMap.width + px];
                int dr = c.r - target.r;
                int dg = c.g - target.g;
                int db = c.b - target.b;
                if (dr * dr + dg * dg + db * db <= MatchToleranceSq)
                {
                    uvSum.x += (px + 0.5f) / idMap.width;
                    uvSum.y += (py + 0.5f) / idMap.height;
                    count++;
                }
            }
        }

        if (count == 0)
            return hit.point;

        Vector2 avgUV = uvSum / count;

        // 标准 Quad 的 UV 与对象空间顶点位置按 0~1 对齐，
        // 映射关系为：local = bounds.min + uv * bounds.size
        Bounds b = mc.sharedMesh.bounds;
        Vector3 local = new Vector3(
            b.min.x + avgUV.x * b.size.x,
            b.min.y + avgUV.y * b.size.y,
            b.min.z);

        return mc.transform.TransformPoint(local);
    }
}