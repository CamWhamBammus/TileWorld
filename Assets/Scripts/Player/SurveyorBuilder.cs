using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The person doing the charting, built the same way the animals are: swept
/// tubes, flat shaded, in the colours the tiles are painted from. Somebody in
/// a field coat and boots with a bag on their hip, a rolled chart on their back
/// and a broad hat against the sun — which is what a person crossing unmapped
/// country looks like, and is not a robot.
/// </summary>
public static class SurveyorBuilder
{
    public struct Figure
    {
        public Transform Root;       // everything, so it can lean and bob
        public Transform Head;
        public Transform[] Legs;     // hips
        public Transform[] Knees;
        public Transform[] Ankles;
        public Transform[] Arms;     // shoulders
        public Transform[] Elbows;
        public Transform Book;       // the sketchbook, out only while drawing
        public Transform Pencil;
    }

    private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    // out of the same palette the tiles and the animals are painted from
    private const int CoatCoat = 0, CoatLight = 1, CoatDark = 2, CoatSkin = 3;

    private static readonly Color[] Palette =
    {
        new Color(0.604f, 0.384f, 0.220f),   // 9A6238 waxed field coat
        new Color(0.886f, 0.816f, 0.675f),   // E2D0AC canvas
        new Color(0.200f, 0.161f, 0.122f),   // 33291F leather
        new Color(0.753f, 0.557f, 0.388f)    // C08E63 weathered skin
    };

    public static Figure Build(Transform parent, float height)
    {
        float h = height;

        var rootGo = new GameObject("surveyor");
        rootGo.transform.SetParent(parent, false);

        var figure = new Figure
        {
            Root = rootGo.transform,
            Legs = new Transform[2],
            Knees = new Transform[2],
            Ankles = new Transform[2],
            Arms = new Transform[2],
            Elbows = new Transform[2]
        };

        var body = new List<CreatureMesh.Piece>();

        // the coat: broad at the shoulder, drawn in at the waist, flaring to a
        // hem that stops above the knee so there are legs under it
        Add(body, CoatCoat, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.398f * h, 0f),
                new Vector3(0f, 0.428f * h, 0f),
                new Vector3(0f, 0.500f * h, 0f),
                new Vector3(0f, 0.575f * h, 0f),
                new Vector3(0f, 0.660f * h, 0f),
                new Vector3(0f, 0.735f * h, 0f),
                new Vector3(0f, 0.778f * h, 0f)
            },
            new[] { 0.062f * h, 0.150f * h, 0.118f * h, 0.100f * h, 0.124f * h, 0.134f * h, 0.116f * h }, 9, 0.70f));

        // a belt at the waist, which is what gives it the shape
        Add(body, CoatDark, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.566f * h, 0f), new Vector3(0f, 0.596f * h, 0f) },
            new[] { 0.108f * h, 0.108f * h }, 9, 0.72f));

        // a turned-up collar, and the neck out of it
        Add(body, CoatLight, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.772f * h, 0f), new Vector3(0f, 0.802f * h, 0f) },
            new[] { 0.090f * h, 0.072f * h }, 9, 0.80f));

        Add(body, CoatSkin, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.792f * h, 0f), new Vector3(0f, 0.828f * h, 0f) },
            new[] { 0.037f * h, 0.035f * h }, 7, 1f));

        // the strap of the bag, over one shoulder and across the chest
        Add(body, CoatDark, CreatureMesh.Taper(
            new Vector3(-0.082f * h, 0.762f * h, -0.012f * h),
            new Vector3(0.092f * h, 0.505f * h, 0.030f * h), 0.020f * h, 0.017f * h, 5));

        // the bag on the hip, with a flap over it
        Add(body, CoatDark, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0.100f * h, 0.474f * h, -0.015f * h),
                new Vector3(0.134f * h, 0.466f * h, -0.015f * h)
            },
            new[] { 0.054f * h, 0.049f * h }, 6, 1.15f));

        Add(body, CoatLight, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0.098f * h, 0.510f * h, -0.015f * h),
                new Vector3(0.136f * h, 0.503f * h, -0.015f * h)
            },
            new[] { 0.050f * h, 0.045f * h }, 6, 0.55f));

        // and the chart itself, rolled and slung diagonally across the back so
        // it reads from behind and from the side
        Add(body, CoatLight, CreatureMesh.Tube(
            new[]
            {
                new Vector3(-0.118f * h, 0.688f * h, -0.086f * h),
                new Vector3(0.118f * h, 0.528f * h, -0.080f * h)
            },
            new[] { 0.018f * h, 0.018f * h }, 7, 1f));

        Add(body, CoatDark, CreatureMesh.Tube(
            new[]
            {
                new Vector3(-0.128f * h, 0.695f * h, -0.086f * h),
                new Vector3(-0.104f * h, 0.679f * h, -0.086f * h)
            },
            new[] { 0.022f * h, 0.022f * h }, 7, 1f));

        Add(body, CoatDark, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0.104f * h, 0.537f * h, -0.081f * h),
                new Vector3(0.128f * h, 0.521f * h, -0.080f * h)
            },
            new[] { 0.022f * h, 0.022f * h }, 7, 1f));

        Part(figure.Root, "body", body);

        // the head, under a brim wider than the crown, which is what makes a
        // hat a hat and not a mushroom
        var headPivot = new GameObject("head").transform;
        headPivot.SetParent(figure.Root, false);
        headPivot.localPosition = new Vector3(0f, 0.828f * h, 0f);
        figure.Head = headPivot;

        var head = new List<CreatureMesh.Piece>();

        Add(head, CoatSkin, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.004f * h, 0f),
                new Vector3(0f, 0.042f * h, 0.004f * h),
                new Vector3(0f, 0.076f * h, 0f)
            },
            new[] { 0.050f * h, 0.066f * h, 0.052f * h }, 9, 1.05f));

        Add(head, CoatSkin, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.040f * h, 0.052f * h),
                new Vector3(0f, 0.030f * h, 0.072f * h)
            },
            new[] { 0.017f * h, 0.010f * h }, 5, 1f));       // a nose, to have a face at all

        Add(head, CoatDark, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.070f * h, 0f), new Vector3(0f, 0.086f * h, 0f) },
            new[] { 0.062f * h, 0.062f * h }, 9, 1.06f));    // the band

        Add(head, CoatLight, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.082f * h, 0.002f * h), new Vector3(0f, 0.092f * h, 0.002f * h) },
            new[] { 0.096f * h, 0.093f * h }, 12, 1.36f),
            Squash(0.087f * h, 0.13f));                      // the brim, long front to back

        for (int side = 0; side < 2; side++)
        {
            float x = (side == 0 ? 1f : -1f) * 0.092f * h;

            Add(head, CoatLight, CreatureMesh.Tube(
                new[]
                {
                    new Vector3(x, 0.089f * h, -0.072f * h),
                    new Vector3(x * 1.10f, 0.106f * h, 0f),
                    new Vector3(x, 0.089f * h, 0.072f * h)
                },
                new[] { 0.020f * h, 0.024f * h, 0.020f * h }, 6, 0.9f),
                Squash(0.090f * h, 0.62f));
        }

        Add(head, CoatLight, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.084f * h, 0f),
                new Vector3(0f, 0.135f * h, 0f),
                new Vector3(0f, 0.172f * h, 0f)
            },
            new[] { 0.064f * h, 0.060f * h, 0.054f * h }, 9, 1.06f),
            Squash(0.084f * h, 0.62f));                              // and the crown, tall and tapered

        Part(headPivot, "head", head);

        // arms and legs, jointed, so they can be made to walk
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? 1f : -1f;

            figure.Arms[side] = Limb(figure.Root, "shoulder", new Vector3(0.114f * h * x, 0.742f * h, 0f));
            Part(figure.Arms[side], "upper arm", Arm(h, true));

            figure.Elbows[side] = Limb(figure.Arms[side], "elbow", new Vector3(0f, -0.135f * h, 0f));
            Part(figure.Elbows[side], "forearm", Arm(h, false));

            figure.Legs[side] = Limb(figure.Root, "hip", new Vector3(0.052f * h * x, 0.44f * h, 0f));
            Part(figure.Legs[side], "thigh", Leg(h, 0));

            figure.Knees[side] = Limb(figure.Legs[side], "knee", new Vector3(0f, -0.21f * h, 0f));
            Part(figure.Knees[side], "shin", Leg(h, 1));

            figure.Ankles[side] = Limb(figure.Knees[side], "ankle", new Vector3(0f, -0.132f * h, 0f));
            Part(figure.Ankles[side], "boot", Leg(h, 2));
        }

        figure.Book = Held(figure.Elbows[0], "sketchbook",
            new Vector3(-0.026f * h, -0.148f * h, 0.034f * h),
            new Vector3(-52f, 10f, 12f), Book(h));

        figure.Pencil = Held(figure.Elbows[1], "pencil",
            new Vector3(0.010f * h, -0.140f * h, 0.020f * h),
            new Vector3(-58f, 0f, 18f), Pencil(h));

        return figure;
    }

    private static List<CreatureMesh.Piece> Arm(float h, bool upper)
    {
        var pieces = new List<CreatureMesh.Piece>();

        if (upper)
        {
            // a cap over the joint, so the shoulder never opens up when it swings
            Add(pieces, CoatCoat, CreatureMesh.Tube(
                new[] { new Vector3(0f, 0.008f * h, 0f), new Vector3(0f, -0.020f * h, 0f) },
                new[] { 0.042f * h, 0.044f * h }, 8, 1f));

            Add(pieces, CoatCoat, CreatureMesh.Tube(
                new[] { new Vector3(0f, 0.010f * h, 0f), new Vector3(0f, -0.135f * h, 0f) },
                new[] { 0.046f * h, 0.038f * h }, 7, 1f));
        }
        else
        {
            Add(pieces, CoatCoat, CreatureMesh.Tube(
                new[] { new Vector3(0f, 0.014f * h, 0f), new Vector3(0f, -0.098f * h, 0f) },
                new[] { 0.038f * h, 0.030f * h }, 7, 1f));

            Add(pieces, CoatSkin, CreatureMesh.Tube(
                new[] { new Vector3(0f, -0.098f * h, 0f), new Vector3(0f, -0.136f * h, 0.006f * h) },
                new[] { 0.031f * h, 0.026f * h }, 7, 1f));       // the hand
        }

        return pieces;
    }

    private static List<CreatureMesh.Piece> Leg(float h, int part)
    {
        var pieces = new List<CreatureMesh.Piece>();

        if (part == 0)
        {
            Add(pieces, CoatLight, CreatureMesh.Tube(
                new[] { new Vector3(0f, 0.012f * h, 0f), new Vector3(0f, -0.21f * h, 0f) },
                new[] { 0.060f * h, 0.048f * h }, 7, 1f));
        }
        else if (part == 1)
        {
            Add(pieces, CoatLight, CreatureMesh.Tube(
                new[] { new Vector3(0f, 0.010f * h, 0f), new Vector3(0f, -0.132f * h, 0f) },
                new[] { 0.046f * h, 0.040f * h }, 7, 1f));
        }
        else
        {
            // the boot hangs off an ankle of its own: a foot welded to the shin
            // keeps the same angle all the way through a stride, and a leg that
            // never rolls over its toe is the stiffest thing about a walk
            Add(pieces, CoatDark, CreatureMesh.Tube(
                new[]
                {
                    new Vector3(0f, 0.008f * h, -0.005f * h),
                    new Vector3(0f, -0.082f * h, 0f),
                    new Vector3(0f, -0.093f * h, 0.038f * h)
                },
                new[] { 0.050f * h, 0.047f * h, 0.032f * h }, 7, 1f));
        }

        return pieces;
    }


    /// <summary>Something carried in a hand, hidden until it is called for.</summary>
    private static Transform Held(Transform hand, string name, Vector3 at, Vector3 turned,
                                  List<CreatureMesh.Piece> pieces)
    {
        var go = new GameObject(name);
        go.transform.SetParent(hand, false);
        go.transform.localPosition = at;
        go.transform.localEulerAngles = turned;

        Part(go.transform, name + " parts", pieces);
        go.SetActive(false);

        return go.transform;
    }

    private static List<CreatureMesh.Piece> Book(float h)
    {
        var pieces = new List<CreatureMesh.Piece>();

        // a board, and a leaf of paper on top of it a little smaller
        Add(pieces, CoatDark, Slab(0.150f * h, 0.190f * h, 0.010f * h), Matrix4x4.identity);
        Add(pieces, CoatLight, Slab(0.132f * h, 0.170f * h, 0.008f * h),
            Matrix4x4.Translate(new Vector3(0f, 0.004f * h, 0.007f * h)));

        return pieces;
    }

    private static List<CreatureMesh.Piece> Pencil(float h)
    {
        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, CoatDark, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.034f * h, 0f), new Vector3(0f, -0.020f * h, 0f) },
            new[] { 0.007f * h, 0.007f * h }, 5, 1f));

        Add(pieces, CoatLight, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.020f * h, 0f), new Vector3(0f, -0.034f * h, 0f) },
            new[] { 0.007f * h, 0.001f * h }, 5, 1f));

        return pieces;
    }

    /// <summary>A flat board. Every face keeps its own corners, so it shades flat.</summary>
    private static Mesh Slab(float wide, float tall, float thick)
    {
        float x = wide * 0.5f, y = tall * 0.5f, z = thick * 0.5f;

        var corners = new[]
        {
            new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z),
            new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z)
        };

        int[][] faces =
        {
            new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
            new[] { 2, 3, 7, 6 }, new[] { 1, 2, 6, 5 }, new[] { 0, 4, 7, 3 }
        };

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        foreach (var face in faces)
        {
            int a = vertices.Count;
            foreach (int corner in face) vertices.Add(corners[corner]);

            triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
            triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 3);
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();

        return mesh;
    }

    private static Transform Limb(Transform parent, string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;

        return go.transform;
    }

    /// <summary>
    /// Combining drops any colour the group never used, which renumbers the
    /// submeshes — so the materials have to be handed back in the order the
    /// combine actually reports, not in palette order.
    /// </summary>
    private static void Part(Transform parent, string name, List<CreatureMesh.Piece> pieces)
    {
        var mesh = CreatureMesh.Combine(pieces, out int[] coats);
        var mats = new Material[coats.Length];

        for (int i = 0; i < coats.Length; i++) mats[i] = Mat(Palette[coats[i]]);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
    }

    private static void Add(List<CreatureMesh.Piece> into, int coat, Mesh mesh)
    {
        Add(into, coat, mesh, Matrix4x4.identity);
    }

    private static void Add(List<CreatureMesh.Piece> into, int coat, Mesh mesh, Matrix4x4 at)
    {
        into.Add(new CreatureMesh.Piece { Mesh = mesh, At = at, Coat = coat });
    }

    /// <summary>Presses a piece down towards a height, keeping that height put.</summary>
    private static Matrix4x4 Squash(float about, float by)
    {
        return Matrix4x4.TRS(new Vector3(0f, about * (1f - by), 0f), Quaternion.identity,
                             new Vector3(1f, by, 1f));
    }

    private static Material Mat(Color c)
    {
        int key = c.GetHashCode();

        if (materials.TryGetValue(key, out var cached) && cached != null) return cached;

        var shader = Shaders.First("Universal Render Pipeline/Lit", "Standard");

        if (shader == null) return null;

        var material = new Material(shader);
        material.SetColor("_BaseColor", c);
        material.color = c;
        material.SetFloat("_Smoothness", 0.08f);
        material.SetFloat("_Metallic", 0f);
        material.enableInstancing = true;

        materials[key] = material;

        return material;
    }
}
