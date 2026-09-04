using System.Collections.Generic;
using UnityEngine;

public enum LandmarkKind
{
    ForestersWatch,   // a tower on a raised square of ground, deep in the woods
    Shipwreck,        // a hull broken on a beach, bow out of the water
    SandGate,         // two towers with a walk between their tops, in the desert
    TrappersCabin,    // a log cabin in the snow, with a yard and a woodpile
    FishingJetty,     // a plank jetty out over a lake in the reeds
    SteppedAltar,     // three tiers of stone and an idol on the top
    ToadstoolRing,    // a ring of giant mushrooms round a raised altar
    CharcoalCamp,     // a burner's kiln and woodpiles in the dead woods
    HilltopBeacon,    // a tower on a tall plinth on the hills, a light on top
    SummitCairn,      // a heap of stones on a peak
    WaysideShrine,    // a stone on a plinth by the way, in the lowlands
    StandingStones,   // a ring of stones in the open
    Lighthouse,       // a tower on a plinth at a beach's edge, a light on top
    HuntersHide,      // a small raised platform in the forest
    BuriedTower       // a tower sunk in the sand, leaning
}

/// <summary>
/// Where the landmarks are. Like the terrain, this is a pure function of chunk
/// coordinates and the world seed -- nothing is stored, so the same seed always
/// puts the same watch on the same rise, and a chunk can be asked about long
/// before it is ever loaded.
///
/// Each kind belongs to one kind of country, and is only ever found there,
/// and each asks for its own sort of site: level ground, a beach's shallows,
/// a lake's shore.
/// </summary>
public static class Landmarks
{
    /// <summary>What sort of ground a kind is built on.</summary>
    public enum Site
    {
        Level,      // level ground, footprint checked for flatness
        Shallows,   // just under the water off a beach, facing the shore
        Shore,      // dry ground at a lake's edge, facing out over the water
        BeachShore  // dry ground at a beach's edge, facing out to the open water
    }

    /// <summary>One structure, entire, the same way a creature is.</summary>
    public struct Kind
    {
        public string Name;
        public Regions.Character Country;   // the only region it is built in
        public Site Site;
        public int SurveyRadius;            // how far it charts, in chunks
        public float SurveyHeight;          // how far up you have to get before it counts
        public string Where;                // where the guide wants it found
        public float LabelHeight;           // how far above it the map writes its name

        // The ground it takes, in tiles, in its own frame before it is turned:
        // +x is 'ahead', where the stair or the jetty or the shore is.
        public int Behind, Ahead, Aside;

        // How level the ground must be under the core and under the rest.
        public int CoreHalf;
        public float CoreVariation, ApronVariation;

        // Out of a hundred chunks of its country that could take it, how many
        // do. Each has its own: a desert is common and level, and at one rate
        // for all there were fifteen gates to a watch.
        public int Chance;
    }

    private static readonly Kind[] kinds =
    {
        new Kind { Chance = 22, Name = "Forester's Watch", Country = Regions.Character.Forest, Site = Site.Level,
                   SurveyRadius = 5, SurveyHeight = 1.4f, LabelHeight = 14f,
                   Where = "in the woods, its tower above the trees",
                   Behind = 5, Ahead = 5, Aside = 4, CoreHalf = 2, CoreVariation = 0.51f, ApronVariation = 1.0f },

        new Kind { Chance = 30, Name = "Shipwreck", Country = Regions.Character.Water, Site = Site.Shallows,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 6f,
                   Where = "broken on a beach, its bow out of the water",
                   Behind = 3, Ahead = 4, Aside = 2, CoreHalf = 0, CoreVariation = 9f, ApronVariation = 9f },

        new Kind { Chance = 7, Name = "Sand Gate", Country = Regions.Character.Desert, Site = Site.Level,
                   SurveyRadius = 4, SurveyHeight = 0f, LabelHeight = 10f,
                   Where = "out in the sand, two towers and the walk between them",
                   Behind = 2, Ahead = 2, Aside = 2, CoreHalf = 1, CoreVariation = 0.26f, ApronVariation = 0.6f },

        new Kind { Chance = 16, Name = "Trapper's Cabin", Country = Regions.Character.Snow, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 8f,
                   Where = "in the snow, a log cabin with a yard",
                   Behind = 3, Ahead = 6, Aside = 3, CoreHalf = 2, CoreVariation = 0.51f, ApronVariation = 1.0f },

        new Kind { Chance = 45, Name = "Fishing Jetty", Country = Regions.Character.Reed, Site = Site.Shore,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 6f,
                   Where = "on a lake shore in the reeds, a pier out over the water",
                   Behind = 4, Ahead = 10, Aside = 3, CoreHalf = 0, CoreVariation = 9f, ApronVariation = 9f },

        new Kind { Chance = 22, Name = "Stepped Altar", Country = Regions.Character.Stone, Site = Site.Level,
                   SurveyRadius = 6, SurveyHeight = 3.0f, LabelHeight = 10f,
                   Where = "on the bare rock, three steps of stone and something on the top",
                   Behind = 2, Ahead = 4, Aside = 2, CoreHalf = 2, CoreVariation = 0.26f, ApronVariation = 0.8f },

        new Kind { Chance = 14, Name = "Toadstool Ring", Country = Regions.Character.Fungal, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 6f,
                   Where = "in the damp woods, a ring of toadstools taller than you",
                   Behind = 3, Ahead = 3, Aside = 3, CoreHalf = 1, CoreVariation = 0.26f, ApronVariation = 0.8f },

        new Kind { Chance = 20, Name = "Charcoal Camp", Country = Regions.Character.Dead, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 6f,
                   Where = "in the dead woods, a kiln and the wood cut for it",
                   Behind = 2, Ahead = 3, Aside = 2, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 0.6f },

        new Kind { Chance = 10, Name = "Hilltop Beacon", Country = Regions.Character.Hills, Site = Site.Level,
                   SurveyRadius = 6, SurveyHeight = 0f, LabelHeight = 12f,
                   Where = "on the hills, a tower on a plinth with a light kept on top",
                   Behind = 2, Ahead = 2, Aside = 2, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 0.9f },

        new Kind { Chance = 6, Name = "Summit Cairn", Country = Regions.Character.Peaks, Site = Site.Level,
                   SurveyRadius = 7, SurveyHeight = 0f, LabelHeight = 5f,
                   Where = "on a peak, a heap of stones and a pole",
                   Behind = 1, Ahead = 1, Aside = 1, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 1.3f },

        new Kind { Chance = 7, Name = "Wayside Shrine", Country = Regions.Character.Lowland, Site = Site.Level,
                   SurveyRadius = 2, SurveyHeight = 0f, LabelHeight = 5f,
                   Where = "on the low ground by the way, a stone on a plinth",
                   Behind = 1, Ahead = 2, Aside = 1, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 0.6f },

        new Kind { Chance = 30, Name = "Standing Stones", Country = Regions.Character.Lowland, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 6f,
                   Where = "out on the open low ground, a ring of stones on end",
                   Behind = 3, Ahead = 3, Aside = 3, CoreHalf = 1, CoreVariation = 0.26f, ApronVariation = 0.8f },

        new Kind { Chance = 20, Name = "Lighthouse", Country = Regions.Character.Water, Site = Site.BeachShore,
                   SurveyRadius = 7, SurveyHeight = 0.8f, LabelHeight = 16f,
                   Where = "at a beach's edge, a tower with a light in a glass room on top",
                   Behind = 5, Ahead = 2, Aside = 3, CoreHalf = 0, CoreVariation = 9f, ApronVariation = 9f },

        new Kind { Chance = 14, Name = "Hunter's Hide", Country = Regions.Character.Forest, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 1.5f, LabelHeight = 6f,
                   Where = "in the woods, a small platform up a stair",
                   Behind = 1, Ahead = 3, Aside = 1, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 0.8f },

        new Kind { Chance = 10, Name = "Buried Tower", Country = Regions.Character.Desert, Site = Site.Level,
                   SurveyRadius = 3, SurveyHeight = 0f, LabelHeight = 5f,
                   Where = "in the sand, a tower sunk to its shoulders and leaning",
                   Behind = 2, Ahead = 2, Aside = 2, CoreHalf = 0, CoreVariation = 0.26f, ApronVariation = 0.6f },
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

    /// <summary>Kept away from chunk edges so a landmark is never cut in half by a seam.</summary>
    private const int EdgeMargin = 5;

    public struct Placement
    {
        public bool Exists;
        public LandmarkKind Kind;
        public Vector3 Position;
        public Vector2Int Chunk;
        public float Yaw;
        public int TileX, TileZ;
    }

    // Asked per tile by the ground and the planting, and a shore site is
    // found by looking over the whole chunk, so the answer is kept.
    private static readonly Dictionary<long, Placement> placements = new Dictionary<long, Placement>();
    private static int placementsFor;

    public static Placement In(Vector2Int chunk, int worldSeed)
    {
        if (placementsFor != worldSeed) { placements.Clear(); placementsFor = worldSeed; }

        long key = ((long)chunk.x << 32) ^ (uint)chunk.y;
        if (placements.TryGetValue(key, out var known)) return known;

        var worked = Work(chunk, worldSeed);
        placements[key] = worked;
        return worked;
    }

    private static Placement Work(Vector2Int chunk, int worldSeed)
    {
        var result = new Placement { Exists = false, Chunk = chunk };

        // The country decides the kind: whichever kinds belong here, one is
        // picked by hash. Rolled the other way round, a kind whose country
        // is rare was rarer still.
        var here = Regions.CharacterAt(chunk, worldSeed);
        int fitting = 0;
        for (int i = 0; i < kinds.Length; i++) if (kinds[i].Country == here) fitting++;
        if (fitting == 0) return result;

        int pick = Hash(chunk.x, chunk.y, worldSeed ^ 0x77) % fitting;
        int index = -1;
        for (int i = 0; i < kinds.Length; i++)
        {
            if (kinds[i].Country != here) continue;
            if (pick-- == 0) { index = i; break; }
        }

        var kind = (LandmarkKind)index;
        var about = kinds[index];

        // and the kind decides how often. A ceiling rather than the rate:
        // the ground still has to suit it.
        if (Hash(chunk.x, chunk.y, worldSeed ^ 0x5F3A) % 100 >= about.Chance) return result;

        int tileX, tileZ, turns;

        switch (about.Site)
        {
            case Site.Shallows:
                if (!FindShallows(chunk, worldSeed, out tileX, out tileZ, out turns)) return result;
                break;

            case Site.Shore:
                if (!FindShore(chunk, worldSeed, false, out tileX, out tileZ, out turns)) return result;
                break;

            case Site.BeachShore:
                if (!FindShore(chunk, worldSeed, true, out tileX, out tileZ, out turns)) return result;
                break;

            default:
            {
                int span = WorldGrid.TilesPerChunk - EdgeMargin * 2;
                tileX = chunk.x * WorldGrid.TilesPerChunk + EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x1234) % span;
                tileZ = chunk.y * WorldGrid.TilesPerChunk + EdgeMargin + Hash(chunk.x, chunk.y, worldSeed ^ 0x9ABC) % span;

                // Square to the grid: the tiles it is built from are.
                turns = Hash(chunk.x, chunk.y, worldSeed ^ 0xBEEF) % 4;

                int apron = Mathf.Max(about.Ahead, Mathf.Max(about.Behind, about.Aside));

                if (!Level(tileX, tileZ, about.CoreHalf, about.CoreVariation, worldSeed)) return result;
                if (!Level(tileX, tileZ, apron, about.ApronVariation, worldSeed)) return result;
                if (Wet(tileX, tileZ, apron + 1, worldSeed)) return result;
                break;
            }
        }

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
    /// The four ways a structure can face, as the yaw that turns its +x to
    /// point that way: 0 is +x, 1 is -z, 2 is -x, 3 is +z, which is what a
    /// quarter turn about y does to +x in this engine.
    /// </summary>
    private static readonly Vector2Int[] Facing =
    {
        new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1)
    };

    /// <summary>
    /// A tile just under the water off a beach, with dry ground a few tiles
    /// off in one direction; that direction is the way the wreck faces.
    /// </summary>
    /// <summary>
    /// A shore is looked for over more of the chunk than level ground is:
    /// five tiles in from every edge left twenty five to choose from, and a
    /// lake's edge is rarely among so few. What hangs past the edge is only
    /// a jetty over water or cargo on a beach, and the neighbouring chunk is
    /// asked about it all the same.
    /// </summary>
    private const int ShoreMargin = 3;

    private static bool FindShallows(Vector2Int chunk, int seed, out int tileX, out int tileZ, out int turns)
    {
        int span = WorldGrid.TilesPerChunk - ShoreMargin * 2;
        int start = Hash(chunk.x, chunk.y, seed ^ 0x2468) % (span * span);

        for (int n = 0; n < span * span; n++)
        {
            int i = (start + n) % (span * span);
            int x = chunk.x * WorldGrid.TilesPerChunk + ShoreMargin + i % span;
            int z = chunk.y * WorldGrid.TilesPerChunk + ShoreMargin + i / span;

            float depth = WaterSurface.Level - WorldHeight.SurfaceY(x, z, seed);
            if (depth < 0.3f || depth > 1.3f) continue;
            if (WaterSurface.BodyAt(x, z, seed) != WaterSurface.Body.Beach) continue;

            for (int f = 0; f < 4; f++)
            {
                // the two tiles behind it under water, and dry ground within four ahead
                if (!WaterSurface.IsUnderwater(x - Facing[f].x, z - Facing[f].y, seed)) continue;
                if (!WaterSurface.IsUnderwater(x - 2 * Facing[f].x, z - 2 * Facing[f].y, seed)) continue;

                bool dry = false;
                for (int d = 1; d <= 4 && !dry; d++)
                    if (!WaterSurface.IsUnderwater(x + d * Facing[f].x, z + d * Facing[f].y, seed)) dry = true;
                if (!dry) continue;

                tileX = x; tileZ = z; turns = f;
                return true;
            }
        }

        tileX = tileZ = turns = 0;
        return false;
    }

    /// <summary>
    /// Dry ground at the water's edge, with water ahead for four tiles, deep
    /// enough for posts. Asked for a lake, a beach will not do: a jetty into
    /// surf is a different thing from a jetty into a lake. Asked for a beach,
    /// only a beach will.
    /// </summary>
    private static bool FindShore(Vector2Int chunk, int seed, bool beach, out int tileX, out int tileZ, out int turns)
    {
        int span = WorldGrid.TilesPerChunk - ShoreMargin * 2;
        int start = Hash(chunk.x, chunk.y, seed ^ 0x1357) % (span * span);

        for (int n = 0; n < span * span; n++)
        {
            int i = (start + n) % (span * span);
            int x = chunk.x * WorldGrid.TilesPerChunk + ShoreMargin + i % span;
            int z = chunk.y * WorldGrid.TilesPerChunk + ShoreMargin + i / span;

            // The water level sits exactly on a terrace, so the shoreline
            // itself is the terrace at nought above the water -- not a hair
            // above it. Asked for a hair above, the search only ever found
            // the terrace one step back, which is never beside water.
            float above = WorldHeight.SurfaceY(x, z, seed) - WaterSurface.Level;
            if (above < -0.001f || above > 1.0f) continue;

            for (int f = 0; f < 4; f++)
            {
                bool water = true;
                for (int d = 1; d <= 4 && water; d++)
                {
                    int wx = x + d * Facing[f].x, wz = z + d * Facing[f].y;
                    if (WaterSurface.Level - WorldHeight.SurfaceY(wx, wz, seed) < 0.2f) water = false;
                }
                if (!water) continue;
                if ((WaterSurface.BodyAt(x + Facing[f].x, z + Facing[f].y, seed) == WaterSurface.Body.Beach) != beach) continue;

                // and ground behind it to stand on
                if (WaterSurface.IsUnderwater(x - Facing[f].x, z - Facing[f].y, seed)) continue;

                tileX = x; tileZ = z; turns = f;
                return true;
            }
        }

        tileX = tileZ = turns = 0;
        return false;
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

        // this chunk's, and the eight around it: a shore site can reach past
        // its chunk's edge
        for (int cx = -1; cx <= 1; cx++)
        for (int cz = -1; cz <= 1; cz++)
        {
            var at = In(new Vector2Int(chunk.x + cx, chunk.y + cz), worldSeed);
            if (!at.Exists) continue;

            // the offset, turned back into the structure's own frame
            Vector3 local = Quaternion.Euler(0f, -at.Yaw, 0f) * new Vector3(tileX - at.TileX, 0f, tileZ - at.TileZ);
            int ahead = Mathf.RoundToInt(local.x);
            int aside = Mathf.RoundToInt(local.z);

            var about = All(at.Kind);

            if (ahead >= -about.Behind && ahead <= about.Ahead && Mathf.Abs(aside) <= about.Aside) return true;
        }

        return false;
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
