using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What grows on the ground rather than being part of it.
///
/// The tiles carry their own trees, baked into the tile, which is why the woods
/// come in whole squares. This is the other way about: small things placed on
/// top of whatever tile is already there, so a region can be given over to
/// mushrooms or to cactus without needing a version of every tile in the pack.
///
/// Nothing is stored. Where a thing stands is a function of the tile and the
/// seed, the same as everything else in this world, so the same ground has the
/// same things standing on it every time you walk back into it.
/// </summary>
public class Undergrowth : MonoBehaviour
{
    [Tooltip("How far out, in chunks, the small things are worth drawing.")]
    [SerializeField] private int reach = 4;

    /// <summary>One sort of thing, and how much of it a region carries.</summary>
    private struct Planting
    {
        public int From, Count;     // where this sort sits in the flattened list
        public float Share;         // share of tiles carrying one
        public float Low, High;     // and how tall it should stand, in world units
    }

    private class Patch
    {
        public List<Matrix4x4>[] ByKind;
    }

    private ChunkManager world;
    private Transform player;
    private Flora flora;
    private RenderParams look;
    private bool ready;

    private Flora.Sprout[] every;
    private Planting[] fungal, desert, stone, dead, reed, snow, forest, ordinary;

    private readonly Dictionary<Vector2Int, Patch> patches = new Dictionary<Vector2Int, Patch>();
    private readonly List<Vector2Int> stale = new List<Vector2Int>();

    private bool shifted = true;
    private Matrix4x4[][] gathered;
    private int[] counts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Undergrowth>() == null)
        {
            new GameObject("Undergrowth (runtime)").AddComponent<Undergrowth>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null) { enabled = false; return; }

        player = world.PlayerTransform;
        flora = Resources.Load<Flora>("Flora");

        if (flora == null || flora.Paint == null)
        {
            Debug.LogWarning("[Undergrowth] nothing to plant; run Tools/Tile World/Index the pack's flora.");
            enabled = false;
            return;
        }

        // Every sort in one list, so drawing does not care which is which.
        var all = new List<Flora.Sprout>();

        Planting Take(Flora.Sprout[] set, float low, float high)
        {
            var planting = new Planting { From = all.Count, Count = set.Length, Share = 0f, Low = low, High = high };
            all.AddRange(set);
            return planting;
        }

        // Sizes are given as how tall the thing should stand rather than as a
        // multiplier, because the models are nothing like each other: a
        // mushroom out of the pack is a third of a unit and a palm is nearly
        // four, and one number over both would be absurd at one end or the other.
        var mushrooms = Take(flora.Mushrooms, 0.45f, 1.25f);
        var cacti = Take(flora.Cacti, 1.40f, 2.60f);
        var palms = Take(flora.Palms, 3.20f, 5.00f);
        var stones = Take(flora.Stones, 0.35f, 0.95f);
        var boulders = Take(flora.Boulders, 0.40f, 1.30f);
        // The pack's trees run from narrow to broad. The narrow ones read as
        // conifers, which is what belongs on a snowfield; the broad ones are
        // what a warm wood is made of.
        var narrow = new List<Flora.Sprout>();
        var broad = new List<Flora.Sprout>();

        foreach (var tree in flora.Trees)
        {
            if (tree.Wide <= 1.0f) narrow.Add(tree);
            else broad.Add(tree);
        }

        if (narrow.Count == 0) narrow.AddRange(flora.Trees);
        if (broad.Count == 0) broad.AddRange(flora.Trees);

        var trees = Take(broad.ToArray(), 3.00f, 4.60f);
        var firs = Take(narrow.ToArray(), 2.60f, 4.20f);
        var deadTrees = Take(flora.DeadTrees, 1.80f, 3.10f);
        var reeds = Take(flora.Reeds, 1.10f, 2.10f);

        // and the same narrow trees again, under snow
        var whiteFirs = Take(
            flora.SnowTrees != null && flora.SnowTrees.Length > 0 ? flora.SnowTrees : narrow.ToArray(),
            2.60f, 4.20f);

        // Conifers stand taller than anything the pack ships, which is most of
        // what makes a snowfield read as high country rather than a white lawn.
        var pines = Take(flora.Pines ?? new Flora.Sprout[0], 4.00f, 7.00f);
        var snowPines = Take(flora.SnowPines ?? new Flora.Sprout[0], 4.00f, 7.50f);

        every = all.ToArray();

        Planting With(Planting p, float share) { p.Share = share; return p; }

        // What each sort of country carries. The order within a set is the
        // order they get first refusal on a tile, so the rarer things are
        // listed first and the ground cover last.
        // A mushroom wood is not only mushrooms: what makes it read is the
        // dead standing timber they are growing out of.
        fungal = new[] { With(deadTrees, 0.075f), With(boulders, 0.05f), With(mushrooms, 0.46f) };
        desert = new[] { With(palms, 0.014f), With(deadTrees, 0.02f), With(cacti, 0.055f), With(stones, 0.085f) };
        stone = new[] { With(boulders, 0.34f) };
        dead = new[] { With(deadTrees, 0.26f), With(mushrooms, 0.04f), With(boulders, 0.06f) };
        reed = new[] { With(reeds, 0.42f), With(boulders, 0.02f) };
        snow = new[] { With(snowPines, 0.055f), With(whiteFirs, 0.02f), With(boulders, 0.05f) };
        // Thicker than it was, and with the pack's own trees standing in it
        // rather than only whatever the tiles happen to carry.
        forest = new[] { With(pines, 0.03f), With(firs, 0.04f), With(trees, 0.12f),
                         With(mushrooms, 0.02f), With(boulders, 0.05f) };
        ordinary = new[] { With(mushrooms, 0.006f), With(boulders, 0.012f) };

        flora.Paint.enableInstancing = true;

        look = new RenderParams(flora.Paint)
        {
            receiveShadows = true,
            lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
            reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On
        };

        ready = every.Length > 0;
    }

    private void LateUpdate()
    {
        if (!ready) return;

        // The player is not always there to be had when this starts, and asked
        // only the once it would never be had at all.
        if (player == null)
        {
            player = world.PlayerTransform;
            if (player == null) return;
        }

        Vector2Int here = WorldGrid.WorldToChunk(player.position);

        for (int dx = -reach; dx <= reach; dx++)
        for (int dz = -reach; dz <= reach; dz++)
        {
            var index = new Vector2Int(here.x + dx, here.y + dz);

            if (!patches.ContainsKey(index))
            {
                patches[index] = Sow(index);
                shifted = true;
            }
        }

        stale.Clear();

        foreach (var pair in patches)
        {
            Vector2Int away = pair.Key - here;

            if (Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y)) > reach + 1) stale.Add(pair.Key);
        }

        foreach (var key in stale) patches.Remove(key);

        if (stale.Count > 0) shifted = true;

        if (shifted) Gather();

        Draw();
    }

    /// <summary>Works out what stands in a chunk, from the ground and the seed.</summary>
    private Patch Sow(Vector2Int index)
    {
        int seed = world.WorldSeed;

        var patch = new Patch { ByKind = new List<Matrix4x4>[every.Length] };

        var character = Regions.At(index, seed).Character;

        Planting[] planting = character switch
        {
            Regions.Character.Fungal => fungal,
            Regions.Character.Desert => desert,
            Regions.Character.Stone => stone,
            Regions.Character.Dead => dead,
            Regions.Character.Reed => reed,
            Regions.Character.Snow => snow,
            Regions.Character.Forest => forest,
            _ => ordinary
        };

        for (int tx = 0; tx < WorldGrid.TilesPerChunk; tx++)
        for (int tz = 0; tz < WorldGrid.TilesPerChunk; tz++)
        {
            int gx = index.x * WorldGrid.TilesPerChunk + tx;
            int gz = index.y * WorldGrid.TilesPerChunk + tz;

            // Standing water takes what is planted in it, except in the
            // shallows: reeds grow out of a lake edge, and nothing says a lake
            // has an edge like something standing up out of it.
            if (WaterSurface.IsUnderwater(gx, gz, seed))
            {
                float deep = WaterSurface.Level - WorldHeight.SurfaceY(gx, gz, seed);

                if (deep > 1.1f || flora.Reeds == null || flora.Reeds.Length == 0) continue;

                if (Hash(gx, gz, seed + 6791) % 100 >= 26) continue;

                var stalk = flora.Reeds[(int)(Hash(gx, gz, seed + 41) % (uint)flora.Reeds.Length)];

                if (stalk.Mesh == null || stalk.Size < 0.0001f) continue;

                float high = Mathf.Lerp(1.2f, 2.3f, ((Hash(gx, gz, seed + 97) >> 9) % 100) / 100f);
                float much = high / stalk.Size;

                int slot = System.Array.IndexOf(every, stalk);

                if (slot < 0) continue;

                if (patch.ByKind[slot] == null) patch.ByKind[slot] = new List<Matrix4x4>();

                patch.ByKind[slot].Add(Matrix4x4.TRS(
                    new Vector3(gx * WorldGrid.TileSize, WorldHeight.SurfaceY(gx, gz, seed),
                                gz * WorldGrid.TileSize),
                    Quaternion.Euler(0f, (Hash(gx, gz, seed + 13) % 360), 0f),
                    Vector3.one * much));

                continue;
            }

            // Snow covers what grows under it -- but a snowfield with nothing
            // standing in it at all is a white sheet, so the few things that
            // belong there are let through.
            if (character != Regions.Character.Snow && SnowCover.IsSnowy(gx, gz, seed)) continue;

            // A beach is bare. The sand above the waterline is ground the
            // trees have not taken, and a wood marching right down into the
            // lake is what it looked like before.
            bool beach = WorldHeight.SurfaceY(gx, gz, seed) - WaterSurface.Level < 0.7f
                      && character != Regions.Character.Desert;

            uint roll = Hash(gx, gz, seed + 5153);

            float where = (roll % 10000) / 10000f;
            float upto = 0f;

            int chosen = -1;
            Planting sort = default;

            foreach (var one in planting)
            {
                upto += one.Share;

                if (where < upto && one.Count > 0)
                {
                    sort = one;
                    chosen = one.From + (int)((roll >> 8) % (uint)one.Count);
                    break;
                }
            }

            if (chosen < 0) continue;

            // steep ground has nothing to hold them
            float slope = Mathf.Abs(WorldHeight.SurfaceY(gx + 1, gz, seed) - WorldHeight.SurfaceY(gx, gz, seed));
            if (slope > 0.9f) continue;

            var sprout = every[chosen];
            if (sprout.Mesh == null || sprout.Size < 0.0001f) continue;

            // only stones lie on a shore
            if (beach && sort.High > 1.5f) continue;

            float tall = Mathf.Lerp(sort.Low, sort.High, ((roll >> 17) % 100) / 100f);
            float size = tall / sprout.Size;

            // Off the middle of the tile, or they stand in rows like a crop.
            float acrossX = ((roll >> 13) % 1000) / 1000f - 0.5f;
            float acrossZ = ((roll >> 23) % 1000) / 1000f - 0.5f;

            // A model that reaches a full tile-half below its own origin was
            // drawn to stand in the tile rather than on it -- the trees and the
            // reeds are like this, with their feet in the ground. Lifting one
            // of those by its foot puts it in the air. Everything smaller is
            // drawn about its middle and does want lifting.
            float lift = sprout.Foot > 0.9f ? 0f : sprout.Foot * size;

            var at = new Vector3(
                gx * WorldGrid.TileSize + acrossX * WorldGrid.TileSize * 0.7f,
                WorldHeight.SurfaceY(gx, gz, seed) + lift,
                gz * WorldGrid.TileSize + acrossZ * WorldGrid.TileSize * 0.7f);

            float turn = (roll >> 3) % 360;

            if (patch.ByKind[chosen] == null) patch.ByKind[chosen] = new List<Matrix4x4>();

            patch.ByKind[chosen].Add(Matrix4x4.TRS(at, Quaternion.Euler(0f, turn, 0f), Vector3.one * size));
        }

        return patch;
    }

    /// <summary>
    /// Every one of a kind in one array, so a kind is one draw. Done when the
    /// chunks about us change rather than every frame: it is thousands of
    /// matrices, and building that fresh sixty times a second is a quarter of a
    /// megabyte a frame handed to the garbage collector for nothing.
    /// </summary>
    private void Gather()
    {
        shifted = false;

        if (gathered == null)
        {
            gathered = new Matrix4x4[every.Length][];
            counts = new int[every.Length];
        }

        for (int kind = 0; kind < every.Length; kind++)
        {
            int many = 0;

            foreach (var patch in patches.Values)
                if (patch.ByKind[kind] != null) many += patch.ByKind[kind].Count;

            counts[kind] = many;

            if (many == 0) continue;

            if (gathered[kind] == null || gathered[kind].Length < many)
                gathered[kind] = new Matrix4x4[Mathf.NextPowerOfTwo(many)];

            int at = 0;

            foreach (var patch in patches.Values)
            {
                var some = patch.ByKind[kind];
                if (some == null) continue;

                for (int i = 0; i < some.Count; i++) gathered[kind][at++] = some[i];
            }
        }
    }

    private void Draw()
    {
        if (gathered == null) return;

        look.worldBounds = new Bounds(player.position, Vector3.one * (WorldGrid.ChunkWorldSize * (reach * 2 + 2)));

        for (int kind = 0; kind < every.Length; kind++)
        {
            var mesh = every[kind].Mesh;

            if (mesh == null || counts[kind] == 0) continue;

            for (int i = 0; i < counts[kind]; i += 1023)
            {
                Graphics.RenderMeshInstanced(look, mesh, 0, gathered[kind],
                    Mathf.Min(1023, counts[kind] - i), i);
            }
        }
    }

    private static uint Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }
}
