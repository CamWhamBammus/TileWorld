using UnityEngine;

/// <summary>
/// Something written at each landmark. Reaching one gave a diamond on the map
/// and a tick in a list; nothing about the place itself was worth arriving for.
///
/// Lines are assembled from the structure, the region it stands in and a hash,
/// so a given watch always carries the same words, and those words are about
/// where it actually is.
/// </summary>
public static class Inscriptions
{
    private static readonly string[] WatchLines =
    {
        "Cut into the signboard: \"{0}. Fire watch. Ring twice.\"",
        "A tally on the rail, one notch a day, and a season's worth of them.",
        "Chalked on the tower door: \"gone down for water, back by dark\".",
        "Above the stair: \"we saw the smoke from here first\".",
        "The lamp has been filled. Someone still comes up.",
        "A logbook in the chest, swollen shut. The cover reads {0}."
    };

    public static string For(Vector2Int chunk, LandmarkKind kind, int worldSeed)
    {
        var region = Regions.At(chunk, worldSeed);

        string[] lines = kind switch
        {
            _ => WatchLines
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
