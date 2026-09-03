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

    // What has been done, and where and when it was done. An entry that says
    // only "done" against "find one out in the open in daylight" reads as a
    // task still to do; one that says where you found it is a record.
    private static readonly Dictionary<string, Dictionary<Study, string>> done =
        new Dictionary<string, Dictionary<Study, string>>();

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
        return done.TryGetValue(subject.Key, out var set) && set.ContainsKey(study);
    }

    /// <summary>Where and when it was done, if that was written down.</summary>
    public static string Detail(Subject subject, Study study)
    {
        return done.TryGetValue(subject.Key, out var set) && set.TryGetValue(study, out var detail)
            ? detail
            : "";
    }

    public static int Count(Subject subject)
    {
        return done.TryGetValue(subject.Key, out var set) ? set.Count : 0;
    }

    public static bool Complete(Subject subject) => Count(subject) >= Wants(subject).Length;

    /// <summary>Finished entries, out of however many there are.</summary>
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

    public static bool Record(Subject subject, Study study, string where = "", string when = "")
    {
        var set = Set(subject);

        if (set.ContainsKey(study)) return false;

        set[study] = Written(where, when);

        Filled?.Invoke(subject, study);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void RecordQuietly(Subject subject, Study study, string detail = "")
    {
        Set(subject)[study] = detail;
    }

    private static Dictionary<Study, string> Set(Subject subject)
    {
        if (!done.TryGetValue(subject.Key, out var set))
        {
            set = new Dictionary<Study, string>();
            done[subject.Key] = set;
        }

        return set;
    }

    private static string Written(string where, string when)
    {
        if (string.IsNullOrEmpty(where)) return when ?? "";

        return string.IsNullOrEmpty(when) ? where : where + ", " + when;
    }

    public static IEnumerable<Study> Of(Subject subject)
    {
        return done.TryGetValue(subject.Key, out var set)
            ? (IEnumerable<Study>)set.Keys
            : new Study[0];
    }

    /// <summary>
    /// What the entry says once it is done: a thing that happened, not a thing
    /// to go and do. "find one out in the open in daylight" against the word
    /// done reads as an instruction nobody has followed.
    /// </summary>
    public static string Did(Subject subject, Study study)
    {
        switch (study)
        {
            case Study.Sketch:
                return subject.Wild ? "drawn up close" : "drawn from far enough back";

            case Study.Inscription:
                return "read the writing"; 

            case Study.Habit:
                return "saw one " + global::Fauna.Habit(subject.Fauna);

            default:
                return "found " + global::Fauna.Country(subject.Fauna);
        }
    }

    /// <summary>What to call each part of an entry, so the page reads as a form.</summary>
    public static string Title(Subject subject, Study study)
    {
        switch (study)
        {
            case Study.Sketch: return "THE DRAWING";
            case Study.Habit: return "WHAT IT DOES";
            case Study.Inscription: return "WHAT IS WRITTEN THERE";
            default: return "WHERE IT LIVES";
        }
    }

    /// <summary>
    /// How you would actually go about it. The requirement says what the book
    /// wants; this says what to do with your hands, which is the part that was
    /// missing and left people looking at a page wondering what it wanted.
    /// </summary>
    public static string How(Subject subject, Study study)
    {
        switch (study)
        {
            case Study.Sketch:
                return subject.Wild
                    ? "walk up slowly, stand still, then hold F"
                    : "back up until all of it fits, then hold F";

            case Study.Habit:
                return "you have to be there when it happens";

            case Study.Inscription:
                return "walk right up to it";

            default:
                return "be in the right place at the right time";
        }
    }

    /// <summary>Where to go looking, which is worth saying before it is finished.</summary>
    public static string Where(Subject subject)
    {
        if (subject.Wild) return Fauna.Describe(subject.Fauna);

        return global::Landmarks.Country(subject.Landmark);
    }

    /// <summary>What each study asks for, in the book's own words.</summary>
    public static string Asks(Subject subject, Study study)
    {
        switch (study)
        {
            case Study.Sketch:
                return subject.Wild
                    ? "draw it up close"
                    : "draw all of it, from far enough back";

            case Study.Habit:
                return Habit(subject.Fauna);

            case Study.Inscription:
                return "read the writing on it";

            default:
                return Country(subject.Fauna);
        }
    }

    /// <summary>The behaviour an entry wants seen, which differs by animal.</summary>
    public static string Habit(FaunaKind kind)
    {
        return "see one " + global::Fauna.Habit(kind);
    }

    public static bool Habit(FaunaKind kind, Doing doing)
    {
        return global::Fauna.Habit(kind, doing);
    }

    /// <summary>The country an entry wants it found in.</summary>
    public static string Country(FaunaKind kind)
    {
        return "find one " + global::Fauna.Country(kind);
    }

    public static bool Country(FaunaKind kind, Vector3 at, int seed, float hour)
    {
        int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        float relief = WorldHeight.HeightAt(x, z, seed) / WorldHeight.MaxRelief;

        return global::Fauna.Proper(kind, hour, relief);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        done.Clear();
        Filled = null;
    }
}
