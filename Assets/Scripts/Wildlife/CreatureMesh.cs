using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mesh building for the animals. Boxes and cylinders can suggest a landmark,
/// where the straight lines are the point, but nothing alive is made of them:
/// an animal is a body that swells and tapers along its length, and reads by
/// its silhouette long before any detail on it does.
///
/// So a creature here is a set of tubes. Each is swept along a line with a
/// changing thickness, the frame carried from one ring to the next so a curved
/// neck does not twist as it bends, and the seam welded afterwards so the join
/// down the side does not catch the light.
/// </summary>
public static class CreatureMesh
{
    /// <summary>One piece of an animal, and which of the two coats it wears.</summary>
    public struct Piece
    {
        public Mesh Mesh;
        public Matrix4x4 At;
        public int Coat;        // 0 the coat, 1 the pale markings, 2 hoof and nose
    }

    /// <summary>
    /// A swept tube. `path` is the centre line, `radius` the thickness at each
    /// point of it, and `flatten` squashes the rings vertically so a body can
    /// be deeper than it is wide.
    /// </summary>
    public static Mesh Tube(Vector3[] path, float[] radius, int sides = 10, float flatten = 1f)
    {
        Dome(ref path, ref radius);

        int rings = path.Length;

        var vertices = new List<Vector3>((rings + 2) * (sides + 1));
        var triangles = new List<int>(rings * sides * 6);

        // The frame is carried along the line rather than rebuilt at each ring,
        // which is what stops a curving neck from spiralling.
        Vector3 up = Vector3.up;
        Vector3 previous = Tangent(path, 0);

        var normals = new Vector3[rings];
        var binormals = new Vector3[rings];

        for (int i = 0; i < rings; i++)
        {
            Vector3 tangent = Tangent(path, i);

            // rotate the carried frame by however much the line turned
            var turn = Quaternion.FromToRotation(previous, tangent);
            up = turn * up;

            Vector3 side = Vector3.Cross(up, tangent).normalized;

            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(Vector3.forward, tangent).normalized;

            up = Vector3.Cross(tangent, side).normalized;

            normals[i] = side;
            binormals[i] = up;
            previous = tangent;
        }

        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j <= sides; j++)
            {
                float angle = j / (float)sides * Mathf.PI * 2f;

                Vector3 offset = normals[i] * (Mathf.Cos(angle) * radius[i])
                               + binormals[i] * (Mathf.Sin(angle) * radius[i] * flatten);

                vertices.Add(path[i] + offset);
            }
        }

        for (int i = 0; i < rings - 1; i++)
        for (int j = 0; j < sides; j++)
        {
            int a = i * (sides + 1) + j;
            int b = a + sides + 1;

            // Wound so the face looks outward. The other way round the whole
            // animal is inside out: the near surface is culled away, you see
            // its own legs through it, and every normal points into the body
            // so the light lands on the wrong side of it.
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
            triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
        }

        // Ends are closed with a fan to a point just beyond the last ring, so a
        // tapered end comes to a nose rather than a hole.
        Cap(vertices, triangles, path, radius, sides, true);
        Cap(vertices, triangles, path, radius, sides, false);

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();

        Weld(mesh, rings, sides);

        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>A tube whose thickness follows a curve, for legs and tails.</summary>
    public static Mesh Taper(Vector3 from, Vector3 to, float thick, float thin, int sides = 8, int rings = 5)
    {
        var path = new Vector3[rings];
        var radius = new float[rings];

        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1);
            path[i] = Vector3.Lerp(from, to, t);
            radius[i] = Mathf.Lerp(thick, thin, t);
        }

        return Tube(path, radius, sides);
    }

    /// <summary>
    /// Everything joined into one mesh with two submeshes, one per coat, so a
    /// whole animal is a couple of draws rather than a dozen.
    /// </summary>
    public static Mesh Combine(List<Piece> pieces)
    {
        return Combine(pieces, out _);
    }

    /// <summary>
    /// Everything joined into one mesh, one submesh per coat that is actually
    /// used. Which coats those were comes back in `coats`: a mesh with nothing
    /// in its middle coat gets two submeshes, not three, and materials handed
    /// to it in the original order would land on the wrong parts of it.
    /// </summary>
    public static Mesh Combine(List<Piece> pieces, out int[] coats)
    {
        var byCoat = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };

        foreach (var piece in pieces)
        {
            byCoat[Mathf.Clamp(piece.Coat, 0, 2)].Add(
                new CombineInstance { mesh = piece.Mesh, transform = piece.At });
        }

        var parts = new List<CombineInstance>();
        var used = new List<int>();

        for (int i = 0; i < byCoat.Length; i++)
        {
            if (byCoat[i].Count == 0) continue;

            var one = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            one.CombineMeshes(byCoat[i].ToArray(), true, true);

            parts.Add(new CombineInstance { mesh = one, transform = Matrix4x4.identity });
            used.Add(i);
        }

        coats = used.ToArray();

        var all = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        all.CombineMeshes(parts.ToArray(), false, false);

        Facet(all);

        all.RecalculateBounds();

        return all;
    }

    /// <summary>
    /// Gives every triangle its own three corners, so each face is lit as a
    /// face. The tiles this world is built from are flat shaded and the light
    /// breaks over them in hard steps; an animal shaded smoothly sits on that
    /// ground looking like it wandered in from another game.
    /// </summary>
    public static void Facet(Mesh mesh)
    {
        var source = mesh.vertices;
        var faces = new List<Vector3>(mesh.triangles.Length);
        var sets = new List<int[]>();

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            var indices = mesh.GetTriangles(sub);
            var rebuilt = new int[indices.Length];

            for (int i = 0; i < indices.Length; i++)
            {
                rebuilt[i] = faces.Count;
                faces.Add(source[indices[i]]);
            }

            sets.Add(rebuilt);
        }

        mesh.Clear();
        mesh.SetVertices(faces);
        mesh.subMeshCount = sets.Count;

        for (int sub = 0; sub < sets.Count; sub++) mesh.SetTriangles(sets[sub], sub);

        mesh.RecalculateNormals();
    }

    public static Matrix4x4 At(Vector3 position, Vector3 euler, float scale = 1f)
    {
        return Matrix4x4.TRS(position, Quaternion.Euler(euler), Vector3.one * scale);
    }

    // ------------------------------------------------------------------ inside

    /// <summary>
    /// Adds a couple of drawn-in rings at each end so a tube finishes as a
    /// dome. Without them the closing fan is a flat lid the width of the tube,
    /// which reads as a cut end and shades like one.
    /// </summary>
    private static void Dome(ref Vector3[] path, ref float[] radius)
    {
        var points = new List<Vector3>(path.Length + 4);
        var radii = new List<float>(radius.Length + 4);

        Vector3 head = Tangent(path, 0);
        Vector3 tail = Tangent(path, path.Length - 1);

        points.Add(path[0] - head * radius[0] * 0.66f);
        radii.Add(radius[0] * 0.34f);
        points.Add(path[0] - head * radius[0] * 0.38f);
        radii.Add(radius[0] * 0.74f);

        points.AddRange(path);
        radii.AddRange(radius);

        int last = path.Length - 1;

        points.Add(path[last] + tail * radius[last] * 0.38f);
        radii.Add(radius[last] * 0.74f);
        points.Add(path[last] + tail * radius[last] * 0.66f);
        radii.Add(radius[last] * 0.34f);

        path = points.ToArray();
        radius = radii.ToArray();
    }

    private static Vector3 Tangent(Vector3[] path, int i)
    {
        Vector3 tangent;

        if (i == 0) tangent = path[1] - path[0];
        else if (i == path.Length - 1) tangent = path[i] - path[i - 1];
        else tangent = path[i + 1] - path[i - 1];

        return tangent.sqrMagnitude < 1e-8f ? Vector3.forward : tangent.normalized;
    }

    private static void Cap(List<Vector3> vertices, List<int> triangles, Vector3[] path, float[] radius,
                            int sides, bool start)
    {
        int ring = start ? 0 : path.Length - 1;
        Vector3 tangent = Tangent(path, ring) * (start ? -1f : 1f);

        // just past the end, so the cap is a dome rather than a lid
        vertices.Add(path[ring] + tangent * radius[ring] * 0.8f);

        int tip = vertices.Count - 1;
        int first = ring * (sides + 1);

        for (int j = 0; j < sides; j++)
        {
            if (start)
            {
                triangles.Add(tip); triangles.Add(first + j + 1); triangles.Add(first + j);
            }
            else
            {
                triangles.Add(tip); triangles.Add(first + j); triangles.Add(first + j + 1);
            }
        }
    }

    /// <summary>
    /// The first and last vertex of every ring sit on top of each other so the
    /// tube can be unwrapped, and RecalculateNormals treats them as two
    /// separate corners. Averaging the pair closes the crease.
    /// </summary>
    private static void Weld(Mesh mesh, int rings, int sides)
    {
        var normals = mesh.normals;

        for (int i = 0; i < rings; i++)
        {
            int a = i * (sides + 1);
            int b = a + sides;

            if (b >= normals.Length) break;

            Vector3 averaged = (normals[a] + normals[b]).normalized;
            normals[a] = averaged;
            normals[b] = averaged;
        }

        mesh.normals = normals;
    }
}
