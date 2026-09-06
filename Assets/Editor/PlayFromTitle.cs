using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Play Mode starts from the title, whichever scene is open in the editor,
/// the way a build does. Clear it with the menu item to play a scene as is.
/// </summary>
[InitializeOnLoad]
public static class PlayFromTitle
{
    private const string Title = "Assets/Scenes/Title.unity";

    static PlayFromTitle()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Title);
        if (scene != null && EditorSceneManager.playModeStartScene == null) EditorSceneManager.playModeStartScene = scene;
    }

    [MenuItem("Tools/Tile World/Play from the title")]
    private static void On() { EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Title); }

    [MenuItem("Tools/Tile World/Play the open scene as is")]
    private static void Off() { EditorSceneManager.playModeStartScene = null; }
}
