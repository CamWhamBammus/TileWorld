using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the animals out of primitives, the same way the landmarks are built.
/// Nothing here is trying to be a model: at the distance you see one, a deer
/// is a body on four legs with its head down, and what tells you it is a deer
/// rather than a goat is the shape of the silhouette and the way it moves.
///
/// The parts that move are handed back so the animal can walk without an
/// animator: legs hang from pivots at the hip, and the head hangs from the
/// neck, so both swing by rotating a transform.
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

    private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    public static Body Build(FaunaKind kind, Transform parent)
    {
        var traits = Fauna.Of(kind);

        var frameGo = new GameObject("frame");
        frameGo.transform.SetParent(parent, false);

        var body = new Body { Frame = frameGo.transform };

        switch (kind)
        {
            case FaunaKind.Deer: Deer(ref body, traits); break;
            case FaunaKind.Rabbit: Rabbit(ref body, traits); break;
            case FaunaKind.Fox: Fox(ref body, traits); break;
            default: Goat(ref body, traits); break;
        }

        return body;
    }

    // ------------------------------------------------------------- the animals

    private static void Deer(ref Body body, Fauna.Traits t)
    {
        float s = t.Size;
        var coat = Mat(t.Coat);
        var under = Mat(t.Under);

        Box(body.Frame, new Vector3(0f, 0.76f * s, 0f), new Vector3(0.34f, 0.36f, 0.86f) * s, Vector3.zero, coat);
        Box(body.Frame, new Vector3(0f, 0.62f * s, 0f), new Vector3(0.30f, 0.16f, 0.72f) * s, Vector3.zero, under);

        var neck = Cyl(body.Frame, new Vector3(0f, 0.98f * s, 0.34f * s), new Vector3(0.13f, 0.24f, 0.13f) * s,
                       new Vector3(-38f, 0f, 0f), coat);

        body.Head = Head(neck, new Vector3(0f, 0.24f * s, 0.06f * s), 0.13f * s, coat, under, s);

        // antlers, on some of them
        Cyl(body.Head, new Vector3(0.055f * s, 0.10f * s, -0.02f * s), new Vector3(0.022f, 0.11f, 0.022f) * s,
            new Vector3(-18f, 0f, 22f), under);
        Cyl(body.Head, new Vector3(-0.055f * s, 0.10f * s, -0.02f * s), new Vector3(0.022f, 0.11f, 0.022f) * s,
            new Vector3(-18f, 0f, -22f), under);

        body.Legs = Legs(body.Frame, coat, 0.58f * s, 0.055f * s, 0.15f * s, 0.30f * s);
        body.Tail = Sph(body.Frame, new Vector3(0f, 0.86f * s, -0.44f * s), Vector3.one * 0.13f * s, Vector3.zero, under);
    }

    private static void Rabbit(ref Body body, Fauna.Traits t)
    {
        float s = t.Size;
        var coat = Mat(t.Coat);
        var under = Mat(t.Under);

        Sph(body.Frame, new Vector3(0f, 0.58f * s, -0.06f * s), new Vector3(0.62f, 0.58f, 0.86f) * s, Vector3.zero, coat);

        var neck = new GameObject("neck").transform;
        neck.SetParent(body.Frame, false);
        neck.localPosition = new Vector3(0f, 0.70f * s, 0.30f * s);

        body.Head = Head(neck, Vector3.zero, 0.24f * s, coat, under, s);

        // the ears are the whole silhouette
        Box(body.Head, new Vector3(0.09f * s, 0.30f * s, -0.04f * s), new Vector3(0.07f, 0.42f, 0.03f) * s,
            new Vector3(-8f, 0f, 9f), coat);
        Box(body.Head, new Vector3(-0.09f * s, 0.30f * s, -0.04f * s), new Vector3(0.07f, 0.42f, 0.03f) * s,
            new Vector3(-8f, 0f, -9f), coat);

        body.Legs = Legs(body.Frame, coat, 0.30f * s, 0.09f * s, 0.20f * s, 0.26f * s);
        body.Tail = Sph(body.Frame, new Vector3(0f, 0.62f * s, -0.48f * s), Vector3.one * 0.22f * s, Vector3.zero, under);
    }

    private static void Fox(ref Body body, Fauna.Traits t)
    {
        float s = t.Size;
        var coat = Mat(t.Coat);
        var under = Mat(t.Under);

        Box(body.Frame, new Vector3(0f, 0.70f * s, 0f), new Vector3(0.34f, 0.32f, 1.00f) * s, Vector3.zero, coat);

        var neck = new GameObject("neck").transform;
        neck.SetParent(body.Frame, false);
        neck.localPosition = new Vector3(0f, 0.82f * s, 0.44f * s);

        body.Head = Head(neck, Vector3.zero, 0.15f * s, coat, under, s);

        // a snout, which is what separates it from every other four legged thing
        Box(body.Head, new Vector3(0f, -0.03f * s, 0.20f * s), new Vector3(0.10f, 0.09f, 0.20f) * s, Vector3.zero, under);
        Box(body.Head, new Vector3(0.08f * s, 0.14f * s, -0.02f * s), new Vector3(0.07f, 0.14f, 0.03f) * s,
            new Vector3(0f, 0f, 10f), coat);
        Box(body.Head, new Vector3(-0.08f * s, 0.14f * s, -0.02f * s), new Vector3(0.07f, 0.14f, 0.03f) * s,
            new Vector3(0f, 0f, -10f), coat);

        body.Legs = Legs(body.Frame, coat, 0.52f * s, 0.06f * s, 0.14f * s, 0.34f * s);

        // the brush, half the animal again
        body.Tail = Cyl(body.Frame, new Vector3(0f, 0.72f * s, -0.66f * s), new Vector3(0.20f, 0.30f, 0.20f) * s,
                        new Vector3(72f, 0f, 0f), coat);
        Sph(body.Tail, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.9f, Vector3.zero, under);
    }

    private static void Goat(ref Body body, Fauna.Traits t)
    {
        float s = t.Size;
        var coat = Mat(t.Coat);
        var under = Mat(t.Under);

        Box(body.Frame, new Vector3(0f, 0.74f * s, 0f), new Vector3(0.36f, 0.38f, 0.80f) * s, Vector3.zero, coat);

        var neck = Cyl(body.Frame, new Vector3(0f, 0.92f * s, 0.32f * s), new Vector3(0.15f, 0.16f, 0.15f) * s,
                       new Vector3(-55f, 0f, 0f), coat);

        body.Head = Head(neck, new Vector3(0f, 0.20f * s, 0f), 0.16f * s, coat, under, s);

        // horns, swept back over the neck
        Cyl(body.Head, new Vector3(0.06f * s, 0.11f * s, -0.02f * s), new Vector3(0.03f, 0.13f, 0.03f) * s,
            new Vector3(48f, 0f, 16f), under);
        Cyl(body.Head, new Vector3(-0.06f * s, 0.11f * s, -0.02f * s), new Vector3(0.03f, 0.13f, 0.03f) * s,
            new Vector3(48f, 0f, -16f), under);

        // a beard, because it is the one thing everyone draws on a goat
        Box(body.Head, new Vector3(0f, -0.13f * s, 0.10f * s), new Vector3(0.05f, 0.14f, 0.05f) * s, Vector3.zero, under);

        body.Legs = Legs(body.Frame, coat, 0.50f * s, 0.06f * s, 0.15f * s, 0.28f * s);
        body.Tail = Sph(body.Frame, new Vector3(0f, 0.88f * s, -0.42f * s), Vector3.one * 0.11f * s, Vector3.zero, under);
    }

    // ------------------------------------------------------------------ pieces

    /// <summary>A head on its own transform, so it can be dipped and raised.</summary>
    private static Transform Head(Transform neck, Vector3 at, float size, Material coat, Material under, float s)
    {
        var pivot = new GameObject("head").transform;
        pivot.SetParent(neck, false);
        pivot.localPosition = at;
        pivot.localRotation = Quaternion.identity;

        Box(pivot, new Vector3(0f, 0f, 0.06f * s), new Vector3(size, size, size * 1.7f), Vector3.zero, coat);
        Sph(pivot, new Vector3(0f, -0.01f * s, size * 0.95f), Vector3.one * size * 0.7f, Vector3.zero, under);

        return pivot;
    }

    /// <summary>
    /// Four legs, each hanging from a pivot at the hip so a rotation swings the
    /// whole leg rather than sliding it through the ground.
    /// </summary>
    private static Transform[] Legs(Transform frame, Material coat, float length, float thickness,
                                    float spread, float reach)
    {
        var legs = new Transform[4];
        int i = 0;

        for (int side = -1; side <= 1; side += 2)
        for (int end = -1; end <= 1; end += 2)
        {
            var pivot = new GameObject("leg").transform;
            pivot.SetParent(frame, false);
            pivot.localPosition = new Vector3(side * spread, length, end * reach);

            Cyl(pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness),
                Vector3.zero, coat);

            legs[i++] = pivot;
        }

        return legs;
    }

    private static Material Mat(Color c)
    {
        int key = c.GetHashCode();

        if (materials.TryGetValue(key, out var cached) && cached != null) return cached;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Without a shader there is no material to make, and an animal with no
        // colour is still an animal: better a grey deer than an exception part
        // way through building one.
        if (lit == null) return null;

        var material = new Material(lit);
        material.SetColor("_BaseColor", c);
        material.color = c;
        material.SetFloat("_Smoothness", 0.08f);

        // Nine animals of a dozen parts each is a lot of small draws, and they
        // share both meshes and coats, so they batch well.
        material.enableInstancing = true;

        materials[key] = material;

        return material;
    }

    private static Transform Prim(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale,
                                  Vector3 euler, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;

        // Nothing on an animal is solid: they keep out of your way on their own,
        // and colliders on something this small mostly serve to snag the player.
        var collider = go.GetComponent<Collider>();

        if (collider != null)
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        return go.transform;
    }

    private static Transform Box(Transform p, Vector3 at, Vector3 scale, Vector3 euler, Material m)
        => Prim(p, PrimitiveType.Cube, at, scale, euler, m);

    private static Transform Cyl(Transform p, Vector3 at, Vector3 scale, Vector3 euler, Material m)
        => Prim(p, PrimitiveType.Cylinder, at, scale, euler, m);

    private static Transform Sph(Transform p, Vector3 at, Vector3 scale, Vector3 euler, Material m)
        => Prim(p, PrimitiveType.Sphere, at, scale, euler, m);
}
