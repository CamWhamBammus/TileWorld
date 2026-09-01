using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which creatures the player has actually laid eyes on. Kept apart from what
/// exists in the world, the same way the chart is: a fox crossing a field at
/// midnight only counts once you have seen it.
/// </summary>
public static class SightingLog
{
    private static readonly HashSet<FaunaKind> seen = new HashSet<FaunaKind>();

    // Where each was first laid eyes on, so the journal can say which country
    // it was in rather than only that it happened.
    private static readonly Dictionary<FaunaKind, Vector2Int> where =
        new Dictionary<FaunaKind, Vector2Int>();

    /// <summary>Raised the first time a kind is seen, never afterwards.</summary>
    public static event System.Action<FaunaKind> Sighted;

    public static IReadOnlyCollection<FaunaKind> Seen => seen;

    public static int Count => seen.Count;

    public static bool Has(FaunaKind kind) => seen.Contains(kind);

    public static bool Record(FaunaKind kind, Vector2Int chunk)
    {
        if (!seen.Add(kind)) return false;

        where[kind] = chunk;
        Sighted?.Invoke(kind);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void RecordQuietly(FaunaKind kind, Vector2Int chunk)
    {
        seen.Add(kind);
        where[kind] = chunk;
    }

    /// <summary>The chunk a kind was first seen in, if it is known.</summary>
    public static bool FirstSeen(FaunaKind kind, out Vector2Int chunk)
    {
        return where.TryGetValue(kind, out chunk);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        seen.Clear();
        where.Clear();
        Sighted = null;
    }
}
