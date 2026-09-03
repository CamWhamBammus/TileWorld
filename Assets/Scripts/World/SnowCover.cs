using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        // Not on a lake or pond floor. Snow settles on the ground it can
        // reach, and the bed of a pond is under several feet of water. This
        // asks height alone, so it cannot loop back round through the regions.
        if (WaterSurface.IsUnderwater(tileX, tileZ, worldSeed)) return false;

        if (Regions.CharacterAtTile(tileX, tileZ, worldSeed) == Regions.Character.Snow) return true;

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

    /// <summary>How far the snow stands above the ground it lies on.</summary>
    private const float Rise = 0.17f;

    /// <summary>And how far it hangs down over the edge of the tile.</summary>
    private const float Skirt = 0.26f;

    // How far snow reaches past the edge of its tile, where there is nothing
    // to meet. The pack's grass cap does the same: on Big Grass Tile_45 it
    // sits 1.25 out against the dirt block's 1.00.
    private const float Spread = 1.06f;

    private static readonly Vector2Int[] Sides =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    public static Mesh BuildMesh(Vector2Int chunkIndex, int worldSeed)
    {
        int originX = chunkIndex.x * WorldGrid.TilesPerChunk;
        int originZ = chunkIndex.y * WorldGrid.TilesPerChunk;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        float half = WorldGrid.TileSize * 0.5f;

        for (int i = 0; i < WorldGrid.TilesPerChunk; i++)
        for (int j = 0; j < WorldGrid.TilesPerChunk; j++)
        {
            int tileX = originX + i;
            int tileZ = originZ + j;

            if (!IsSnowy(tileX, tileZ, worldSeed)) continue;

            float ground = WorldHeight.SurfaceY(tileX, tileZ, worldSeed);

            float x = i * WorldGrid.TileSize;
            float z = j * WorldGrid.TileSize;

            int terrace = WorldHeight.TerraceAt(tileX, tileZ, worldSeed);

            // How far the snow reaches on each side. Where the tile next door
            // is snow at the same height, it stops exactly on the shared edge
            // so the two surfaces meet and the seam disappears. Anywhere else
            // -- open ground, or a step up or down -- it reaches past and hangs
            // over, the way the grass cap does on the tiles below.
            var reach = new float[4];
            var hangs = new bool[4];

            for (int d = 0; d < 4; d++)
            {
                int nx = tileX + Sides[d].x;
                int nz = tileZ + Sides[d].y;

                bool flush = IsSnowy(nx, nz, worldSeed)
                    && WorldHeight.TerraceAt(nx, nz, worldSeed) == terrace;

                hangs[d] = !flush;
                reach[d] = flush ? half : half * Spread;
            }

            float top = ground + Rise;
            float low = ground - Skirt;

            // corners, named by which way they lie: A is +x +z, and round.
            var a = new Vector3(x + reach[0], top, z + reach[2]);
            var b = new Vector3(x - reach[1], top, z + reach[2]);
            var c = new Vector3(x - reach[1], top, z - reach[3]);
            var d2 = new Vector3(x + reach[0], top, z - reach[3]);

            Face(vertices, triangles, a, c, b);
            Face(vertices, triangles, a, d2, c);

            // and a wall down each side that meets nothing
            if (hangs[0]) Wall(vertices, triangles, a, d2, low);
            if (hangs[1]) Wall(vertices, triangles, c, b, low);
            if (hangs[2]) Wall(vertices, triangles, b, a, low);
            if (hangs[3]) Wall(vertices, triangles, d2, c, low);
        }

        if (vertices.Count == 0) return null;

        var mesh = new Mesh { name = "Snow " + chunkIndex };

        if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }


    /// <summary>
    /// A vertical wall hanging from the top edge p-q down to lowY. Which of p
    /// and q comes first decides which way the wall faces: the outward normal
    /// works out as (-dz, 0, dx) for d = q - p, so the pair goes clockwise
    /// round the tile seen from above.
    /// </summary>
    private static void Wall(List<Vector3> vertices, List<int> triangles, Vector3 p, Vector3 q, float lowY)
    {
        var pl = new Vector3(p.x, lowY, p.z);
        var ql = new Vector3(q.x, lowY, q.z);

        Face(vertices, triangles, p, ql, q);
        Face(vertices, triangles, p, pl, ql);
    }

    /// <summary>
    /// One triangle with its own three corners. Snow is flat shaded like
    /// everything else here, and corners shared between faces average their
    /// normals into a smooth thing that does not match the rest of the world.
    /// </summary>
    private static void Face(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int at = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        triangles.Add(at);
        triangles.Add(at + 1);
        triangles.Add(at + 2);
    }

    public static Material CreateMaterial()
    {
        Shader lit = Shaders.First("Universal Render Pipeline/Lit", "Standard");
        var m = new Material(lit);

        // Brighter than it was. Flat sheets took the light square on; a mound
        // takes it at an angle and comes back grey, and grey snow reads as
        // stone -- which the ground under it now actually is.
        var colour = new Color(0.98f, 0.99f, 1f);

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
