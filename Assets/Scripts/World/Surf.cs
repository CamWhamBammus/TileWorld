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
    private const int Out = 3;                   // tiles of shallows it starts from

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

    public static Mesh BuildWashMesh(Vector2Int chunkIndex, int worldSeed)
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
                if (toWater > 0) { d = (toWater - 0.5f) * WorldGrid.TileSize; y = WorldHeight.SurfaceY(tileX, tileZ, worldSeed) + 0.03f; }
            }
            else if (IsShallows(tileX, tileZ, worldSeed))
            {
                int toSand = Nearest(tileX, tileZ, worldSeed, false, Out);
                if (toSand > 0) { d = -(toSand - 0.5f) * WorldGrid.TileSize; y = WaterSurface.Level + 0.02f; }
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

            float y = height[i + 1, j + 1];
            float phase = Mathf.PerlinNoise((originX + i) * 0.018f + worldSeed * 0.01f, (originZ + j) * 0.018f);
            float x = i * WorldGrid.TileSize, z = j * WorldGrid.TileSize;
            int v = verts.Count;
            verts.Add(new Vector3(x - half, y, z - half)); uvs.Add(new Vector2(Corner(i, j, own), phase));
            verts.Add(new Vector3(x - half, y, z + half)); uvs.Add(new Vector2(Corner(i, j + 1, own), phase));
            verts.Add(new Vector3(x + half, y, z + half)); uvs.Add(new Vector2(Corner(i + 1, j + 1, own), phase));
            verts.Add(new Vector3(x + half, y, z - half)); uvs.Add(new Vector2(Corner(i + 1, j, own), phase));
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
        }

        if (verts.Count == 0) return null;
        var mesh = new Mesh { name = "Wash " + chunkIndex };
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

    public static Material CreateMaterial()
    {
        var own = Resources.Load<Material>("Wash");
        return own != null && own.shader != null && own.shader.isSupported ? new Material(own) : null;
    }
}
