using UnityEngine;

public enum LandmarkKind
{
    ForestersWatch   // a tower on a raised square of ground, deep in the woods
}

/// <summary>
/// Where the landmarks are. Like the terrain, this is a pure function of chunk
/// coordinates and the world seed -- nothing is stored, so the same seed always
/// puts the same watch on the same rise, and a chunk can be asked about long
/// before it is ever loaded.
///
/// Each kind belongs to one kind of country, and is only ever found there.
/// </summary>
public static class Landmarks
{
    /// <summary>One structure, entire, the same way a creature is.</summary>
    public struct Kind
    {
        public string Name;
        public Regions.Character Country;   // the only region it is built in
        public int SurveyRadius;            // how far it charts, in chunks
        public float SurveyHeight;          // how far up you have to get before it counts
        public string Where;                // where the guide wants it found
        public float LabelHeight;           // how far above it the map writes its name
    }

    private static readonly Kind[] kinds =
    {
        new Kind { Name = "Forester's Watch", Country = Regions.Character.Forest,
                   SurveyRadius = 5, SurveyHeight = 1.5f,
                   Where = "in the woods, its tower above the trees", LabelHeight = 9f },
    };

    /// <summary>How many kinds there are, so nothing has to be told twice.</summary>
    public static int Count => kinds.Length;

    static Landmarks()
    {
        int named = System.Enum.GetValues(typeof(LandmarkKind)).Length;

        if (kinds.Length != named)
        {
            Debug.LogError("[Landmarks] " + named + " kinds are named but " + kinds.Length
                + " are described. Every kind in LandmarkKind needs its entry, in the same order.");
        }
    }

    public static Kind All(LandmarkKind kind) => kinds[Mathf.Clamp((int)kind, 0, kinds.Length - 1)];

    /// <summary>
    /// A ceiling rather than the real rate: most rolls are turned down by the
    /// ground, which has to be nearly level under the whole footprint, and by
    /// the country, since each kind is only built in its own.
    /// </summary>
    private const int ChanceInHundred = 22;

    /// <summary>Kept away from chunk edges so a landmark is never cut in half by a seam.</summary>
    private const int EdgeMargin = 5;

    /// <summary>
    /// The platform is three tiles square, and the stair reaches two more to
    /// one side. The ground under the platform has to be level to within one
    /// terrace step, which the grass cap's overhang hides; under the stair it
    /// may fall a little further.
    /// </summary>
    public const int PlatformHalf = 1;
    public const int StairReach = 2;
    private const float PlatformVariation = 0.26f;
    private const float ApronVariation = 0.8f;

    public struct Placement
    {
        public bool Exists;
        public LandmarkKind Kind;
        public Vector3 Position;
        public Vector2Int Chunk;
        public float Yaw;
        public int TileX, TileZ;
    }

    public static Placement In(Vector2Int chunk, int worldSeed)
    {
        var result = new Placement { Exists = false, Chunk = chunk };

        int roll = Hash(chunk.x, chunk.y, worldSeed ^ 0x5F3A) % 100;

        if (roll >= ChanceInHundred) return result;

        var kind = (LandmarkKind)(Hash(chunk.x, chunk.y, worldSeed ^ 0x77) % kinds.Length);

        // only in its own country -- asked of the chunk's middle, the way the
        // chunk's name is
        if (Regions.CharacterAt(chunk, worldSeed) != All(kind).Country) return result;

        int span = WorldGrid.TilesPerChunk - EdgeMargin * 2;
        int localX = EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x1234) % span;
        int localZ = EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x9ABC) % span;

        int tileX = chunk.x * WorldGrid.TilesPerChunk + localX;
        int tileZ = chunk.y * WorldGrid.TilesPerChunk + localZ;

        // Square to the grid: the tiles it is built from are.
        int turns = Hash(chunk.x, chunk.y, worldSeed ^ 0xBEEF) % 4;

        if (!Level(tileX, tileZ, PlatformHalf, PlatformVariation, worldSeed)) return result;
        if (!Level(tileX, tileZ, PlatformHalf + StairReach, ApronVariation, worldSeed)) return result;
        if (Wet(tileX, tileZ, PlatformHalf + StairReach + 1, worldSeed)) return result;

        result.Exists = true;
        result.Kind = kind;
        result.Yaw = turns * 90f;
        result.TileX = tileX;
        result.TileZ = tileZ;
        result.Position = new Vector3(
            tileX * WorldGrid.TileSize,
            WorldHeight.SurfaceY(tileX, tileZ, worldSeed),
            tileZ * WorldGrid.TileSize);

        return result;
    }

    /// <summary>
    /// Whether a tile is under a structure, so that nothing is planted on it
    /// and no tile with a tree on it is laid there: a tree up through the
    /// middle of the platform is what the old ruins had.
    /// </summary>
    public static bool Occupies(int tileX, int tileZ, int worldSeed)
    {
        var chunk = new Vector2Int(
            Mathf.FloorToInt(tileX / (float)WorldGrid.TilesPerChunk),
            Mathf.FloorToInt(tileZ / (float)WorldGrid.TilesPerChunk));

        var at = In(chunk, worldSeed);
        if (!at.Exists) return false;

        int dx = tileX - at.TileX;
        int dz = tileZ - at.TileZ;

        // the platform, and the strip the stair comes down, which is on the
        // structure's +x side before it is turned
        int ahead = Mathf.RoundToInt(at.Yaw) switch
        {
            90 => dz,
            180 => -dx,
            270 => -dz,
            _ => dx
        };
        int aside = Mathf.RoundToInt(at.Yaw) switch
        {
            90 => -dx,
            180 => -dz,
            270 => dx,
            _ => dz
        };

        if (Mathf.Abs(ahead) <= PlatformHalf && Mathf.Abs(aside) <= PlatformHalf) return true;

        return ahead > PlatformHalf && ahead <= PlatformHalf + StairReach + 1 && Mathf.Abs(aside) <= 1;
    }

    private static bool Level(int tileX, int tileZ, int half, float allow, int seed)
    {
        float lo = float.MaxValue, hi = float.MinValue;

        for (int dx = -half; dx <= half; dx++)
        for (int dz = -half; dz <= half; dz++)
        {
            float h = WorldHeight.SurfaceY(tileX + dx, tileZ + dz, seed);
            lo = Mathf.Min(lo, h);
            hi = Mathf.Max(hi, h);
        }

        return hi - lo <= allow;
    }

    private static bool Wet(int tileX, int tileZ, int half, int seed)
    {
        for (int dx = -half; dx <= half; dx++)
        for (int dz = -half; dz <= half; dz++)
            if (WaterSurface.IsUnderwater(tileX + dx, tileZ + dz, seed)) return true;

        return false;
    }

    public static int SurveyRadius(LandmarkKind kind) => All(kind).SurveyRadius;
    public static float SurveyHeight(LandmarkKind kind) => All(kind).SurveyHeight;
    public static string NameOf(LandmarkKind kind) => All(kind).Name;

    /// <summary>Where the guide wants it found.</summary>
    public static string Country(LandmarkKind kind) => All(kind).Where;

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
