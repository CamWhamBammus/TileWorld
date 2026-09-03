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

    /// <summary>How far the snow stands above the ground it lies on.</summary>
    private const float Rise = 0.17f;

    /// <summary>And how far it hangs down over the edge of the tile.</summary>
    private const float Skirt = 0.26f;

    /// <summary>How many corners the rim of one tile's snow has.</summary>
    private const int PerSide = 3;
    private const int Corners = PerSide * 4;

    // The pack's grass cap is a square, not a disc: on Big Grass Tile_45 it
    // matches a square outline to within 0.18 and sits 1.25 out against the
    // dirt block's 1.00, while a circle through the same corners would be 1.41
    // times too wide. So snow is a square too, oversized the same way, with the
    // rim pushed out here and there rather than rounded off.
    private const float Spread = 1.06f;

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

            // The rim: a square, with several points along each side so the
            // sides need not be straight. A drift that has drifted has an
            // uneven edge, and that is most of what makes it look laid on
            // rather than printed on -- but it is still a square.
            var rim = new Vector3[Corners];

            for (int c = 0; c < Corners; c++)
            {
                Vector2 edge = Perimeter(c);

                int wobble = Mathf.Abs(Hash(tileX * 31 + c, tileZ * 17 - c, worldSeed + 811));

                // Outward only. Pulled in, a rim point opens a notch of bare
                // rock at the seam it shares with the tile next door.
                float reach = half * (Spread + (wobble % 100) / 100f * 0.14f);

                rim[c] = new Vector3(x + edge.x * reach, ground + Rise, z + edge.y * reach);
            }

            // and a crown a touch above the rim, so the top is a low mound
            // rather than a plate. Two mounds meeting cross at an angle; two
            // plates at one height argue over which is in front.
            var crown = new Vector3(x, ground + Rise + 0.035f, z);

            for (int c = 0; c < Corners; c++)
            {
                Vector3 one = rim[c];
                Vector3 two = rim[(c + 1) % Corners];

                // The top. Corners run anticlockwise around the tile, so the
                // pair goes in backwards -- wound the other way the face looks
                // at the ground, takes no sun, and snow comes back grey.
                Face(vertices, triangles, crown, two, one);

                // and the side of it, hanging over the edge of the tile the way
                // the grass on the other tiles hangs over theirs
                Vector3 underOne = new Vector3(one.x, ground - Skirt, one.z);
                Vector3 underTwo = new Vector3(two.x, ground - Skirt, two.z);

                Face(vertices, triangles, one, two, underOne);
                Face(vertices, triangles, two, underTwo, underOne);
            }
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
    /// Corner c of the unit square, walked anticlockwise from the middle of the
    /// +x edge, with PerSide points along each side. The walk has to turn the
    /// same way the old ring of angles did, or every face ends up inside out.
    /// </summary>
    private static Vector2 Perimeter(int c)
    {
        int side = c / PerSide;
        float t = (c % PerSide) / (float)PerSide * 2f - 1f;

        switch (side)
        {
            case 0: return new Vector2(1f, t);
            case 1: return new Vector2(-t, 1f);
            case 2: return new Vector2(-1f, -t);
            default: return new Vector2(t, -1f);
        }
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
