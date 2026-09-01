using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the animals as meshes rather than as heaps of primitives. Each kind
/// is described as a set of swept shapes — a body that deepens at the chest and
/// narrows at the waist, a neck that curves up out of it, a head that tapers to
/// a muzzle, legs that bend at the knee and stand on something — and the whole
/// lot is welded into one mesh per moving part.
///
/// The shapes are built once per kind and shared by every animal of it, so a
/// field of deer costs one set of meshes and two materials.
/// </summary>
public static class AnimalBuilder
{
    public struct Body
    {
        public Transform Frame;          // everything, bobbed while moving
        public Transform Head;           // dips to the grass, comes up to look
        public Transform[] Legs;
        public Transform Tail;
    }

    /// <summary>A mesh with the materials that go on it, in submesh order.</summary>
    private struct Skin
    {
        public Mesh Mesh;
        public int[] Coats;
    }

    private class Kit
    {
        public Skin Trunk;               // body and neck together
        public Skin Head;
        public Skin ForeLeg;
        public Skin HindLeg;
        public Skin Tail;
        public Material[] Palette;

        public Vector3 Neck;             // where the head hangs from
        public Vector3 Hip;              // hind leg attachment, mirrored across x
        public Vector3 Shoulder;
        public Vector3 Rump;             // where the tail hangs from
    }

    private static readonly Dictionary<FaunaKind, Kit> kits = new Dictionary<FaunaKind, Kit>();
    private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    public static Body Build(FaunaKind kind, Transform parent)
    {
        var kit = KitFor(kind);

        var frameGo = new GameObject("frame");
        frameGo.transform.SetParent(parent, false);

        // No two animals of a kind are quite the same size.
        frameGo.transform.localScale = Vector3.one * Random.Range(0.92f, 1.09f);

        var body = new Body { Frame = frameGo.transform };
        var frame = frameGo.transform;

        Part(frame, "trunk", kit.Trunk, kit.Palette, Vector3.zero);

        body.Head = Pivot(frame, "head", kit.Neck);
        Part(body.Head, "skull", kit.Head, kit.Palette, Vector3.zero);

        body.Legs = new Transform[4];

        for (int i = 0; i < 4; i++)
        {
            bool fore = i < 2;
            float side = (i % 2 == 0) ? 1f : -1f;

            Vector3 at = fore ? kit.Shoulder : kit.Hip;
            at.x *= side;

            body.Legs[i] = Pivot(frame, fore ? "foreleg" : "hindleg", at);
            Part(body.Legs[i], "leg", fore ? kit.ForeLeg : kit.HindLeg, kit.Palette, Vector3.zero);
        }

        body.Tail = Pivot(frame, "tail", kit.Rump);
        Part(body.Tail, "brush", kit.Tail, kit.Palette, Vector3.zero);

        return body;
    }

    // ------------------------------------------------------------- the animals

    private static Kit KitFor(FaunaKind kind)
    {
        if (kits.TryGetValue(kind, out var cached) && cached.Trunk.Mesh != null) return cached;

        var traits = Fauna.Of(kind);

        Kit kit;

        switch (kind)
        {
            case FaunaKind.Deer: kit = Deer(traits.Size); break;
            case FaunaKind.Rabbit: kit = Rabbit(traits.Size); break;
            case FaunaKind.Fox: kit = Fox(traits.Size); break;
            default: kit = Goat(traits.Size); break;
        }

        kit.Palette = new[] { Mat(traits.Coat), Mat(traits.Under), Mat(traits.Dark) };

        kits[kind] = kit;

        return kit;
    }

    private static Kit Deer(float h)
    {
        var kit = new Kit
        {
            Neck = new Vector3(0f, 1.16f * h, 0.50f * h),
            Shoulder = new Vector3(0.060f * h, 0.80f * h, 0.19f * h),
            Hip = new Vector3(0.064f * h, 0.80f * h, -0.25f * h),
            Rump = new Vector3(0f, 0.88f * h, -0.46f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        // One line from the rump to the top of the neck. Built as separate
        // pieces the joins show as hard ellipses wherever two tubes cross;
        // swept in a single run there is nothing to cross.
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.84f * h, -0.50f * h),
                new Vector3(0f, 0.83f * h, -0.42f * h),
                new Vector3(0f, 0.83f * h, -0.26f * h),
                new Vector3(0f, 0.81f * h, -0.04f * h),
                new Vector3(0f, 0.83f * h, 0.18f * h),
                new Vector3(0f, 0.88f * h, 0.33f * h),
                new Vector3(0f, 0.97f * h, 0.42f * h),
                new Vector3(0f, 1.07f * h, 0.47f * h),
                new Vector3(0f, 1.15f * h, 0.50f * h)
            },
            new[]
            {
                0.050f * h, 0.120f * h, 0.152f * h, 0.140f * h, 0.158f * h,
                0.132f * h, 0.100f * h, 0.076f * h, 0.058f * h
            },
            16, 1.20f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.04f * h, -0.06f * h),
                new Vector3(0f, 0.03f * h, -0.01f * h),
                new Vector3(0f, 0.01f * h, 0.07f * h),
                new Vector3(0f, -0.03f * h, 0.18f * h),
                new Vector3(0f, -0.05f * h, 0.28f * h)
            },
            new[] { 0.030f * h, 0.070f * h, 0.058f * h, 0.044f * h, 0.026f * h }, 8, 1.10f));

        Ear(head, h, 1f, 0.050f, 0.06f, -0.02f, 0.125f, 0.15f, -0.07f, 0.032f);
        Ear(head, h, -1f, 0.050f, 0.06f, -0.02f, 0.125f, 0.15f, -0.07f, 0.032f);

        Antler(head, h, 1f);
        Antler(head, h, -1f);

        // a dark muzzle, which is the only two tone marking that reads at range
        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.035f * h, 0.20f * h), new Vector3(0f, -0.045f * h, 0.26f * h) },
            new[] { 0.032f * h, 0.020f * h }, 7, 1f));

        kit.Head = Wrap(head);

        kit.ForeLeg = Leg(h, 0.60f, 0.046f, 0.028f, 0.020f, 0.03f, -0.02f);
        kit.HindLeg = Leg(h, 0.60f, 0.056f, 0.032f, 0.020f, -0.08f, 0.03f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.05f * h, -0.07f * h), new Vector3(0f, -0.10f * h, -0.11f * h) },
            new[] { 0.032f * h, 0.030f * h, 0.014f * h }, 6, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static void Antler(List<CreatureMesh.Piece> head, float h, float side)
    {
        // A beam that sweeps up and back with tines off the front of it. Small
        // and stubby reads as a lump; the length is what says deer.
        var beam = new[]
        {
            new Vector3(0.032f * h * side, 0.06f * h, -0.02f * h),
            new Vector3(0.062f * h * side, 0.18f * h, -0.09f * h),
            new Vector3(0.090f * h * side, 0.30f * h, -0.10f * h),
            new Vector3(0.115f * h * side, 0.40f * h, -0.02f * h)
        };

        Add(head, 1, CreatureMesh.Tube(beam, new[] { 0.017f * h, 0.013f * h, 0.010f * h, 0.005f * h }, 5));

        Add(head, 1, CreatureMesh.Taper(beam[1], beam[1] + new Vector3(0.02f * h * side, 0.10f * h, 0.09f * h),
                                        0.010f * h, 0.004f * h, 5));
        Add(head, 1, CreatureMesh.Taper(beam[2], beam[2] + new Vector3(0.02f * h * side, 0.09f * h, 0.10f * h),
                                        0.009f * h, 0.004f * h, 5));
        Add(head, 1, CreatureMesh.Taper(beam[0] + new Vector3(0.004f * h * side, 0.03f * h, 0f),
                                        beam[0] + new Vector3(0.02f * h * side, 0.10f * h, 0.09f * h),
                                        0.009f * h, 0.004f * h, 5));
    }

    private static Kit Rabbit(float h)
    {
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.74f * h, 0.34f * h),
            Shoulder = new Vector3(0.060f * h, 0.44f * h, 0.14f * h),
            Hip = new Vector3(0.070f * h, 0.48f * h, -0.24f * h),
            Rump = new Vector3(0f, 0.52f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        // A rabbit is nearly all body and sits close to the ground: high over
        // the haunch, dipping at the shoulder, then up into a short neck.
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.52f * h, -0.50f * h),
                new Vector3(0f, 0.58f * h, -0.40f * h),
                new Vector3(0f, 0.62f * h, -0.26f * h),
                new Vector3(0f, 0.56f * h, -0.04f * h),
                new Vector3(0f, 0.52f * h, 0.14f * h),
                new Vector3(0f, 0.60f * h, 0.28f * h),
                new Vector3(0f, 0.72f * h, 0.35f * h)
            },
            new[]
            {
                0.085f * h, 0.200f * h, 0.255f * h, 0.235f * h, 0.190f * h,
                0.140f * h, 0.090f * h
            },
            16, 1.02f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.01f * h, -0.08f * h),
                new Vector3(0f, 0f, -0.02f * h),
                new Vector3(0f, -0.02f * h, 0.06f * h),
                new Vector3(0f, -0.05f * h, 0.13f * h)
            },
            new[] { 0.060f * h, 0.110f * h, 0.090f * h, 0.040f * h }, 8, 1f));

        // ears about a head and a half long, leaning apart
        Ear(head, h, 1f, 0.042f, 0.05f, -0.05f, 0.085f, 0.30f, -0.10f, 0.055f);
        Ear(head, h, -1f, 0.042f, 0.05f, -0.05f, 0.085f, 0.30f, -0.10f, 0.055f);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.10f * h), new Vector3(0f, -0.06f * h, 0.15f * h) },
            new[] { 0.040f * h, 0.022f * h }, 7, 1f));

        kit.Head = Wrap(head);

        kit.ForeLeg = Leg(h, 0.30f, 0.055f, 0.040f, 0.032f, 0.02f, 0.01f);
        kit.HindLeg = Leg(h, 0.42f, 0.080f, 0.050f, 0.034f, -0.14f, 0.10f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 1, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, 0.01f * h, -0.05f * h),
                new Vector3(0f, 0f, -0.10f * h)
            },
            new[] { 0.040f * h, 0.070f * h, 0.028f * h }, 7, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Fox(float h)
    {
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.77f * h, 0.45f * h),
            Shoulder = new Vector3(0.056f * h, 0.62f * h, 0.22f * h),
            Hip = new Vector3(0.060f * h, 0.62f * h, -0.26f * h),
            Rump = new Vector3(0f, 0.62f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        // low and level, and longer than it is tall, in one run to the ears
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.62f * h, -0.54f * h),
                new Vector3(0f, 0.63f * h, -0.46f * h),
                new Vector3(0f, 0.64f * h, -0.26f * h),
                new Vector3(0f, 0.63f * h, 0f),
                new Vector3(0f, 0.65f * h, 0.22f * h),
                new Vector3(0f, 0.70f * h, 0.36f * h),
                new Vector3(0f, 0.75f * h, 0.44f * h)
            },
            new[]
            {
                0.045f * h, 0.108f * h, 0.140f * h, 0.132f * h, 0.130f * h,
                0.105f * h, 0.082f * h
            },
            16, 1.06f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        // a wedge: wide at the ears, coming to a point at the nose
        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.03f * h, -0.09f * h),
                new Vector3(0f, 0.02f * h, -0.02f * h),
                new Vector3(0f, 0f, 0.07f * h),
                new Vector3(0f, -0.02f * h, 0.17f * h),
                new Vector3(0f, -0.03f * h, 0.26f * h)
            },
            new[] { 0.045f * h, 0.100f * h, 0.070f * h, 0.042f * h, 0.022f * h }, 8, 1f));

        Ear(head, h, 1f, 0.060f, 0.06f, -0.07f, 0.085f, 0.24f, -0.12f, 0.052f);
        Ear(head, h, -1f, 0.060f, 0.06f, -0.07f, 0.085f, 0.24f, -0.12f, 0.052f);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.025f * h, 0.21f * h), new Vector3(0f, -0.035f * h, 0.29f * h) },
            new[] { 0.030f * h, 0.014f * h }, 7, 1f));

        kit.Head = Wrap(head);

        kit.ForeLeg = Leg(h, 0.56f, 0.040f, 0.030f, 0.024f, 0.03f, -0.01f);
        kit.HindLeg = Leg(h, 0.56f, 0.050f, 0.034f, 0.024f, -0.09f, 0.04f);

        // The brush. Thicker in the middle than at the root, which is the whole
        // difference between a fox's tail and a length of rope.
        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, -0.02f * h, -0.14f * h),
                new Vector3(0f, -0.08f * h, -0.32f * h),
                new Vector3(0f, -0.16f * h, -0.48f * h),
                new Vector3(0f, -0.24f * h, -0.58f * h)
            },
            new[] { 0.060f * h, 0.115f * h, 0.125f * h, 0.100f * h, 0.045f * h }, 8, 1f));
        Add(tail, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.22f * h, -0.55f * h), new Vector3(0f, -0.27f * h, -0.63f * h) },
            new[] { 0.070f * h, 0.030f * h }, 7, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Goat(float h)
    {
        var kit = new Kit
        {
            Neck = new Vector3(0f, 1.04f * h, 0.40f * h),
            Shoulder = new Vector3(0.070f * h, 0.74f * h, 0.20f * h),
            Hip = new Vector3(0.074f * h, 0.74f * h, -0.24f * h),
            Rump = new Vector3(0f, 0.84f * h, -0.44f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        // heavier through the middle than the deer, and flatter over the back
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.80f * h, -0.48f * h),
                new Vector3(0f, 0.80f * h, -0.40f * h),
                new Vector3(0f, 0.81f * h, -0.22f * h),
                new Vector3(0f, 0.79f * h, 0.02f * h),
                new Vector3(0f, 0.81f * h, 0.22f * h),
                new Vector3(0f, 0.86f * h, 0.33f * h),
                new Vector3(0f, 0.94f * h, 0.39f * h),
                new Vector3(0f, 1.02f * h, 0.41f * h)
            },
            new[]
            {
                0.060f * h, 0.145f * h, 0.180f * h, 0.172f * h, 0.170f * h,
                0.140f * h, 0.108f * h, 0.086f * h
            },
            16, 1.12f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.04f * h, -0.06f * h),
                new Vector3(0f, 0.03f * h, 0f),
                new Vector3(0f, 0f, 0.10f * h),
                new Vector3(0f, -0.03f * h, 0.19f * h),
                new Vector3(0f, -0.05f * h, 0.26f * h)
            },
            new[] { 0.040f * h, 0.082f * h, 0.070f * h, 0.052f * h, 0.034f * h }, 8, 1.05f));

        // ears out and down, the way a goat carries them
        Ear(head, h, 1f, 0.066f, 0.04f, -0.01f, 0.135f, -0.07f, -0.06f, 0.042f);
        Ear(head, h, -1f, 0.066f, 0.04f, -0.01f, 0.135f, -0.07f, -0.06f, 0.042f);

        Horn(head, h, 1f);
        Horn(head, h, -1f);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.22f * h), new Vector3(0f, -0.06f * h, 0.28f * h) },
            new[] { 0.036f * h, 0.022f * h }, 7, 1f));

        // the beard
        Add(head, 1, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, -0.08f * h, 0.15f * h),
                new Vector3(0f, -0.17f * h, 0.13f * h),
                new Vector3(0f, -0.24f * h, 0.11f * h)
            },
            new[] { 0.030f * h, 0.024f * h, 0.008f * h }, 6, 1f));

        kit.Head = Wrap(head);

        kit.ForeLeg = Leg(h, 0.70f, 0.050f, 0.034f, 0.026f, 0.03f, -0.01f);
        kit.HindLeg = Leg(h, 0.70f, 0.060f, 0.038f, 0.026f, -0.08f, 0.03f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, 0.03f * h, -0.06f * h), new Vector3(0f, 0.04f * h, -0.10f * h) },
            new[] { 0.034f * h, 0.028f * h, 0.010f * h }, 6, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static void Horn(List<CreatureMesh.Piece> head, float h, float side)
    {
        var path = new[]
        {
            new Vector3(0.042f * h * side, 0.08f * h, -0.03f * h),
            new Vector3(0.062f * h * side, 0.19f * h, -0.12f * h),
            new Vector3(0.070f * h * side, 0.20f * h, -0.26f * h),
            new Vector3(0.062f * h * side, 0.13f * h, -0.35f * h),
            new Vector3(0.050f * h * side, 0.06f * h, -0.36f * h)
        };

        Add(head, 1, CreatureMesh.Tube(path,
            new[] { 0.028f * h, 0.022f * h, 0.016f * h, 0.011f * h, 0.006f * h }, 6));
    }

    // -------------------------------------------------------------------- bits

    /// <summary>
    /// A leg, hanging from the hip so a rotation swings the whole of it. The
    /// knee sits forward or back of the line between hip and foot, which is
    /// most of what separates a hind leg from a fore one.
    ///
    /// The top of the leg has to finish inside the barrel: attach it where the
    /// two surfaces meet and the top ring cuts through the flank as a wedge.
    /// </summary>
    private static Skin Leg(float h, float length, float top, float middle, float ankle,
                            float kneeZ, float footZ)
    {
        float l = length * h;

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -l * 0.34f, kneeZ * h),
                new Vector3(0f, -l * 0.68f, footZ * h * 0.5f),
                new Vector3(0f, -l * 0.94f, footZ * h)
            },
            new[] { top * h, middle * h, ankle * h, ankle * h * 0.85f }, 7, 1f));

        // a foot, so it stands on something rather than tapering into the grass
        Add(pieces, 2, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, -l * 0.94f, footZ * h),
                new Vector3(0f, -l, footZ * h + 0.012f * h)
            },
            new[] { ankle * h * 0.95f, ankle * h * 0.80f }, 7, 0.9f));

        return Wrap(pieces);
    }

    /// <summary>
    /// The muscle over a shoulder or a hip. Without it the legs appear to be
    /// pushed into the side of the barrel, which is most of what made the
    /// first attempt at these look like furniture.
    /// </summary>
    /// <summary>An ear: a blade rather than a spike, flattened across its width.</summary>
    private static void Ear(List<CreatureMesh.Piece> head, float h, float side,
                            float rootX, float rootY, float rootZ,
                            float tipX, float tipY, float tipZ, float width)
    {
        var root = new Vector3(rootX * h * side, rootY * h, rootZ * h);
        var tip = new Vector3(tipX * h * side, tipY * h, tipZ * h);
        var mid = Vector3.Lerp(root, tip, 0.45f);

        Add(head, 0, CreatureMesh.Tube(new[] { root, mid, tip },
            new[] { width * h * 0.75f, width * h, width * h * 0.18f }, 6, 0.34f));
    }

    private static void Add(List<CreatureMesh.Piece> into, int coat, Mesh mesh)
    {
        into.Add(new CreatureMesh.Piece { Mesh = mesh, At = Matrix4x4.identity, Coat = coat });
    }

    private static Transform Pivot(Transform parent, string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;

        return go.transform;
    }

    private static Skin Wrap(List<CreatureMesh.Piece> pieces)
    {
        var mesh = CreatureMesh.Combine(pieces, out var coats);

        return new Skin { Mesh = mesh, Coats = coats };
    }

    private static Transform Part(Transform parent, string name, Skin skin, Material[] palette, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;

        go.AddComponent<MeshFilter>().sharedMesh = skin.Mesh;

        // One material per submesh, in the order the submeshes were actually
        // built: a leg with no pale on it wears its coat and then its hoof.
        var worn = new Material[skin.Coats.Length];

        for (int i = 0; i < worn.Length; i++) worn[i] = palette[skin.Coats[i]];

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = worn;

        return go.transform;
    }

    private static Material Mat(Color c)
    {
        int key = c.GetHashCode();

        if (materials.TryGetValue(key, out var cached) && cached != null) return cached;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Without a shader there is no material to make, and a grey animal is
        // better than an exception part way through building one.
        if (lit == null) return null;

        var material = new Material(lit);
        material.SetColor("_BaseColor", c);
        material.color = c;
        material.SetFloat("_Smoothness", 0.10f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0f);
        material.enableInstancing = true;

        materials[key] = material;

        return material;
    }
}
