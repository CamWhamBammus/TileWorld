using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The book you are filling in, which is now the game.
///
/// It holds both halves of the country: the creatures in it and the things
/// built in it. A creature's entry wants a drawing, something seen of how it
/// lives, and it found in the ground it belongs to. A structure's wants a
/// drawing and whatever is written there, which means going inside.
///
/// Like the chart, this is what the player has personally done rather than
/// what exists, so it is kept with the world rather than derived from it.
/// </summary>
public static class FieldGuide
{
    public enum Study { Sketch, Habit, Country, Inscription }

    private static readonly Dictionary<string, HashSet<Study>> done =
        new Dictionary<string, HashSet<Study>>();

    public static event System.Action<Subject, Study> Filled;

    /// <summary>What a subject's entry asks for. Creatures want three, ruins two.</summary>
    public static Study[] Wants(Subject subject)
    {
        return subject.Wild
            ? new[] { Study.Sketch, Study.Habit, Study.Country }
            : new[] { Study.Sketch, Study.Inscription };
    }

    public static bool Has(Subject subject, Study study)
    {
        return done.TryGetValue(subject.Key, out var set) && set.Contains(study);
    }

    public static int Count(Subject subject)
    {
        return done.TryGetValue(subject.Key, out var set) ? set.Count : 0;
    }

    public static bool Complete(Subject subject) => Count(subject) >= Wants(subject).Length;

    /// <summary>Finished entries, out of the eight there are.</summary>
    public static int Entries
    {
        get
        {
            int n = 0;

            foreach (var subject in Subject.All()) if (Complete(subject)) n++;

            return n;
        }
    }

    /// <summary>Notes made, out of the twenty the book holds.</summary>
    public static int Notes
    {
        get
        {
            int n = 0;

            foreach (var subject in Subject.All()) n += Count(subject);

            return n;
        }
    }

    public static int NotesWanted
    {
        get
        {
            int n = 0;

            foreach (var subject in Subject.All()) n += Wants(subject).Length;

            return n;
        }
    }

    public static bool Record(Subject subject, Study study)
    {
        if (!done.TryGetValue(subject.Key, out var set))
        {
            set = new HashSet<Study>();
            done[subject.Key] = set;
        }

        if (!set.Add(study)) return false;

        Filled?.Invoke(subject, study);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void RecordQuietly(Subject subject, Study study)
    {
        if (!done.TryGetValue(subject.Key, out var set))
        {
            set = new HashSet<Study>();
            done[subject.Key] = set;
        }

        set.Add(study);
    }

    public static IEnumerable<Study> Of(Subject subject)
    {
        return done.TryGetValue(subject.Key, out var set) ? (IEnumerable<Study>)set : new Study[0];
    }

    /// <summary>What each study asks for, in the book's own words.</summary>
    public static string Asks(Subject subject, Study study)
    {
        switch (study)
        {
            case Study.Sketch:
                return subject.Wild
                    ? "draw it from close by, without putting it to flight"
                    : "draw it whole, from far enough back";

            case Study.Habit:
                return Habit(subject.Fauna);

            case Study.Inscription:
                return "read what is written there";

            default:
                return Country(subject.Fauna);
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
