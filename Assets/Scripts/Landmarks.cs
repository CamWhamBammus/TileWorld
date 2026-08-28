using UnityEngine;

public enum LandmarkKind
{
    AbandonedHouse, // roof half gone, chimney still standing
    RuinedTower,    // a broken stair of stone, tall enough to see over the trees
    StoneCircle,    // a ring around an altar, older than the forest
    Watchtower      // timber legs and a platform someone kept watch from
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
    /// Most rolls are rejected by the flatness test below, so this is a ceiling
    /// rather than the real rate: 12 here yields about 3.2% of chunks, which
    /// measured out at 106 units apart — roughly twenty seconds of running, so
    /// you usually have one in view without the horizon being cluttered.
    /// </summary>
    private const int ChanceInHundred = 12;

    /// <summary>Kept away from chunk edges so a landmark is never cut in half by a seam.</summary>
    private const int EdgeMargin = 4;

    /// <summary>
    /// How flat the ground has to be across the whole footprint. These are
    /// buildings, not markers: a house is five tiles wide, so checking only the
    /// tile under its centre would leave corners hanging in the air or buried.
    /// </summary>
    private const int FootprintTiles = 3;
    private const float MaxFootprintVariation = 0.8f;

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

        if (FootprintVariation(tileX, tileZ, worldSeed) > MaxFootprintVariation)
        {
            return result;   // ground is not flat enough to build on here
        }

        result.Exists = true;
        result.Kind = (LandmarkKind)(Hash(chunk.x, chunk.y, worldSeed ^ 0x77) % 4);
        result.Yaw = Hash(chunk.x, chunk.y, worldSeed ^ 0xBEEF) % 360;
        result.Position = new Vector3(
            tileX * WorldGrid.TileSize,
            WorldHeight.SurfaceY(tileX, tileZ, worldSeed),
            tileZ * WorldGrid.TileSize
        );

        return result;
    }

    /// <summary>Height range across the footprint the structure will stand on.</summary>
    private static float FootprintVariation(int tileX, int tileZ, int seed)
    {
        float lo = float.MaxValue;
        float hi = float.MinValue;

        for (int dx = -FootprintTiles; dx <= FootprintTiles; dx++)
        for (int dz = -FootprintTiles; dz <= FootprintTiles; dz++)
        {
            float h = WorldHeight.SurfaceY(tileX + dx, tileZ + dz, seed);
            lo = Mathf.Min(lo, h);
            hi = Mathf.Max(hi, h);
        }

        return hi - lo;
    }

    public static string NameOf(LandmarkKind kind)
    {
        switch (kind)
        {
            case LandmarkKind.AbandonedHouse: return "Abandoned House";
            case LandmarkKind.RuinedTower: return "Ruined Tower";
            case LandmarkKind.StoneCircle: return "Stone Circle";
            default: return "Watchtower";
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
