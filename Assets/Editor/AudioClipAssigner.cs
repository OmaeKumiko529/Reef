using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AudioClipAssigner
{
    static AudioClipAssigner()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        Assign();
    }

    [MenuItem("暗礁/修复音效引用")]
    public static void Assign()
    {
        var handler = Object.FindFirstObjectByType<ProvinceClickHandler>();
        if (handler == null)
        {
            Debug.LogWarning("未找到 ProvinceClickHandler");
            return;
        }

        handler.tickClip = Load("tick_audio");
        handler.penetrationClip = Load("kick_audio_2");
        handler.uiClickClip = Load("kick_audio_3");
        handler.eventClip = Load("event_audio");

        EditorUtility.SetDirty(handler);
        EditorSceneManager.MarkSceneDirty(handler.gameObject.scene);
        Debug.Log("已修复 ProvinceClickHandler 音效引用");
    }

    static AudioClip Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".mp3");
    }
}
