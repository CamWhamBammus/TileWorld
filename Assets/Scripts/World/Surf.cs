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
        return Regions.CharacterAtTile(tileX, tileZ, seed, false) == Regions.Character.Water;
    }

    /// <summary>Open water off a beach, shallow.</summary>
    private static bool IsShallows(int tileX, int tileZ, int seed)
    {
        if (!WaterSurface.IsOpenWater(tileX, tileZ, seed)) return false;
        if (WaterSurface.Level - WorldHeight.SurfaceY(tileX, tileZ, seed) > 1.4f) return false;
        return Regions.CharacterAtTile(tileX, tileZ, seed, false) == Regions.Character.Water;
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

        for (int i = -1; i <= n; i++)
        for (int j = -1; j <= n; j++)
        {
            int tileX = originX + i, tileZ = originZ + j;
            float d = float.NaN, y = 0f;

            if (IsStrand(tileX, tileZ, worldSeed))
            {
                int toWater = Nearest(tileX, tileZ, worldSeed, true, Reach);
                if (toWater > 0) { d = (toWater - 0.5f) * WorldGrid.TileSize; y = WorldHeight.SurfaceY(tileX, tileZ, worldSeed) + 0.06f; }
            }
            else if (IsShallows(tileX, tileZ, worldSeed))
            {
                int toSand = Nearest(tileX, tileZ, worldSeed, false, Out);
                if (toSand > 0) { d = -(toSand - 0.5f) * WorldGrid.TileSize; y = WaterSurface.Level + 0.04f; }
            }

            dist[i + 1, j + 1] = d;
            height[i + 1, j + 1] = y;
        }

        float Corner(int ci, int cj, float own)
        {
            // the corner between tiles (ci-1..ci, cj-1..cj): the mean of those that have a value
            float sum = 0f; int count = 0;
            for (int a = ci - 1; a <= ci; a++)
            for (int b = cj - 1; b <= cj; b++)
            {
                if (a < -1 || b < -1 || a > n || b > n) continue;
                float v = dist[a + 1, b + 1];
                if (float.IsNaN(v)) continue;
                sum += v; count++;
            }
            return count > 0 ? sum / count : own;
        }

        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
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
            for (int a = 0; a < Cut; a++)
            for (int b = 0; b < Cut; b++)
            {
                float u0 = a / (float)Cut, u1 = (a + 1) / (float)Cut, w0 = b / (float)Cut, w1 = (b + 1) / (float)Cut;
                float D(float u, float w) => Mathf.Lerp(Mathf.Lerp(d00, d10, u), Mathf.Lerp(d01, d11, u), w);
                int v = verts.Count;
                verts.Add(new Vector3(x - half + u0 * WorldGrid.TileSize, y, z - half + w0 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u0, w0), phase));
                verts.Add(new Vector3(x - half + u0 * WorldGrid.TileSize, y, z - half + w1 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u0, w1), phase));
                verts.Add(new Vector3(x - half + u1 * WorldGrid.TileSize, y, z - half + w1 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u1, w1), phase));
                verts.Add(new Vector3(x - half + u1 * WorldGrid.TileSize, y, z - half + w0 * WorldGrid.TileSize)); uvs.Add(new Vector2(D(u1, w0), phase));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
        }

        if (verts.Count == 0) return null;
        var mesh = new Mesh { name = (sand ? "Wash " : "Foam ") + chunkIndex };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
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
        m.renderQueue = 3003;
        return m;
    }
}
