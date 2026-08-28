using UnityEngine;

/// <summary>
/// The shape of the land: continents of plains, ridged mountain ranges, and
/// the valleys between them. A pure function of tile coordinates — no state,
/// no storage — so any system can ask how high the ground is anywhere and get
/// the same answer, including across chunk borders.
///
/// What limits the terrain is the player's CharacterController slope limit
/// (45 degrees), not its step offset: the collision surface ramps between tile
/// centres rather than following the visual terraces, so it contains no
/// vertical ledges to step over. Over a 2 unit tile, 45 degrees allows a rise
/// of 2.0 per tile. MaxRisePerTile stays well inside that.
/// </summary>
public static class WorldHeight
{
    /// <summary>Height of the flat ground the scene was built around.</summary>
    public const float BaseSurfaceY = 1.05f;

    /// <summary>Vertical quantum. Terrain is terraced to this so tiles stay level.</summary>
    public const float StepHeight = 0.25f;

    /// <summary>Peak height above the valley floor, in world units.</summary>
    public const float MaxRelief = 90f;

    /// <summary>
    /// Ceiling on the rise between neighbouring tiles, and the reason the
    /// ranges are as wide as they are. Two limits bind: the collision ramp must
    /// stay under the 45 degree slope limit (2.0 per 2 unit tile), and the step
    /// must stay shallower than a tile block is deep (about 2.0) or a raised
    /// tile shows daylight under its edge. Measured maximum with the current
    /// constants is 1.25, which is 32 degrees.
    /// </summary>
    public const float MaxRisePerTile = 1.5f;

    // Where mountains are allowed to exist at all — huge, slow regions.
    private const float ContinentScale = 0.0016f;

    // The ranges themselves. Ridged noise gives sharp crests instead of blobs.
    private const float RidgeScale = 0.0022f;

    // Rolling ground that shapes the lowlands.
    private const float HillScale = 0.011f;
    private const float HillAmplitude = 6.0f;

    /// <summary>Terrain height above the base plane, in world units.</summary>
    public static float HeightAt(int tileX, int tileZ, int worldSeed)
    {
        float o = 500f + (worldSeed % 977) * 3.77f;

        // Which parts of the world are mountainous. Squared so that most of the
        // map stays low and ranges feel like a feature, not the default.
        float continent = Fbm(tileX * ContinentScale, tileZ * ContinentScale, o, 3);
        float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.62f, continent));
        mountainMask *= mountainMask;

        float ridges = RidgedFbm(tileX * RidgeScale, tileZ * RidgeScale, o + 91f, 4);
        float hills = Fbm(tileX * HillScale, tileZ * HillScale, o + 311f, 2);

        return mountainMask * ridges * MaxRelief + hills * HillAmplitude;
    }

    /// <summary>The terrace a tile sits on.</summary>
    public static int TerraceAt(int tileX, int tileZ, int worldSeed)
    {
        return Mathf.Max(0, Mathf.FloorToInt(HeightAt(tileX, tileZ, worldSeed) / StepHeight));
    }

    /// <summary>World Y of a tile's walking surface.</summary>
    public static float SurfaceY(int tileX, int tileZ, int worldSeed)
    {
        return BaseSurfaceY + TerraceAt(tileX, tileZ, worldSeed) * StepHeight;
    }

    /// <summary>How far a tile is raised above the base plane.</summary>
    public static float TileYOffset(int tileX, int tileZ, int worldSeed)
    {
        return TerraceAt(tileX, tileZ, worldSeed) * StepHeight;
    }

    private static float Fbm(float x, float z, float offset, int octaves)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(offset + x * freq, offset + z * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }

        return sum / norm;
    }

    /// <summary>Folded noise — the crease becomes a ridge line rather than a bump.</summary>
    private static float RidgedFbm(float x, float z, float offset, int octaves)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(offset + x * freq, offset + z * freq);
            n = 1f - Mathf.Abs(n * 2f - 1f);
            sum += n * n * amp;
            norm += amp;
            amp *= 0.45f;
            freq *= 2.1f;
        }

        return sum / norm;
    }
}
