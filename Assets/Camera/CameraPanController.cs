using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按住鼠标中键拖动平移视角；滚轮缩放（正交相机通过 orthographicSize 实现拉近/拉远）。
/// 平移与缩放均使用 SmoothDamp 平滑逼近目标值，避免生硬的瞬时跳变。
///
/// R3 变更（契约 00_方案与分工.md 2.5）：
/// - 开局按地图包围盒 + HUD 安全区自动取景，13 省全部入画且不被顶栏/底栏遮挡；
/// - 平移钳制在地图范围内（地图比视野小时锁中心）；
/// - 缩放锚定光标：滚轮时"光标下的地块"保持不动；
/// - 静止时不再写 transform（原实现每帧 SmoothDamp，即使输入为零）；
/// - `R` 键复位到开局取景；
/// - 相机底色用代码设置（场景文件禁止改动）。
/// </summary>
public class CameraPanController : MonoBehaviour
{
    [Header("引用")]
    public Camera cam;

    [Tooltip("地图根物体；留空则运行时自动查找（先按 MapBoundsTarget 组件，再按名称 Map）。")]
    public Transform mapRoot;

    /// <summary>可选标记组件：挂在任意 Renderer 子物体上即可让相机知道地图包围盒。</summary>
    public sealed class MapBoundsTarget : MonoBehaviour { }

    [Header("平移")]
    [Tooltip("拖拽灵敏度，1 表示正交相机下 1 屏幕像素对应 1 世界单位。")]
    public float sensitivity = 2f;

    [Header("缩放")]
    [Tooltip("滚轮缩放速度，正交相机下每格滚轮改变的 orthographicSize。")]
    public float zoomSpeed = 1f;
    [Tooltip("最小缩放（orthographicSize 下限，值越小越近）。")]
    public float minZoom = 2f;
    // 缩放上限不在这里配：开局取景后由 ComputeFit() 算出 maxZoomLimit = max(minZoomLimit * 1.01, fitZoom * 1.10)。
    // （曾有一个 public float maxZoom = 20f 的死字段，运行期从不读取，只会误导下一个人在 Inspector 上调它——
    //   实际生效的是 fitZoom 派生值，调这里的数不会改变"最远只能缩到 1.10× 取景"这件事。）

    [Header("平滑")]
    [Tooltip("平移平滑时间（秒），越小越跟手、越大越绵。")]
    public float panSmoothTime = 0.12f;
    [Tooltip("缩放平滑时间（秒），越小越跟手、越大越绵。")]
    public float zoomSmoothTime = 0.12f;

    [Header("取景")]
    [Tooltip("顶栏保留高度（像素）：与 Assets/Agency/TopBarUI.cs 的上边距 20 + 顶栏高 64 = 84 成对（契约 70_ §9.0 L-05）。")]
    public float topReservePixels = 84f;
    [Tooltip("底栏保留高度（像素）：与 Assets/Agency/AgencyNavUI.cs 一致。")]
    public float bottomReservePixels = 80f;
    [Tooltip("取景留白系数，1.06 表示地图四周再留 6% 边距。")]
    public float fitPadding = 1.06f;

    /// <summary>缩放锚定只在"视口变大"方向生效的容差（像素）。</summary>
    const float AnchorDeadzonePx = 1f;

    // 平滑逼近用到的内部状态
    Vector3 targetPosition;
    Vector3 panVelocity;
    float targetZoom;
    float zoomVelocity;
    float lastAnchorZoom;

    // 地图范围与开局取景
    Bounds mapBounds;
    bool hasMapBounds;
    float fitZoom = 5f;
    float minZoomLimit = 2f;
    float maxZoomLimit = 20f;

    /// <summary>地图世界包围盒（未识别到地图时为退化包围盒，hasMapBounds=false）。</summary>
    public Bounds MapBounds => mapBounds;
    public bool HasMapBounds => hasMapBounds;
    /// <summary>开局自适应取景的 orthographicSize（1080p/16:9 参考 ≈7.0）。</summary>
    public float FitZoom => fitZoom;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            // 场景文件不能改（禁令 2.0），底色只能在运行期设
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.MapBackdrop;
        }
        ResolveMapBounds();

        // 先给一个合法初值，避免首帧 Screen.height 还没准备好
        targetPosition = cam != null ? cam.transform.position : Vector3.zero;
        targetZoom = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        lastAnchorZoom = targetZoom;
    }

    void Start()
    {
        // 等一帧：CanvasScaler / Screen 尺寸在 Awake 阶段可能尚未最终确定
        ResetView();
    }

    void Update()
    {
        if (cam == null) return;

        // 复活点：无论输入设备是否可用，都要驱动平滑与钳制，否则第一帧会卡在旧位姿
        ApplyPanAndZoom();

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                if (cam.orthographic)
                {
                    targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoomLimit, maxZoomLimit);
                }
                else
                {
                    // 透视相机兜底：沿视线方向前后移动（调整到地图的距离）
                    targetPosition += cam.transform.forward * scroll * zoomSpeed;
                }
            }

            // 中键平移：只更新目标值
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta != Vector2.zero)
                {
                    float worldPerPixel = PixelsToWorldUnits() * sensitivity;

                    // 鼠标向右拖 -> 画面向右 -> 相机向左；向上拖同理。
                    // 竖直方向必须用世界 Z：相机绕 X 转了 90° 俯视，"相机上方"在世界里是 Z 轴，
                    // 用 cam.transform.up 会让纵向拖拽反向且被钳制到边界。
                    targetPosition += -cam.transform.right * delta.x * worldPerPixel
                                    - Vector3.forward * delta.y * worldPerPixel;
                }
            }
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetView();
    }

    /// <summary>回到开局自适应取景（R 键 / 初始化）。</summary>
    public void ResetView()
    {
        ComputeFit();

        targetZoom = fitZoom;
        zoomVelocity = 0f;
        lastAnchorZoom = fitZoom;
        panVelocity = Vector3.zero;

        Vector3 p = cam.transform.position;
        Vector3 center = hasMapBounds ? mapBounds.center : Vector3.zero;
        targetPosition = ClampToMap(new Vector3(center.x, p.y, center.z));
        cam.transform.position = targetPosition;
        cam.orthographicSize = fitZoom;
    }

    // ---------------------------------------------------------------- 取景

    /// <summary>按地图包围盒 + HUD 安全区求"整图恰好入画"的 orthographicSize 与缩放上下限。</summary>
    void ComputeFit()
    {
        float aspect = cam.aspect;
        if (aspect <= 0.0001f) aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;

        // HUD 安全区：地图可用的竖向屏幕比例 = 1 - (顶栏 + 底栏) / 屏高
        float screenH = Mathf.Max(1f, Screen.height);
        float safeRatio = Mathf.Clamp(1f - (topReservePixels + bottomReservePixels) / screenH, 0.35f, 1f);

        Vector2 extent = hasMapBounds ? (Vector2)mapBounds.extents : new Vector2(8f, 6f);
        float fitY = extent.y / safeRatio;      // 竖向恰好放进安全区
        float fitX = extent.x / aspect;         // 横向恰好放进屏宽
        fitZoom = Mathf.Max(fitY, fitX) * fitPadding;

        minZoomLimit = Mathf.Min(minZoom, fitZoom);
        maxZoomLimit = Mathf.Max(minZoomLimit * 1.01f, fitZoom * 1.10f);
    }

    /// <summary>把目标位置钳在地图内；地图比视野小时锁死中心。</summary>
    Vector3 ClampToMap(Vector3 p)
    {
        if (!hasMapBounds || cam == null || !cam.orthographic) return p;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector2 e = mapBounds.extents;
        Vector2 c = mapBounds.center;

        p.x = halfW >= e.x ? c.x : Mathf.Clamp(p.x, c.x - e.x + halfW, c.x + e.x - halfW);
        p.z = halfH >= e.y ? c.y : Mathf.Clamp(p.z, c.y - e.y + halfH, c.y + e.y - halfH);
        return p;
    }

    // ---------------------------------------------------------------- 平滑

    void ApplyPanAndZoom()
    {
        // 静止时彻底不写 transform：原实现每帧 SmoothDamp 会让画面永远有亚像素抖动
        bool panMoving = (cam.transform.position - targetPosition).sqrMagnitude > 1e-8f;
        if (panMoving)
        {
            cam.transform.position = Vector3.SmoothDamp(
                cam.transform.position, targetPosition, ref panVelocity, panSmoothTime);
        }

        if (!cam.orthographic) return;

        bool zoomMoving = Mathf.Abs(cam.orthographicSize - targetZoom) > 1e-4f;
        if (!zoomMoving) return;

        float prev = cam.orthographicSize;
        float next = Mathf.SmoothDamp(prev, targetZoom, ref zoomVelocity, zoomSmoothTime);

        // 缩放锚定光标：按"本帧实际缩放量"补偿相机位置，使光标下的世界点不动。
        // 只在视口变大（prev > next，画面缩小）时补偿：视口变小时若也补偿，
        // 钳制会把整图往鼠标方向推，看起来像镜头在漂移。
        float heightDelta = prev - next;
        if (heightDelta > AnchorDeadzonePx * PixelsToWorldUnits() && lastAnchorZoom > 0.0001f)
        {
            var mouse = Mouse.current;
            Vector2 anchorPx = mouse != null
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            targetPosition += ComputeAnchorOffset(lastAnchorZoom, next, anchorPx);
        }

        lastAnchorZoom = next;
        cam.orthographicSize = next;
        cam.transform.position = ClampToMap(cam.transform.position);
        targetPosition = ClampToMap(targetPosition);
    }

    /// <summary>
    /// 计算 orthographicSize 由 h0 变为 h1 时，为保持屏幕点 mouse 下的世界坐标不动所需的相机位移。
    /// 推导：世界点 = camPos + 屏幕偏移(∝h)，要求 h0/h1 两种状态下世界点相同
    ///   → camPos1 = camPos0 + (0.5-mouseNorm) ⊙ (2*(h0-h1)*aspect, 2*(h0-h1))
    /// </summary>
    Vector3 ComputeAnchorOffset(float h0, float h1, Vector2 mouse)
    {
        float sw = Mathf.Max(1f, Screen.width);
        float sh = Mathf.Max(1f, Screen.height);
        Vector2 n = new Vector2(mouse.x / sw, mouse.y / sh);

        float dh = h0 - h1;
        return new Vector3((0.5f - n.x) * 2f * dh * cam.aspect, 0f, (0.5f - n.y) * 2f * dh);
    }

    float PixelsToWorldUnits()
    {
        if (cam.orthographic)
            return cam.orthographicSize * 2f / Mathf.Max(1f, Screen.height);

        // 透视相机：用摄像机到地图平面(y=0)的距离近似单个像素对应的世界尺寸
        float dist = Mathf.Abs(cam.transform.position.y);
        return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height);
    }

    // ---------------------------------------------------------------- 地图包围盒

    void ResolveMapBounds()
    {
        if (mapRoot == null)
        {
            var marker = FindAnyObjectByType<MapBoundsTarget>();
            if (marker != null) mapRoot = marker.transform;
        }
        if (mapRoot == null)
        {
            var go = GameObject.Find("Map");
            if (go != null) mapRoot = go.transform;
        }
        if (mapRoot == null) return;

        var renderers = mapRoot.GetComponentsInChildren<MeshRenderer>(true);
        bool any = false;
        Bounds b = new Bounds();
        foreach (var r in renderers)
        {
            // 逻辑取景只需要地图本身的四边形：排除阴影投射体等额外网格
            if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) continue;
            if (r.bounds.size.sqrMagnitude <= 0.0001f) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        hasMapBounds = any;
        mapBounds = any ? b : new Bounds(Vector3.zero, new Vector3(16f, 0f, 12f));
    }
}
