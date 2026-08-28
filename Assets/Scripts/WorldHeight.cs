using UnityEngine;

/// <summary>
/// The shape of the land. A pure function of tile coordinates — no state, no
/// storage — so any system can ask how high the ground is anywhere in the
/// world and get the same answer, including across chunk borders.
/// </summary>
public static class WorldHeight
{
    /// <summary>
    /// Vertical distance between terraces. Capped by the player's
    /// CharacterController step offset (0.25) so no ledge is ever too tall to
    /// walk up — terrain is traversable by construction, not by luck.
    /// </summary>
    public const float StepHeight = 0.25f;

    /// <summary>
    /// Number of terraces above the base plane. Relief and steepness rise
    /// together, so raising this without widening the noise below would create
    /// ledges taller than a single step — which the player could not climb.
    /// </summary>
    public const int MaxLevel = 20;

    /// <summary>Height of the flat ground the scene was built around.</summary>
    public const float BaseSurfaceY = 1.05f;

    private const float HillScale = 0.0072f;  // broad hills and valleys
    private const float RidgeScale = 0.019f;  // secondary shaping
    private const float RidgeWeight = 0.35f;

    /// <summary>Continuous terrain height in levels, before terracing.</summary>
    public static float LevelAt(int tileX, int tileZ, int worldSeed)
    {
        float offset = 500f + (worldSeed % 977) * 3.77f;

        float hills = Mathf.PerlinNoise(
            offset + tileX * HillScale,
            offset + tileZ * HillScale);

        float ridges = Mathf.PerlinNoise(
            offset + tileX * RidgeScale,
            offset + tileZ * RidgeScale);

        float n = Mathf.Clamp01(hills * (1f - RidgeWeight) + ridges * RidgeWeight);

        // Pushed towards the low end so valleys and flats dominate and peaks
        // stay rare — an evenly distributed height field reads as noise.
        n = n * n * (3f - 2f * n);

        return n * MaxLevel;
    }

    /// <summary>The terrace a tile sits on.</summary>
    public static int TerraceAt(int tileX, int tileZ, int worldSeed)
    {
        return Mathf.Clamp(Mathf.FloorToInt(LevelAt(tileX, tileZ, worldSeed)), 0, MaxLevel);
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
}
