using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player has actually walked up to, as opposed to what exists.</summary>
public static class LandmarkLog
{
    private static readonly Dictionary<Vector2Int, LandmarkKind> found = new Dictionary<Vector2Int, LandmarkKind>();

    public static event System.Action<Vector2Int, LandmarkKind> Discovered;

    public static IReadOnlyDictionary<Vector2Int, LandmarkKind> Found => found;

    public static int Count => found.Count;

    public static bool Discover(Vector2Int chunk, LandmarkKind kind)
    {
        if (found.ContainsKey(chunk))
        {
            return false;
        }

        found.Add(chunk, kind);
        Discovered?.Invoke(chunk, kind);

        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        found.Clear();
        Discovered = null;
    }
}
