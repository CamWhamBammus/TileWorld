using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the surface the player actually walks on.
///
/// The tiles are terraces, and the collider follows them: flat across each
/// tile at the tile's own height, so a foot on a tile is on the tile and
/// not inside it. Where a neighbour stands higher than the controller can
/// step (0.45), the lower tile carries a ramp in the band beside that edge,
/// as steep as the controller can climb; and where the step is taller than
/// such a ramp can take, the higher tile gives a little at its own edge
/// too, so a two-metre cliff is still the climb it was. Five vertices a
/// side per tile, none shared with the next, so every tile is its own
/// flat.
///
/// The old collider ran smoothly through the tile centres, which put the
/// surface a foot under the edge of any tile beside a lower one and a metre
/// under it beside a cliff: landing there, you stood inside the ground.
/// </summary>
public static class TerrainCollision
{
    private const int Cells = 4;                 // per tile side
    private const float StepClimb = 0.42f;       // what the controller steps up by itself
    private const float RampRise = 0.84f;        // what an in-tile ramp will take, at about forty degrees
    private const float DipBand = 1.0f;          // how far into the higher tile a cliff's remainder is taken

    public static Mesh BuildMesh(Vector2Int chunkIndex, int worldSeed)
    {
        int n = WorldGrid.TilesPerChunk;
        int originX = chunkIndex.x * n, originZ = chunkIndex.y * n;
        float half = WorldGrid.TileSize * 0.5f;

        var vertices = new List<Vector3>(n * n * (Cells + 1) * (Cells + 1));
        var triangles = new List<int>(n * n * Cells * Cells * 6);

        for (int tx = 0; tx < n; tx++)
        for (int tz = 0; tz < n; tz++)
        {
            int gx = originX + tx, gz = originZ + tz;
            float h = WaterSurface.WalkingY(gx, gz, worldSeed);
            // the four neighbours across the edges: +x, -x, +z, -z
            float ex = WaterSurface.WalkingY(gx + 1, gz, worldSeed), wx = WaterSurface.WalkingY(gx - 1, gz, worldSeed);
            float nz = WaterSurface.WalkingY(gx, gz + 1, worldSeed), sz = WaterSurface.WalkingY(gx, gz - 1, worldSeed);

            int first = vertices.Count;
            for (int i = 0; i <= Cells; i++)
            for (int j = 0; j <= Cells; j++)
            {
                float u = -half + WorldGrid.TileSize * i / Cells;   // local x, -1..1
                float v = -half + WorldGrid.TileSize * j / Cells;   // local z
                float y = h;
                float raise = 0f, dip = 0f;
                Edge(ref raise, ref dip, h, ex, half - u);
                Edge(ref raise, ref dip, h, wx, half + u);
                Edge(ref raise, ref dip, h, nz, half - v);
                Edge(ref raise, ref dip, h, sz, half + v);
                y += raise > 0f ? raise : -dip;
                vertices.Add(new Vector3(tx * WorldGrid.TileSize + u, y, tz * WorldGrid.TileSize + v));
            }
            for (int i = 0; i < Cells; i++)
            for (int j = 0; j < Cells; j++)
            {
                int a = first + i * (Cells + 1) + j, b = a + 1, c = a + Cells + 1, d = c + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(d);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
        }

        var mesh = new Mesh { name = "ChunkCollision " + chunkIndex, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// What one edge does to a vertex at <paramref name="d"/> metres from it:
    /// a rise toward a taller neighbour, in a band as wide as the rise needs
    /// at about forty degrees; or a dip toward a much lower one, over the
    /// outer metre, taking whatever the neighbour's own ramp cannot.
    /// </summary>
    private static void Edge(ref float raise, ref float dip, float h, float neighbour, float d)
    {
        float step = neighbour - h;
        if (step > StepClimb)
        {
            float rise = Mathf.Min(step, RampRise);
            float width = Mathf.Clamp(rise * 1.19f, 0.5f, 1.0f);
            if (d < width) raise = Mathf.Max(raise, rise * (1f - d / width));
        }
        else if (-step > RampRise)
        {
            float remainder = -step - RampRise;
            if (d < DipBand) dip = Mathf.Max(dip, remainder * (1f - d / DipBand));
        }
    }
}
