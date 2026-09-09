using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One square of the world. Generated once, then never changes — so the tile
/// transforms are baked into flat arrays the renderer can hand straight to the
/// GPU without copying anything per frame.
/// </summary>
public class Chunk
{
    private const int Categories = 21;          // the pack's bands, sand and stone, unused now; then ours: forest floor, three grasses, marsh, beach, desert, stone, scree, snow, fungal, dead, reef, jungle
    private const int VariantsPerCategory = 5;  // grass tile meshes within a band

    // Only three of the five shade categories contain a treed tile, so height
    // cannot simply be spread across all five: two of the bands would come out
    // bare whatever the altitude. The gradient runs through the treed ones,
    // and the other two are used for ground that should be bare anyway.
    private static readonly int[] ShadeByHeight = { DarkGrassCategory, LightGrassCategory, PaleGrassCategory };   // dark, light, pale

    private const int FungalCategory = 17;      // the fungal country's loam, toadstools and glowing caps
    private const int DeadCategory = 18;        // the dead woods' ash, charred wood and bones
    private const int ReefCategory = 19;        // the coral floor of the warm shallows: sand channels, coral heads, seagrass, urchins
    private const int JungleCategory = 20;      // the floor of a closed canopy: rotted leaf, buttress roots, standing water, undergrowth
    private const int SnowCategory = 16;
    private const int FillEarthId = 200, FillRockId = 201;   // plain blocks laid under a tile where the ground drops away
    private const float FillDepth = 2.05f;                // how deep a tile's body is, from its top at 1.05 to -1.00        // our snow: drifts, frosted rock, a frozen puddle, laden shrubs, tracks, wherever snow lies
    private const int BareSteepCategory = 15;  // our scree: broken rock and gravel on the steep faces (the pack's Big Grass, 3, is left unused)
    private const int MarshCategory = 11;      // our marsh: the low flats, the sodden woods and reedbeds, and the beds of lakes and ponds (the pack's Very Dark, 4, is left unused)
    private const int SandCategory = 5;        // the sand update, for the deserts
    private const int StoneCategory = 14;      // our stone: slabs, cracks, lichen, shards, for the barrens and the beds of deep and frozen water (the pack's, 6, is left unused)
    private const int ForestCategory = 7;       // the forest floor, built for it: litter, roots, logs, ferns, moss
    private const int PaleGrassCategory = 8;    // our own meadows, by height: pale on the high ground,
    private const int LightGrassCategory = 9;   // light between,
    private const int DarkGrassCategory = 10;   // dark in the low. The pack's bands 0 to 2 are left in the library, unused.
    private const int BeachCategory = 12;       // our sand for the shores and the shallows: shells, driftwood, dune grass
    private const int DesertCategory = 13;      // and our sand for the deserts: ripples, a cracked pan, scrub, stones. The pack's sand, 5, is left unused.

    // These four tiles carry a tree. Above the treeline they are swapped out,
    // which is what makes a summit read as a summit.
    private static readonly bool[] CarriesTree = BuildTreeTable();

    private const float TreelineFraction = 0.72f;
    private const float SteepFraction = 0.62f;
    private const float MarshFraction = 0.10f;

    /// <summary>Depth past which a lake bed is rock rather than sand.</summary>
    private const float DeepWater = 1.6f;

    /// <summary>
    /// How much water a reef wants over it. Under this the floor is left sand:
    /// the wash bares anything shallower than about two thirds of a metre, and
    /// coral standing in less than this comes out of the top of the water.
    /// </summary>
    public const float ReefDepth = 1.0f;

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
        // Perlin noise mirrors around 0, so a fixed offset keeps the sampled
        // region firmly positive and stops the world repeating across the axes.
        float offset = 1000f + (worldSeed % 1000) * 7.31f;

        var buckets = new Dictionary<int, List<Matrix4x4>>();

        for (int tx = 0; tx < WorldGrid.TilesPerChunk; tx++)
        for (int tz = 0; tz < WorldGrid.TilesPerChunk; tz++)
        {
            int gx = chunkIndexX() * WorldGrid.TilesPerChunk + tx;
            int gz = chunkIndexZ() * WorldGrid.TilesPerChunk + tz;

            // Per tile, not per chunk: a border between two regions runs
            // through a chunk now rather than round it, and the ground has to
            // change where the border is and not where the chunk ends.
            var character = Regions.CharacterAtTile(gx, gz, worldSeed);

            bool fungal = character == Regions.Character.Fungal;
            bool desert = character == Regions.Character.Desert;
            bool stone = character == Regions.Character.Stone;

            // Under a snowfield goes rock, not grass. The grass tiles carry
            // blades standing a third of a unit above the block, and the snow
            // is laid on the block -- so on a grass tile the blades come up
            // through the snow and draw a green fringe over every tile.
            bool underSnow = character == Regions.Character.Snow;

            // Ground that is dark and wet underfoot: the dead woods and the
            // reedbeds both stand on it.
            bool sodden = character == Regions.Character.Reed;   // the dead woods have a floor of their own now

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
                var body = WaterSurface.BodyAt(gx, gz, worldSeed);

                // A frozen lake is stone under the ice. An open shore is sand
                // in the shallows and rock below, which is what gives water a
                // bottom rather than an edge. A lake or a pond inland is soft
                // dark mud, because that is what is under one.
                // A reef is the whole floor of its sea, deep water and all:
                // asked after the depth it would only be the sandy fringe, and
                // coral in ankle-deep water reads as a flooded field.
                category = underSnow ? StoneCategory
                         : body != WaterSurface.Body.Beach ? MarshCategory
                         : character == Regions.Character.Reef && underBy >= ReefDepth ? ReefCategory
                         : underBy >= DeepWater ? StoneCategory
                         : BeachCategory;
            }
            else if (-underBy < BeachHeight && !underSnow && !stone
                     && WaterSurface.BodyAt(gx, gz, worldSeed) == WaterSurface.Body.Beach)
            {
                // A strand of sand above the waterline, so the grass does not
                // stop dead at the water -- but only on an open shore. A pond
                // in a wood has grass to its edge, not a beach.
                category = BeachCategory;
            }
            else if (SnowCover.IsSnowy(gx, gz, worldSeed))
            {
                category = SnowCategory;            // the snowfields, and any summit above the snowline
            }
            else if (fungal)
            {
                category = FungalCategory;
            }
            else if (character == Regions.Character.Dead)
            {
                category = DeadCategory;
            }
            else if (character == Regions.Character.Jungle)
            {
                // all of it, steep faces included: scree through a jungle
                // reads as a patch of somewhere else, the same as in the sand
                category = JungleCategory;
            }
            else if (desert)
            {
                // sand over the whole of it, steep faces and all: scree in the
                // middle of a desert reads as a patch of somewhere else
                category = DesertCategory;
            }
            else if (stone)
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
            else if (character == Regions.Character.Forest)
            {
                category = ForestCategory;          // the forest's own floor under its own trees
            }

            int variant = Hash2D(gx, gz, worldSeed) % VariantsPerCategory;

            // under the water the beach is bare sand: no dune grass on a lake bed. Of the five
            // beach tiles the first, third and fourth carry none.
            if (category == BeachCategory && submerged) variant = new[] { 0, 2, 3 }[variant % 3];

            int id = category * VariantsPerCategory + variant;

            // Above the treeline, or under water, swap a treed tile for a bare
            // one of the same shade.
            if (CarriesTree[id] && (bare > TreelineFraction || submerged || Landmarks.Occupies(gx, gz, worldSeed)))
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
            //
            // The stone is laid wide the same way and for the same reason, so
            // it takes the same settling.
            float settle = category == SandCategory || category == StoneCategory || category == BeachCategory || category == DesertCategory
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

            // A tile is a body 2.05 deep. Where a neighbour stands further down than that,
            // plain fill is laid under it, block on block, so a cliff is solid to its foot
            // rather than a shelf with the void under it.
            float lowest = Mathf.Min(Mathf.Min(WorldHeight.TileYOffset(gx + 1, gz, worldSeed), WorldHeight.TileYOffset(gx - 1, gz, worldSeed)),
                                     Mathf.Min(WorldHeight.TileYOffset(gx, gz + 1, worldSeed), WorldHeight.TileYOffset(gx, gz - 1, worldSeed)));
            float drop = WorldHeight.TileYOffset(gx, gz, worldSeed) - lowest;
            if (drop > FillDepth)
            {
                int fillId = category == StoneCategory || category == BareSteepCategory || category == SnowCategory ? FillRockId : FillEarthId;
                if (!buckets.TryGetValue(fillId, out var fills)) { fills = new List<Matrix4x4>(); buckets[fillId] = fills; }
                for (int k = 1; k * FillDepth < drop; k++)
                    fills.Add(Matrix4x4.TRS(position - Vector3.up * (FillDepth * k), Quaternion.identity, Vector3.one));
            }
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
