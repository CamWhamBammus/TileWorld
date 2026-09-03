using UnityEngine;

/// <summary>
/// Something worth drawing. The book does not care whether it breathes: a
/// watchtower on a ridge and a fox at the water are both things you came
/// across and put on paper, and the game is the same game either way.
/// </summary>
public struct Subject
{
    public bool Wild;        // a creature, as against a structure
    public int Kind;         // FaunaKind or LandmarkKind

    public static Subject Creature(FaunaKind kind) => new Subject { Wild = true, Kind = (int)kind };

    public static Subject Structure(LandmarkKind kind) => new Subject { Wild = false, Kind = (int)kind };

    public FaunaKind Fauna => (FaunaKind)Kind;

    public LandmarkKind Landmark => (LandmarkKind)Kind;

    /// <summary>How it is written down, in the save and on the drawing's file.</summary>
    public string Key => (Wild ? "c" : "s") + Kind;

    public string Name => Wild ? global::Fauna.Of(Fauna).Name : Landmarks.NameOf(Landmark).ToLower();

    public static Subject FromKey(string key)
    {
        bool wild = key.Length > 0 && key[0] == 'c';
        int.TryParse(key.Substring(1), out int kind);

        return new Subject { Wild = wild, Kind = kind };
    }

    public static Subject[] All()
    {
        // Counted, not written down. Told there were four of each, a fifth
        // creature would have lived in the world without ever reaching the book.
        int creatures = global::Fauna.Count;
        int structures = global::Landmarks.Count;

        var all = new Subject[creatures + structures];

        for (int i = 0; i < creatures; i++) all[i] = Creature((FaunaKind)i);
        for (int i = 0; i < structures; i++) all[creatures + i] = Structure((LandmarkKind)i);

        return all;
    }
}
