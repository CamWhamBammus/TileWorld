using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The book you are filling in. A creature is not done with when you have laid
/// eyes on it: an entry wants a drawing of it, something seen of how it lives,
/// and it found in the country it belongs to.
///
/// Like the chart, this is what the player has personally done rather than
/// what exists, so it is kept with the world rather than derived from it.
/// </summary>
public static class FieldGuide
{
    /// <summary>The three things an entry asks for.</summary>
    public enum Study { Sketch, Habit, Country }

    private static readonly Dictionary<FaunaKind, HashSet<Study>> done =
        new Dictionary<FaunaKind, HashSet<Study>>();

    public static event System.Action<FaunaKind, Study> Filled;

    public static bool Has(FaunaKind kind, Study study)
    {
        return done.TryGetValue(kind, out var set) && set.Contains(study);
    }

    public static int Count(FaunaKind kind)
    {
        return done.TryGetValue(kind, out var set) ? set.Count : 0;
    }

    public static bool Complete(FaunaKind kind) => Count(kind) >= 3;

    /// <summary>How many of the four are finished.</summary>
    public static int Entries
    {
        get
        {
            int n = 0;

            for (int i = 0; i < 4; i++) if (Complete((FaunaKind)i)) n++;

            return n;
        }
    }

    public static int Studies
    {
        get
        {
            int n = 0;

            for (int i = 0; i < 4; i++) n += Count((FaunaKind)i);

            return n;
        }
    }

    public static bool Record(FaunaKind kind, Study study)
    {
        if (!done.TryGetValue(kind, out var set))
        {
            set = new HashSet<Study>();
            done[kind] = set;
        }

        if (!set.Add(study)) return false;

        Filled?.Invoke(kind, study);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void RecordQuietly(FaunaKind kind, Study study)
    {
        if (!done.TryGetValue(kind, out var set))
        {
            set = new HashSet<Study>();
            done[kind] = set;
        }

        set.Add(study);
    }

    public static IEnumerable<Study> Of(FaunaKind kind)
    {
        return done.TryGetValue(kind, out var set) ? (IEnumerable<Study>)set : new Study[0];
    }

    /// <summary>What each study asks for, in the guide's own words.</summary>
    public static string Asks(FaunaKind kind, Study study)
    {
        switch (study)
        {
            case Study.Sketch:
                return "draw it from close by, without putting it to flight";

            case Study.Habit:
                return Habit(kind);

            default:
                return Country(kind);
        }
    }

    /// <summary>The behaviour an entry wants seen, which differs by animal.</summary>
    public static string Habit(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return "watch one grazing";
            case FaunaKind.Rabbit: return "catch one at rest";
            case FaunaKind.Fox: return "see one drink";
            default: return "watch one on the move";
        }
    }

    public static bool Habit(FaunaKind kind, Doing doing)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return doing == Doing.Grazing;
            case FaunaKind.Rabbit: return doing == Doing.Resting;
            case FaunaKind.Fox: return doing == Doing.Drinking;
            default: return doing == Doing.Walking;
        }
    }

    /// <summary>The country an entry wants it found in.</summary>
    public static string Country(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return "find one under the trees at dusk";
            case FaunaKind.Rabbit: return "find one out in the open in daylight";
            case FaunaKind.Fox: return "find one after dark";
            default: return "find one on the high ground";
        }
    }

    public static bool Country(FaunaKind kind, Vector3 at, int seed, float hour)
    {
        int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        float relief = WorldHeight.HeightAt(x, z, seed) / WorldHeight.MaxRelief;

        switch (kind)
        {
            case FaunaKind.Deer: return hour > 0.62f && hour < 0.86f;
            case FaunaKind.Rabbit: return hour > 0.30f && hour < 0.72f && relief < 0.22f;
            case FaunaKind.Fox: return hour < 0.22f || hour > 0.82f;
            default: return relief > 0.55f;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        done.Clear();
        Filled = null;
    }
}
