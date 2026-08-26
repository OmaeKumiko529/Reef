using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按住鼠标中键拖动平移视角；滚轮缩放（正交相机通过 orthographicSize 实现拉近/拉远）。
/// 平移与缩放均使用 SmoothDamp 平滑逼近目标值，避免生硬的瞬时跳变。
/// </summary>
public class CameraPanController : MonoBehaviour
{
    [Header("引用")]
    public Camera cam;

    [Header("平移")]
    [Tooltip("拖拽灵敏度，1 表示正交相机下 1 屏幕像素对应 1 世界单位。")]
    public float sensitivity = 2f;

    [Header("缩放")]
    [Tooltip("滚轮缩放速度，正交相机下每格滚轮改变的 orthographicSize。")]
    public float zoomSpeed = 1f;
    [Tooltip("最小缩放（orthographicSize 下限，值越小越近）。")]
    public float minZoom = 2f;
    [Tooltip("最大缩放（orthographicSize 上限，值越大越远）。")]
    public float maxZoom = 20f;

    [Header("平滑")]
    [Tooltip("平移平滑时间（秒），越小越跟手、越大越绵。")]
    public float panSmoothTime = 0.12f;
    [Tooltip("缩放平滑时间（秒），越小越跟手、越大越绵。")]
    public float zoomSmoothTime = 0.12f;

    // 平滑逼近用到的内部状态
    Vector3 targetPosition;
    Vector3 panVelocity;
    float targetZoom;
    float zoomVelocity;
    bool initialized;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;
        if (Mouse.current == null) return;

        EnsureInitialized();

        // 滚轮缩放：只更新目标值
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (cam.orthographic)
            {
                targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoom, maxZoom);
            }
            else
            {
                // 透视相机兜底：沿视线方向前后移动（调整到地图的距离）
                targetPosition += cam.transform.forward * scroll * zoomSpeed;
            }
        }

        // 中键平移：只更新目标值
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            if (delta != Vector2.zero)
            {
                float worldPerPixel = PixelsToWorldUnits() * sensitivity;

                // 鼠标向右拖 -> 画面向右 -> 相机向左；向上拖同理
                targetPosition += -cam.transform.right * delta.x * worldPerPixel
                                - cam.transform.up * delta.y * worldPerPixel;
            }
        }

        // 每帧平滑追赶目标值（松开按键后仍会平滑收尾，不再戛然而止）
        cam.transform.position = Vector3.SmoothDamp(
            cam.transform.position, targetPosition, ref panVelocity, panSmoothTime);
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.SmoothDamp(
                cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }
    }

    void EnsureInitialized()
    {
        if (initialized) return;
        targetPosition = cam.transform.position;
        targetZoom = cam.orthographic ? cam.orthographicSize : 0f;
        initialized = true;
    }

    float PixelsToWorldUnits()
    {
        if (cam.orthographic)
            return cam.orthographicSize * 2f / Screen.height;

        // 透视相机：用摄像机到地图平面(y=0)的距离近似单个像素对应的世界尺寸
        float dist = Mathf.Abs(cam.transform.position.y);
        return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
    }
}
