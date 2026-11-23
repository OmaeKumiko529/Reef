using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 编辑器/CI 用的截图工具：在 batchmode 下也能把当前画面渲染成 PNG，
/// 让视觉改动可以被"看到"而不是靠猜测。输出默认写到 Logs/（已被 .gitignore 忽略）。
/// 用法：Unity.exe -batchmode -projectPath F:\Projects\Reef -executeMethod ReefScreenshot.CaptureGameView
/// </summary>
public static class ReefScreenshot
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static string OutPath(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "-out" && i + 1 < args.Length) return args[i + 1];
        return Path.Combine(Directory.GetCurrentDirectory(), "Logs/ui_capture.png");
    }

    [MenuItem("Reef/截图当前游戏画面")]
    public static void CaptureGameViewMenu() => CaptureGameView();

    public static void CaptureGameView()
    {
        int width = 1920, height = 1080;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-w" && i + 1 < args.Length) int.TryParse(args[i + 1], out width);
            if (args[i] == "-h" && i + 1 < args.Length) int.TryParse(args[i + 1], out height);
        }

        var path = OutPath(args);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Capture(path, width, height);
        Debug.Log("[ReefScreenshot] 已写出 " + path);
    }

    /// <summary>渲染 MainCamera 到 RT 并写成 PNG。可在 batchmode（无窗口）下运行。</summary>
    public static void Capture(string path, int width, int height)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[ReefScreenshot] 场景里没有 MainCamera（未打 MainCamera tag？）");
            return;
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.Create();

        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevTarget;

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        rt.Release();
        Object.DestroyImmediate(rt);

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
