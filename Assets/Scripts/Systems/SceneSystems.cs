using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the runtime systems back after a scene reload.
///
/// Most of this game's systems spawn themselves from a RuntimeInitializeOnLoad
/// hook, so nothing has to be dragged into the scene by hand. Unity runs those
/// hooks once, when the game starts. Changing world reloads the scene, which
/// destroys every one of those objects and never asks for them again — the
/// world would come back with no compass, no notices, no pause menu and no way
/// to save. So the same hooks are found and run again here.
/// </summary>
public static class SceneSystems
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        // Subscribed after the first scene is already up, so this never doubles
        // up on the load that Unity has just handled itself.
        SceneManager.sceneLoaded -= Respawn;
        SceneManager.sceneLoaded += Respawn;
    }

    private static void Respawn(Scene scene, LoadSceneMode mode)
    {
        const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public
                                   | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (var type in typeof(SceneSystems).Assembly.GetTypes())
        {
            if (type == typeof(SceneSystems)) continue;

            foreach (var method in type.GetMethods(Statics))
            {
                if (method.GetParameters().Length > 0) continue;

                var hook = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

                if (hook == null || hook.loadType != RuntimeInitializeLoadType.AfterSceneLoad) continue;

                try
                {
                    // Every one of these checks whether it already exists, so
                    // running it a second time is harmless.
                    method.Invoke(null, null);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Systems] " + type.Name + "." + method.Name
                                   + " would not start: " + e.Message);
                }
            }
        }
    }
}
