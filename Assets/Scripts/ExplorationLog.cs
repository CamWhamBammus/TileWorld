using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player has discovered. One record, shared by the quest log, the
/// objective counter and the map — before this, each tracked its own set and
/// they could quietly disagree about how much of the world you had seen.
/// </summary>
public static class ExplorationLog
{
    private static readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

    /// <summary>Raised the first time a chunk is entered, never on revisits.</summary>
    public static event System.Action<Vector2Int> ChunkDiscovered;

    public static IReadOnlyCollection<Vector2Int> Visited => visited;

    public static int Count => visited.Count;

    /// <summary>Records a chunk. Returns true only if it had never been seen.</summary>
    public static bool Visit(Vector2Int chunk)
    {
        if (!visited.Add(chunk))
        {
            return false;
        }

        ChunkDiscovered?.Invoke(chunk);
        return true;
    }

    public static bool HasVisited(Vector2Int chunk) => visited.Contains(chunk);

    /// <summary>
    /// Statics survive between play sessions when the editor is set to skip
    /// domain reload, which would leave the last run's map on screen.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        visited.Clear();
        ChunkDiscovered = null;
    }
}
