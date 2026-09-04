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
        public Transform[] Legs;         // hips, which swing the whole leg
        public Transform[] Knees;        // and the joint half way down each one
        public Transform Tail;
        public Transform[] Ears;         // which swivel, flick, and go flat when it runs
        public bool Winged;              // the fore pair are wings, not legs
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
        public Skin ForeThigh;
        public Skin ForeShin;
        public Skin HindThigh;
        public Skin HindShin;
        public Skin Tail;
        public Skin EarLeft;
        public Skin EarRight;
        public Vector3 EarRootLeft;
        public Vector3 EarRootRight;

        public Vector3 ForeKnee;         // where the joint sits below the hip
        public Vector3 HindKnee;
        public Material[] Palette;

        public Vector3 Neck;             // where the head hangs from
        public Vector3 Hip;              // hind leg attachment, mirrored across x
        public Vector3 Shoulder;
        public Vector3 Rump;             // where the tail hangs from
        public bool Winged;
    }

    private static readonly Dictionary<FaunaKind, Kit> kits = new Dictionary<FaunaKind, Kit>();

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
        body.Knees = new Transform[4];

        body.Winged = kit.Winged;

        for (int i = 0; i < 4; i++)
        {
            bool fore = i < 2;
            float side = (i % 2 == 0) ? 1f : -1f;

            Vector3 at = fore ? kit.Shoulder : kit.Hip;
            at.x *= side;

            // a bird's fore pair are wings: one piece each, hung at the shoulder
            if (kit.Winged && fore)
            {
                body.Legs[i] = Pivot(frame, "wing", at);
                Part(body.Legs[i], "feathers", kit.ForeThigh, kit.Palette, Vector3.zero);
                body.Knees[i] = null;
                continue;
            }

            // Hip carries the whole leg; the knee below it carries the lower
            // half, which is what stops a walk looking like a swinging stick.
            body.Legs[i] = Pivot(frame, fore ? "foreleg" : "hindleg", at);
            Part(body.Legs[i], "thigh", fore ? kit.ForeThigh : kit.HindThigh, kit.Palette, Vector3.zero);

            body.Knees[i] = Pivot(body.Legs[i], "knee", fore ? kit.ForeKnee : kit.HindKnee);
            Part(body.Knees[i], "shin", fore ? kit.ForeShin : kit.HindShin, kit.Palette, Vector3.zero);
        }

        body.Tail = Pivot(frame, "tail", kit.Rump);
        Part(body.Tail, "brush", kit.Tail, kit.Palette, Vector3.zero);

        // Ears on their own pivots at the root, so they can be moved: an ear
        // baked into the skull is a horn. They point up and out from the root.
        if (kit.EarLeft.Mesh != null)
        {
            body.Ears = new Transform[2];
            body.Ears[0] = Pivot(body.Head, "ear", kit.EarRootLeft);
            Part(body.Ears[0], "flap", kit.EarLeft, kit.Palette, Vector3.zero);
            body.Ears[1] = Pivot(body.Head, "ear", kit.EarRootRight);
            Part(body.Ears[1], "flap", kit.EarRight, kit.Palette, Vector3.zero);
        }

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
            case FaunaKind.Goat: kit = Goat(traits.Size); break;
            case FaunaKind.Tortoise: kit = Tortoise(traits.Size); break;
            case FaunaKind.Wolf: kit = Wolf(traits.Size); break;
            case FaunaKind.Heron: kit = Heron(traits.Size); break;
            case FaunaKind.Boar: kit = Boar(traits.Size); break;
            case FaunaKind.Raven: kit = Raven(traits.Size); break;
            case FaunaKind.Marmot: kit = Marmot(traits.Size); break;
            case FaunaKind.Crab: kit = Crab(traits.Size); break;
            case FaunaKind.Owl: kit = Owl(traits.Size); break;
            case FaunaKind.Frog: kit = Frog(traits.Size); break;
            case FaunaKind.Bat: kit = Bat(traits.Size); break;
            case FaunaKind.Hedgehog: kit = Hedgehog(traits.Size); break;
            case FaunaKind.Fish: kit = Fish(traits.Size); break;
            case FaunaKind.Eagle: kit = Eagle(traits.Size); break;
            case FaunaKind.Hare: kit = Hare(traits.Size); break;
            case FaunaKind.Scorpion: kit = Scorpion(traits.Size); break;

            default:
                // A shape has to be built, it cannot be described in the table,
                // so a new kind needs one written. Said out loud, because the
                // alternative is a creature that is quietly a goat.
                Debug.LogError("[AnimalBuilder] no shape for " + kind + ", standing in a goat.");
                kit = Goat(traits.Size);
                break;
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
                0.050f * h, 0.120f * h, 0.152f * h, 0.140f * h, 0.160f * h,
                0.148f * h, 0.118f * h, 0.092f * h, 0.068f * h
            },
            16, 1.20f));

        // The markings that say deer at a hundred paces: a pale belly, the
        // pale rump patch, a pale throat. Each is a flattened tube set just
        // proud of the coat where the colour should show.
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.68f * h, -0.30f * h), new Vector3(0f, 0.65f * h, -0.06f * h), new Vector3(0f, 0.67f * h, 0.16f * h) },
            new[] { 0.075f * h, 0.092f * h, 0.080f * h }, 10, 0.55f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.80f * h, -0.48f * h), new Vector3(0f, 0.76f * h, -0.55f * h) },
            new[] { 0.095f * h, 0.070f * h }, 10, 1.1f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.86f * h, 0.42f * h), new Vector3(0f, 0.98f * h, 0.52f * h) },
            new[] { 0.070f * h, 0.052f * h }, 8, 0.8f));

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
            new[] { 0.034f * h, 0.074f * h, 0.062f * h, 0.046f * h, 0.027f * h }, 8, 1.10f));

        Eyes(head, h, 0.058f, 0.025f, 0.075f, 0.014f);

        Ears(kit, h, 0.050f, 0.06f, -0.02f, 0.150f, 0.19f, -0.08f, 0.040f, 0);

        Antler(head, h, 1f);
        Antler(head, h, -1f);

        // a dark muzzle, which is the only two tone marking that reads at range
        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.035f * h, 0.20f * h), new Vector3(0f, -0.045f * h, 0.26f * h) },
            new[] { 0.032f * h, 0.020f * h }, 7, 1f));

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.60f, 0.054f, 0.032f, 0.021f, 0.03f, -0.02f);
        Leg(kit, false, h, 0.60f, 0.064f, 0.037f, 0.021f, -0.08f, 0.03f);

        // the tail, dark above and pale beneath, which is what it flags
        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.06f * h, -0.08f * h), new Vector3(0f, -0.13f * h, -0.13f * h) },
            new[] { 0.036f * h, 0.036f * h, 0.016f * h }, 6, 1f));
        Add(tail, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.02f * h, -0.01f * h), new Vector3(0f, -0.08f * h, -0.07f * h), new Vector3(0f, -0.14f * h, -0.12f * h) },
            new[] { 0.026f * h, 0.036f * h, 0.018f * h }, 6, 0.6f));
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
        // A rabbit sits on its haunches with its belly nearly on the ground;
        // the first one hung its body a third of its height up on four sticks.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.62f * h, 0.34f * h),
            Shoulder = new Vector3(0.062f * h, 0.30f * h, 0.16f * h),
            Hip = new Vector3(0.080f * h, 0.36f * h, -0.22f * h),
            Rump = new Vector3(0f, 0.40f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.40f * h, -0.50f * h),
                new Vector3(0f, 0.46f * h, -0.40f * h),
                new Vector3(0f, 0.50f * h, -0.26f * h),
                new Vector3(0f, 0.44f * h, -0.04f * h),
                new Vector3(0f, 0.40f * h, 0.14f * h),
                new Vector3(0f, 0.48f * h, 0.28f * h),
                new Vector3(0f, 0.60f * h, 0.35f * h)
            },
            new[]
            {
                0.085f * h, 0.200f * h, 0.255f * h, 0.235f * h, 0.190f * h,
                0.140f * h, 0.090f * h
            },
            16, 1.02f));

        // the pale belly, and a pale bib under the chin
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.22f * h, -0.30f * h), new Vector3(0f, 0.20f * h, -0.06f * h), new Vector3(0f, 0.24f * h, 0.16f * h) },
            new[] { 0.12f * h, 0.15f * h, 0.11f * h }, 10, 0.4f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.42f * h, 0.28f * h), new Vector3(0f, 0.52f * h, 0.36f * h) },
            new[] { 0.07f * h, 0.05f * h }, 8, 0.7f));

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

        Eyes(head, h, 0.085f, 0.02f, 0.02f, 0.022f);

        // ears about a head and a half long, leaning apart
        Ears(kit, h, 0.042f, 0.05f, -0.05f, 0.085f, 0.30f, -0.10f, 0.055f, 1);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.10f * h), new Vector3(0f, -0.06f * h, 0.15f * h) },
            new[] { 0.040f * h, 0.022f * h }, 7, 1f));

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.28f, 0.058f, 0.040f, 0.030f, 0.02f, 0.01f);
        Leg(kit, false, h, 0.34f, 0.100f, 0.060f, 0.036f, -0.12f, 0.06f, 0, 0.20f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 1, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, 0.02f * h, -0.05f * h),
                new Vector3(0f, 0.02f * h, -0.10f * h)
            },
            new[] { 0.040f * h, 0.075f * h, 0.030f * h }, 7, 1f));
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

        // the white bib down the chest and along the belly
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.60f * h, 0.46f * h), new Vector3(0f, 0.53f * h, 0.30f * h), new Vector3(0f, 0.51f * h, 0.06f * h), new Vector3(0f, 0.53f * h, -0.24f * h) },
            new[] { 0.055f * h, 0.085f * h, 0.085f * h, 0.070f * h }, 10, 0.5f));

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

        // a pale cheek and chin, the way the white runs up a fox's face
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.02f * h), new Vector3(0f, -0.05f * h, 0.14f * h), new Vector3(0f, -0.045f * h, 0.22f * h) },
            new[] { 0.062f * h, 0.045f * h, 0.026f * h }, 8, 0.5f));

        Eyes(head, h, 0.062f, 0.018f, 0.05f, 0.016f);

        Ears(kit, h, 0.060f, 0.06f, -0.07f, 0.085f, 0.24f, -0.12f, 0.052f, 2);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.025f * h, 0.21f * h), new Vector3(0f, -0.035f * h, 0.29f * h) },
            new[] { 0.030f * h, 0.014f * h }, 7, 1f));

        kit.Head = Wrap(head);

        // black stockings: the lower leg in the dark coat
        Leg(kit, true, h, 0.56f, 0.042f, 0.030f, 0.024f, 0.03f, -0.01f, 2);
        Leg(kit, false, h, 0.56f, 0.052f, 0.034f, 0.024f, -0.09f, 0.04f, 2);

        // The brush. Thicker in the middle than at the root, which is the whole
        // difference between a fox's tail and a length of rope.
        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, -0.01f * h, -0.14f * h),
                new Vector3(0f, -0.05f * h, -0.32f * h),
                new Vector3(0f, -0.09f * h, -0.48f * h),
                new Vector3(0f, -0.10f * h, -0.60f * h)
            },
            new[] { 0.060f * h, 0.118f * h, 0.130f * h, 0.105f * h, 0.050f * h }, 8, 1f));
        Add(tail, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.095f * h, -0.56f * h), new Vector3(0f, -0.10f * h, -0.66f * h) },
            new[] { 0.075f * h, 0.032f * h }, 7, 1f));
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
                0.060f * h, 0.150f * h, 0.192f * h, 0.186f * h, 0.180f * h,
                0.146f * h, 0.112f * h, 0.088f * h
            },
            16, 1.12f));

        // a dark stripe down the spine, and the shag under the chest
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.99f * h, -0.40f * h), new Vector3(0f, 1.00f * h, -0.10f * h), new Vector3(0f, 1.00f * h, 0.20f * h), new Vector3(0f, 1.05f * h, 0.34f * h) },
            new[] { 0.030f * h, 0.036f * h, 0.036f * h, 0.026f * h }, 7, 0.5f));
        Add(pieces, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.62f * h, 0.04f * h), new Vector3(0f, 0.60f * h, 0.20f * h), new Vector3(0f, 0.66f * h, 0.32f * h) },
            new[] { 0.10f * h, 0.13f * h, 0.09f * h }, 9, 0.7f));

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

        Eyes(head, h, 0.066f, 0.03f, 0.06f, 0.016f);

        // ears out and down, the way a goat carries them
        Ears(kit, h, 0.066f, 0.04f, -0.01f, 0.150f, -0.06f, -0.06f, 0.044f, 0);

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

        Leg(kit, true, h, 0.64f, 0.062f, 0.042f, 0.030f, 0.03f, -0.01f);
        Leg(kit, false, h, 0.64f, 0.074f, 0.046f, 0.030f, -0.08f, 0.03f);

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

    private static Kit Tortoise(float h)
    {
        // Nearly all shell: a low dome over four stubs, the head out in front
        // on a neck that can go back in.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.30f * h, 0.34f * h),
            Shoulder = new Vector3(0.17f * h, 0.22f * h, 0.20f * h),
            Hip = new Vector3(0.17f * h, 0.22f * h, -0.20f * h),
            Rump = new Vector3(0f, 0.24f * h, -0.36f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.30f * h, -0.36f * h),
                new Vector3(0f, 0.37f * h, -0.22f * h),
                new Vector3(0f, 0.41f * h, 0f),
                new Vector3(0f, 0.37f * h, 0.22f * h),
                new Vector3(0f, 0.30f * h, 0.36f * h)
            },
            new[] { 0.15f * h, 0.27f * h, 0.31f * h, 0.27f * h, 0.15f * h }, 12, 0.55f));

        // the scutes: a ridge along the top and one down each side
        Add(pieces, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.50f * h, -0.24f * h), new Vector3(0f, 0.58f * h, 0f), new Vector3(0f, 0.50f * h, 0.24f * h) },
            new[] { 0.02f * h, 0.026f * h, 0.02f * h }, 6, 0.5f));
        foreach (float side in new[] { 1f, -1f })
            Add(pieces, 2, CreatureMesh.Tube(
                new[] { new Vector3(0.20f * h * side, 0.40f * h, -0.22f * h), new Vector3(0.25f * h * side, 0.44f * h, 0f), new Vector3(0.20f * h * side, 0.40f * h, 0.22f * h) },
                new[] { 0.016f * h, 0.02f * h, 0.016f * h }, 6, 0.5f));

        // the plastron, paler, and the rim of it showing all round
        Add(pieces, 1, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.22f * h, -0.34f * h),
                new Vector3(0f, 0.22f * h, -0.18f * h),
                new Vector3(0f, 0.22f * h, 0.18f * h),
                new Vector3(0f, 0.22f * h, 0.34f * h)
            },
            new[] { 0.18f * h, 0.29f * h, 0.29f * h, 0.18f * h }, 12, 0.22f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0.03f * h, 0.10f * h),
                new Vector3(0f, 0.06f * h, 0.20f * h),
                new Vector3(0f, 0.06f * h, 0.30f * h)
            },
            new[] { 0.055f * h, 0.055f * h, 0.068f * h, 0.040f * h }, 8, 1f));

        Eyes(head, h, 0.05f, 0.09f, 0.22f, 0.013f);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.045f * h, 0.28f * h), new Vector3(0f, 0.04f * h, 0.32f * h) },
            new[] { 0.028f * h, 0.018f * h }, 6, 0.8f));

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.24f, 0.080f, 0.066f, 0.058f, 0.02f, 0f);
        Leg(kit, false, h, 0.24f, 0.085f, 0.070f, 0.060f, -0.02f, 0f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.03f * h, -0.06f * h), new Vector3(0f, -0.06f * h, -0.10f * h) },
            new[] { 0.03f * h, 0.022f * h, 0.008f * h }, 6, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Wolf(float h)
    {
        // Built like the fox and half again as big: deeper in the chest,
        // longer in the leg, the brush carried low and straight.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.86f * h, 0.44f * h),
            Shoulder = new Vector3(0.066f * h, 0.66f * h, 0.22f * h),
            Hip = new Vector3(0.070f * h, 0.66f * h, -0.26f * h),
            Rump = new Vector3(0f, 0.68f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.66f * h, -0.54f * h),
                new Vector3(0f, 0.68f * h, -0.46f * h),
                new Vector3(0f, 0.70f * h, -0.26f * h),
                new Vector3(0f, 0.69f * h, 0f),
                new Vector3(0f, 0.72f * h, 0.22f * h),
                new Vector3(0f, 0.78f * h, 0.36f * h),
                new Vector3(0f, 0.84f * h, 0.44f * h)
            },
            new[]
            {
                0.050f * h, 0.118f * h, 0.150f * h, 0.142f * h, 0.152f * h,
                0.124f * h, 0.096f * h
            },
            16, 1.10f));

        // pale beneath, and the dark saddle along the back
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.62f * h, 0.40f * h), new Vector3(0f, 0.55f * h, 0.20f * h), new Vector3(0f, 0.54f * h, -0.06f * h), new Vector3(0f, 0.56f * h, -0.30f * h) },
            new[] { 0.06f * h, 0.09f * h, 0.09f * h, 0.07f * h }, 10, 0.5f));
        Add(pieces, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.84f * h, -0.40f * h), new Vector3(0f, 0.86f * h, -0.14f * h), new Vector3(0f, 0.88f * h, 0.14f * h) },
            new[] { 0.05f * h, 0.075f * h, 0.06f * h }, 8, 0.32f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.03f * h, -0.10f * h),
                new Vector3(0f, 0.02f * h, -0.02f * h),
                new Vector3(0f, 0f, 0.08f * h),
                new Vector3(0f, -0.02f * h, 0.19f * h),
                new Vector3(0f, -0.03f * h, 0.28f * h)
            },
            new[] { 0.05f * h, 0.105f * h, 0.080f * h, 0.052f * h, 0.028f * h }, 8, 1f));

        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.02f * h), new Vector3(0f, -0.05f * h, 0.14f * h), new Vector3(0f, -0.045f * h, 0.23f * h) },
            new[] { 0.066f * h, 0.05f * h, 0.03f * h }, 8, 0.5f));

        Eyes(head, h, 0.064f, 0.02f, 0.06f, 0.016f);
        Ears(kit, h, 0.062f, 0.07f, -0.08f, 0.105f, 0.23f, -0.13f, 0.050f, 0);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.025f * h, 0.23f * h), new Vector3(0f, -0.035f * h, 0.31f * h) },
            new[] { 0.032f * h, 0.016f * h }, 7, 1f));

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.62f, 0.050f, 0.036f, 0.028f, 0.03f, -0.01f);
        Leg(kit, false, h, 0.62f, 0.062f, 0.040f, 0.028f, -0.09f, 0.04f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, -0.10f * h, -0.14f * h),
                new Vector3(0f, -0.22f * h, -0.30f * h),
                new Vector3(0f, -0.32f * h, -0.44f * h),
                new Vector3(0f, -0.37f * h, -0.54f * h)
            },
            new[] { 0.060f * h, 0.100f * h, 0.110f * h, 0.090f * h, 0.045f * h }, 8, 1f));
        Add(tail, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.35f * h, -0.51f * h), new Vector3(0f, -0.39f * h, -0.59f * h) },
            new[] { 0.06f * h, 0.025f * h }, 7, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Heron(float h)
    {
        // A bird: an egg of a body on two long legs, a neck that rises in an
        // S to a spear of a beak, and wings folded along the flanks that
        // open when it goes.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.74f * h, 0.20f * h),
            Shoulder = new Vector3(0.11f * h, 0.72f * h, 0.08f * h),
            Hip = new Vector3(0.045f * h, 0.58f * h, -0.04f * h),
            Rump = new Vector3(0f, 0.66f * h, -0.30f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.64f * h, -0.30f * h),
                new Vector3(0f, 0.66f * h, -0.18f * h),
                new Vector3(0f, 0.68f * h, 0f),
                new Vector3(0f, 0.66f * h, 0.16f * h),
                new Vector3(0f, 0.62f * h, 0.26f * h)
            },
            new[] { 0.05f * h, 0.12f * h, 0.15f * h, 0.13f * h, 0.07f * h }, 14, 1.15f));

        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.56f * h, 0.22f * h), new Vector3(0f, 0.52f * h, 0.02f * h), new Vector3(0f, 0.55f * h, -0.16f * h) },
            new[] { 0.07f * h, 0.095f * h, 0.07f * h }, 10, 0.5f));

        kit.Trunk = Wrap(pieces);

        // neck and head together, so the whole neck moves when the head does
        var head = new List<CreatureMesh.Piece>();

        Add(head, 1, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0.12f * h, 0.09f * h),
                new Vector3(0f, 0.26f * h, 0.10f * h),
                new Vector3(0f, 0.36f * h, 0.04f * h),
                new Vector3(0f, 0.44f * h, 0.03f * h)
            },
            new[] { 0.048f * h, 0.052f * h, 0.046f * h, 0.042f * h, 0.046f * h }, 8, 1f));

        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.44f * h, 0f), new Vector3(0f, 0.47f * h, 0.06f * h), new Vector3(0f, 0.465f * h, 0.13f * h) },
            new[] { 0.046f * h, 0.052f * h, 0.036f * h }, 8, 1.05f));

        // the black cap and the plume off the back of it
        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.50f * h, 0.08f * h), new Vector3(0f, 0.51f * h, -0.02f * h), new Vector3(0f, 0.49f * h, -0.12f * h), new Vector3(0f, 0.45f * h, -0.20f * h) },
            new[] { 0.03f * h, 0.03f * h, 0.014f * h, 0.006f * h }, 6, 0.6f));

        Add(head, 2, CreatureMesh.Taper(new Vector3(0f, 0.465f * h, 0.12f * h), new Vector3(0f, 0.45f * h, 0.38f * h), 0.02f * h, 0.004f * h, 6));

        Eyes(head, h, 0.034f, 0.475f, 0.08f, 0.011f);

        kit.Head = Wrap(head);

        // a wing, one piece, lying back along the flank
        var wing = new List<CreatureMesh.Piece>();
        Add(wing, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.02f * h, 0.02f * h, -0.16f * h),
                new Vector3(0.01f * h, 0f, -0.34f * h),
                new Vector3(0f, -0.03f * h, -0.48f * h)
            },
            new[] { 0.045f * h, 0.080f * h, 0.065f * h, 0.022f * h }, 8, 0.22f));
        Add(wing, 2, CreatureMesh.Tube(
            new[] { new Vector3(0.005f * h, -0.01f * h, -0.40f * h), new Vector3(0f, -0.035f * h, -0.50f * h) },
            new[] { 0.05f * h, 0.018f * h }, 6, 0.22f));
        kit.ForeThigh = Wrap(wing);
        kit.ForeShin = default;

        // the legs: long, thin, dark, bending backward at the joint
        Leg(kit, false, h, 0.60f, 0.024f, 0.017f, 0.014f, 0.05f, -0.04f, 2, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.02f * h, -0.08f * h), new Vector3(0f, -0.05f * h, -0.14f * h) },
            new[] { 0.05f * h, 0.045f * h, 0.02f * h }, 6, 0.5f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Boar(float h)
    {
        // High at the shoulder and low at the rump, a bristle ridge down the
        // back, a wedge of a head carried low with the snout on the end of it
        // and the tusks either side.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.82f * h, 0.42f * h),
            Shoulder = new Vector3(0.080f * h, 0.62f * h, 0.20f * h),
            Hip = new Vector3(0.080f * h, 0.58f * h, -0.24f * h),
            Rump = new Vector3(0f, 0.72f * h, -0.46f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.70f * h, -0.48f * h),
                new Vector3(0f, 0.72f * h, -0.40f * h),
                new Vector3(0f, 0.74f * h, -0.20f * h),
                new Vector3(0f, 0.76f * h, 0.04f * h),
                new Vector3(0f, 0.80f * h, 0.24f * h),
                new Vector3(0f, 0.82f * h, 0.36f * h),
                new Vector3(0f, 0.80f * h, 0.44f * h)
            },
            new[]
            {
                0.070f * h, 0.170f * h, 0.205f * h, 0.215f * h, 0.225f * h,
                0.190f * h, 0.130f * h
            },
            16, 1.05f));

        // the bristles along the spine, and a paler belly
        Add(pieces, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.90f * h, -0.36f * h), new Vector3(0f, 0.97f * h, -0.10f * h), new Vector3(0f, 1.02f * h, 0.18f * h), new Vector3(0f, 0.98f * h, 0.36f * h) },
            new[] { 0.03f * h, 0.05f * h, 0.055f * h, 0.03f * h }, 6, 0.9f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.56f * h, -0.24f * h), new Vector3(0f, 0.55f * h, 0.02f * h), new Vector3(0f, 0.58f * h, 0.22f * h) },
            new[] { 0.09f * h, 0.11f * h, 0.09f * h }, 10, 0.4f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.02f * h, -0.08f * h),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -0.06f * h, 0.12f * h),
                new Vector3(0f, -0.12f * h, 0.24f * h),
                new Vector3(0f, -0.15f * h, 0.32f * h)
            },
            new[] { 0.09f * h, 0.115f * h, 0.09f * h, 0.06f * h, 0.045f * h }, 8, 1.05f));

        // the snout, a disc; the tusks, curving up and out
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.15f * h, 0.31f * h), new Vector3(0f, -0.15f * h, 0.35f * h) },
            new[] { 0.048f * h, 0.045f * h }, 8, 0.9f));
        foreach (float side in new[] { 1f, -1f })
            Add(head, 1, CreatureMesh.Tube(
                new[] { new Vector3(0.05f * h * side, -0.15f * h, 0.24f * h), new Vector3(0.075f * h * side, -0.11f * h, 0.28f * h), new Vector3(0.09f * h * side, -0.05f * h, 0.29f * h) },
                new[] { 0.016f * h, 0.013f * h, 0.006f * h }, 5, 1f));

        Eyes(head, h, 0.07f, 0.01f, 0.09f, 0.014f);
        Ears(kit, h, 0.06f, 0.06f, -0.04f, 0.11f, 0.15f, -0.08f, 0.036f, 0);

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.50f, 0.070f, 0.044f, 0.030f, 0.03f, -0.01f);
        Leg(kit, false, h, 0.46f, 0.080f, 0.048f, 0.030f, -0.07f, 0.03f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.10f * h, -0.06f * h), new Vector3(0f, -0.22f * h, -0.08f * h) },
            new[] { 0.02f * h, 0.016f * h, 0.012f * h }, 5, 1f));
        Add(tail, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.21f * h, -0.08f * h), new Vector3(0f, -0.28f * h, -0.09f * h) },
            new[] { 0.03f * h, 0.012f * h }, 5, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Raven(float h)
    {
        // A bird again, made compact: a heavy beak, a short neck, wings that
        // reach the tail. Built on the heron's plan.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.60f * h, 0.26f * h),
            Shoulder = new Vector3(0.14f * h, 0.62f * h, 0.14f * h),
            Hip = new Vector3(0.07f * h, 0.42f * h, -0.02f * h),
            Rump = new Vector3(0f, 0.56f * h, -0.36f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.54f * h, -0.36f * h),
                new Vector3(0f, 0.56f * h, -0.20f * h),
                new Vector3(0f, 0.58f * h, 0f),
                new Vector3(0f, 0.58f * h, 0.18f * h),
                new Vector3(0f, 0.56f * h, 0.30f * h)
            },
            new[] { 0.07f * h, 0.17f * h, 0.20f * h, 0.18f * h, 0.11f * h }, 12, 1.1f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0.08f * h, 0.06f * h),
                new Vector3(0f, 0.16f * h, 0.10f * h),
                new Vector3(0f, 0.18f * h, 0.20f * h)
            },
            new[] { 0.09f * h, 0.10f * h, 0.11f * h, 0.07f * h }, 8, 1f));

        Add(head, 2, CreatureMesh.Taper(new Vector3(0f, 0.16f * h, 0.18f * h), new Vector3(0f, 0.13f * h, 0.42f * h), 0.05f * h, 0.008f * h, 6));
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.12f * h, 0.14f * h), new Vector3(0f, 0.10f * h, 0.26f * h) },
            new[] { 0.05f * h, 0.03f * h }, 6, 0.7f));

        Eyes(head, h, 0.075f, 0.18f, 0.12f, 0.02f);

        kit.Head = Wrap(head);

        var wing = new List<CreatureMesh.Piece>();
        Add(wing, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.02f * h, 0.02f * h, -0.20f * h),
                new Vector3(0.01f * h, -0.02f * h, -0.42f * h),
                new Vector3(0f, -0.06f * h, -0.56f * h)
            },
            new[] { 0.06f * h, 0.10f * h, 0.08f * h, 0.025f * h }, 8, 0.22f));
        kit.ForeThigh = Wrap(wing);
        kit.ForeShin = default;

        Leg(kit, false, h, 0.34f, 0.030f, 0.022f, 0.018f, 0.04f, -0.03f, 2, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.03f * h, -0.14f * h), new Vector3(0f, -0.07f * h, -0.30f * h) },
            new[] { 0.07f * h, 0.075f * h, 0.045f * h }, 6, 0.3f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Marmot(float h)
    {
        // A loaf of an animal: heavy through the middle, small in the head,
        // short in the leg, a tuft of a tail.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.60f * h, 0.36f * h),
            Shoulder = new Vector3(0.10f * h, 0.34f * h, 0.20f * h),
            Hip = new Vector3(0.11f * h, 0.36f * h, -0.22f * h),
            Rump = new Vector3(0f, 0.44f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.44f * h, -0.50f * h),
                new Vector3(0f, 0.50f * h, -0.36f * h),
                new Vector3(0f, 0.54f * h, -0.14f * h),
                new Vector3(0f, 0.52f * h, 0.10f * h),
                new Vector3(0f, 0.52f * h, 0.28f * h),
                new Vector3(0f, 0.58f * h, 0.38f * h)
            },
            new[] { 0.09f * h, 0.22f * h, 0.26f * h, 0.25f * h, 0.20f * h, 0.12f * h }, 14, 0.95f));

        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.30f * h, -0.30f * h), new Vector3(0f, 0.27f * h, -0.02f * h), new Vector3(0f, 0.32f * h, 0.24f * h) },
            new[] { 0.13f * h, 0.16f * h, 0.12f * h }, 10, 0.4f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.02f * h, -0.06f * h),
                new Vector3(0f, 0.02f * h, 0.02f * h),
                new Vector3(0f, 0f, 0.10f * h),
                new Vector3(0f, -0.03f * h, 0.16f * h)
            },
            new[] { 0.08f * h, 0.11f * h, 0.09f * h, 0.05f * h }, 8, 1f));

        Eyes(head, h, 0.085f, 0.03f, 0.05f, 0.02f);
        Ears(kit, h, 0.07f, 0.07f, -0.02f, 0.10f, 0.13f, -0.05f, 0.04f, 0);

        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.03f * h, 0.14f * h), new Vector3(0f, -0.035f * h, 0.18f * h) },
            new[] { 0.035f * h, 0.02f * h }, 6, 1f));

        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.32f, 0.070f, 0.050f, 0.036f, 0.02f, 0.01f);
        Leg(kit, false, h, 0.34f, 0.090f, 0.060f, 0.040f, -0.08f, 0.05f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.02f * h, -0.10f * h), new Vector3(0f, -0.06f * h, -0.20f * h) },
            new[] { 0.05f * h, 0.06f * h, 0.03f * h }, 7, 1f));
        Add(tail, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.055f * h, -0.19f * h), new Vector3(0f, -0.07f * h, -0.24f * h) },
            new[] { 0.035f * h, 0.015f * h }, 6, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Crab(float h)
    {
        // A carapace, wider than it is long, on legs set well out to the
        // sides; two claws in front on the head pivot, so they can come up;
        // eyes on stalks.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.30f * h, 0.34f * h),
            Shoulder = new Vector3(0.44f * h, 0.28f * h, 0.16f * h),
            Hip = new Vector3(0.44f * h, 0.28f * h, -0.16f * h),
            Rump = new Vector3(0f, 0.26f * h, -0.36f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        // the shell, swept across so it is wide, and a pale underside
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(-0.50f * h, 0.32f * h, 0f),
                new Vector3(-0.30f * h, 0.38f * h, 0f),
                new Vector3(0f, 0.42f * h, 0f),
                new Vector3(0.30f * h, 0.38f * h, 0f),
                new Vector3(0.50f * h, 0.32f * h, 0f)
            },
            new[] { 0.14f * h, 0.26f * h, 0.30f * h, 0.26f * h, 0.14f * h }, 10, 0.5f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(-0.44f * h, 0.24f * h, 0f), new Vector3(0f, 0.24f * h, 0f), new Vector3(0.44f * h, 0.24f * h, 0f) },
            new[] { 0.16f * h, 0.28f * h, 0.16f * h }, 10, 0.22f));

        // the eyes, up on stalks at the front
        foreach (float side in new[] { 1f, -1f })
        {
            Add(pieces, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.12f * h * side, 0.40f * h, 0.26f * h), new Vector3(0.13f * h * side, 0.56f * h, 0.30f * h) },
                new[] { 0.03f * h, 0.025f * h }, 5, 1f));
            Add(pieces, 2, CreatureMesh.Tube(
                new[] { new Vector3(0.13f * h * side, 0.56f * h, 0.30f * h), new Vector3(0.13f * h * side, 0.62f * h, 0.31f * h) },
                new[] { 0.04f * h, 0.03f * h }, 6, 1f));
        }

        kit.Trunk = Wrap(pieces);

        // the claws, on the head pivot: an arm out and forward, a pincer on it
        var head = new List<CreatureMesh.Piece>();
        foreach (float side in new[] { 1f, -1f })
        {
            Add(head, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.10f * h * side, 0f, 0f), new Vector3(0.26f * h * side, 0.02f * h, 0.14f * h), new Vector3(0.30f * h * side, 0.04f * h, 0.30f * h) },
                new[] { 0.05f * h, 0.055f * h, 0.045f * h }, 7, 1f));
            Add(head, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.30f * h * side, 0.04f * h, 0.30f * h), new Vector3(0.34f * h * side, 0.06f * h, 0.42f * h), new Vector3(0.30f * h * side, 0.04f * h, 0.56f * h) },
                new[] { 0.06f * h, 0.09f * h, 0.03f * h }, 7, 0.7f));
            Add(head, 1, CreatureMesh.Tube(
                new[] { new Vector3(0.24f * h * side, 0.02f * h, 0.42f * h), new Vector3(0.20f * h * side, 0.02f * h, 0.56f * h) },
                new[] { 0.035f * h, 0.012f * h }, 5, 0.7f));
        }
        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.30f, 0.045f, 0.035f, 0.025f, 0.02f, 0f, 2);
        Leg(kit, false, h, 0.30f, 0.045f, 0.035f, 0.025f, -0.02f, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(new[] { Vector3.zero, new Vector3(0f, -0.01f * h, -0.04f * h) }, new[] { 0.03f * h, 0.015f * h }, 5, 0.5f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Owl(float h)
    {
        // A bird that is mostly head: a round body, a big flat face with the
        // eyes on the front of it, ear tufts, a hooked beak; short legs.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.74f * h, 0.14f * h),
            Shoulder = new Vector3(0.16f * h, 0.62f * h, 0.06f * h),
            Hip = new Vector3(0.07f * h, 0.36f * h, 0f),
            Rump = new Vector3(0f, 0.46f * h, -0.30f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.44f * h, -0.30f * h),
                new Vector3(0f, 0.50f * h, -0.16f * h),
                new Vector3(0f, 0.56f * h, 0f),
                new Vector3(0f, 0.60f * h, 0.12f * h),
                new Vector3(0f, 0.68f * h, 0.18f * h)
            },
            new[] { 0.08f * h, 0.20f * h, 0.24f * h, 0.22f * h, 0.14f * h }, 12, 1.1f));

        // the pale front, barred
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.40f * h, 0.10f * h), new Vector3(0f, 0.52f * h, 0.20f * h), new Vector3(0f, 0.64f * h, 0.22f * h) },
            new[] { 0.10f * h, 0.14f * h, 0.10f * h }, 10, 0.55f));
        for (int i = 0; i < 3; i++)
            Add(pieces, 2, CreatureMesh.Tube(
                new[] { new Vector3(-0.08f * h, (0.44f + i * 0.08f) * h, 0.26f * h), new Vector3(0.08f * h, (0.44f + i * 0.08f) * h, 0.26f * h) },
                new[] { 0.012f * h, 0.012f * h }, 4, 0.5f));

        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();

        Add(head, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.12f * h), new Vector3(0f, 0.02f * h, 0f), new Vector3(0f, 0f, 0.10f * h) },
            new[] { 0.14f * h, 0.20f * h, 0.16f * h }, 10, 1f));

        // the face: a pale disc, the eyes set into it, the beak between
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, 0.06f * h), new Vector3(0f, 0f, 0.16f * h) },
            new[] { 0.19f * h, 0.15f * h }, 12, 1.05f));
        foreach (float side in new[] { 1f, -1f })
            Add(head, 2, CreatureMesh.Tube(
                new[] { new Vector3(0.08f * h * side, 0.03f * h, 0.14f * h), new Vector3(0.08f * h * side, 0.03f * h, 0.19f * h) },
                new[] { 0.055f * h, 0.045f * h }, 8, 1f));
        Add(head, 2, CreatureMesh.Taper(new Vector3(0f, -0.02f * h, 0.15f * h), new Vector3(0f, -0.09f * h, 0.20f * h), 0.03f * h, 0.006f * h, 5));

        // the tufts
        Ears(kit, h, 0.11f, 0.14f, -0.02f, 0.17f, 0.30f, -0.06f, 0.05f, 0);

        kit.Head = Wrap(head);

        var wing = new List<CreatureMesh.Piece>();
        Add(wing, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.02f * h, 0f, -0.18f * h),
                new Vector3(0.01f * h, -0.04f * h, -0.36f * h),
                new Vector3(0f, -0.08f * h, -0.46f * h)
            },
            new[] { 0.07f * h, 0.11f * h, 0.08f * h, 0.03f * h }, 8, 0.22f));
        kit.ForeThigh = Wrap(wing);
        kit.ForeShin = default;

        Leg(kit, false, h, 0.30f, 0.040f, 0.028f, 0.022f, 0.02f, -0.01f, 2, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.04f * h, -0.10f * h), new Vector3(0f, -0.10f * h, -0.20f * h) },
            new[] { 0.08f * h, 0.08f * h, 0.05f * h }, 6, 0.3f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Frog(float h)
    {
        // Squat, all head and haunch: a wide flat body, the eyes up on top,
        // long hind legs folded under it, no tail to speak of.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.50f * h, 0.34f * h),
            Shoulder = new Vector3(0.22f * h, 0.30f * h, 0.22f * h),
            Hip = new Vector3(0.28f * h, 0.36f * h, -0.20f * h),
            Rump = new Vector3(0f, 0.36f * h, -0.48f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();

        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.36f * h, -0.48f * h),
                new Vector3(0f, 0.48f * h, -0.30f * h),
                new Vector3(0f, 0.54f * h, -0.06f * h),
                new Vector3(0f, 0.52f * h, 0.20f * h),
                new Vector3(0f, 0.50f * h, 0.36f * h)
            },
            new[] { 0.10f * h, 0.30f * h, 0.36f * h, 0.34f * h, 0.24f * h }, 12, 0.7f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.26f * h, -0.30f * h), new Vector3(0f, 0.24f * h, 0f), new Vector3(0f, 0.26f * h, 0.30f * h) },
            new[] { 0.24f * h, 0.30f * h, 0.20f * h }, 10, 0.3f));

        // the eyes, up and out on top
        foreach (float side in new[] { 1f, -1f })
        {
            Add(pieces, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.20f * h * side, 0.60f * h, 0.30f * h), new Vector3(0.22f * h * side, 0.72f * h, 0.30f * h) },
                new[] { 0.09f * h, 0.07f * h }, 8, 1f));
            Add(pieces, 2, CreatureMesh.Tube(
                new[] { new Vector3(0.22f * h * side, 0.74f * h, 0.30f * h), new Vector3(0.23f * h * side, 0.80f * h, 0.32f * h) },
                new[] { 0.055f * h, 0.03f * h }, 6, 1f));
        }

        kit.Trunk = Wrap(pieces);

        // the mouth end, on the head pivot, so a croak can lift it
        var head = new List<CreatureMesh.Piece>();
        Add(head, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.06f * h), new Vector3(0f, -0.02f * h, 0.10f * h), new Vector3(0f, -0.05f * h, 0.22f * h) },
            new[] { 0.22f * h, 0.20f * h, 0.10f * h }, 10, 0.6f));
        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.30f, 0.045f, 0.032f, 0.026f, 0.04f, 0.02f);
        Leg(kit, false, h, 0.36f, 0.11f, 0.06f, 0.03f, -0.22f, 0.26f, 0, 0.26f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(new[] { Vector3.zero, new Vector3(0f, -0.01f * h, -0.03f * h) }, new[] { 0.03f * h, 0.015f * h }, 5, 0.5f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Bat(float h)
    {
        // A scrap of a body between two wide wings; big ears, a pug of a
        // face, feet you would not notice.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.62f * h, 0.20f * h),
            Shoulder = new Vector3(0.10f * h, 0.62f * h, 0.08f * h),
            Hip = new Vector3(0.05f * h, 0.50f * h, -0.10f * h),
            Rump = new Vector3(0f, 0.56f * h, -0.24f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.56f * h, -0.24f * h), new Vector3(0f, 0.60f * h, -0.06f * h), new Vector3(0f, 0.62f * h, 0.14f * h) },
            new[] { 0.06f * h, 0.13f * h, 0.10f * h }, 10, 1.05f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.52f * h, -0.14f * h), new Vector3(0f, 0.52f * h, 0.10f * h) },
            new[] { 0.07f * h, 0.06f * h }, 8, 0.5f));
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        Add(head, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.06f * h), new Vector3(0f, 0.01f * h, 0.04f * h), new Vector3(0f, -0.01f * h, 0.12f * h) },
            new[] { 0.08f * h, 0.10f * h, 0.06f * h }, 8, 1f));
        Eyes(head, h, 0.05f, 0.02f, 0.09f, 0.016f);
        Add(head, 2, CreatureMesh.Tube(new[] { new Vector3(0f, -0.01f * h, 0.11f * h), new Vector3(0f, -0.015f * h, 0.15f * h) }, new[] { 0.03f * h, 0.02f * h }, 6, 1f));
        Ears(kit, h, 0.06f, 0.06f, -0.02f, 0.10f, 0.26f, -0.06f, 0.07f, 0);
        kit.Head = Wrap(head);

        // the wings, wide and thin
        var wing = new List<CreatureMesh.Piece>();
        Add(wing, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.02f * h, 0f, -0.22f * h),
                new Vector3(0.02f * h, -0.02f * h, -0.50f * h),
                new Vector3(0f, -0.06f * h, -0.72f * h)
            },
            new[] { 0.06f * h, 0.16f * h, 0.15f * h, 0.03f * h }, 8, 0.12f));
        Add(wing, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, 0f), new Vector3(0.01f * h, 0.03f * h, -0.36f * h), new Vector3(0f, -0.02f * h, -0.72f * h) },
            new[] { 0.02f * h, 0.02f * h, 0.008f * h }, 5, 1f));
        kit.ForeThigh = Wrap(wing);
        kit.ForeShin = default;

        Leg(kit, false, h, 0.12f, 0.02f, 0.016f, 0.012f, 0.01f, 0f, 2, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(new[] { Vector3.zero, new Vector3(0f, -0.02f * h, -0.10f * h) }, new[] { 0.04f * h, 0.01f * h }, 5, 0.3f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Hedgehog(float h)
    {
        // A dome of spines over a pale pointed face and four short legs.
        // The spines are spines: three dozen short tapers set into the back.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.36f * h, 0.36f * h),
            Shoulder = new Vector3(0.14f * h, 0.24f * h, 0.20f * h),
            Hip = new Vector3(0.16f * h, 0.24f * h, -0.20f * h),
            Rump = new Vector3(0f, 0.30f * h, -0.44f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 2, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.30f * h, -0.44f * h),
                new Vector3(0f, 0.42f * h, -0.28f * h),
                new Vector3(0f, 0.48f * h, 0f),
                new Vector3(0f, 0.44f * h, 0.24f * h),
                new Vector3(0f, 0.36f * h, 0.38f * h)
            },
            new[] { 0.10f * h, 0.28f * h, 0.32f * h, 0.28f * h, 0.14f * h }, 12, 0.85f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.20f * h, -0.26f * h), new Vector3(0f, 0.18f * h, 0f), new Vector3(0f, 0.20f * h, 0.26f * h) },
            new[] { 0.18f * h, 0.24f * h, 0.16f * h }, 10, 0.35f));

        var spines = new System.Random(7);
        for (int i = 0; i < 40; i++)
        {
            float a = (float)spines.NextDouble() * Mathf.PI * 2f;
            float along = (float)spines.NextDouble() * 0.6f - 0.3f;
            float outR = 0.28f * Mathf.Cos(along * 2.6f);
            var foot = new Vector3(Mathf.Cos(a) * outR * h, (0.46f + Mathf.Sin(a) * outR * 0.85f) * h, along * h);
            var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.85f + 0.3f, along * 0.6f).normalized;
            if (foot.y < 0.30f * h) continue;
            Add(pieces, i % 3 == 0 ? 2 : 1, CreatureMesh.Taper(foot, foot + outward * 0.14f * h, 0.018f * h, 0.004f * h, 4, 3));
        }
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.06f * h), new Vector3(0f, -0.02f * h, 0.06f * h), new Vector3(0f, -0.06f * h, 0.18f * h), new Vector3(0f, -0.08f * h, 0.24f * h) },
            new[] { 0.11f * h, 0.10f * h, 0.05f * h, 0.03f * h }, 8, 1f));
        Eyes(head, h, 0.055f, 0.0f, 0.08f, 0.018f);
        Add(head, 2, CreatureMesh.Tube(new[] { new Vector3(0f, -0.08f * h, 0.23f * h), new Vector3(0f, -0.085f * h, 0.27f * h) }, new[] { 0.025f * h, 0.016f * h }, 6, 1f));
        Ears(kit, h, 0.07f, 0.04f, -0.02f, 0.10f, 0.10f, -0.04f, 0.035f, 0);
        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.22f, 0.04f, 0.03f, 0.024f, 0.02f, 0f, 2);
        Leg(kit, false, h, 0.22f, 0.045f, 0.032f, 0.024f, -0.02f, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(new[] { Vector3.zero, new Vector3(0f, -0.02f * h, -0.05f * h) }, new[] { 0.02f * h, 0.008f * h }, 5, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Fish(float h)
    {
        // A spindle with fins: the pectorals hang from the wing pivots, the
        // tail is a fin on end, the dorsal a ridge along the back.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.50f * h, 0.34f * h),
            Shoulder = new Vector3(0.09f * h, 0.44f * h, 0.16f * h),
            Hip = new Vector3(0.05f * h, 0.40f * h, -0.12f * h),
            Rump = new Vector3(0f, 0.50f * h, -0.44f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.50f * h, -0.44f * h),
                new Vector3(0f, 0.50f * h, -0.24f * h),
                new Vector3(0f, 0.50f * h, 0f),
                new Vector3(0f, 0.50f * h, 0.22f * h),
                new Vector3(0f, 0.50f * h, 0.36f * h)
            },
            new[] { 0.04f * h, 0.10f * h, 0.13f * h, 0.11f * h, 0.05f * h }, 12, 1.4f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.40f * h, -0.20f * h), new Vector3(0f, 0.38f * h, 0f), new Vector3(0f, 0.40f * h, 0.20f * h) },
            new[] { 0.07f * h, 0.09f * h, 0.07f * h }, 8, 0.6f));
        Add(pieces, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.64f * h, -0.18f * h), new Vector3(0f, 0.74f * h, -0.06f * h), new Vector3(0f, 0.66f * h, 0.08f * h) },
            new[] { 0.02f * h, 0.05f * h, 0.02f * h }, 5, 0.25f));
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        Add(head, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.04f * h), new Vector3(0f, 0f, 0.06f * h), new Vector3(0f, -0.01f * h, 0.14f * h) },
            new[] { 0.06f * h, 0.055f * h, 0.03f * h }, 8, 1.3f));
        Eyes(head, h, 0.05f, 0.01f, 0.05f, 0.016f);
        kit.Head = Wrap(head);

        var fin = new List<CreatureMesh.Piece>();
        Add(fin, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, 0f), new Vector3(0.10f * h, -0.04f * h, -0.06f * h), new Vector3(0.18f * h, -0.08f * h, -0.14f * h) },
            new[] { 0.03f * h, 0.045f * h, 0.015f * h }, 5, 0.25f));
        kit.ForeThigh = Wrap(fin);
        kit.ForeShin = default;

        Leg(kit, false, h, 0.06f, 0.03f, 0.02f, 0.012f, 0f, -0.02f, 2, 0f, 2);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 2, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, 0f, -0.08f * h), new Vector3(0f, 0f, -0.18f * h) },
            new[] { 0.04f * h, 0.08f * h, 0.14f * h }, 6, 0.2f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Eagle(float h)
    {
        // Wings first: long, broad, fingered at the tips. A pale head and
        // neck on a dark body, a hooked beak, a fan of a tail.
        var kit = new Kit
        {
            Winged = true,
            Neck = new Vector3(0f, 0.56f * h, 0.24f * h),
            Shoulder = new Vector3(0.10f * h, 0.56f * h, 0.10f * h),
            Hip = new Vector3(0.05f * h, 0.42f * h, -0.06f * h),
            Rump = new Vector3(0f, 0.50f * h, -0.30f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.48f * h, -0.30f * h),
                new Vector3(0f, 0.50f * h, -0.14f * h),
                new Vector3(0f, 0.52f * h, 0.04f * h),
                new Vector3(0f, 0.52f * h, 0.18f * h),
                new Vector3(0f, 0.52f * h, 0.28f * h)
            },
            new[] { 0.06f * h, 0.12f * h, 0.14f * h, 0.12f * h, 0.08f * h }, 12, 1.05f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.42f * h, -0.10f * h), new Vector3(0f, 0.40f * h, 0.10f * h), new Vector3(0f, 0.44f * h, 0.24f * h) },
            new[] { 0.08f * h, 0.09f * h, 0.06f * h }, 8, 0.5f));
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        Add(head, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0f, -0.04f * h), new Vector3(0f, 0.03f * h, 0.06f * h), new Vector3(0f, 0.04f * h, 0.14f * h), new Vector3(0f, 0.03f * h, 0.20f * h) },
            new[] { 0.07f * h, 0.075f * h, 0.07f * h, 0.05f * h }, 8, 1f));
        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.03f * h, 0.19f * h), new Vector3(0f, 0.02f * h, 0.27f * h), new Vector3(0f, -0.03f * h, 0.30f * h) },
            new[] { 0.035f * h, 0.022f * h, 0.006f * h }, 6, 1f));
        Eyes(head, h, 0.05f, 0.05f, 0.14f, 0.014f);
        kit.Head = Wrap(head);

        var wing = new List<CreatureMesh.Piece>();
        Add(wing, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.02f * h, 0.01f * h, -0.30f * h),
                new Vector3(0.02f * h, 0f, -0.66f * h),
                new Vector3(0f, -0.02f * h, -0.92f * h)
            },
            new[] { 0.08f * h, 0.15f * h, 0.14f * h, 0.06f * h }, 8, 0.14f));
        // the fingers at the tip
        for (int i = 0; i < 4; i++)
            Add(wing, 2, CreatureMesh.Taper(new Vector3(0f, 0f, (-0.86f + i * 0.02f) * h), new Vector3((0.04f + i * 0.06f) * h, -0.01f * h, (-1.02f + i * 0.04f) * h), 0.018f * h, 0.004f * h, 4, 3));
        kit.ForeThigh = Wrap(wing);
        kit.ForeShin = default;

        Leg(kit, false, h, 0.26f, 0.035f, 0.026f, 0.02f, 0.02f, -0.01f, 2, 0f, 1);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, -0.02f * h, -0.14f * h), new Vector3(0f, -0.04f * h, -0.30f * h) },
            new[] { 0.06f * h, 0.11f * h, 0.14f * h }, 6, 0.18f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Hare(float h)
    {
        // The rabbit's cousin, leaner and longer in the leg and ear, white
        // with the ear tips black.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.66f * h, 0.36f * h),
            Shoulder = new Vector3(0.060f * h, 0.36f * h, 0.16f * h),
            Hip = new Vector3(0.076f * h, 0.42f * h, -0.22f * h),
            Rump = new Vector3(0f, 0.46f * h, -0.50f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.46f * h, -0.50f * h),
                new Vector3(0f, 0.54f * h, -0.38f * h),
                new Vector3(0f, 0.58f * h, -0.22f * h),
                new Vector3(0f, 0.50f * h, 0f),
                new Vector3(0f, 0.46f * h, 0.16f * h),
                new Vector3(0f, 0.54f * h, 0.30f * h),
                new Vector3(0f, 0.64f * h, 0.37f * h)
            },
            new[] { 0.07f * h, 0.18f * h, 0.22f * h, 0.20f * h, 0.16f * h, 0.12f * h, 0.08f * h }, 16, 1.02f));
        Add(pieces, 1, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.30f * h, -0.28f * h), new Vector3(0f, 0.28f * h, -0.02f * h), new Vector3(0f, 0.30f * h, 0.16f * h) },
            new[] { 0.10f * h, 0.13f * h, 0.09f * h }, 10, 0.4f));
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        Add(head, 0, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.01f * h, -0.08f * h), new Vector3(0f, 0f, 0f), new Vector3(0f, -0.02f * h, 0.08f * h), new Vector3(0f, -0.05f * h, 0.16f * h) },
            new[] { 0.055f * h, 0.095f * h, 0.08f * h, 0.036f * h }, 8, 1f));
        Eyes(head, h, 0.075f, 0.02f, 0.02f, 0.02f);
        Ears(kit, h, 0.038f, 0.05f, -0.05f, 0.08f, 0.36f, -0.12f, 0.05f, 2);
        Add(head, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, -0.05f * h, 0.13f * h), new Vector3(0f, -0.06f * h, 0.18f * h) },
            new[] { 0.035f * h, 0.02f * h }, 7, 1f));
        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.34f, 0.050f, 0.036f, 0.028f, 0.02f, 0.01f);
        Leg(kit, false, h, 0.42f, 0.090f, 0.055f, 0.034f, -0.14f, 0.08f, 0, 0.22f);

        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 1, CreatureMesh.Tube(
            new[] { Vector3.zero, new Vector3(0f, 0.01f * h, -0.05f * h), new Vector3(0f, 0f, -0.10f * h) },
            new[] { 0.035f * h, 0.06f * h, 0.025f * h }, 7, 1f));
        Add(tail, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.03f * h, -0.02f * h), new Vector3(0f, 0.03f * h, -0.09f * h) },
            new[] { 0.03f * h, 0.015f * h }, 6, 0.6f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    private static Kit Scorpion(float h)
    {
        // Low and flat, in segments; pincers out in front on the head pivot;
        // the tail up and over the back with the sting on the end of it.
        var kit = new Kit
        {
            Neck = new Vector3(0f, 0.14f * h, 0.30f * h),
            Shoulder = new Vector3(0.24f * h, 0.14f * h, 0.14f * h),
            Hip = new Vector3(0.24f * h, 0.14f * h, -0.12f * h),
            Rump = new Vector3(0f, 0.16f * h, -0.30f * h)
        };

        var pieces = new List<CreatureMesh.Piece>();
        Add(pieces, 0, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0.14f * h, -0.30f * h),
                new Vector3(0f, 0.16f * h, -0.18f * h),
                new Vector3(0f, 0.18f * h, -0.04f * h),
                new Vector3(0f, 0.18f * h, 0.12f * h),
                new Vector3(0f, 0.16f * h, 0.26f * h)
            },
            new[] { 0.06f * h, 0.11f * h, 0.13f * h, 0.13f * h, 0.10f * h }, 10, 0.5f));
        // the segments, as ridges across the back
        for (int i = 0; i < 5; i++)
            Add(pieces, 2, CreatureMesh.Tube(
                new[] { new Vector3(-0.11f * h, 0.23f * h, (-0.22f + i * 0.11f) * h), new Vector3(0.11f * h, 0.23f * h, (-0.22f + i * 0.11f) * h) },
                new[] { 0.014f * h, 0.014f * h }, 4, 0.6f));
        Eyes(pieces, h, 0.03f, 0.22f, 0.22f, 0.012f);
        kit.Trunk = Wrap(pieces);

        var head = new List<CreatureMesh.Piece>();
        foreach (float side in new[] { 1f, -1f })
        {
            Add(head, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.06f * h * side, 0f, 0f), new Vector3(0.18f * h * side, 0.02f * h, 0.12f * h), new Vector3(0.20f * h * side, 0.03f * h, 0.24f * h) },
                new[] { 0.035f * h, 0.04f * h, 0.035f * h }, 6, 1f));
            Add(head, 0, CreatureMesh.Tube(
                new[] { new Vector3(0.20f * h * side, 0.03f * h, 0.24f * h), new Vector3(0.22f * h * side, 0.04f * h, 0.34f * h), new Vector3(0.18f * h * side, 0.03f * h, 0.44f * h) },
                new[] { 0.05f * h, 0.07f * h, 0.02f * h }, 6, 0.7f));
            Add(head, 1, CreatureMesh.Tube(
                new[] { new Vector3(0.14f * h * side, 0.02f * h, 0.34f * h), new Vector3(0.11f * h * side, 0.02f * h, 0.44f * h) },
                new[] { 0.028f * h, 0.01f * h }, 5, 0.7f));
        }
        kit.Head = Wrap(head);

        Leg(kit, true, h, 0.16f, 0.03f, 0.024f, 0.018f, 0.02f, 0.02f, 2);
        Leg(kit, false, h, 0.16f, 0.03f, 0.024f, 0.018f, -0.02f, -0.02f, 2);

        // the tail: up and over, the sting at the end
        var tail = new List<CreatureMesh.Piece>();
        Add(tail, 0, CreatureMesh.Tube(
            new[]
            {
                Vector3.zero,
                new Vector3(0f, 0.08f * h, -0.12f * h),
                new Vector3(0f, 0.24f * h, -0.16f * h),
                new Vector3(0f, 0.38f * h, -0.08f * h),
                new Vector3(0f, 0.44f * h, 0.04f * h)
            },
            new[] { 0.05f * h, 0.05f * h, 0.045f * h, 0.045f * h, 0.04f * h }, 7, 1f));
        Add(tail, 2, CreatureMesh.Tube(
            new[] { new Vector3(0f, 0.44f * h, 0.04f * h), new Vector3(0f, 0.42f * h, 0.12f * h), new Vector3(0f, 0.34f * h, 0.18f * h) },
            new[] { 0.045f * h, 0.03f * h, 0.006f * h }, 6, 1f));
        kit.Tail = Wrap(tail);

        return kit;
    }

    // -------------------------------------------------------------------- bits

    /// <summary>
    /// A leg in two parts, so it has a knee. The upper half hangs from the hip
    /// and the lower half from the joint, and the joint only ever folds one
    /// way: backwards on the front legs, forwards on the back ones, which is
    /// most of what separates a hind leg from a fore one.
    ///
    /// The top has to finish inside the barrel: attach it where the two
    /// surfaces meet and the top ring cuts through the flank as a wedge.
    /// </summary>
    private static void Leg(Kit kit, bool fore, float h, float length, float top, float middle,
                            float ankle, float kneeZ, float footZ, int shinCoat = 0, float longFoot = 0f, int thighCoat = 0)
    {
        float l = length * h;
        float knee = l * 0.44f;

        var thigh = new List<CreatureMesh.Piece>();

        Add(thigh, thighCoat, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -knee * 0.55f, kneeZ * h * 0.6f),
                new Vector3(0f, -knee, kneeZ * h)
            },
            new[] { top * h, middle * h * 1.15f, middle * h }, 7, 1f));

        var shin = new List<CreatureMesh.Piece>();
        float rest = l - knee;

        Add(shin, shinCoat, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -rest * 0.5f, (footZ - kneeZ) * h * 0.5f),
                new Vector3(0f, -rest * 0.92f, (footZ - kneeZ) * h)
            },
            new[] { middle * h, ankle * h, ankle * h * 0.85f }, 7, 1f));

        // a rabbit's hind foot: long, flat, and lying along the ground
        if (longFoot > 0f)
            Add(shin, 0, CreatureMesh.Tube(
                new[]
                {
                    new Vector3(0f, -rest * 0.9f, (footZ - kneeZ) * h - 0.02f * h),
                    new Vector3(0f, -rest * 0.96f, (footZ - kneeZ) * h + longFoot * h * 0.5f),
                    new Vector3(0f, -rest * 0.96f, (footZ - kneeZ) * h + longFoot * h)
                },
                new[] { ankle * h * 1.1f, ankle * h * 1.2f, ankle * h * 0.7f }, 7, 0.55f));

        // a foot, so it stands on something rather than tapering into the grass
        Add(shin, 2, CreatureMesh.Tube(
            new[]
            {
                new Vector3(0f, -rest * 0.92f, (footZ - kneeZ) * h),
                new Vector3(0f, -rest, (footZ - kneeZ) * h + 0.012f * h)
            },
            new[] { ankle * h * 0.95f, ankle * h * 0.80f }, 7, 0.9f));

        if (fore)
        {
            kit.ForeThigh = Wrap(thigh);
            kit.ForeShin = Wrap(shin);
            kit.ForeKnee = new Vector3(0f, -knee, kneeZ * h);
        }
        else
        {
            kit.HindThigh = Wrap(thigh);
            kit.HindShin = Wrap(shin);
            kit.HindKnee = new Vector3(0f, -knee, kneeZ * h);
        }
    }

    /// <summary>
    /// The ears, one skin each, built about their own root so they can hang
    /// from a pivot on the head. A blade rather than a spike, flattened
    /// across its width, with a paler inside; the tip can take the dark coat,
    /// which is a fox's.
    /// </summary>
    private static void Ears(Kit kit, float h, float rootX, float rootY, float rootZ,
                             float tipX, float tipY, float tipZ, float width, int tipCoat)
    {
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? 1f : -1f;
            var root = new Vector3(rootX * h * side, rootY * h, rootZ * h);
            var tip = new Vector3((tipX - rootX) * h * side, (tipY - rootY) * h, (tipZ - rootZ) * h);
            var mid = tip * 0.45f;

            var ear = new List<CreatureMesh.Piece>();
            Add(ear, 0, CreatureMesh.Tube(new[] { Vector3.zero, mid, tip },
                new[] { width * h * 0.75f, width * h, width * h * 0.18f }, 6, 0.34f));

            // the inside, a shade paler, set a hair toward the middle
            var inward = new Vector3(-side * width * h * 0.12f, 0f, width * h * 0.16f);
            Add(ear, 1, CreatureMesh.Tube(new[] { mid * 0.35f + inward, mid + inward, tip * 0.85f + inward },
                new[] { width * h * 0.45f, width * h * 0.55f, width * h * 0.12f }, 5, 0.3f));

            if (tipCoat != 0)
                Add(ear, tipCoat, CreatureMesh.Tube(new[] { tip * 0.7f, tip * 1.02f },
                    new[] { width * h * 0.5f, width * h * 0.12f }, 6, 0.36f));

            if (i == 0) { kit.EarLeft = Wrap(ear); kit.EarRootLeft = root; }
            else { kit.EarRight = Wrap(ear); kit.EarRootRight = root; }
        }
    }

    /// <summary>Two dark beads on the skull, which is all a face needs at this size.</summary>
    private static void Eyes(List<CreatureMesh.Piece> head, float h, float x, float y, float z, float r)
    {
        foreach (float side in new[] { 1f, -1f })
        {
            var at = new Vector3(x * h * side, y * h, z * h);
            Add(head, 2, CreatureMesh.Tube(
                new[] { at, at + new Vector3(side * r * h * 0.9f, 0f, 0f) },
                new[] { r * h, r * h * 0.7f }, 6, 1f));
        }
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

    private static Material Mat(Color c) => Paint.Flat(c);
}
