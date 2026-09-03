using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snow on the high ground, laid over the tiles the same way water is laid
/// under them. The tile pack is grass and nothing else, so height could only
/// ever be shown by shade and by thinning the trees; this is the one thing that
/// makes a summit look like a summit.
/// </summary>
public static class SnowCover
{
    /// <summary>Fraction of full relief where snow starts to appear.</summary>
    public const float SnowlineFraction = 0.62f;

    /// <summary>Fraction by which it is complete, so the edge is ragged rather than a line.</summary>
    public const float FullCoverFraction = 0.86f;

    /// <summary>
    /// Snow, whether from height or because the whole region is under it.
    ///
    /// The two are kept apart because working out a region's character counts
    /// how much of it lies under snow, so a region asked here would be asking
    /// itself. Anything deciding what a region *is* wants the height rule
    /// below; everything else wants this.
    /// </summary>
    public static bool IsSnowy(int tileX, int tileZ, int worldSeed)
    {
        var chunk = new Vector2Int(
            Mathf.FloorToInt(tileX / (float)WorldGrid.TilesPerChunk),
            Mathf.FloorToInt(tileZ / (float)WorldGrid.TilesPerChunk));

        if (Regions.CharacterAt(chunk, worldSeed) == Regions.Character.Snow) return true;

        return SnowByHeight(tileX, tileZ, worldSeed);
    }

    /// <summary>Snow that is there because of how high the ground is, and nothing else.</summary>
    public static bool SnowByHeight(int tileX, int tileZ, int worldSeed)
    {
        float relief = WorldHeight.HeightAt(tileX, tileZ, worldSeed) / WorldHeight.MaxRelief;

        if (relief < SnowlineFraction) return false;
        if (relief >= FullCoverFraction) return true;

        // Between the two, thin out with a hash so the snowline is broken up
        // instead of a contour drawn round the mountain.
        float t = Mathf.InverseLerp(SnowlineFraction, FullCoverFraction, relief);

        return Hash(tileX, tileZ, worldSeed) % 1000 < t * 1000f;
    }

    public static Mesh BuildMesh(Vector2Int chunkIndex, int worldSeed)
    {
        int originX = chunkIndex.x * WorldGrid.TilesPerChunk;
        int originZ = chunkIndex.y * WorldGrid.TilesPerChunk;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        // Wider than the tile it covers. Snow laid exactly tile-sized leaves
        // the ground showing wherever two tiles sit at different heights --
        // every step in the land drew a green line across the snowfield. The
        // overhang hides the step. Two sheets meeting are at different heights
        // by construction, so overlapping them costs nothing.
        float half = WorldGrid.TileSize * 0.64f;

        for (int i = 0; i < WorldGrid.TilesPerChunk; i++)
        for (int j = 0; j < WorldGrid.TilesPerChunk; j++)
        {
            int tileX = originX + i;
            int tileZ = originZ + j;

            if (!IsSnowy(tileX, tileZ, worldSeed)) continue;

            // just clear of the tile, so it does not fight with the ground
            float y = WorldHeight.SurfaceY(tileX, tileZ, worldSeed) + 0.04f;

            float x = i * WorldGrid.TileSize;
            float z = j * WorldGrid.TileSize;

            int v = vertices.Count;

            vertices.Add(new Vector3(x - half, y, z - half));
            vertices.Add(new Vector3(x - half, y, z + half));
            vertices.Add(new Vector3(x + half, y, z + half));
            vertices.Add(new Vector3(x + half, y, z - half));

            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        }

        if (vertices.Count == 0) return null;

        var mesh = new Mesh { name = "Snow " + chunkIndex };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public static Material CreateMaterial()
    {
        Shader lit = Shaders.First("Universal Render Pipeline/Lit", "Standard");
        var m = new Material(lit);

        var colour = new Color(0.93f, 0.95f, 0.97f);

        m.SetColor("_BaseColor", colour);
        m.color = colour;
        m.SetFloat("_Smoothness", 0.28f);

        return m;
    }

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
