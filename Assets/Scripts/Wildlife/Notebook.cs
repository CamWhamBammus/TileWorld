using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the book has remarked on. Each line is kept with the country it
/// happened in and the hour of the day, because half of what makes a note
/// worth having is where you were standing when you made it.
/// </summary>
public static class Notebook
{
    public struct Entry
    {
        public string Id;
        public string Line;
        public string Where;
        public string When;
    }

    private static readonly List<Entry> kept = new List<Entry>();
    private static readonly HashSet<string> ids = new HashSet<string>();

    public static event System.Action<Entry> Written;

    public static int Count => kept.Count;

    public static int Possible => Observations.All.Count;

    public static IReadOnlyList<Entry> All => kept;

    public static bool Has(string id) => ids.Contains(id);

    public static bool Write(string id, string line, string where, string when)
    {
        if (!ids.Add(id)) return false;

        var entry = new Entry { Id = id, Line = line, Where = where, When = when };

        kept.Add(entry);
        Written?.Invoke(entry);

        return true;
    }

    /// <summary>Restoring a save, where nothing should be announced again.</summary>
    public static void WriteQuietly(string id, string where, string when)
    {
        if (!ids.Add(id)) return;

        string line = id;

        foreach (var note in Observations.All)
        {
            if (note.Id == id) { line = note.Line; break; }
        }

        kept.Add(new Entry { Id = id, Line = line, Where = where, When = when });
    }

    /// <summary>
    /// Where the book thinks you have not been looking. Deliberately about
    /// hours and places rather than about particular notes: told "go and find a
    /// fox drinking" you would be running an errand, but told the country has
    /// only been seen by daylight you go out at night and find your own things.
    /// </summary>
    public static string Wondering()
    {
        bool night = false, high = false, water = false, wet = false, close = false;

        foreach (var entry in kept)
        {
            if (entry.Id.Contains("night") || entry.Id == "dark-alone") night = true;
            if (entry.Id.Contains("high") || entry.Id.Contains("snow") || entry.Id.Contains("slope")) high = true;
            if (entry.Id.Contains("water") || entry.Id.Contains("drink")) water = true;
            if (entry.Id.Contains("rain") || entry.Id.Contains("weather") || entry.Id == "rest-rain") wet = true;
            if (entry.Id == "close-quarters" || entry.Id == "unbothered") close = true;
        }

        if (!night) return "you have not been out at night yet";
        if (!high) return "you have not been up high yet";
        if (!water) return "you have not been to the water yet";
        if (!wet) return "you have not been out in bad weather";
        if (!close) return "you have not got close to anything yet";

        return kept.Count < Possible
            ? "more to find"
            : "you have seen most of it";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        kept.Clear();
        ids.Clear();
        Written = null;
    }
}
