using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Makes the title scene, and puts it first in the build.</summary>
public static class MakeTitleScene
{
    [MenuItem("Tools/Tile World/Make the title scene")]
    public static void Go()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        var events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        events.AddComponent<StandaloneInputModule>();
#endif

        new GameObject("Title").AddComponent<TitleMenu>();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Title.unity");

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Title.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("TITLE scene made and put first");
        EditorApplication.Exit(0);
    }
}
