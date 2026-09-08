using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The wash on the beaches: where the sea meets sand, a wave runs up the
/// strand and draws back on a loop. This lays the quads it happens on --
/// the dry sand within reach of the water and the shallows just out from
/// it -- each vertex told how far it is from the waterline and given a
/// phase, and the shader does the rest. Beaches only: a lake is still.
/// </summary>
public static class Surf
{
    private const float StrandHeight = 0.7f;    // how far above the water the sand still counts as beach, the same as the tiles'
    private const int Reach = 5;                 // tiles of sand the wash can reach
    private const int Out = 5;                   // tiles of shallows it starts from
    private const int Cut = 3;                   // each tile cut this many across, so a crest rolls rather than steps

    /// <summary>Dry sand on an open shore, low enough for the wash to reach.</summary>
    public static bool IsStrand(int tileX, int tileZ, int seed)
    {
        if (WaterSurface.IsUnderwater(tileX, tileZ, seed)) return false;
        if (WorldHeight.SurfaceY(tileX, tileZ, seed) - WaterSurface.Level > StrandHeight) return false;
        return Regions.Sea(Regions.CharacterAtTile(tileX, tileZ, seed, false));
    }

    /// <summary>
    /// Open water off a beach, shallow enough that the wave's backwash bares
    /// it: these tiles have no fixed sea surface -- the wash sheet is their
    /// water, and it stops at the wave's front, so the foam line is always
    /// the water's farthest point and the sand shows behind it going out.
    /// </summary>
    public static bool IsSurfShallows(int tileX, int tileZ, int seed)
    {
        if (!WaterSurface.IsOpenWater(tileX, tileZ, seed)) return false;
        if (WaterSurface.Level - WorldHeight.SurfaceY(tileX, tileZ, seed) > 0.6f) return false;
        if (!Regions.Sea(Regions.CharacterAtTile(tileX, tileZ, seed, false))) return false;
        return Nearest(tileX, tileZ, seed, false, Out) > 0;
    }

    /// <summary>How far, in tiles, to the nearest lake or pond water, up to six; 99 with none that near.</summary>
    public static int PondNear(int tileX, int tileZ, int seed)
    {
        for (int r = 1; r <= 6; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            int tx = tileX + dx, tz = tileZ + dz;
            if (WaterSurface.IsUnderwater(tx, tz, seed) && WaterSurface.BodyAt(tx, tz, seed) != WaterSurface.Body.Beach) return r;
        }
        return 99;
    }

    /// <summary>
    /// How far out the wave's front may retreat over a tile: held a metre
    /// past the tile within two tiles of a lake or a pond, so the sea and
    /// the pond stay one sheet there, easing out to a free retreat six
    /// tiles away. Bared, those shallows left the pond's surface hanging
    /// over the sand; kept as a fixed surface, they showed their own edge.
    /// </summary>
    public static float Hold(float dist, int pondNear)
    {
        float ramp = Mathf.Clamp01((pondNear - 2f) / 4f);
        return Mathf.Lerp(dist + 1.0f, -12f, ramp);
    }

    /// <summary>Whether a lake or a pond lies within two tiles.</summary>
    public static bool ByAnotherWater(int tileX, int tileZ, int seed) => PondNear(tileX, tileZ, seed) <= 2;

    private static bool IsShallows(int tileX, int tileZ, int seed) => IsSurfShallows(tileX, tileZ, seed);

    // the wave's timing, the same numbers the water shader uses in wash mode; the clock is handed
    // to the shader every frame, so the game and the picture agree about where the wave is
    private const float Period = 14f, ReachMetres = 6.5f, Back = -5f;
    public static float Now => Time.time + Offset;

    /// <summary>A shift of the wave's clock, so the title can have a wave due when its picture comes up.</summary>
    public static float Offset { get; private set; }

    /// <summary>Sets the clock so the wave at a tile is just beginning to come in, so many seconds from now.</summary>
    public static void WaveDue(int tileX, int tileZ, int seed, float inSeconds)
    {
        float phase = Mathf.PerlinNoise(tileX * 0.018f + seed * 0.01f, tileZ * 0.018f);
        float then = Time.time + inSeconds;
        Offset = Mathf.Repeat((0.02f - phase) * Period - then, Period);
    }

    /// <summary>Where the wave's front is now, in metres from the waterline (out to sea is negative), for a tile's phase.</summary>
    private static float Front(float phase)
    {
        float cycle = Mathf.Repeat(Now / Period + phase, 1f);
        float f = cycle < 0.3f ? Mathf.SmoothStep(0f, 1f, cycle / 0.3f)
                : cycle < 0.42f ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (cycle - 0.42f) / 0.58f);
        return Mathf.Lerp(Back, ReachMetres, f);
    }

    /// <summary>
    /// Whether there is water over a point right now. Off the surf this is
    /// the tile map; in the surf's shallows it is where the wave is, so
    /// nothing splashes on sand the water has drawn back from.
    /// </summary>
    public static bool Covered(Vector3 at, int seed)
    {
        int tileX = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tileZ = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
        float d;
        if (IsStrand(tileX, tileZ, seed))
        {
            int toWater = Nearest(tileX, tileZ, seed, true, Reach);
            if (toWater < 0) return false;
            d = (toWater - 0.5f) * WorldGrid.TileSize;
        }
        else if (IsSurfShallows(tileX, tileZ, seed))
        {
            d = -(Nearest(tileX, tileZ, seed, false, Out) - 0.5f) * WorldGrid.TileSize;
            float phaseHere = Mathf.PerlinNoise(tileX * 0.018f + seed * 0.01f, tileZ * 0.018f);
            return d < Mathf.Max(Front(phaseHere), Hold(d, PondNear(tileX, tileZ, seed))) - 0.3f;
        }
        else return WaterSurface.IsOpenWater(tileX, tileZ, seed);
        float phase = Mathf.PerlinNoise(tileX * 0.018f + seed * 0.01f, tileZ * 0.018f);
        return d < Front(phase) - 0.3f;
    }

    public static bool Covered(Vector3 at) => Covered(at, SeedNow);

    /// <summary>Whether a point is on the strand, where the wash runs over sand that is above the water level.</summary>
    public static bool OnStrand(Vector3 at, int seed) => IsStrand(Mathf.RoundToInt(at.x / WorldGrid.TileSize), Mathf.RoundToInt(at.z / WorldGrid.TileSize), seed);

    /// <summary>The height of the water's face at a point: the water level, or the wash's sheet where it runs up the sand.</summary>
    public static float SurfaceAt(Vector3 at)
    {
        int seed = SeedNow;
        int tileX = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tileZ = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
        if (IsStrand(tileX, tileZ, seed)) return WorldHeight.SurfaceY(tileX, tileZ, seed) + 0.06f;
        return WaterSurface.Level;
    }

    private static ChunkManager world;
    private static int SeedNow
    {
        get
        {
            if (world == null) world = Object.FindFirstObjectByType<ChunkManager>();
            return world != null ? world.WorldSeed : 0;
        }
    }

    /// <summary>The water that runs up the sand: quads over the strand only.</summary>
    public static Mesh BuildWashMesh(Vector2Int chunkIndex, int worldSeed) => BuildSheet(chunkIndex, worldSeed, true);

    /// <summary>The foam over the shallows, where the wave breaks before it runs up.</summary>
    public static Mesh BuildFoamMesh(Vector2Int chunkIndex, int worldSeed) => BuildSheet(chunkIndex, worldSeed, false);

    private static Mesh BuildSheet(Vector2Int chunkIndex, int worldSeed, bool sand)
    {
        int originX = chunkIndex.x * WorldGrid.TilesPerChunk;
        int originZ = chunkIndex.y * WorldGrid.TilesPerChunk;
        int n = WorldGrid.TilesPerChunk;

        // The distance from the waterline for every tile in the chunk and a
        // ring round it, or NaN where the wash does not go. Each quad's
        // corners then take the mean of the tiles round them, so the value
        // runs smoothly across a tile and the front sweeps rather than
        // jumping two metres at a time.
        var dist = new float[n + 2, n + 2];
        var height = new float[n + 2, n + 2];
        var hold = new float[n + 2, n + 2];

        for (int i = -1; i <= n; i++)
        for (int j = -1; j <= n; j++)
        {
            int tileX = originX + i, tileZ = originZ + j;
            float d = float.NaN, y = 0f, h = -12f;

            if (IsStrand(tileX, tileZ, worldSeed))
            {
                int toWater = Nearest(tileX, tileZ, worldSeed, true, Reach);
                if (toWater > 0) { d = (toWater - 0.5f) * WorldGrid.TileSize; y = WorldHeight.SurfaceY(tileX, tileZ, worldSeed) + 0.06f; }
            }
            else if (IsShallows(tileX, tileZ, worldSeed))
            {
                int toSand = Nearest(tileX, tileZ, worldSeed, false, Out);
                if (toSand > 0) { d = -(toSand - 0.5f) * WorldGrid.TileSize; y = WaterSurface.Level + 0.012f; h = Hold(d, PondNear(tileX, tileZ, worldSeed)); }
            }

            dist[i + 1, j + 1] = d;
            height[i + 1, j + 1] = y;
            hold[i + 1, j + 1] = h;
        }

        float Corner(int ci, int cj, float own) => CornerOf(dist, ci, cj, own);
        float HoldCorner(int ci, int cj, float own) => CornerOf(hold, ci, cj, own);

        float CornerOf(float[,] map, int ci, int cj, float own)
        {
            // the corner between tiles (ci-1..ci, cj-1..cj): the mean of those that have a value
            float sum = 0f; int count = 0;
            for (int a = ci - 1; a <= ci; a++)
            for (int b = cj - 1; b <= cj; b++)
            {
                if (a < -1 || b < -1 || a > n || b > n) continue;
                if (float.IsNaN(dist[a + 1, b + 1])) continue;
                sum += map[a + 1, b + 1]; count++;
            }
            return count > 0 ? sum / count : own;
        }

        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var holds = new List<Vector2>(); var tris = new List<int>();
        float half = WorldGrid.TileSize * 0.5f;

        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            float own = dist[i + 1, j + 1];
            if (float.IsNaN(own)) continue;
            if ((own >= 0f) != sand) continue;        // the sand sheet and the shallows sheet

            float y = height[i + 1, j + 1];
            float phase = Mathf.PerlinNoise((originX + i) * 0.018f + worldSeed * 0.01f, (originZ + j) * 0.018f);
            float x = i * WorldGrid.TileSize, z = j * WorldGrid.TileSize;

            // the four corners' distances, and a grid of Cut x Cut quads between them
            float d00 = Corner(i, j, own), d01 = Corner(i, j + 1, own), d11 = Corner(i + 1, j + 1, own), d10 = Corner(i + 1, j, own);
            float ownHold = hold[i + 1, j + 1];
            float h00 = HoldCorner(i, j, ownHold), h01 = HoldCorner(i, j + 1, ownHold), h11 = HoldCorner(i + 1, j + 1, ownHold), h10 = HoldCorner(i + 1, j, ownHold);
            for (int a = 0; a < Cut; a++)
            for (int b = 0; b < Cut; b++)
            {
                float u0 = a / (float)Cut, u1 = (a + 1) / (float)Cut, w0 = b / (float)Cut, w1 = (b + 1) / (float)Cut;
                float D(float u, float w) => Mathf.Lerp(Mathf.Lerp(d00, d10, u), Mathf.Lerp(d01, d11, u), w);
                float H(float u, float w) => Mathf.Lerp(Mathf.Lerp(h00, h10, u), Mathf.Lerp(h01, h11, u), w);
                int v = verts.Count;
                verts.Add(new Vector3(x - half + u0 * WorldGrid.TileSize, y, z - half + w0 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u0, w0), phase)); holds.Add(new Vector2(H(u0, w0), 0f));
                verts.Add(new Vector3(x - half + u0 * WorldGrid.TileSize, y, z - half + w1 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u0, w1), phase)); holds.Add(new Vector2(H(u0, w1), 0f));
                verts.Add(new Vector3(x - half + u1 * WorldGrid.TileSize, y, z - half + w1 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u1, w1), phase)); holds.Add(new Vector2(H(u1, w1), 0f));
                verts.Add(new Vector3(x - half + u1 * WorldGrid.TileSize, y, z - half + w0 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u1, w0), phase)); holds.Add(new Vector2(H(u1, w0), 0f));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
        }

        if (verts.Count == 0) return null;
        var mesh = new Mesh { name = (sand ? "Wash " : "Foam ") + chunkIndex };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetUVs(1, holds); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>The nearest tile of the other kind, in tiles, or -1 past the reach.</summary>
    private static int Nearest(int tileX, int tileZ, int seed, bool wantWater, int most)
    {
        for (int r = 1; r <= most; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            if (WaterSurface.IsUnderwater(tileX + dx, tileZ + dz, seed) == wantWater) return r;
        }
        return -1;
    }

    /// <summary>The water shader in wash mode: a thin sheet that stops at the wave's front, up the sand.</summary>
    public static Material CreateMaterial()
    {
        var water = Resources.Load<Material>("Water");
        if (water == null || water.shader == null || !water.shader.isSupported) return null;
        var m = new Material(water);
        m.SetFloat("_Wash", 1f);
        m.SetFloat("_WaveHeight", 0.012f);
        m.SetFloat("_Back", -5f);
        m.renderQueue = 3004;
        return m;
    }

    /// <summary>The shallows' sheet: water too, so the crest rises out there, drawn under the sand's sheet where they meet.</summary>
    public static Material CreateFoamMaterial()
    {
        var water = Resources.Load<Material>("Water");
        if (water == null || water.shader == null || !water.shader.isSupported) return null;
        var m = new Material(water);
        m.SetFloat("_Wash", 1f);
        m.SetFloat("_WaveHeight", 0.02f);
        m.SetFloat("_Back", -5f);
        m.renderQueue = 3003;
        return m;
    }
}
