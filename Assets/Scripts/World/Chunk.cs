using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One square of the world. Generated once, then never changes — so the tile
/// transforms are baked into flat arrays the renderer can hand straight to the
/// GPU without copying anything per frame.
/// </summary>
public class Chunk
{
    private const int Categories = 7;           // five grass bands, then sand, then stone
    private const int VariantsPerCategory = 5;  // grass tile meshes within a band

    // Only three of the five shade categories contain a treed tile, so height
    // cannot simply be spread across all five: two of the bands would come out
    // bare whatever the altitude. The gradient runs through the treed ones,
    // and the other two are used for ground that should be bare anyway.
    private static readonly int[] ShadeByHeight = { 2, 1, 0 };   // dark, light, pale

    private const int BareSteepCategory = 3;   // Big Grass, no trees: steep faces
    private const int MarshCategory = 4;       // Very Dark, no trees: low flat ground
    private const int SandCategory = 5;        // the sand update, for the deserts
    private const int StoneCategory = 6;       // the pack's stone tiles, for the barrens

    // These four tiles carry a tree. Above the treeline they are swapped out,
    // which is what makes a summit read as a summit.
    private static readonly bool[] CarriesTree = BuildTreeTable();

    private const float TreelineFraction = 0.72f;
    private const float SteepFraction = 0.62f;
    private const float MarshFraction = 0.10f;

    /// <summary>Depth past which a lake bed is rock rather than sand.</summary>
    private const float DeepWater = 1.6f;

    /// <summary>And how far above the water the sand carries on up the shore.</summary>
    private const float BeachHeight = 0.7f;
    private const float BlendNoiseScale = 0.09f;
    private const float BlendWeight = 0.22f;

    private static bool[] BuildTreeTable()
    {
        var table = new bool[Categories * VariantsPerCategory];
        table[4] = table[8] = table[11] = table[13] = true;
        return table;
    }

    /// <summary>Tile transforms grouped by tile id, ready for instanced drawing.</summary>
    public readonly Dictionary<int, Matrix4x4[]> idToTransforms = new Dictionary<int, Matrix4x4[]>();

    /// <summary>Used for render culling, so off-screen chunks cost nothing.</summary>
    public Bounds Bounds { get; private set; }

    public Vector2Int Index { get; private set; }

    public Chunk(Vector2Int chunkIndex, int worldSeed)
    {
        Index = chunkIndex;
        Generate(worldSeed);

        // Tall enough to contain the terraces plus the trees some tiles carry,
        // or the renderer culls chunks that are still partly on screen.
        float span = WorldHeight.MaxRelief + 12f;

        // Generous vertically: a chunk on a mountainside spans a lot of height,
        // and bounds that are too tight cull chunks that are still on screen.
        Bounds = new Bounds(
            WorldGrid.ChunkCenter(chunkIndex) + Vector3.up * (span * 0.25f),
            new Vector3(WorldGrid.ChunkWorldSize, span, WorldGrid.ChunkWorldSize)
        );
    }

    private void Generate(int worldSeed)
    {
        // Asked once for the whole chunk rather than per tile: working out a
        // region's character samples it in a couple of dozen places.
        var character = Regions.At(Index, worldSeed).Character;

        bool fungal = character == Regions.Character.Fungal;
        bool desert = character == Regions.Character.Desert;
        bool stone = character == Regions.Character.Stone;

        // Under a snowfield goes rock, not grass. The grass tiles carry blades
        // standing a third of a unit above the block, and the snow is laid on
        // the block -- so on a grass tile the blades come up through the snow
        // and draw a green fringe over every tile in the field.
        bool underSnow = character == Regions.Character.Snow;

        // Ground that is dark and wet underfoot: the dead woods and the
        // reedbeds both stand on it.
        bool sodden = character == Regions.Character.Dead || character == Regions.Character.Reed;

        // Perlin noise mirrors around 0, so a fixed offset keeps the sampled
        // region firmly positive and stops the world repeating across the axes.
        float offset = 1000f + (worldSeed % 1000) * 7.31f;

        var buckets = new Dictionary<int, List<Matrix4x4>>();

        for (int tx = 0; tx < WorldGrid.TilesPerChunk; tx++)
        for (int tz = 0; tz < WorldGrid.TilesPerChunk; tz++)
        {
            int gx = chunkIndexX() * WorldGrid.TilesPerChunk + tx;
            int gz = chunkIndexZ() * WorldGrid.TilesPerChunk + tz;

            // Height and steepness decide the ground; noise only softens the edge.
            float relief = Mathf.Clamp01(WorldHeight.HeightAt(gx, gz, worldSeed) / WorldHeight.MaxRelief);
            float steep = Mathf.Clamp01(SlopeAt(gx, gz, worldSeed) / 1.2f);

            float wobble = Mathf.PerlinNoise(offset + gx * BlendNoiseScale, offset + gz * BlendNoiseScale) - 0.5f;
            float bare = Mathf.Clamp01(relief + steep * 0.30f + wobble * BlendWeight);

            // Fungus keeps to the dark and the damp, so the ground under it is
            // read as lower and wetter than it is and comes out darker for it.
            if (fungal) bare = Mathf.Clamp01(bare - 0.20f);

            int band = Mathf.Clamp(Mathf.FloorToInt(bare * ShadeByHeight.Length), 0, ShadeByHeight.Length - 1);
            int category = ShadeByHeight[band];

            // Anything under the water line uses the bare dark ground. Left on
            // a forested tile, the tree simply carries on standing and pokes
            // out of the pond, since the water is only a surface over the top.
            bool submerged = WaterSurface.IsUnderwater(gx, gz, worldSeed);

            // How far under, or how far clear. A lake bed was dark grass with
            // the trees taken out of it, which is a drowned field rather than a
            // lake: grass does not grow on a lake bottom and the eye knows it.
            float underBy = WaterSurface.Level - WorldHeight.SurfaceY(gx, gz, worldSeed);

            if (submerged)
            {
                // Sand in the shallows where the light still reaches, rock
                // further down. The line between the two is what makes a lake
                // look as though it has a bottom rather than an edge.
                category = underBy < DeepWater ? SandCategory : StoneCategory;
            }
            else if (-underBy < BeachHeight)
            {
                // and a run of sand above the waterline, so the grass does not
                // simply stop at the water
                category = SandCategory;
            }
            else if (desert)
            {
                // sand over the whole of it, steep faces and all: scree in the
                // middle of a desert reads as a patch of somewhere else
                category = SandCategory;
            }
            else if (stone || underSnow)
            {
                category = StoneCategory;
            }
            else if (sodden)
            {
                category = MarshCategory;
            }
            else if (steep > SteepFraction)
            {
                category = BareSteepCategory;       // scree on the steep faces
            }
            else if (bare < MarshFraction)
            {
                category = MarshCategory;           // dark ground in the low flats
            }

            int variant = Hash2D(gx, gz, worldSeed) % VariantsPerCategory;
            int id = category * VariantsPerCategory + variant;

            // Above the treeline, or under water, swap a treed tile for a bare
            // one of the same shade.
            if (CarriesTree[id] && (bare > TreelineFraction || submerged))
            {
                for (int step = 1; step < VariantsPerCategory; step++)
                {
                    int candidate = category * VariantsPerCategory + (variant + step) % VariantsPerCategory;
                    if (!CarriesTree[candidate]) { id = candidate; break; }
                }
            }

            // The sand tiles are laid wider than the grid so that they meet,
            // which puts every cap through its neighbours. Two surfaces at the
            // same height leave the depth buffer no way to choose between them
            // and it picks differently from frame to frame, which is the
            // flickering across a desert.
            //
            // A hash gave each tile one of seventeen heights, which left about
            // one pair of neighbours in seventeen still level with each other
            // and still flickering. This is not a hash: stepping one tile in
            // either direction changes it by two or three parts in seven, so no
            // tile is ever level with any of the eight around it. Four
            // thousandths of a metre at the widest, which is far too little to
            // see and enough to settle the argument.
            float settle = category == SandCategory
                ? ((gx * 2 + gz * 3) % 7 + 7) % 7 * 0.0006f
                : 0f;

            Vector3 position = new Vector3(
                chunkIndexX() * WorldGrid.ChunkWorldSize + tx * WorldGrid.TileSize,
                WorldHeight.TileYOffset(gx, gz, worldSeed) + settle,
                chunkIndexZ() * WorldGrid.ChunkWorldSize + tz * WorldGrid.TileSize
            );

            // Square floor tiles, so quarter turns are the only rotation that
            // varies the look without opening seams between neighbours.
            int quarterTurns = Hash2D(gx, gz, worldSeed + 977) % 4;
            Quaternion rotation = Quaternion.Euler(0f, quarterTurns * 90f, 0f);

            if (!buckets.TryGetValue(id, out var list))
            {
                list = new List<Matrix4x4>();
                buckets[id] = list;
            }

            list.Add(Matrix4x4.TRS(position, rotation, Vector3.one));
        }

        foreach (var pair in buckets)
        {
            idToTransforms[pair.Key] = pair.Value.ToArray();
        }
    }

    private int chunkIndexX() { return Index.x; }
    private int chunkIndexZ() { return Index.y; }

    /// <summary>Steepest rise to a neighbouring tile.</summary>
    private static float SlopeAt(int gx, int gz, int worldSeed)
    {
        float h = WorldHeight.SurfaceY(gx, gz, worldSeed);

        return Mathf.Max(
            Mathf.Abs(WorldHeight.SurfaceY(gx + 1, gz, worldSeed) - h),
            Mathf.Abs(WorldHeight.SurfaceY(gx, gz + 1, worldSeed) - h));
    }

    private static int Hash2D(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
