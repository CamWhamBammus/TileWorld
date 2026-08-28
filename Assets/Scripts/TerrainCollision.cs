using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the surface the player actually walks on.
///
/// The tiles are drawn as discrete terraces, but a stepped collider would be
/// unwalkable: the CharacterController's step offset is 0.25, so anything
/// taller than one terrace would stop the player dead. Instead the collision
/// mesh ramps between tile centres — a terrace of 0.25 over a 2-unit tile is
/// about a 7 degree slope, well inside the 45 degree limit. Visually terraced,
/// physically smooth.
/// </summary>
public static class TerrainCollision
{
    /// <summary>
    /// One vertex per tile centre, plus a ring one tile beyond the chunk on
    /// each side. The overlap is what makes neighbouring chunk colliders line
    /// up exactly instead of leaving a crack at the seam.
    /// </summary>
    public static Mesh BuildMesh(Vector2Int chunkIndex, int worldSeed)
    {
        const int pad = 1;
        int span = WorldGrid.TilesPerChunk + pad * 2;

        var vertices = new Vector3[span * span];
        var triangles = new List<int>((span - 1) * (span - 1) * 6);

        int originTileX = chunkIndex.x * WorldGrid.TilesPerChunk;
        int originTileZ = chunkIndex.y * WorldGrid.TilesPerChunk;

        // Vertices are positioned in the chunk's local space; the collider
        // object itself is placed at the chunk origin.
        for (int i = 0; i < span; i++)
        for (int j = 0; j < span; j++)
        {
            int tileX = originTileX + i - pad;
            int tileZ = originTileZ + j - pad;

            vertices[i * span + j] = new Vector3(
                (i - pad) * WorldGrid.TileSize,
                WorldHeight.SurfaceY(tileX, tileZ, worldSeed),
                (j - pad) * WorldGrid.TileSize
            );
        }

        for (int i = 0; i < span - 1; i++)
        for (int j = 0; j < span - 1; j++)
        {
            int a = i * span + j;
            int b = a + 1;
            int c = (i + 1) * span + j;
            int d = c + 1;

            triangles.Add(a); triangles.Add(b); triangles.Add(d);
            triangles.Add(a); triangles.Add(d); triangles.Add(c);
        }

        var mesh = new Mesh { name = "ChunkCollision " + chunkIndex };
        mesh.vertices = vertices;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }
}
