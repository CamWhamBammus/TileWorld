using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One square of the world. Generated once, then never changes — so the tile
/// transforms are baked into flat arrays the renderer can hand straight to the
/// GPU without copying anything per frame.
/// </summary>
public class Chunk
{
    private const int Categories = 40;          // the pack's bands, sand and stone, unused now; then ours: forest floor, three grasses, marsh, beach, desert, stone, scree, snow, fungal, dead, reef, jungle, peak, verge, then fifteen pairs of mixed ground, then savanna
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
    private const int PeakCategory = 21;        // the high country's own ground: frost-split slabs, thin turf, lichen, snow lying in the lee

    /// <summary>
    /// The ground between a beach and whatever grows behind it. Its five are a series rather
    /// than variants: 0 is sand with a tuft in it, 4 is turf with sand showing through, and
    /// the variant is chosen by how far up the shore the tile is instead of by a hash. Laid
    /// in a band above the sand, it turns a line into a thinning.
    /// </summary>
    private const int VergeCategory = 22;

    /// <summary>
    /// How much height the verge series is spread over. Five terraces, and it has to be a whole
    /// number of them: the ground is terraced to WorldHeight.StepHeight and the series has five
    /// steps, so at a metre -- four terraces -- five tiles were being fitted onto four rungs and
    /// one rung of the grade had to be skipped. Walking up a shore, a fifth of every pair of
    /// neighbouring verge tiles a terrace apart jumped two steps of the series at once.
    /// </summary>
    private const float VergeHeight = 5 * WorldHeight.StepHeight;

    /// <summary>
    /// The mixed ground where two countries meet. A tile for every pair of countries would be
    /// a square number of them, but the grounds are only six things to look at -- sand, grass,
    /// dark floor, rock, snow and the plain's dry straw -- so six families make fifteen pairs
    /// and fifteen series of five cover every border in the world. Categories 23 to 32 and
    /// 34 to 38; BlendCategory below maps a pair index to its category, since the five the
    /// plain added could not be given numbers next to the first ten.
    /// </summary>
    /// <summary>
    /// Sand into coral, graded by depth. The mixed ground covers borders between countries and
    /// this is not one: a reef stops where the water stops being a metre deep, a line of depth
    /// inside a single country, and it was the last hard edge left in the ground anywhere.
    /// </summary>
    private const int ReefVergeCategory = 39;
    // Wider than ReefWander, or the verge is narrower than the wander on the very edge it is
    // there to soften and the two fight each other.
    private const float ReefVergeBand = 0.75f;

    /// <summary>
    /// Half the depth over which an open sea's bed turns from sand to rock. Two and a half
    /// terraces, so the whole band is five of them: one rung of depth for each step of the
    /// series. It has to be a whole number of rungs for the same reason the shore verge does.
    /// The ground is terraced, so a depth is an exact multiple of WorldHeight.StepHeight, and a
    /// band of 0.9 was three and three fifths rungs carrying five steps: an eighth of every pair
    /// of neighbouring bed tiles one rung apart jumped two steps of the grade at once.
    /// The middle of the band does not move -- it is still DeepWater either way -- so the depth
    /// at which the bed reads half sand and half rock is unchanged. What moves is the ends: the
    /// pure sand runs seventeen centimetres less far out and the pure rock starts seventeen
    /// further down, on a bed you are looking at through water the depth shader keeps at 0.72
    /// alpha on purpose, "which is worth seeing, so the water is let go a little".
    /// </summary>
    private const float BedBand = 2.5f * WorldHeight.StepHeight;

    private const int SavannaCategory = 33;    // dry grassland: straw over red earth, worn patches, tussock, bone
    private const int Families = 6;
    private const int Sand = 0, Grass = 1, Dark = 2, Rock = 3, White = 4, Dry = 5;

    /// <summary>
    /// Which category each of the fifteen pairs lives in. Not a formula any more: the first ten
    /// pairs were built and numbered before dry grass was a family of its own, and the savanna's
    /// own ground sits at 33 between them and the five new ones, so the numbers are not
    /// contiguous. The order here is the order the pairs come out of the two nested loops over
    /// the families, which is also the order Tools/blend_tiles.py builds them in.
    /// </summary>
    /// <summary>
    /// Where each of the five beach tiles goes when it is under water, since two of them carry
    /// dune grass and nothing grows on a sea floor. The two marram tiles fall back on the plain
    /// sand and the wet patch either side of them, so the weighting the pick gave them is kept.
    /// </summary>
    private static readonly int[] BareBeach = { 0, 0, 2, 3, 3 };

    private static readonly int[] BlendCategory =
    {
        23, 24, 25, 26, 34,      // sand with grass, dark, rock, snow, dry
        27, 28, 29, 35,          // grass with dark, rock, snow, dry
        30, 31, 36,              // dark with rock, snow, dry
        32, 37,                  // rock with snow, dry
        38                       // snow with dry
    };

    /// <summary>
    /// The rock-into-snow series -- category 32, ids 160 to 164. Written as the pair formula
    /// rather than as 32, so it cannot part from BlendCategory or from the order the Blender
    /// script builds the pairs in. Declared below BlendCategory because a static field
    /// initialiser runs in the order it is written.
    /// </summary>
    private static readonly int RockSnowCategory =
        BlendCategory[Rock * (2 * Families - 1 - Rock) / 2 + (White - Rock - 1)];

    /// <summary>Which of the six a laid ground belongs to, or -1 for one that does not mix.</summary>
    private static int FamilyOfGround(int category)
    {
        switch (category)
        {
            case ForestCategory: case MarshCategory: case FungalCategory:
            case JungleCategory: return Dark;
            // These fell through into the savanna's case together, so every meadow tile called
            // itself Dry while the country over the border called itself Grass. The Grass
            // family was produced by no ground at all, and the two sides of a meadow border
            // therefore picked different series and met each other rather than the middle.
            case PaleGrassCategory: case LightGrassCategory: case DarkGrassCategory: return Grass;
            case SavannaCategory: return Dry;
            case BeachCategory: case DesertCategory: return Sand;
            // The dead wood's floor is grey ash and charred wood, and it was filed with the four
            // brown floors because it is a wood. Sampled off the sheet, ash is (119,115,111) and
            // the barrens' stone is (127,123,116) -- twelve apart -- while the forest floor,
            // the jungle's, the marsh's and the fungal loam are eighty to a hundred and eleven
            // away from it. So every border a dead wood had was drawn wrong: against the barrens
            // it laid a warm brown seam down the middle of two greys, and against the four
            // browns, which are the joins that actually show a step, it laid nothing at all
            // because they were the same family. It is rock.
            case StoneCategory: case BareSteepCategory: case PeakCategory:
            case DeadCategory: return Rock;
            case SnowCategory: return White;
            default: return -1;
        }
    }

    /// <summary>
    /// The same, for a bed under water -- and asked of the bed rule at THIS tile's depth, not of
    /// the country alone. The bed is chosen per tile: stone under the ice, mud under any lake or
    /// pond whatever grows above it, and on an open shore sand in the shallows giving way to rock
    /// in the deep. Answered per country, four of the fourteen were handed a family their bed
    /// never lays -- the desert and the plain were answered sand and the peaks and the barrens
    /// rock, where all four lay mud -- and the two seas were answered sand at every depth,
    /// although past DeepWater their bed is stone. The depth is the same on both sides of a
    /// border line, so asking it here is what lets the two sides walk toward one middle.
    /// </summary>
    private static int FamilyUnderWater(Regions.Character who, int gx, int gz, float underBy, float ripple, int seed)
    {
        if (who == Regions.Character.Snow) return Rock;      // stone under the ice
        if (!Regions.Sea(who)) return Dark;                  // a lake or a pond: mud, whatever grows above it

        // The reef draws its own edge against the sand that fringes it. Minus one makes the
        // guard at the call site skip the tile, which is what CarriesOwnEdge does for its floor.
        if (who == Regions.Character.Reef && underBy >= ReefLineAt(gx, gz, seed)) return -1;

        // The sea bed's own line. SandOrRockBed grades across BedBand either side of this point
        // and a graded tile has no family of its own, so the sea side never mixes inside that
        // band anyway; splitting at the middle of the grade is the one threshold that agrees
        // with the pure rock above it and the pure sand below it.
        return underBy - (DeepWater + ripple * DeepWander) >= 0f ? Rock : Sand;
    }

    /// <summary>What the country over the border mostly lays, taken from its character alone.</summary>
    /// <summary>
    /// The name of the ground a country lays, for anything measuring the world from outside it.
    /// Two countries whose families match get no mixed ground at their border, because there is
    /// nothing to mix -- which is right when they lay the same tiles and wrong when they do not,
    /// and that is the difference this is here to let a probe count.
    /// </summary>
    public static string FamilyNameOf(Regions.Character who)
    {
        switch (FamilyOfCountry(who))
        {
            case Sand: return "sand";
            case Grass: return "grass";
            case Dark: return "dark";
            case Rock: return "rock";
            case White: return "snow";
            case Dry: return "dry";
            default: return "none";
        }
    }

    private static int FamilyOfCountry(Regions.Character who)
    {
        switch (who)
        {
            case Regions.Character.Lowland: case Regions.Character.Hills: return Grass;
            case Regions.Character.Savanna: return Dry;
            case Regions.Character.Forest: case Regions.Character.Jungle:
            case Regions.Character.Fungal: case Regions.Character.Reed: return Dark;
            // The sea's countries lay grass, not sand. A region is named for its water at a
            // fifth of it under water, so four fifths of one is dry ground, and on that dry
            // ground a Water or a Reef matches no branch in the chooser and comes out as the
            // grass bands like any meadow. The sand it does lay is the strand, which is decided
            // by height above the waterline and not by the country at all. So a neighbour was
            // blending toward sand along a border that might be two hundred metres inland of
            // any water -- shells and dune grass strewn through a meadow -- while the tile the
            // other side of the line found grass on both sides of it and laid nothing. Worse
            // where a wood is across the line: the wood took the sand-into-dark series and the
            // water's own ground took the grass-into-dark one, so the two sides walked along
            // different series and met each other instead of meeting in the middle.
            // The sea bed is not affected: under water this is asked of FamilyUnderWater.
            case Regions.Character.Water: case Regions.Character.Reef: return Grass;
            case Regions.Character.Desert: return Sand;
            case Regions.Character.Snow: return White;
            // Ash is grey, whatever grew out of it once. See FamilyOfGround above.
            case Regions.Character.Stone: case Regions.Character.Peaks:
            case Regions.Character.Dead: return Rock;
            default: return -1;
        }
    }
    private const int SnowCategory = 16;
    private const int FillEarthId = 900, FillRockId = 901;   // plain blocks laid under a tile where the ground drops away
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
    // There used to be a table here saying which tile ids had a tree modelled into them, and a
    // swap above the treeline that reached for a bare one of the same shade. The ids it marked
    // -- 4, 8, 11 and 13 -- are in the pack's own grass bands, categories 0 to 2, and nothing
    // has selected those since the ground became ours: every tile the world lays now is bare,
    // and what stands on it is planted separately by the undergrowth. The swap could not fire.

    private const float TreelineFraction = 0.72f;
    private const float SteepFraction = 0.62f;

    /// <summary>The rise between two tile tops that counts as fully steep. Written once: the
    /// number appeared in the tile loop and in TooSteepToHold, and it is one rule.</summary>
    private const float SlopeSpan = 1.2f;

    /// <summary>
    /// How much of the slope range the ground takes to turn into scree: one terrace, and it
    /// cannot be a free number. The ground is terraced to WorldHeight.StepHeight and the slope is
    /// the difference between two tile tops, so it is not continuous -- it only ever takes the
    /// six values 0, 0.21, 0.42, 0.63, 0.83 and 1. A band narrower than one of those steps
    /// catches whichever single value happens to fall inside it and is empty everywhere else.
    /// One step wide catches exactly the class below the line wherever the wander has put it,
    /// which is the tile that actually stands against the scree.
    /// </summary>
    private const float ScreeBand = WorldHeight.StepHeight / SlopeSpan;

    /// <summary>How steep the sand and the straw have to get before rock shows through.</summary>
    private const float DryOutcrop = 0.30f;
    private const float MarshFraction = 0.10f;

    /// <summary>
    /// How low the ground in a mushroom wood reads before it comes out wet mud rather than loam.
    /// Its own number rather than the marsh line above: that one is measured against meadows,
    /// and widening it there would flood every low flat in the world at once.
    /// Measured with Tools/probe/Dark.cs.txt over nine chunks of a mushroom wood: at 0.16 the mud
    /// was one tile in a hundred and did not read, at 0.28 it was one in six and had become the
    /// country's main ground. At 0.25 the floor is 91% loam and the rest is hollows.
    /// </summary>
    private const float FungalWet = 0.25f;

    /// <summary>Which of the jungle floor's five is the pool of standing water.</summary>
    private const int JunglePool = 4;

    /// <summary>
    /// How low the ground in a jungle reads before the water stands on it. Its own number, the
    /// way the mushroom wood's is, and landed off the probe rather than guessed: over nine chunks
    /// of a jungle the pool went from three tiles in ten to four in a hundred at 0.16, which is
    /// not a jungle any more -- its own Blender script says a jungle floor is half made of
    /// standing water -- and to eleven in a hundred at 0.20, which is water you walk round.
    /// Absolute, not local, the same limitation the mushroom wood's line has: a jungle cell that
    /// sits high in the range keeps fewer pools and a low one keeps more, which is right for the
    /// world and wrong for any one cell.
    /// </summary>
    private const float JungleWet = 0.20f;

    /// <summary>Depth past which a lake bed is rock rather than sand.</summary>
    private const float DeepWater = 1.6f;

    /// <summary>
    /// How much water a reef wants over it. Under this the floor is left sand:
    /// the wash bares anything shallower than about two thirds of a metre, and
    /// coral standing in less than this comes out of the top of the water.
    /// </summary>
    public const float ReefDepth = 1.0f;

    /// <summary>
    /// How much water a reef wants over it HERE, which is the constant above plus the wander that
    /// breaks its line up. Both the ground and the planting have to ask the same question: the
    /// ground asked with the wander and the planting asked without it, so in the band between the
    /// two answers -- up to eleven centimetres of depth -- coral stood on plain sand.
    ///
    /// The noise is recomputed rather than passed in, because the only other copy of it is a local
    /// inside the tile loop. Two Perlin lookups; the planting already does several per tile.
    /// </summary>
    /// <summary>
    /// Where the chunk's own noise starts for a world. Perlin mirrors about nought, so the
    /// sampled ground is pushed well clear of the axes; written once because three places used
    /// to carry the same two constants and any one of them drifting would have put a tile's
    /// edge somewhere different from the edge everything else was drawing.
    /// </summary>
    private static float NoiseOrigin(int worldSeed) => 1000f + (worldSeed % 1000) * 7.31f;

    /// <summary>
    /// Whether the ground here is steep enough that the chunk lays broken rock on it instead of
    /// whatever the country grows. The undergrowth has to ask the same question -- it had a bar
    /// of its own at 0.9 of a metre against this one's 0.744, so on every rise between the two
    /// the chunk laid scree and the undergrowth planted a full-grown tree in it.
    /// </summary>
    public static bool TooSteepToHold(int gx, int gz, int worldSeed)
    {
        float o = NoiseOrigin(worldSeed);
        float ripple = Mathf.PerlinNoise(o + 311f + gx * EdgeNoiseScale, o + 311f + gz * EdgeNoiseScale) - 0.5f;

        return TooSteepToHold(Mathf.Clamp01(SlopeAt(gx, gz, worldSeed) / SlopeSpan), ripple);
    }

    /// <summary>Where the scree starts here: the threshold, with the wander that breaks its line up.</summary>
    private static float ScreeLine(float ripple) => SteepFraction + ripple * SteepWander;

    /// <summary>The same, for the tile loop, which has both of these to hand already.</summary>
    private static bool TooSteepToHold(float steep, float ripple) => steep > ScreeLine(ripple);

    public static float ReefLineAt(int tileX, int tileZ, int seed)
    {
        float o = NoiseOrigin(seed);

        return ReefDepth + (Mathf.PerlinNoise(o + 311f + tileX * EdgeNoiseScale,
                                              o + 311f + tileZ * EdgeNoiseScale) - 0.5f) * ReefWander;
    }

    /// <summary>And how far above the water the sand carries on up the shore.</summary>
    private const float BeachHeight = 0.7f;

    /// <summary>
    /// How far above the water the sand runs up the shore HERE: the constant above plus the
    /// wander that stops it drawing a contour round every island. The height was written down
    /// in three places and only this one wandered, so the wash -- the white breaking line, the
    /// loudest thing on any coast -- was still stopping at a dead level 0.7 all the way round.
    /// The contour that was taken off the sand was still being drawn in foam. Everything asks
    /// this now, the way everything asks ReefLineAt where the coral starts.
    /// </summary>
    public static float SandLineAt(int tileX, int tileZ, int seed)
    {
        float o = NoiseOrigin(seed);

        return BeachHeight + (Mathf.PerlinNoise(o + 133f + tileX * 0.07f, o + 133f + tileZ * 0.07f) - 0.5f) * 0.9f;
    }
    private const float BlendNoiseScale = 0.09f;
    private const float BlendWeight = 0.22f;

    // How much the edges that compare a slope or a depth are allowed to wander, and how
    // tight the wander is. The reef's is the smallest of the three on purpose: its floor
    // carries seagrass 0.58 above the block, so the shallow end of its range has to stay
    // deeper than that however far the line moves.
    private const float EdgeNoiseScale = 0.16f;
    private const float SteepWander = 0.14f;
    private const float DeepWander = 0.55f;
    private const float ReefWander = 0.22f;

    /// <summary>Ground that already draws its own edge and must not be mixed away.</summary>
    private static bool CarriesOwnEdge(int category) => category == ReefCategory || category == ReefVergeCategory;

    /// <summary>
    /// The bed of an open sea: sand in the shallows, rock in the deep, and the change graded
    /// across half a metre of depth rather than made at a line. The same treatment the snowline
    /// and the reef's fringe got, and for the same reason -- it is a line of depth inside one
    /// country, so the mixed ground, which only covers borders between countries, never saw it.
    /// No new tiles: the sand-into-rock series was built for the border between a desert and a
    /// barrens and is laid here as well.
    /// </summary>
    private static int SandOrRockBed(float over, ref int forced)
    {
        if (over >= BedBand) return StoneCategory;
        if (over <= -BedBand) return BeachCategory;

        // Floored over five, not rounded over four: rounding gives the two ends of the series
        // half the depth-width of the middle three, so pure sand and pure rock were the rarest
        // tiles in a grade that exists to run from one to the other. The shore verge has done it
        // this way since it was built.
        forced = Mathf.Clamp(Mathf.FloorToInt((over + BedBand) / (2f * BedBand) * VariantsPerCategory),
                             0, VariantsPerCategory - 1);

        return BlendCategory[Sand * (2 * Families - 1 - Sand) / 2 + (Rock - Sand - 1)];
    }

    /// <summary>
    /// The reef floor, or its verge if the water has only just got deep enough for coral. The
    /// variant runs 0 to 4 across the band, so the sand gives way to coral over about half a
    /// metre of depth instead of changing between one tile and the next.
    /// </summary>
    private static int ReefOrVerge(float over, ref int forced)
    {
        if (over >= ReefVergeBand) return ReefCategory;
        forced = Mathf.Clamp(Mathf.RoundToInt(over / ReefVergeBand * (VariantsPerCategory - 1)), 0, VariantsPerCategory - 1);
        return ReefVergeCategory;
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
        float offset = NoiseOrigin(worldSeed);

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

            // And the same without the fray, which is what the ice sheet and the water's own
            // body are decided on: a lake lying across a border has to be frozen up to a line
            // and not in a speckle. Taken once here rather than again where the mixing wants it.
            var standing = Regions.CharacterAtTile(gx, gz, worldSeed, false);

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

            // Which countries get the SCREE'S RIM -- the mixed ground where a patch of broken
            // rock meets what grows round it. What is left after the branches above the scree
            // test have taken theirs. The sand and the straw take an outcrop at DryOutcrop
            // instead; the jungle and the mushroom wood keep their own floor on any slope, since
            // loam and leaf litter do sit on a face; the dead wood and the barrens lay their own
            // scree in their branches above and are named here only to keep the rim off two
            // greys of one family; and a reedbed is not hillside.
            bool screes = !fungal && !desert && !stone && !sodden
                       && character != Regions.Character.Dead
                       && character != Regions.Character.Jungle
                       && character != Regions.Character.Savanna;

            // Height and steepness decide the ground; noise only softens the edge.
            float relief = Mathf.Clamp01(WorldHeight.HeightAt(gx, gz, worldSeed) / WorldHeight.MaxRelief);
            float steep = Mathf.Clamp01(SlopeAt(gx, gz, worldSeed) / SlopeSpan);

            float wobble = Mathf.PerlinNoise(offset + gx * BlendNoiseScale, offset + gz * BlendNoiseScale) - 0.5f;
            float bare = Mathf.Clamp01(relief + steep * 0.30f + wobble * BlendWeight);

            // The grass bands, the marsh line and the treeline all read off bare, so they
            // inherit that wobble and wander on their own. The three edges below are bare
            // comparisons against a slope or a depth, and without this they follow a contour
            // exactly: scree appears along a perfectly smooth curve, and the sea floor changes
            // ground along a circle. A finer noise on the threshold itself is all they need.
            float ripple = Mathf.PerlinNoise(offset + 311f + gx * EdgeNoiseScale, offset + 311f + gz * EdgeNoiseScale) - 0.5f;

            int band = Mathf.Clamp(Mathf.FloorToInt(bare * ShadeByHeight.Length), 0, ShadeByHeight.Length - 1);
            int category = ShadeByHeight[band];

            // Anything under the water line uses the bare dark ground. Left on
            // a forested tile, the tree simply carries on standing and pokes
            // out of the pond, since the water is only a surface over the top.
            bool submerged = WaterSurface.IsUnderwater(gx, gz, worldSeed);

            // Where the sand stops, this tile. Wandering, so no contour shows.
            float sandLine = SandLineAt(gx, gz, worldSeed);

            // A tile whose variant is decided by where it is rather than by a hash.
            int forced = -1;

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
                // Stone under the ice, and the ice is the unfrayed border's business: the bed
                // and the sheet over it were being decided by two different questions.
                category = standing == Regions.Character.Snow ? StoneCategory
                         : body != WaterSurface.Body.Beach ? MarshCategory
                         : character == Regions.Character.Reef && underBy >= ReefLineAt(gx, gz, worldSeed)
                             ? ReefOrVerge(underBy - ReefLineAt(gx, gz, worldSeed), ref forced)
                         : SandOrRockBed(underBy - (DeepWater + ripple * DeepWander), ref forced);
            }
            // How far up the shore the sand goes, wandering rather than following the
            // contour: a strand that stops at one height all the way along a coast draws a
            // line round the island, and the eye finds a line like that at any distance.
            else if (-underBy < sandLine && !underSnow && !stone
                     && WaterSurface.BodyAt(gx, gz, worldSeed) == WaterSurface.Body.Beach)
            {
                // A strand of sand above the waterline, so the grass does not
                // stop dead at the water -- but only on an open shore. A pond
                // in a wood has grass to its edge, not a beach.
                category = BeachCategory;
            }
            // And above the sand, the verge: the same shore thinning into whatever grows
            // behind it over five terraces of height, one per step of the series, rather than
            // stopping dead.
            else if (-underBy < sandLine + VergeHeight && !underSnow && !stone && !desert
                     && WaterSurface.BodyAt(gx, gz, worldSeed) == WaterSurface.Body.Beach)
            {
                category = VergeCategory;
                float up = (-underBy - sandLine) / VergeHeight;
                forced = Mathf.Clamp(Mathf.FloorToInt(up * VariantsPerCategory), 0, VariantsPerCategory - 1);
            }
            else if ((TooSteepToHold(steep, ripple) && relief > SnowCover.SnowlineFraction)
                     || SnowCover.IsSnowy(gx, gz, worldSeed))
            {
                // Snow does not lie on a face too steep to hold it: the wind scours it and the
                // rock under it shows through, which is most of what gives a summit a shape at
                // all. This branch sat five above the steep test, so nothing above the snowline
                // anywhere in the world could be broken rock -- not a crag on a summit, not the
                // white shoulder of a downland, and not one tile of the snow country, which is
                // named among the countries that scree and could never reach the test to use it.
                // Every cliff above the snowline was the same flat white block as the drift
                // lying beside it.
                // No new tiles: the rock-into-snow series already runs along every snowline,
                // where the block further down puts a bare rock tile into it, and its low end is
                // rock with snow caught in the ledges, which is what a scoured face looks like.
                //
                // Asked of every steep tile above the snowline, not only the ones the thinning
                // hash happened to call snowy. Taking only those, the nine tiles in ten the hash
                // left bare fell through to plain scree instead -- and the snowline block below
                // then graded them toward the SNOW end of this same series. So one face at one
                // altitude had the two rules pulling opposite ways a tile apart: some of it rock
                // with snow in the ledges, the rest of it snow with rock showing through.
                // The steep test is cheap and goes first, so a crag no longer pays for the
                // snowy lookup at all.
                if (TooSteepToHold(steep, ripple))
                {
                    category = RockSnowCategory;
                    forced = Hash2D(gx, gz, worldSeed + 823) % 2;   // 0 or 1 -- the rock end of the five
                }
                else category = SnowCategory;       // the snowfields, and any summit above the snowline
            }
            else if (fungal)
            {
                // The damp this country is named for. A flat 0.20 used to come off `bare` above
                // -- "read as lower and wetter than it is" -- and it had two readers when it was
                // written: the grass band, because the mushroom woods then stood on the darkest
                // grass, and the marsh line further down. Giving them a floor of their own put
                // this branch above both, and the subtraction has moved no tile on any seed
                // since; the country came out loam to its border. Asked here instead, so the
                // hollows between the caps are the wet mud a mushroom wood grows out of.
                // No new tiles, and no border moves: FamilyOfGround answers Dark for the marsh
                // and the loam alike, so the neighbours pick the same series either way. What
                // stands here does not change either -- the planting reads the character.
                category = bare < FungalWet ? MarshCategory : FungalCategory;
            }
            else if (character == Regions.Character.Dead)
            {
                // Its faces too, and for a better reason than the barrens': ash is the one
                // ground in the game that visibly cannot stay on a slope, and a bluff in a dead
                // wood was grey ash standing on end. This branch sat above the steep test as
                // well. Free of every border consequence -- the ash is already filed with the
                // rock, twelve apart from the barrens' stone off the sheet -- and the fill under
                // a cliff now gets rock under it rather than the earth an ash tile was getting.
                category = TooSteepToHold(steep, ripple) ? BareSteepCategory : DeadCategory;
            }
            else if (character == Regions.Character.Jungle)
            {
                // all of it, steep faces included: scree through a jungle
                // reads as a patch of somewhere else, the same as in the sand
                category = JungleCategory;

                // Where the water stands. The fifth of this floor is a pool, and it is one of
                // the three full-weight variants, so getting on for three tiles in ten of every
                // jungle in the world was standing water laid by a flat hash -- as likely on the
                // crown of a rise as in the hollow beside it. It is not a subtle tile: algae and
                // puddle green on a floor of brown humus, a metre across on a two-metre block,
                // and the ground is terraced, so the rise it sat on was drawn for the eye. The
                // mushroom wood had this exact fault one country over. The jungle needs no new
                // category at all, because the pool is already one of its own five: no tile is
                // new, no family changes, and the border, the snowline and the planting read
                // what they read before.
                // Folded onto both plain floors rather than onto the litter alone -- all to one
                // and that tile would be over half the country, which is what weighting the
                // features was done to get away from. Its own salt, so a tile that goes through
                // this and the jitter below is not moved by the same hash twice.
                int pick = PickVariant(JungleCategory, gx, gz, worldSeed);
                forced = pick == JunglePool && bare >= JungleWet
                    ? (Hash2D(gx, gz, worldSeed + 149) % 2 == 0 ? 0 : 3)
                    : pick;
            }
            else if (character == Regions.Character.Savanna || desert)
            {
                // Sand and straw over nearly all of it. Scree through a desert reads as a patch
                // of somewhere else, which is why these two used to take every tile including
                // the steep ones -- but that left a country of twelve percent of the world with
                // one ground and no relief to look at. Genuinely steep ground gets the stone
                // slabs instead: an outcrop standing out of the sand, which a desert does have.
                // A lower bar than the scree's, because these countries are flat: at the
                // scree's threshold two deserts sampled at random had not one steep tile
                // between them and the rule did nothing at all.
                category = steep > DryOutcrop + ripple * SteepWander
                    ? StoneCategory
                    : (desert ? DesertCategory : SavannaCategory);
            }
            else if (stone)
            {
                // The barrens is the one country named for its scree -- Scree, Rubble, Grit,
                // Shatter, Rake, and its own description answers "bare rock and scree" -- and it
                // laid none: this branch sits two above the steep test, so every face in it was
                // the same flat slab as the floor beside it. Nothing stood on them either, since
                // the undergrowth asks the steep test and skips them, so they were bare slabs
                // for no reason the ground ever gave. The same fault as the snowy branch above.
                // Its own set already exists, ids 75 to 79, and the slabs and the scree are both
                // the rock family, so no border moves and no tile is new.
                category = TooSteepToHold(steep, ripple) ? BareSteepCategory : StoneCategory;
            }
            else if (sodden)
            {
                category = MarshCategory;
            }
            else if (TooSteepToHold(steep, ripple))
            {
                category = BareSteepCategory;       // scree on the steep faces
            }
            else if (character == Regions.Character.Peaks)
            {
                // The high country used to stand on the lowland's pale grass,
                // which is a meadow tile three thousand feet up. Its steep
                // faces are still scree, asked for above this.
                category = PeakCategory;
            }
            else if (bare < MarshFraction)
            {
                category = MarshCategory;           // dark ground in the low flats
            }
            else if (character == Regions.Character.Forest)
            {
                category = ForestCategory;          // the forest's own floor under its own trees
            }

            // The snowline. The rule that decides it thins out with a hash, which breaks the
            // contour up but leaves whole snow tiles scattered among whole bare ones: salt and
            // pepper rather than a change. The tiles that the hash left bare, inside the band,
            // take the mixed ground between their own family and snow instead, graded by how
            // far through the band they are. No new tiles: the series already existed for the
            // border between a snowfield and its neighbours.
            if (!submerged && category != SnowCategory && !CarriesOwnEdge(category))
            {
                float cover = SnowCover.CoverAt(gx, gz, worldSeed);
                int mine = FamilyOfGround(category);

                if (cover > 0f && cover < 1f && mine >= 0 && mine != White)
                {
                    int low = Mathf.Min(mine, White), high = Mathf.Max(mine, White);
                    category = BlendCategory[low * (2 * Families - 1 - low) / 2 + (high - low - 1)];
                    // Not halved the way the border below is. There the tile over the line is
                    // walking toward the same middle and the two meet at the half-way tile;
                    // here the thing on the other side is a plain snow tile and is not walking
                    // anywhere, so a tile in the band has to cover the whole series. Halved, the
                    // most snow any tile at any snowline in the world could show was half of it
                    // -- and it was showing half at the top of the band, among tiles that were
                    // all white. The snow-heavy end of all five snow series was never laid.
                    float toward = mine == low ? cover : 1f - cover;

                    // And jittered, for the same reason the border below is: cover is a smooth
                    // function of height, so without this every tile at one height picks the
                    // same one of the five and the band comes out as two rings drawn round the
                    // mountain. Its own salt, so a tile that goes through both blocks is not
                    // moved the same way twice.
                    toward += (Hash2D(gx, gz, worldSeed + 619) % 1000) / 1000f * 0.30f - 0.15f;
                    forced = Mathf.Clamp(Mathf.RoundToInt(toward * (VariantsPerCategory - 1)), 0, VariantsPerCategory - 1);
                }
            }

            // Where two countries meet, the ground between them: the tile's own family and
            // the one over the border pick a series, and how near the line picks how far along
            // it. Both sides walk toward the middle of the same series, so they meet there
            // instead of meeting each other.
            // Under water too: a lake bed meeting a marsh, or sand meeting the rock of the
            // deep, were the last hard joins left in the world. The reef is the exception --
            // it draws its own edge against the sand that fringes it.
            if (category != VergeCategory && !CarriesOwnEdge(category))
            {
                float near = Regions.Border(gx, gz, worldSeed, out var over);

                // A tile the fray has handed across the line is standing in one country's
                // ground carrying the other's, and it was the one tile in the world that got
                // no mixing at all. The border asks what lies over the line and hands back the
                // country the tile has just joined, so the tile agreed with itself and was
                // laid plain: a speck of forest floor out in open sand with a hard edge round
                // every side of it, which is what the fray looks like close to. It is asked
                // instead what it is standing in, and mixed toward that -- and it is mixed all
                // the way to the middle of the series whatever the distance, because a speck
                // with the other ground on all four sides is not part way through a border,
                // it is the whole of one.
                bool handed = standing != character;
                if (handed) near = 1f;

                if (near > 0f)
                {
                    int mine = FamilyOfGround(category);
                    int theirs = FamilyOfCountry(handed ? standing : over);

                    // Under water, the country over the border is read for what it lays on its
                    // sea bed rather than for what it lays in the air. A lake in a meadow is
                    // still mud at the bottom, and blending toward the meadow's own family put
                    // turf and flowers down there, a foot under the surface.
                    if (submerged) theirs = FamilyUnderWater(handed ? standing : over, gx, gz, underBy, ripple, worldSeed);

                    // And above the water a sea lays grass only above its own strand. Where the
                    // strand and the verge stop is a height over the waterline, and this tile's
                    // height is that same height, so for as long as it is low enough to be
                    // standing on one, the country over the line is still sand. Moving the seas
                    // to the grass family fixed the ground two hundred metres inland of them and
                    // broke it at the water's edge: the strand grew turf and flowers wherever
                    // one sea's cell met another's, and the last eight tiles of a desert before
                    // a beach graded into meadow.
                    // An else, not a second if: on a deep floor the sea bed's answer has to win.
                    else if (Regions.Sea(handed ? standing : over) && -underBy < sandLine + VergeHeight) theirs = Sand;

                    if (mine >= 0 && theirs >= 0 && mine != theirs)
                    {
                        int low = Mathf.Min(mine, theirs), high = Mathf.Max(mine, theirs);
                        category = BlendCategory[low * (2 * Families - 1 - low) / 2 + (high - low - 1)];
                        float toward = mine == low ? near * 0.5f : 1f - near * 0.5f;

                        // Jittered, or every tile the same distance from the line picks the
                        // same one of the five and the band comes out in stripes.
                        toward += (Hash2D(gx, gz, worldSeed + 313) % 1000) / 1000f * 0.30f - 0.15f;
                        forced = Mathf.Clamp(Mathf.RoundToInt(toward * (VariantsPerCategory - 1)), 0, VariantsPerCategory - 1);
                    }
                }
            }

            // The scree's outer edge, which was the last hard rim left in the ground. Scree is
            // laid where the slope passes SteepFraction -- a line of slope inside one country, so
            // the mixed ground, which only covers borders between countries, never saw it, and a
            // patch of broken rock met the turf round it on a hard edge the whole way round. The
            // same fault the snowline and the reef's fringe had, and the largest colour step of
            // the four: scree is (139,131,118) and it meets the meadow grasses at (102,152,63).
            //
            // It is the shallowest scree that changes, not the ground below it. Grading the
            // ground instead was tried and measured first: because the terrain is terraced the
            // slope only takes six values, so "one class below the line" is not the tile against
            // the scree, it is every tile in the country with a half-metre step anywhere near it
            // -- half of the downs, a fifth of the meadows and a fifth of the woods turned to
            // mixed ground, which is the same cost that had a blend band of twelve reverted.
            // This way only the scree's own outer rung moves, so a patch fades out at its edge
            // and the country's own floor is untouched.
            if (screes && !submerged && category == BareSteepCategory)
            {
                // What the country would have laid here if the slope had not taken it.
                int theirs = FamilyOfCountry(character);

                // Thinned with a hash rather than taken whole, and for the reason the snowline
                // is: the ground is terraced, so the slope only ever takes six values, and every
                // band drawn on it is all or nothing -- taking the shallowest rung outright left
                // a downland with no scree in it at all. Half of that rung, chosen by hash,
                // interlocks with the other half and gives the patch a ragged edge instead of a
                // ring, which is what SnowByHeight does one field over.
                if (steep <= ScreeLine(ripple) + ScreeBand && (theirs == Grass || theirs == Dark)
                    && Hash2D(gx, gz, worldSeed + 617) % 2 == 0)
                {
                    category = BlendCategory[theirs * (2 * Families - 1 - theirs) / 2 + (Rock - theirs - 1)];

                    // Well toward the rock end: this is scree with the ground showing through it,
                    // not ground with stones in it. A step either way, so the rim is not uniform.
                    forced = 3 + Hash2D(gx, gz, worldSeed + 881) % 2;
                }
            }

            // The same rim for the dry countries' outcrop, which is the other line of slope drawn
            // inside one country. Two faults at once. The rim: stone is (127,123,116) and it met
            // the desert's sand and the plain's straw with nothing between, the fault the scree
            // had one change ago, and the flag above names these two as the countries its rim
            // does not cover. And the wander: the ground is terraced, so the slope only ever
            // reads 0, 0.21, 0.42, 0.63, 0.83 or 1, and DryOutcrop at 0.30 with the wander at
            // 0.14 spans 0.23 to 0.37 -- a band holding none of the six. The ripple could not
            // move one tile in the world, and the outcrop's edge was a ruled contour drawn on
            // "a half-metre step to a neighbour". The scree's own band, 0.55 to 0.69, does hold
            // 0.63, which is why its wander works and this one did not.
            // Half the outer rung by hash breaks both at once, the way the scree's rim does.
            // No new tiles: sand into rock is already laid on every sea bed, and rock into dry
            // already runs along every border a plain has with a barrens.
            if ((desert || character == Regions.Character.Savanna) && !submerged
                && category == StoneCategory
                && steep <= DryOutcrop + ripple * SteepWander + ScreeBand
                && Hash2D(gx, gz, worldSeed + 617) % 2 == 0)
            {
                // What the country would have laid here if the slope had not taken it, asked of
                // the same table the borders ask so the two cannot answer differently.
                int theirs = FamilyOfCountry(character);
                int low = Mathf.Min(theirs, Rock), high = Mathf.Max(theirs, Rock);
                category = BlendCategory[low * (2 * Families - 1 - low) / 2 + (high - low - 1)];

                // Well toward the rock end, whichever end of the series that is: the sand runs
                // into the rock and the rock runs into the straw. A step either way on a hash,
                // so the rim is not one uniform ring.
                forced = Rock == high
                    ? 3 + Hash2D(gx, gz, worldSeed + 881) % 2
                    : 1 - Hash2D(gx, gz, worldSeed + 881) % 2;
            }

            int variant = forced >= 0 ? forced : PickVariant(category, gx, gz, worldSeed);

            // Under the water the beach is bare sand: no dune grass on a sea bed. Of the five
            // beach tiles the first, third and fourth carry none. Only a sea gets here -- a lake
            // or a pond is laid as mud further up, before this branch is reached.
            // Written out one variant at a time rather than as `variant % 3`, which cannot come
            // out even and was giving the driftwood plank two fifths of every shallow sea floor
            // in the world and the starfish one fifth. With the pick weighted it would have been
            // worse: the modulo folds the two marram tiles onto the driftwood.
            if (category == BeachCategory && submerged) variant = BareBeach[variant];

            int id = category * VariantsPerCategory + variant;

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
                // A scoured face is rock under its top as much as the scree is, and a crag is
                // exactly where a drop deep enough to need filling happens.
                int fillId = category == StoneCategory || category == BareSteepCategory
                          || category == SnowCategory || category == RockSnowCategory ? FillRockId : FillEarthId;
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

    /// <summary>
    /// Steepest rise to a neighbouring tile. Public because the undergrowth asks the same
    /// question and was asking it of one neighbour only -- see the note where it calls this.
    /// </summary>
    public static float SlopeAt(int gx, int gz, int worldSeed)
    {
        float h = WorldHeight.SurfaceY(gx, gz, worldSeed);

        // All four, not the two on the high side. Of the pair of tiles flanking a riser only
        // the one to the west or the south could see it, so the scree and the desert's outcrops
        // were laid one tile off the step: on a face rising east the broken rock came out on
        // the flat at its foot, on one falling east on the flat at its top, and the other end of
        // the face was left bare either way. The same shape of mistake the undergrowth had, one
        // step smaller. The ground is terraced and every tile top is level, so the steepness is
        // on the riser between two tiles and neither tread alone is the face: both of them get
        // it now, which is why this widens the bands as well as moving them.
        return Mathf.Max(
            Mathf.Max(Mathf.Abs(WorldHeight.SurfaceY(gx + 1, gz, worldSeed) - h),
                      Mathf.Abs(WorldHeight.SurfaceY(gx - 1, gz, worldSeed) - h)),
            Mathf.Max(Mathf.Abs(WorldHeight.SurfaceY(gx, gz + 1, worldSeed) - h),
                      Mathf.Abs(WorldHeight.SurfaceY(gx, gz - 1, worldSeed) - h)));
    }

    /// <summary>
    /// Which of a set's five carry a big thing standing on them -- a fallen log, a stump, a
    /// boulder, an erratic, a ring of toadstools -- one bit per variant. The variant is a flat
    /// hash, so each of the five is laid on a fifth of its country, and a set with three of them
    /// is a floor littered with logs rather than a floor with the odd log on it. The forest has
    /// three: a log, a stump and a mossy boulder on three of its five, which is three fifths of
    /// every wood in the world. It is the mistake the termite mound was taken off a savanna tile
    /// for, and the note about it is still at the top of Tools/savanna_tiles.py.
    ///
    /// The dead woods, the barrens and the snowfields are left out on purpose. The first two are
    /// made of the thing -- a dead wood is charred stumps and bones -- and the undergrowth plants
    /// the barrens' boulders at a third of its tiles anyway.
    /// </summary>
    private static int FeatureVariants(int category)
    {
        switch (category)
        {
            case ForestCategory:    return (1 << 2) | (1 << 3) | (1 << 4);  // log, stump, mossy boulder
            case JungleCategory:    return (1 << 1) | (1 << 2);             // buttress roots, rotten log
            case FungalCategory:    return (1 << 1) | (1 << 4);             // fairy ring, rotten log
            case MarshCategory:     return 1 << 3;                          // half-sunk log
            case PeakCategory:      return 1 << 4;                          // erratic -- and one is planted too
            case DesertCategory:    return 1 << 2;                          // sandstone block
            case BeachCategory:     return (1 << 2) | (1 << 4);             // driftwood, sandstone
            case BareSteepCategory: return 1 << 2;                          // boulder in the scree
            case DarkGrassCategory: case LightGrassCategory: case PaleGrassCategory:
                                    return 1 << 2;                          // the lichened rock
            default: return 0;
        }
    }

    /// <summary>
    /// One against four, so a tile with a feature on it is one in eleven where a set has three
    /// of them and one in seventeen where it has one. All five stay reachable, which matters:
    /// Tools/ids.py checks that nothing in the library is unreachable.
    /// </summary>
    private const int FeatureWeight = 1, PlainWeight = 4;

    /// <summary>Which of a set's five to lay, with the ones carrying a feature made rarer.</summary>
    private static int PickVariant(int category, int gx, int gz, int worldSeed)
    {
        int mask = FeatureVariants(category);

        if (mask == 0) return Hash2D(gx, gz, worldSeed) % VariantsPerCategory;

        int total = 0;
        for (int v = 0; v < VariantsPerCategory; v++)
            total += (mask & (1 << v)) != 0 ? FeatureWeight : PlainWeight;

        int roll = Hash2D(gx, gz, worldSeed) % total;

        for (int v = 0; v < VariantsPerCategory; v++)
        {
            roll -= (mask & (1 << v)) != 0 ? FeatureWeight : PlainWeight;
            if (roll < 0) return v;
        }

        return VariantsPerCategory - 1;
    }

    private static int Hash2D(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
