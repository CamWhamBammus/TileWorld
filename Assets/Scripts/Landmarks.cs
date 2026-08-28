using UnityEngine;

public enum LandmarkKind
{
    Cairn,          // a stack of stones left by someone who came before
    StandingStones, // a ring, half-fallen
    Obelisk         // one tall marker, visible from a distance
}

/// <summary>
/// Where the landmarks are. Like the terrain, this is a pure function of chunk
/// coordinates and the world seed — nothing is stored, so the same seed always
/// puts the same ruin on the same hill, and a chunk can be asked about long
/// before it is ever loaded.
/// </summary>
public static class Landmarks
{
    /// <summary>
    /// About one chunk in thirty. At 8% they averaged 53 units apart, which is
    /// ten seconds of sprinting — close enough that they stopped being a find.
    /// </summary>
    private const int ChanceInHundred = 3;

    /// <summary>Kept away from chunk edges so a landmark is never cut in half by a seam.</summary>
    private const int EdgeMargin = 3;

    /// <summary>Ground steeper than this is skipped — a ruin on a cliff looks like a bug.</summary>
    private const float MaxLocalRise = 0.6f;

    public struct Placement
    {
        public bool Exists;
        public LandmarkKind Kind;
        public Vector3 Position;
        public Vector2Int Chunk;
        public float Yaw;
    }

    public static Placement In(Vector2Int chunk, int worldSeed)
    {
        var result = new Placement { Exists = false, Chunk = chunk };

        int roll = Hash(chunk.x, chunk.y, worldSeed ^ 0x5F3A) % 100;

        if (roll >= ChanceInHundred)
        {
            return result;
        }

        int span = WorldGrid.TilesPerChunk - EdgeMargin * 2;
        int localX = EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x1234) % span;
        int localZ = EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x9ABC) % span;

        int tileX = chunk.x * WorldGrid.TilesPerChunk + localX;
        int tileZ = chunk.y * WorldGrid.TilesPerChunk + localZ;

        if (LocalRise(tileX, tileZ, worldSeed) > MaxLocalRise)
        {
            return result;   // too steep here; this chunk simply has none
        }

        result.Exists = true;
        result.Kind = (LandmarkKind)(Hash(chunk.x, chunk.y, worldSeed ^ 0x77) % 3);
        result.Yaw = Hash(chunk.x, chunk.y, worldSeed ^ 0xBEEF) % 360;
        result.Position = new Vector3(
            tileX * WorldGrid.TileSize,
            WorldHeight.SurfaceY(tileX, tileZ, worldSeed),
            tileZ * WorldGrid.TileSize
        );

        return result;
    }

    /// <summary>Steepest step to a neighbouring tile.</summary>
    private static float LocalRise(int tileX, int tileZ, int seed)
    {
        float h = WorldHeight.SurfaceY(tileX, tileZ, seed);

        float rise = 0f;
        rise = Mathf.Max(rise, Mathf.Abs(WorldHeight.SurfaceY(tileX + 1, tileZ, seed) - h));
        rise = Mathf.Max(rise, Mathf.Abs(WorldHeight.SurfaceY(tileX - 1, tileZ, seed) - h));
        rise = Mathf.Max(rise, Mathf.Abs(WorldHeight.SurfaceY(tileX, tileZ + 1, seed) - h));
        rise = Mathf.Max(rise, Mathf.Abs(WorldHeight.SurfaceY(tileX, tileZ - 1, seed) - h));

        return rise;
    }

    public static string NameOf(LandmarkKind kind)
    {
        switch (kind)
        {
            case LandmarkKind.Cairn: return "Cairn";
            case LandmarkKind.StandingStones: return "Standing Stones";
            default: return "Obelisk";
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
