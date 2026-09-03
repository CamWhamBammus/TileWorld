using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the player from the command line, for checking work without the
/// editor open. Tools/build.sh calls this; Builds/ is ignored by git.
/// </summary>
public static class PlayerBuild
{
    public static void Dev()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = "Builds/Dev/TileWorld.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development,
        };

        BuildReport r = BuildPipeline.BuildPlayer(opts);
        Debug.Log("GAME BUILD: " + r.summary.result);
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
