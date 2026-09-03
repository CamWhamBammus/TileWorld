using UnityEngine;

/// <summary>
/// The world in named pieces. Everything so far has been terrain and geometry,
/// which means nowhere in it is anywhere in particular: a chart of unnamed
/// squares is a shape, not a map of somewhere.
///
/// A region's name comes from what its ground is actually like, so a name is a
/// description rather than decoration, and like everything else here it is a
/// function of position and seed rather than anything stored.
/// </summary>
public static class Regions
{
    /// <summary>Chunks across one region. About 240 metres, a few minutes' walk.</summary>
    public const int ChunksAcross = 8;

    public enum Character { Lowland, Forest, Water, Hills, Peaks, Fungal, Desert }

    public struct Region
    {
        public Vector2Int Cell;
        public string Name;
        public Character Character;
    }

    private static readonly string[] LowlandNouns = { "Flats", "Meadows", "Green", "Bottom", "Common", "Reach", "Vale", "Furlong" };
    private static readonly string[] ForestNouns = { "Wood", "Thicket", "Forest", "Holt", "Shaw", "Weald", "Coppice", "Stand" };
    private static readonly string[] WaterNouns = { "Mere", "Marsh", "Waters", "Tarns", "Sink", "Fen", "Shallows", "Lough" };
    private static readonly string[] HillNouns = { "Downs", "Rise", "Fells", "Ridge", "Brow", "Bank", "Scarp", "Shoulder" };
    private static readonly string[] PeakNouns = { "Heights", "Crags", "Spires", "Roof", "Teeth", "Cairns", "Horns", "Summit" };
    private static readonly string[] FungalNouns = { "Rings", "Caps", "Gills", "Blight", "Hollow", "Rot", "Spores", "Damp" };
    private static readonly string[] DesertNouns = { "Sands", "Dunes", "Waste", "Barrens", "Basin", "Drift", "Scour", "Pan" };

    // Doubled from sixteen: with five nouns and sixteen adjectives, four in ten
    // regions shared a name with another one.
    private static readonly string[] Adjectives =
    {
        "Ashen", "Quiet", "Long", "Old", "Broken", "Hollow", "Pale", "Cold",
        "Far", "Still", "Grey", "Deep", "Bitter", "Low", "Wandering", "Lost",
        "Iron", "Hushed", "Bright", "Sunken", "Weathered", "Narrow", "Wide", "Crooked",
        "Silent", "Frozen", "Withered", "Green", "Black", "White", "Empty", "Thin"
    };

    public static Vector2Int CellOf(Vector2Int chunk)
    {
        return new Vector2Int(
            Mathf.FloorToInt(chunk.x / (float)ChunksAcross),
            Mathf.FloorToInt(chunk.y / (float)ChunksAcross));
    }

    public static Region At(Vector2Int chunk, int worldSeed)
    {
        Vector2Int cell = CellOf(chunk);

        var region = new Region { Cell = cell };
        region.Character = CharacterOf(cell, worldSeed);
        region.Name = NameOf(cell, region.Character, worldSeed);

        return region;
    }

    /// <summary>What the ground in this region is mostly like.</summary>
    private static Character CharacterOf(Vector2Int cell, int worldSeed)
    {
        int originX = cell.x * ChunksAcross * WorldGrid.TilesPerChunk;
        int originZ = cell.y * ChunksAcross * WorldGrid.TilesPerChunk;
        int span = ChunksAcross * WorldGrid.TilesPerChunk;

        float relief = 0f;
        int wet = 0, snowy = 0, samples = 0;

        for (int x = 0; x < span; x += 12)
        for (int z = 0; z < span; z += 12)
        {
            int tileX = originX + x;
            int tileZ = originZ + z;

            relief += WorldHeight.HeightAt(tileX, tileZ, worldSeed) / WorldHeight.MaxRelief;

            if (WaterSurface.IsUnderwater(tileX, tileZ, worldSeed)) wet++;
            if (SnowCover.IsSnowy(tileX, tileZ, worldSeed)) snowy++;

            samples++;
        }

        relief /= Mathf.Max(1, samples);

        float wetShare = wet / (float)Mathf.Max(1, samples);
        float snowShare = snowy / (float)Mathf.Max(1, samples);

        // Ordered by how much a feature dominates the impression of a place:
        // standing water and snow are what you would name it for.
        // Water names were going to nearly a quarter of regions when only
        // about a twentieth of the ground is actually under water, so a region
        // has to be properly wet before it is named for it.
        if (wetShare > 0.22f) return Character.Water;
        if (snowShare > 0.16f) return Character.Peaks;

        // Dry, low and open: sand. Asked before the fungus, which wants the
        // same low ground but wants it damp.
        if (relief < 0.44f && wetShare < 0.12f
            && Hash(cell.x, cell.y, worldSeed + 3391) % 3 == 0) return Character.Desert;

        // Now and then a low wooded region has gone over to fungus. Rare on
        // purpose: a thing you come across every few regions is somewhere you
        // remember, and one you meet constantly is only scenery.
        if (relief < 0.44f && Hash(cell.x, cell.y, worldSeed + 7717) % 7 == 0) return Character.Fungal;
        if (relief > 0.44f) return Character.Hills;
        if (relief > 0.22f) return Character.Forest;

        return Character.Lowland;
    }

    private static string NameOf(Vector2Int cell, Character character, int worldSeed)
    {
        int hash = Hash(cell.x, cell.y, worldSeed);

        string[] nouns = character switch
        {
            Character.Water => WaterNouns,
            Character.Peaks => PeakNouns,
            Character.Hills => HillNouns,
            Character.Forest => ForestNouns,
            Character.Fungal => FungalNouns,
            Character.Desert => DesertNouns,
            _ => LowlandNouns
        };

        // Different parts of the hash for each choice. Taking the adjective
        // from the low bits and the noun from bits four upward overlapped, so
        // the two were correlated and fewer combinations came out than the
        // word lists allow.
        string adjective = Adjectives[hash % Adjectives.Length];
        string noun = nouns[(hash >> 8) % nouns.Length];

        // A few read better as one word, the way real place names do, but only
        // the short ones: "Wanderingheights" does not.
        bool joined = adjective.Length <= 5 && (hash >> 16) % 4 == 0;

        return joined ? adjective + noun.ToLowerInvariant() : "the " + adjective + " " + noun;
    }

    public static string Describe(Character character)
    {
        switch (character)
        {
            case Character.Water: return "standing water";
            case Character.Peaks: return "snow and bare rock";
            case Character.Hills: return "high ground";
            case Character.Forest: return "deep forest";
            case Character.Fungal: return "mushrooms under a dark wood";
            case Character.Desert: return "open sand";
            default: return "open lowland";
        }
    }

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
