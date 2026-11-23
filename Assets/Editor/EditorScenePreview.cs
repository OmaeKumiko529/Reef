using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 设计评审用预览器：进入 Play 模式 → 让所有运行期 UI 正常构建 → 截屏 → 退出。
/// 这样视觉改动可以"被看到"，而不是靠读代码猜。
/// 用法：
///   Unity.exe -projectPath F:\Projects\Reef -executeMethod EditorScenePreview.CapturePreview
/// 参数：-out &lt;png&gt; / -w / -h / -frames &lt;进入Play后等待帧数&gt;
/// </summary>
public static class EditorScenePreview
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string HarnessName = "__PreviewHarness";

    static string Arg(string[] args, string key, string fallback)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == key && i + 1 < args.Length) return args[i + 1];
        return fallback;
    }

    [MenuItem("Reef/预览：进入Play并截图")]
    public static void CapturePreviewMenu() => CapturePreview();

    public static void CapturePreview()
    {
        var args = Environment.GetCommandLineArgs();
        string outPath = Arg(args, "-out", Path.Combine(Directory.GetCurrentDirectory(), "Logs/ui_play.png"));
        int wait = int.Parse(Arg(args, "-frames", "12"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        _outPath = outPath;
        _waitFrames = wait;
        _phase = Phase.Loading;
        _deadline = EditorApplication.timeSinceStartup + 180.0;

        Debug.Log("[Preview] 打开场景 " + ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var go = new GameObject(HarnessName);
        go.AddComponent<PreviewHarness>();

        Debug.Log("[Preview] 进入 Play 模式");
        EditorApplication.EnterPlaymode();
    }

    enum Phase { Idle, Loading, Waiting, Capturing, Done }

    static Phase _phase = Phase.Idle;
    static string _outPath;
    static int _waitFrames;
    static int _startFrame;
    static double _deadline;

    static void Tick()
    {
        if (_phase == Phase.Idle || _phase == Phase.Done) return;

        if (EditorApplication.timeSinceStartup > _deadline)
        {
            Debug.LogError("[Preview] 超时，放弃");
            Finish(1);
            return;
        }

        if (_phase == Phase.Loading)
        {
            if (!EditorApplication.isPlaying) return; // 等 Play 真正开始
            Debug.Log("[Preview] 已进入 Play，等待 " + _waitFrames + " 帧");
            _phase = Phase.Waiting;
            _startFrame = Time.frameCount;
            return;
        }

        if (_phase == Phase.Waiting)
        {
            if (Time.frameCount - _startFrame < _waitFrames) return;

            _phase = Phase.Capturing;
            // 冻结时钟，避免事件弹窗/打字机继续推进，保证可复现
            GameClock.PauseForEvent();

            try
            {
                ScreenCapture.CaptureScreenshot(_outPath, 1);
                Debug.Log("[Preview] 已请求截图 " + _outPath + " 屏幕=" + Screen.width + "x" + Screen.height);
            }
            catch (Exception e)
            {
                Debug.LogError("[Preview] 截图失败: " + e.Message);
            }

            int framesLeft = 8;
            EditorApplication.update += () =>
            {
                framesLeft--;
                if (framesLeft > 0) return;
                bool ok = File.Exists(_outPath);
                Debug.Log("[Preview] 截图文件存在=" + ok);
                Finish(ok ? 0 : 2);
            };
        }
    }

    static void Finish(int code)
    {
        _phase = Phase.Done;
        EditorApplication.update -= Tick;
        if (Application.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        };
    }
}

/// <summary>
/// 运行期预览脚手架：
/// 1) 复现 ProvincePicker.Awake 的初始化（含 UI 构建）；
/// 2) 把相机对准地图（含按地图尺寸自适应，便于评审完整画面）；
/// 3) 可选地摆出指定 UI 状态。
/// </summary>
public class PreviewHarness : MonoBehaviour
{
    public string previewState = "";
    public bool fitMap = true;
    public bool buildUI = true;

    static string Arg(string[] args, string key, string fallback)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == key && i + 1 < args.Length) return args[i + 1];
        return fallback;
    }

    void Start()
    {
        var args = Environment.GetCommandLineArgs();
        previewState = Arg(args, "-state", previewState);

        if (fitMap) FitCameraToMap();

        if (buildUI)
        {
            BuildUI();
            ApplyState(previewState);
        }

        Debug.Log("[Preview] 状态=" + (string.IsNullOrEmpty(previewState) ? "(默认开局)" : previewState) + " 屏幕=" + Screen.width + "x" + Screen.height);
    }

    void FitCameraToMap()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 地图是内置 Quad(1x1) 缩放 (16,12)，位于原点平面内
        var map = GameObject.Find("Map");
        float halfH = 6f, halfW = 8f;
        if (map != null)
        {
            var s = map.transform.lossyScale;
            halfW = Mathf.Abs(s.x) * 0.5f;
            halfH = Mathf.Abs(s.y) * 0.5f;
        }

        float aspect = Screen.width > 0 && Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        float sizeForHeight = halfH;
        float sizeForWidth = halfW / aspect;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth) * 1.03f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
    }

    void BuildUI()
    {
        var picker = UnityEngine.Object.FindFirstObjectByType<ProvincePicker>();
        if (picker == null)
        {
            Debug.LogWarning("[Preview] 场景里没有 ProvincePicker，无法复现初始化");
            return;
        }

        // 直接触发原初始化流程，保证预览与真实开局完全一致
        var m = typeof(ProvincePicker).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (m != null) m.Invoke(picker, null);
        else Debug.LogError("[Preview] 反射调用 ProvincePicker.Awake 失败");
    }

    void ApplyState(string state)
    {
        switch (state)
        {
            case "block":
                BlockManager.Instance.OpenBlock("1008");
                break;
            case "penetration":
                PenetrationManager.Instance.SetActive(true);
                PenetrationManager.Instance.ForcePenetrate("1008");
                break;
            case "agency":
                AgencyPanelUI.Instance.SetOpen(true);
                break;
            case "nav":
                AgencyNavUI.Instance.ShowPanel(1);
                break;
            case "journal":
                CausalChainManager.Instance.Reveal("spring_of_the_people");
                CausalChainManager.Instance.SetSummary("spring_of_the_people", "当地驻军哗变的消息传来，起因被层层遮蔽，只能确认与外省粮价有关。");
                break;
            default:
                break;
        }
    }
}
