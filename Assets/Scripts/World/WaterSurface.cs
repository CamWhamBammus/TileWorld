using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standing water in the low ground. Built per chunk from the tiles that sit
/// below the water line, rather than one plane over everything, so a pond fills
/// a hollow instead of flooding the hillside next to it.
/// </summary>
public static class WaterSurface
{
    /// <summary>
    /// Height of the water, above the base ground plane. Measured rather than
    /// guessed: the terrain never drops below 1.63 above base and averages
    /// 14.25, so a line near zero put water nowhere at all. At 4.5 about one
    /// chunk in ten has some, which reads as tarns in hollows rather than a
    /// flooded world.
    /// </summary>
    public const float DepthAboveBase = 4.5f;

    public static float Level => WorldHeight.BaseSurfaceY + DepthAboveBase;

    /// <summary>
    /// What sort of water this is. They want different things of the ground
    /// and of what grows in them: sand belongs on an open shore and nowhere
    /// else, and reeds belong anywhere but.
    /// </summary>
    public enum Body
    {
        /// <summary>Open water, of a region named for being under it: a coast.</summary>
        Beach,

        /// <summary>Inland water with some depth to it.</summary>
        Lake,

        /// <summary>Inland water with none.</summary>
        Pond
    }

    public static Body BodyAt(int tileX, int tileZ, int worldSeed)
    {
        // A region wet enough to be named for it is open water, and open water
        // has a shore: that is the one place sand belongs.
        if (Regions.CharacterAtTile(tileX, tileZ, worldSeed, false) == Regions.Character.Water) return Body.Beach;

        // Otherwise it is a lake or a pond, and which one is a question of how
        // far down it goes rather than how far across.
        float deepest = Level - WorldHeight.SurfaceY(tileX, tileZ, worldSeed);

        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;

            int ox = Mathf.RoundToInt(Mathf.Cos(a) * 5f);
            int oz = Mathf.RoundToInt(Mathf.Sin(a) * 5f);

            deepest = Mathf.Max(deepest, Level - WorldHeight.SurfaceY(tileX + ox, tileZ + oz, worldSeed));
        }

        return deepest > 2.2f ? Body.Lake : Body.Pond;
    }

    public static bool IsUnderwater(int tileX, int tileZ, int seed)
    {
        return WorldHeight.SurfaceY(tileX, tileZ, seed) < Level;
    }

    /// <summary>A quad for every submerged tile, or null if the chunk is dry.</summary>
    public static Mesh BuildMesh(Vector2Int chunkIndex, int worldSeed)
    {
        int originX = chunkIndex.x * WorldGrid.TilesPerChunk;
        int originZ = chunkIndex.y * WorldGrid.TilesPerChunk;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        float half = WorldGrid.TileSize * 0.5f;
        float y = Level;

        for (int i = 0; i < WorldGrid.TilesPerChunk; i++)
        for (int j = 0; j < WorldGrid.TilesPerChunk; j++)
        {
            int tileX = originX + i;
            int tileZ = originZ + j;

            if (!IsUnderwater(tileX, tileZ, worldSeed)) continue;

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

        var mesh = new Mesh { name = "Water " + chunkIndex };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>Translucent, unlit enough to read as water without a shader of its own.</summary>
    public static Material CreateMaterial()
    {
        Shader lit = Shaders.First("Universal Render Pipeline/Lit", "Standard");
        var m = new Material(lit);

        // This used to be nearly opaque, at 0.88, and for a good reason: the
        // bed was dark grass with the trees taken out of it, so anything you
        // could see through the water was a submerged field and it read as a
        // flood rather than a lake.
        //
        // The bed is sand in the shallows and rock below that now, which is
        // worth seeing, so the water is let go a little. Shallow water shows
        // its bottom and deep water keeps it, which is the whole of why water
        // looks deep.
        var colour = new Color(0.20f, 0.40f, 0.50f, 0.72f);

        m.SetColor("_BaseColor", colour);
        m.color = colour;
        m.SetFloat("_Smoothness", 0.85f);

        // transparent surface, set by hand since this material is made in code
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return m;
    }
}
