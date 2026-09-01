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

    /// <summary>Raised the first time a kind is seen, never afterwards.</summary>
    public static event System.Action<FaunaKind> Sighted;

    public static IReadOnlyCollection<FaunaKind> Seen => seen;

    public static int Count => seen.Count;

    public static bool Has(FaunaKind kind) => seen.Contains(kind);

    public static bool Record(FaunaKind kind)
    {
        if (!seen.Add(kind)) return false;

        Sighted?.Invoke(kind);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void RecordQuietly(FaunaKind kind)
    {
        seen.Add(kind);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        seen.Clear();
        Sighted = null;
    }
}
