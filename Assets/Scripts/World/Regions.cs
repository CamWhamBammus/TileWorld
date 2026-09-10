using System.Collections.Generic;
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

    public enum Character { Lowland, Forest, Water, Hills, Peaks, Fungal, Desert, Snow, Stone, Dead, Reed, Reef, Jungle, Savanna }

    /// <summary>
    /// Whether a region is open sea rather than an inland pool. A reef is a
    /// stretch of sea that happens to be coral, so everything the sea does --
    /// a sand strand above the tide, the wash running up it, gulls, crabs,
    /// wrecks -- belongs to it as much as to plain water.
    /// </summary>
    public static bool Sea(Character character) => character == Character.Water || character == Character.Reef;

    public struct Region
    {
        public Vector2Int Cell;
        public string Name;
        public Character Character;
    }

    private static readonly string[] LowlandNouns = { "Flats", "Meadows", "Green", "Bottom", "Common", "Reach", "Vale", "Furlong" };
    private static readonly string[] ForestNouns = { "Wood", "Thicket", "Forest", "Holt", "Shaw", "Weald", "Coppice", "Stand" };
    private static readonly string[] WaterNouns = { "Mere", "Marsh", "Waters", "Tarns", "Sink", "Fen", "Shallows", "Lough" };
    private static readonly string[] ReefNouns = { "Reef", "Garden", "Shoal", "Coral", "Banks", "Bloom", "Ledges", "Bar" };
    private static readonly string[] JungleNouns = { "Tangle", "Canopy", "Deep", "Green", "Thicket", "Shade", "Reach", "Vault" };
    private static readonly string[] SavannaNouns = { "Plain", "Flats", "Reach", "Grasslands", "Open", "Sweep", "Run", "Wide" };
    private static readonly string[] HillNouns = { "Downs", "Rise", "Fells", "Ridge", "Brow", "Bank", "Scarp", "Shoulder" };
    private static readonly string[] PeakNouns = { "Heights", "Crags", "Spires", "Roof", "Teeth", "Cairns", "Horns", "Summit" };
    private static readonly string[] FungalNouns = { "Rings", "Caps", "Gills", "Blight", "Hollow", "Rot", "Spores", "Damp" };
    private static readonly string[] DesertNouns = { "Sands", "Dunes", "Waste", "Barrens", "Basin", "Drift", "Scour", "Pan" };
    private static readonly string[] SnowNouns = { "Snows", "White", "Drifts", "Cold", "Hush", "Frost", "Blanket", "Winter" };
    private static readonly string[] StoneNouns = { "Stones", "Scree", "Rubble", "Grit", "Shatter", "Flint", "Boulders", "Rake" };
    private static readonly string[] DeadNouns = { "Deadwood", "Standing", "Bones", "Snags", "Kindling", "Ash", "Widows", "Stumps" };
    private static readonly string[] ReedNouns = { "Reeds", "Rushes", "Sedge", "Beds", "Whisper", "Quills", "Bows", "Wet" };

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

    // Snow asks for a region's character once per tile, and working one out
    // samples the ground in a couple of dozen places. Kept, it is a dictionary
    // lookup instead; unkept, it was the most expensive thing in the world.
    private static readonly Dictionary<long, Character> remembered = new Dictionary<long, Character>();
    private static int rememberedFor;

    /// <summary>
    /// How far, in tiles, a border wanders either side of the line between two
    /// cells. Cells are 120 tiles across, so this is a good way short of a
    /// border ever reaching the middle of one.
    /// </summary>
    private const float Wander = 14f;

    /// <summary>
    /// The band, in tiles, either side of the wandering line in which tiles are
    /// scattered into the region across it, so the two run into each other
    /// rather than meeting at an edge.
    /// </summary>
    private const float Fray = 13f;

    /// <summary>How wide the band of mixed ground either side of a border is, in tiles.</summary>
    private const float Blend = 8f;

    /// <summary>Tiles across one region.</summary>
    private const int TilesAcross = ChunksAcross * WorldGrid.TilesPerChunk;

    /// <summary>
    /// The cell a tile belongs to. Not simply the one it sits in: a region's
    /// borders were the edges of its cell, dead straight for a hundred and
    /// twenty tiles, and a snowfield ending on a ruled line looks like a map
    /// and not like ground. So the tile is first moved by a slow noise, and
    /// the cell asked for is the one under where it moved to. Every tile asks
    /// the same question of the same noise, so neighbours agree and the
    /// border becomes a line that wanders instead of one that is ruled.
    ///
    /// With fray, tiles close to that line are then scattered across it by
    /// hash, most often right on the line and not at all a few tiles back.
    /// Without, the line is a line -- for things that speak for a whole chunk,
    /// like its name, and cannot be speckled.
    /// </summary>
    public static Vector2Int CellOfTile(int tileX, int tileZ, int worldSeed, bool fray)
    {
        float o = 3000f + (worldSeed % 733) * 2.13f;
        const float scale = 1f / 38f;

        float wx = tileX + (Mathf.PerlinNoise(o + tileX * scale, o + tileZ * scale) - 0.5f) * 2f * Wander;
        float wz = tileZ + (Mathf.PerlinNoise(o + 77f + tileX * scale, o + 77f + tileZ * scale) - 0.5f) * 2f * Wander;

        int cx = Mathf.FloorToInt(wx / TilesAcross);
        int cz = Mathf.FloorToInt(wz / TilesAcross);

        if (!fray) return new Vector2Int(cx, cz);

        // how far into the cell the moved tile lies, and so how near an edge
        float inX = wx - cx * TilesAcross;
        float inZ = wz - cz * TilesAcross;

        float toX = Mathf.Min(inX, TilesAcross - inX);
        float toZ = Mathf.Min(inZ, TilesAcross - inZ);

        bool acrossX = toX <= toZ;
        float near = acrossX ? toX : toZ;

        if (near >= Fray) return new Vector2Int(cx, cz);

        // Half the tiles on the line itself, none at the edge of the band -- eased, so the
        // change is slow at both ends of the band rather than a straight ramp.
        float across = 1f - near / Fray;
        float chance = across * across * (3f - 2f * across) * 0.5f;

        // Which tiles change hands is decided by a noise field rather than by a hash of the
        // tile. A hash gives every tile its own coin, so a border comes out as salt and
        // pepper: single tiles of forest standing in sand, which reads as speckle and not as
        // a border at all. One field over the whole world hands them over in patches and
        // fingers instead, so the two grounds interlock the way they do on the way out of a
        // wood. A little of the hash is kept in so the fingers have ragged edges.
        // Two scales of it, so the fingers have fingers of their own.
        float grain = Mathf.PerlinNoise(o + 411f + tileX * 0.075f, o + 411f + tileZ * 0.075f) * 0.68f
                    + Mathf.PerlinNoise(o + 913f + tileX * 0.21f, o + 913f + tileZ * 0.21f) * 0.32f;
        grain = Mathf.Clamp01((grain - 0.5f) * 2.4f + 0.5f);

        // Nearly half of it left to the tile's own coin. All noise and the patches come out
        // with clean edges, which is a staircase rather than a border; all coin and it is salt
        // and pepper. The mix gives patches that interlock and fray into each other.
        float roll = grain * 0.56f + ((uint)Hash(tileX, tileZ, worldSeed + 5557) % 1000) / 1000f * 0.44f;

        if (roll >= chance) return new Vector2Int(cx, cz);

        if (acrossX) return new Vector2Int(inX < TilesAcross - inX ? cx - 1 : cx + 1, cz);

        return new Vector2Int(cx, inZ < TilesAcross - inZ ? cz - 1 : cz + 1);
    }

    /// <summary>
    /// The country on the other side of the nearest border, and how near the line this tile
    /// lies: nought at the outer edge of the band and one on the line itself. Nought means
    /// there is no border within reach, and <paramref name="other"/> is then meaningless.
    ///
    /// This repeats the wander that <see cref="CellOfTile"/> does, two noise lookups, rather
    /// than returning it from there: the ground asks this once per tile and the cell it lands
    /// in is remembered, so the cost is the noise and nothing else.
    /// </summary>
    public static float Border(int tileX, int tileZ, int worldSeed, out Character other)
    {
        other = Character.Lowland;

        float o = 3000f + (worldSeed % 733) * 2.13f;
        const float scale = 1f / 38f;

        float wx = tileX + (Mathf.PerlinNoise(o + tileX * scale, o + tileZ * scale) - 0.5f) * 2f * Wander;
        float wz = tileZ + (Mathf.PerlinNoise(o + 77f + tileX * scale, o + 77f + tileZ * scale) - 0.5f) * 2f * Wander;

        int cx = Mathf.FloorToInt(wx / TilesAcross);
        int cz = Mathf.FloorToInt(wz / TilesAcross);

        float inX = wx - cx * TilesAcross;
        float inZ = wz - cz * TilesAcross;

        float toX = Mathf.Min(inX, TilesAcross - inX);
        float toZ = Mathf.Min(inZ, TilesAcross - inZ);

        bool acrossX = toX <= toZ;
        float near = acrossX ? toX : toZ;

        if (near >= Blend) return 0f;

        var neighbour = acrossX
            ? new Vector2Int(inX < TilesAcross - inX ? cx - 1 : cx + 1, cz)
            : new Vector2Int(cx, inZ < TilesAcross - inZ ? cz - 1 : cz + 1);

        other = CharacterOfCell(neighbour, worldSeed);

        return 1f - near / Blend;
    }

    /// <summary>What the ground is like at one tile.</summary>
    public static Character CharacterAtTile(int tileX, int tileZ, int worldSeed)
    {
        return CharacterOfCell(CellOfTile(tileX, tileZ, worldSeed, true), worldSeed);
    }

    /// <summary>
    /// The same, but with the border a line rather than a scatter. For what
    /// must not speckle: a lake that lies across a border got reeds on every
    /// tile the fray happened to hand to the other side.
    /// </summary>
    public static Character CharacterAtTile(int tileX, int tileZ, int worldSeed, bool fray)
    {
        return CharacterOfCell(CellOfTile(tileX, tileZ, worldSeed, fray), worldSeed);
    }

    /// <summary>
    /// What a chunk is like, taken at its middle. Only the middle: the edges of
    /// a chunk can lie across a border now, and a chunk has to be one thing
    /// for the sake of everything that names it.
    /// </summary>
    public static Character CharacterAt(Vector2Int chunk, int worldSeed)
    {
        return CharacterOfCell(CellOfChunk(chunk, worldSeed), worldSeed);
    }

    private static Vector2Int CellOfChunk(Vector2Int chunk, int worldSeed)
    {
        return CellOfTile(
            chunk.x * WorldGrid.TilesPerChunk + WorldGrid.TilesPerChunk / 2,
            chunk.y * WorldGrid.TilesPerChunk + WorldGrid.TilesPerChunk / 2,
            worldSeed, false);
    }

    /// <summary>What a region is like, worked out once and then kept.</summary>
    private static Character CharacterOfCell(Vector2Int cell, int worldSeed)
    {
        if (rememberedFor != worldSeed)
        {
            remembered.Clear();
            rememberedFor = worldSeed;
        }

        long key = ((long)cell.x << 32) ^ (uint)cell.y;

        if (remembered.TryGetValue(key, out var known)) return known;

        var worked = CharacterOf(cell, worldSeed);
        remembered[key] = worked;

        return worked;
    }

    public static Region At(Vector2Int chunk, int worldSeed)
    {
        Vector2Int cell = CellOfChunk(chunk, worldSeed);

        var region = new Region { Cell = cell };
        region.Character = CharacterOfCell(cell, worldSeed);
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
        int wet = 0, snowy = 0, deep = 0, samples = 0;

        for (int x = 0; x < span; x += 12)
        for (int z = 0; z < span; z += 12)
        {
            int tileX = originX + x;
            int tileZ = originZ + z;

            relief += WorldHeight.HeightAt(tileX, tileZ, worldSeed) / WorldHeight.MaxRelief;

            if (WaterSurface.IsUnderwater(tileX, tileZ, worldSeed)) wet++;
            if (WaterSurface.Level - WorldHeight.SurfaceY(tileX, tileZ, worldSeed) >= Chunk.ReefDepth) deep++;
            if (SnowCover.SnowByHeight(tileX, tileZ, worldSeed)) snowy++;

            samples++;
        }

        relief /= Mathf.Max(1, samples);

        float wetShare = wet / (float)Mathf.Max(1, samples);
        float deepShare = deep / (float)Mathf.Max(1, samples);
        float snowShare = snowy / (float)Mathf.Max(1, samples);

        // Ordered by how much a feature dominates the impression of a place:
        // standing water and snow are what you would name it for.
        // Water names were going to nearly a quarter of regions when only
        // about a twentieth of the ground is actually under water, so a region
        // has to be properly wet before it is named for it.
        // A reef is asked of the wettest regions before they are named for
        // their water. It wants low warm ground with no snow near it, and it
        // wants water with a middle to it: a reef needs somewhere the coral can
        // stand without standing out of the top of the sea, so a region whose
        // water is all shin-deep is left as plain water. One in five of those
        // that qualify, since a coast of nothing but coral is a coast with
        // nothing to find.
        if (wetShare > 0.22f)
            return relief < 0.30f && snowShare <= 0f && deepShare > 0.10f
                && Hash(cell.x, cell.y, worldSeed + 5309) % 5 == 0
                 ? Character.Reef : Character.Water;
        if (snowShare > 0.16f) return Character.Peaks;

        // The fungus is asked before the sand. Both want low ground and the
        // sand will take a great deal of it, so asked the other way round the
        // sand was swallowing woods that should have been mushrooms. Rare on
        // purpose all the same: a place you meet every few regions is one you
        // remember, and one you meet constantly is only scenery.
        // A plain that never thaws, well below the height snow would keep at.
        if (relief > 0.14f && relief < 0.56f
            && Hash(cell.x, cell.y, worldSeed + 4441) % 9 == 0) return Character.Snow;

        // Damp ground short of open water, where the reeds stand.
        if (wetShare > 0.09f && Hash(cell.x, cell.y, worldSeed + 8821) % 3 == 0) return Character.Reed;

        // Jungle: low warm ground with water somewhere in it, and nothing
        // frozen anywhere near it -- a region averaging a third of the relief
        // still has summits in it, and a canopy running up into snow is two
        // countries in one. Asked before the mushrooms and well before the
        // sand, both of which want the same ground and would take it all.
        if (relief < 0.20f && wetShare > 0.02f && snowShare <= 0f
            && Hash(cell.x, cell.y, worldSeed + 1187) % 2 == 0) return Character.Jungle;

        if (relief < 0.44f && Hash(cell.x, cell.y, worldSeed + 7717) % 7 == 0) return Character.Fungal;

        // Woods that died standing.
        if (relief < 0.50f && Hash(cell.x, cell.y, worldSeed + 2237) % 9 == 0) return Character.Dead;

        // Bare rock, where nothing has got a hold.
        if (relief > 0.22f && Hash(cell.x, cell.y, worldSeed + 6163) % 7 == 0) return Character.Stone;

        // Savanna: low, dry and open, the same ground the sand wants, so it is asked first
        // and takes a share of what would otherwise all have been desert.
        if (relief < 0.46f && wetShare < 0.20f && snowShare <= 0f
            && Hash(cell.x, cell.y, worldSeed + 4523) % 3 == 0) return Character.Savanna;

        // And then sand, wherever is low, dry and open enough to take it.
        // There is a good deal of it: somewhere you have to hunt for is not a
        // biome, it is a rumour.
        if (relief < 0.52f && wetShare < 0.16f
            && Hash(cell.x, cell.y, worldSeed + 3391) % 2 == 0) return Character.Desert;
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
            Character.Reef => ReefNouns,
            Character.Jungle => JungleNouns,
            Character.Savanna => SavannaNouns,
            Character.Peaks => PeakNouns,
            Character.Hills => HillNouns,
            Character.Forest => ForestNouns,
            Character.Fungal => FungalNouns,
            Character.Desert => DesertNouns,
            Character.Snow => SnowNouns,
            Character.Stone => StoneNouns,
            Character.Dead => DeadNouns,
            Character.Reed => ReedNouns,
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
            case Character.Reef: return "coral in warm shallows";
            case Character.Jungle: return "jungle under a closed canopy";
            case Character.Savanna: return "dry grassland under flat-topped trees";
            case Character.Peaks: return "snow and bare rock";
            case Character.Hills: return "high ground";
            case Character.Forest: return "deep forest";
            case Character.Fungal: return "mushrooms under a dark wood";
            case Character.Desert: return "open sand";
            case Character.Snow: return "snow that never leaves";
            case Character.Stone: return "bare rock and scree";
            case Character.Dead: return "a wood that died standing";
            case Character.Reed: return "reeds in standing water";
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
