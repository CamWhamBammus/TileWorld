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

    // Ground charted from a high place rather than walked. Kept apart so the
    // map can draw it faintly: seeing a valley from a tower is not the same as
    // having been down in it.
    private static readonly HashSet<Vector2Int> surveyed = new HashSet<Vector2Int>();

    /// <summary>Raised the first time a chunk is entered, never on revisits.</summary>
    public static event System.Action<Vector2Int> ChunkDiscovered;

    public static IReadOnlyCollection<Vector2Int> Visited => visited;

    public static IReadOnlyCollection<Vector2Int> Surveyed => surveyed;

    /// <summary>Everything on the chart, however it got there.</summary>
    public static int Count => visited.Count + surveyed.Count;

    /// <summary>Records a chunk. Returns true only if it had never been seen.</summary>
    public static bool Visit(Vector2Int chunk)
    {
        // walking somewhere you had only seen from afar upgrades it
        surveyed.Remove(chunk);

        if (!visited.Add(chunk))
        {
            return false;
        }

        ChunkDiscovered?.Invoke(chunk);
        return true;
    }

    /// <summary>Chart a chunk seen from a height. Never downgrades one already walked.</summary>
    public static bool Survey(Vector2Int chunk)
    {
        if (visited.Contains(chunk) || !surveyed.Add(chunk))
        {
            return false;
        }

        ChunkDiscovered?.Invoke(chunk);
        return true;
    }

    public static bool HasVisited(Vector2Int chunk) => visited.Contains(chunk);

    public static bool IsCharted(Vector2Int chunk) => visited.Contains(chunk) || surveyed.Contains(chunk);

    /// <summary>
    /// Statics survive between play sessions when the editor is set to skip
    /// domain reload, which would leave the last run's map on screen.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        visited.Clear();
        surveyed.Clear();
        ChunkDiscovered = null;
    }
}
