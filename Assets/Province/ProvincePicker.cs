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

    // 悬停状态：只在"命中的省份发生变化"时写一次材质，避免每帧写 MPB
    string hoverProvinceId;
    bool lastWantLabel;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        // ❗字体兜底必须放在最前面：运行期创建的 Text 若 font 为 null，UGUI 的
        // Text.OnPopulateMesh 会直接 return，文字一个像素都不画（也不报错）。场景里
        // labelFont 是全项目唯一的字体来源，面板 chrome 的内建文字（标题栏/眉标/
        // 档号/印章）都不带字体参数，只能靠 UIFactory.DefaultFont 兜底。
        if (labelFont != null) UIFactory.DefaultFont = labelFont;
        else if (UIFactory.DefaultFont != null) labelFont = UIFactory.DefaultFont;
        else Debug.LogError("[Reef] ProvincePicker.labelFont 未赋值：运行期所有文字（含面板 chrome 的标题/眉标/档号/印章）都不会绘制，请在 SampleScene 的 ProvincePicker 上指定字体。");
        if (database != null) database.BuildLookup();
        if (labelUI == null) labelUI = gameObject.AddComponent<ProvinceLabelUI>();
        if (labelUI != null) labelUI.SetFont(labelFont);

        clickHandler = GetComponent<ProvinceClickHandler>();
        if (clickHandler == null) clickHandler = gameObject.AddComponent<ProvinceClickHandler>();
        // 直接把地图渲染器交给渗透管理器：渗透模式切换 _BaseMap 时走 MPB，不再写共享材质资产
        PenetrationManager.Instance.Setup(idMap, database,
            clickHandler != null ? clickHandler.mapMaterial : null,
            clickHandler != null ? clickHandler.MapRenderer : null,
            labelFont);

        BlockManager.Instance.Setup(database, labelFont);
        AgencyNavUI.Instance.Setup(labelFont, clickHandler != null ? clickHandler.uiClickClip : null);
        AgencyPanelUI.Instance.Setup(labelFont);
        GameClock.Instance.Setup(labelFont);
        EventManager.Instance.Setup(labelFont, clickHandler != null ? clickHandler.uiClickClip : null, clickHandler != null ? clickHandler.eventClip : null);
        CausalChainManager.Instance.Setup(labelFont);
        TopBarUI.Instance.Setup(labelFont);

        // 纸印叠层（第三轮）：网点 + 颗粒 + 暗角，画布 sortingOrder = 50 → 压在地图之上、HUD(100) 之下。
        // 素材缺失时整块不建（返回 null），没有副作用。
        UIArt.BuildScreenOverlays(null);
    }

    void Update()
    {
        // Esc 清除选中（R4/AC-08）：ClearSelection() 此前全项目零调用者，玩家点过一次省份后
        // 黄填充 + 白描边就永远消不掉，而空点又会隐藏详情面板，两件事自相矛盾。
        // ❗本项目 ProjectSettings.asset 的 activeInputHandler = 1（仅新输入系统），旧写法
        //   Input.GetKeyDown(KeyCode.Escape) 会在运行期抛 InvalidOperationException 并中断
        //   本方法后面的拾取逻辑，所以必须走 Keyboard.current。
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClearSelection();

        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool penActive = PenetrationManager.Instance.IsActive;
        bool overUI = IsPointerOverUI();

        // 悬停反馈：光标下的省份名 + 地块描边（每帧最多一次射线，且只在命中变化时写材质）
        UpdateHover(mousePos, overUI, penActive);

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (overUI) return;

        TryPick(mousePos);
    }

    /// <summary>
    /// Esc：清掉省份选中高亮与省名标签。详情面板复用已有的 <see cref="BlockManager.Instance"/> 引用一并收起
    /// （<c>CloseBlock()</c> 内部已做 null 守卫），不为这一处新增引用字段。
    /// </summary>
    void ClearSelection()
    {
        if (clickHandler != null) clickHandler.ClearSelection();
        labelUI.Hide();
        lastWantLabel = false;
        if (BlockManager.Instance != null) BlockManager.Instance.CloseBlock();
    }

    /// <summary>
    /// 悬停：把光标所在省份写进 `_HoverId / _HoverStrength`，并把省名标签跟着光标显示。
    /// ❗不走 <see cref="ComputeCentroid"/>（那会全图扫描 267 万像素），直接用射线命中点。
    /// </summary>
    void UpdateHover(Vector2 screenPos, bool overUI, bool penActive)
    {
        ProvinceData hovered = null;
        bool wantLabel = false;
        Vector3 labelPos = Vector3.zero;

        if (!overUI && !penActive)
        {
            hovered = PickProvinceAt(screenPos, out Vector3 point);
            wantLabel = hovered != null;
            labelPos = point;
        }

        string id = hovered != null ? hovered.id : null;
        if (id != hoverProvinceId)
        {
            hoverProvinceId = id;
            if (clickHandler != null) clickHandler.SetHover(hovered);
        }

        // 渗透模式隐藏标签（原行为）；否则悬停跟随；两者都不成立时保留点击选中的标签
        if (penActive)
        {
            wantLabel = false;
            labelUI.Hide();
        }
        else if (wantLabel)
        {
            labelUI.Show(labelPos, hovered.displayName);
        }
        else if (lastWantLabel)
        {
            labelUI.Hide();
        }

        lastWantLabel = wantLabel;
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

    /// <summary>屏幕点 -> 省份；命中点世界坐标从 <paramref name="worldPoint"/> 返回。</summary>
    ProvinceData PickProvinceAt(Vector3 screenPos, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (idMap == null || database == null) return null;

        if (cam == null) cam = Camera.main;
        if (cam == null) return null;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return null;
        worldPoint = hit.point;

        // 没点到带 MeshCollider 的地图就忽略
        if (hit.collider == null || hit.collider.GetComponent<MeshCollider>() == null) return null;

        Vector2 uv = hit.textureCoord;
        int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * idMap.width), 0, idMap.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * idMap.height), 0, idMap.height - 1);

        Color32 picked = idMap.GetPixel(x, y);
        return database.TryGetByColor(picked, out ProvinceData province) ? province : null;
    }

    void TryPick(Vector3 screenPos)
    {
        if (idMap == null || database == null)
        {
            Debug.LogError("需求idMap和database");
            return;
        }

        ProvinceData province = PickProvinceAt(screenPos, out Vector3 hitPoint);

        if (province == null)
        {
            labelUI.Hide();
            lastWantLabel = false;
            return;
        }

        if (PenetrationManager.Instance.IsActive)
        {
            if (clickHandler != null) clickHandler.PlayPenetrationTick();
            PenetrationManager.Instance.Toggle(province.id);
            return;
        }

        Debug.Log($"province.displayName:{province.displayName} | id:{province.id}");

        // 文字定位到省份重心（每次点击一次全图扫描，这是既有行为，本轮不动）
        Vector3 centroid = ComputeCentroid(province, out bool computed);
        labelUI.Show(computed ? centroid : hitPoint, province.displayName);
        lastWantLabel = true;

        onProvinceClicked?.Invoke(province.id);

        BlockManager.Instance.OpenBlock(province.id);
    }

    /// <summary>
    /// 计算省份在 ID 图中的像素重心，并转换为世界坐标。
    /// </summary>
    Vector3 ComputeCentroid(ProvinceData province, out bool computed)
    {
        computed = false;

        MeshFilter mf = clickHandler != null && clickHandler.MapRenderer != null
            ? clickHandler.MapRenderer.GetComponent<MeshFilter>()
            : null;
        if (mf == null || mf.sharedMesh == null)
            return Vector3.zero;

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
            return Vector3.zero;

        Vector2 avgUV = uvSum / count;

        // 标准 Quad 的 UV 与对象空间顶点位置按 0~1 对齐，
        // 映射关系为：local = bounds.min + uv * bounds.size
        Bounds b = mf.sharedMesh.bounds;
        Vector3 local = new Vector3(
            b.min.x + avgUV.x * b.size.x,
            b.min.y + avgUV.y * b.size.y,
            b.min.z);

        computed = true;
        return mf.transform.TransformPoint(local);
    }
}