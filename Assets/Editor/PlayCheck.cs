using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs the game in the editor from the command line, for a while, and
/// leaves: what the editor's console would have shown is in the log. With
/// a probe in place it plays through whatever the probe does.
///
///   Unity -batchmode -projectPath . -executeMethod PlayCheck.Go -logFile play.log
///
/// (no -quit: the editor is quit from here once play mode has had its time,
/// and no -nographics: rendering is most of what a play check is for).
/// </summary>
public static class PlayCheck
{
    private const float Seconds = 75f;

    public static void Go()
    {
        SessionState.SetBool("playcheck", true);
        SessionState.SetFloat("playcheck.start", (float)EditorApplication.timeSinceStartup);
        Debug.Log("[PlayCheck] entering play mode for " + Seconds + " s");
        EditorApplication.EnterPlaymode();
    }

    // Entering play mode reloads the domain, which drops the update hook, so
    // it is put back after every reload while the check is on.
    [InitializeOnLoadMethod]
    private static void Hook()
    {
        if (!SessionState.GetBool("playcheck", false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        double since = EditorApplication.timeSinceStartup - SessionState.GetFloat("playcheck.start", 0f);

        if (EditorApplication.isPlaying && since > Seconds)
        {
            Debug.Log("[PlayCheck] time is up, leaving play mode");
            EditorApplication.ExitPlaymode();
        }

        if (!EditorApplication.isPlaying && since > Seconds + 6f)
        {
            SessionState.SetBool("playcheck", false);
            Debug.Log("[PlayCheck] done");
            EditorApplication.Exit(0);
        }
    }
}
