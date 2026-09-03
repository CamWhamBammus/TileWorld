using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What grows on the ground rather than being part of it.
///
/// The tiles carry their own trees, baked into the tile, which is why the woods
/// come in whole squares. This is the other way about: small things placed on
/// top of whatever tile is already there, so a region can be given over to
/// mushrooms without needing a mushroom version of every tile in the pack.
///
/// Nothing is stored. Where a mushroom stands is a function of the tile and the
/// seed, the same as everything else in the world, so the same wood has the
/// same mushrooms in it every time you walk back into it.
/// </summary>
public class Undergrowth : MonoBehaviour
{
    [Tooltip("How far out, in chunks, the small things are worth drawing.")]
    [SerializeField] private int reach = 4;

    [Tooltip("Share of tiles carrying a mushroom where the fungus has taken over.")]
    [SerializeField, Range(0f, 1f)] private float thick = 0.5f;

    [Tooltip("And in ordinary woods, where they are only an occasional thing.")]
    [SerializeField, Range(0f, 0.1f)] private float sparse = 0.006f;

    [SerializeField] private float sizeOnTheGround = 2.8f;

    private class Patch
    {
        public List<Matrix4x4>[] ByKind;
    }

    private ChunkManager world;
    private Transform player;
    private Flora flora;
    private RenderParams look;
    private bool ready;

    private readonly Dictionary<Vector2Int, Patch> patches = new Dictionary<Vector2Int, Patch>();
    private readonly List<Vector2Int> stale = new List<Vector2Int>();

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

        if (flora == null || flora.Mushrooms == null || flora.Mushrooms.Length == 0 || flora.Paint == null)
        {
            Debug.LogWarning("[Undergrowth] no flora to plant; run Tools/Tile World/Index the pack's flora.");
            enabled = false;
            return;
        }

        flora.Paint.enableInstancing = true;

        look = new RenderParams(flora.Paint)
        {
            receiveShadows = true,
            lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
            reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On
        };

        ready = true;
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

        // bring in what is near, and let go of what is not
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

        var patch = new Patch { ByKind = new List<Matrix4x4>[flora.Mushrooms.Length] };

        bool fungal = Regions.At(index, seed).Character == Regions.Character.Fungal;
        float share = fungal ? thick : sparse;

        if (share <= 0f) return patch;

        for (int tx = 0; tx < WorldGrid.TilesPerChunk; tx++)
        for (int tz = 0; tz < WorldGrid.TilesPerChunk; tz++)
        {
            int gx = index.x * WorldGrid.TilesPerChunk + tx;
            int gz = index.y * WorldGrid.TilesPerChunk + tz;

            if (WaterSurface.IsUnderwater(gx, gz, seed)) continue;
            if (SnowCover.IsSnowy(gx, gz, seed)) continue;

            uint roll = Hash(gx, gz, seed + 5153);

            if ((roll % 10000) / 10000f > share) continue;

            // steep ground has nothing to hold them
            float slope = Mathf.Abs(WorldHeight.SurfaceY(gx + 1, gz, seed) - WorldHeight.SurfaceY(gx, gz, seed));
            if (slope > 0.9f) continue;

            int kind = (int)((roll >> 8) % (uint)flora.Mushrooms.Length);

            var sprout = flora.Mushrooms[kind];
            if (sprout.Mesh == null) continue;

            // Off the middle of the tile, or they stand in rows like a crop.
            float acrossX = ((roll >> 13) % 1000) / 1000f - 0.5f;
            float acrossZ = ((roll >> 23) % 1000) / 1000f - 0.5f;

            float turn = ((roll >> 3) % 360);
            float size = sizeOnTheGround * (0.7f + ((roll >> 17) % 100) / 100f * 0.7f);

            // The models are drawn about their middle, so placed at the height
            // of the ground they stand in it up to the cap. Lifted by half of
            // what they are, they stand on it.
            var at = new Vector3(
                gx * WorldGrid.TileSize + acrossX * WorldGrid.TileSize * 0.7f,
                WorldHeight.SurfaceY(gx, gz, seed) + sprout.Size * size * 0.5f,
                gz * WorldGrid.TileSize + acrossZ * WorldGrid.TileSize * 0.7f);

            if (patch.ByKind[kind] == null) patch.ByKind[kind] = new List<Matrix4x4>();

            patch.ByKind[kind].Add(Matrix4x4.TRS(at, Quaternion.Euler(0f, turn, 0f), Vector3.one * size));


        }

        return patch;
    }

    private bool shifted = true;
    private Matrix4x4[][] gathered;
    private int[] counts;

    /// <summary>
    /// Every mushroom of a kind in one array, so a kind is one draw. Done when
    /// the chunks about us change rather than every frame: it is four thousand
    /// matrices, and building that fresh sixty times a second is a quarter of a
    /// megabyte a frame handed to the garbage collector for nothing.
    /// </summary>
    private void Gather()
    {
        shifted = false;

        if (gathered == null)
        {
            gathered = new Matrix4x4[flora.Mushrooms.Length][];
            counts = new int[flora.Mushrooms.Length];
        }

        for (int kind = 0; kind < flora.Mushrooms.Length; kind++)
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

        var bounds = new Bounds(player.position, Vector3.one * (WorldGrid.ChunkWorldSize * (reach * 2 + 2)));

        look.worldBounds = bounds;

        if (gathered == null) return;

        for (int kind = 0; kind < flora.Mushrooms.Length; kind++)
        {
            var mesh = flora.Mushrooms[kind].Mesh;

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
