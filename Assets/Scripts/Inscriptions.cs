using UnityEngine;

/// <summary>
/// Something written at each landmark. Reaching one gave a diamond on the map
/// and a tick in a list; nothing about the place itself was worth arriving for.
///
/// Lines are assembled from the structure, the region it stands in and a hash,
/// so a given ruin always carries the same words, and those words are about
/// where it actually is.
/// </summary>
public static class Inscriptions
{
    private static readonly string[] HouseLines =
    {
        "Scratched by the door: \"{0}. Twelve winters. Enough.\"",
        "A tally cut into the beam, and then no more of them.",
        "Someone kept a list of names here. The last is unfinished.",
        "Chalked inside the chimney: \"the water rises in spring\"."
    };

    private static readonly string[] TowerLines =
    {
        "Cut into the lintel: \"watch kept over {0}\".",
        "A soldier's name, and a date, and nothing after it.",
        "Someone counted the days on this wall. They stopped at ninety.",
        "Above the stair: \"we saw them come from the west\"."
    };

    private static readonly string[] CircleLines =
    {
        "The altar is worn smooth. Older than {0}, older than the forest.",
        "Marks on the stones line up with something that is no longer there.",
        "One stone is newer than the rest, and badly cut.",
        "Nothing is written here. That may be the point."
    };

    private static readonly string[] WatchtowerLines =
    {
        "A logbook, swollen shut. The cover reads {0}.",
        "Notches on the rail, one for each day of a long watch.",
        "\"Relieved at dawn\" carved above the ladder. No one came.",
        "A lantern hook, and the burn mark under it."
    };

    public static string For(Vector2Int chunk, LandmarkKind kind, int worldSeed)
    {
        var region = Regions.At(chunk, worldSeed);

        string[] lines = kind switch
        {
            LandmarkKind.AbandonedHouse => HouseLines,
            LandmarkKind.RuinedTower => TowerLines,
            LandmarkKind.StoneCircle => CircleLines,
            _ => WatchtowerLines
        };

        int hash = Hash(chunk.x, chunk.y, worldSeed ^ 0x51F3);

        return string.Format(lines[hash % lines.Length], region.Name);
    }

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
