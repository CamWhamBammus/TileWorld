using UnityEngine;

/// <summary>
/// Builds the structures out of primitives. The tile art has no buildings in
/// it, so these are assembled by hand: timber framing over plaster, coursed
/// stone with the courses knocked out of true, roofs that have partly fallen.
///
/// Everything is driven by a seeded random derived from the chunk, so a
/// structure rebuilt after you walk away is identical to the one you left.
/// Only structure carries colliders; trim, ivy and scatter do not, which keeps
/// the physics cost to a fraction of the part count.
///
/// Swapping in bought or modelled assets means replacing Build() alone.
/// </summary>
public static class LandmarkBuilder
{
    private static Material stone, darkStone, mossStone, plaster, timber, wood, thatch, iron, ivy, soil;

    public static GameObject Build(Landmarks.Placement placement, Transform parent)
    {
        EnsureMaterials();

        var root = new GameObject(Landmarks.NameOf(placement.Kind) + " " + placement.Chunk);
        root.transform.SetParent(parent, false);
        root.transform.position = placement.Position;
        root.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);

        var rng = new System.Random(placement.Chunk.x * 73856093 ^ placement.Chunk.y * 19349663 ^ ((int)placement.Kind * 977));

        switch (placement.Kind)
        {
            case LandmarkKind.AbandonedHouse: BuildHouse(root.transform, rng); break;
            case LandmarkKind.RuinedTower: BuildTower(root.transform, rng); break;
            case LandmarkKind.StoneCircle: BuildCircle(root.transform, rng); break;
            default: BuildWatchtower(root.transform, rng); break;
        }

        return root;
    }

    // ---------------------------------------------------------------- helpers

    private static void EnsureMaterials()
    {
        if (stone != null) return;

        stone = Mat(new Color(0.63f, 0.62f, 0.58f));
        darkStone = Mat(new Color(0.46f, 0.45f, 0.43f));
        mossStone = Mat(new Color(0.36f, 0.42f, 0.30f));
        plaster = Mat(new Color(0.78f, 0.74f, 0.64f));
        timber = Mat(new Color(0.24f, 0.17f, 0.11f));
        wood = Mat(new Color(0.45f, 0.32f, 0.20f));
        thatch = Mat(new Color(0.60f, 0.49f, 0.26f));
        iron = Mat(new Color(0.22f, 0.23f, 0.25f));
        ivy = Mat(new Color(0.27f, 0.40f, 0.22f));
        soil = Mat(new Color(0.30f, 0.25f, 0.18f));
    }

    private static Material Mat(Color c)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(lit);
        m.SetColor("_BaseColor", c);
        m.color = c;
        m.SetFloat("_Smoothness", 0.05f);
        return m;
    }

    private static float Rand(System.Random r, float a, float b)
    {
        return a + (float)r.NextDouble() * (b - a);
    }

    /// <summary>One piece. Non solid pieces lose their collider, which most trim should.</summary>
    private static Transform Prim(Transform p, PrimitiveType type, Vector3 pos, Vector3 scale,
        Vector3 euler, Material mat, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        if (!solid)
        {
            var col = go.GetComponent<Collider>();

            if (col != null)
            {
                // DestroyImmediate is an edit mode call; using it while playing
                // is unsupported and can fail inside a callback.
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }

        return go.transform;
    }

    private static Transform Box(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool solid = true)
        => Prim(p, PrimitiveType.Cube, pos, scale, euler, m, solid);

    private static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool solid = true)
        => Prim(p, PrimitiveType.Cylinder, pos, scale, euler, m, solid);

    private static Transform Sph(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool solid = false)
        => Prim(p, PrimitiveType.Sphere, pos, scale, euler, m, solid);

    /// <summary>
    /// Fallen masonry. Tumbled blocks rather than spheres: a squashed sphere
    /// still reads as a ball, and a pile of balls does not look like stone.
    /// </summary>
    private static void Rubble(Transform p, System.Random rng, Vector3 centre, float spread, int count, float size)
    {
        for (int i = 0; i < count; i++)
        {
            var at = centre + new Vector3(Rand(rng, -spread, spread), 0f, Rand(rng, -spread, spread));
            float s = size * Rand(rng, 0.6f, 1.4f);

            Box(p, at + Vector3.up * s * 0.3f,
                new Vector3(s, s * Rand(rng, 0.45f, 0.8f), s * Rand(rng, 0.7f, 1.15f)),
                new Vector3(Rand(rng, -26f, 26f), Rand(rng, 0f, 360f), Rand(rng, -26f, 26f)),
                rng.Next(5) == 0 ? mossStone : (rng.Next(3) == 0 ? darkStone : stone), solid: false);
        }
    }

    /// <summary>Patches of growth up a wall.</summary>
    private static void Ivy(Transform p, System.Random rng, Vector3 at, float height, int patches)
    {
        for (int i = 0; i < patches; i++)
        {
            float t = i / (float)Mathf.Max(1, patches - 1);

            Box(p, at + new Vector3(Rand(rng, -0.5f, 0.5f), t * height, Rand(rng, -0.12f, 0.12f)),
                new Vector3(Rand(rng, 0.7f, 1.4f), Rand(rng, 0.5f, 1.2f), 0.18f),
                new Vector3(Rand(rng, -8f, 8f), Rand(rng, -20f, 20f), Rand(rng, -12f, 12f)), ivy, solid: false);
        }
    }

    /// <summary>A helix of steps, kept under the player's step offset.</summary>
    private static void SpiralStair(Transform p, float radius, float fromY, float toY,
        float stepRise, float startAngle, Material mat)
    {
        int steps = Mathf.CeilToInt((toY - fromY) / stepRise);
        float perStep = 360f / Mathf.Max(6f, 2f * Mathf.PI * radius / 1.1f);

        for (int i = 0; i < steps; i++)
        {
            float a = (startAngle + i * perStep) * Mathf.Deg2Rad;
            float y = fromY + i * stepRise;

            Box(p, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius),
                new Vector3(1.5f, stepRise, 1.15f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f), mat);
        }
    }

    // ------------------------------------------------------------ the cottage

    /// <summary>
    /// Timber framed, on a stone plinth, with one gable end fallen in. The
    /// framing is what makes it read as a building rather than four slabs:
    /// dark beams over pale panels, with the panels inset so the frame stands
    /// proud of them.
    /// </summary>
    private static void BuildHouse(Transform p, System.Random rng)
    {
        const float w = 9.5f, d = 7.5f, h = 3.7f;
        bool leftRoofGone = rng.Next(2) == 0;

        // plinth, with rougher stones at the corners
        Box(p, new Vector3(0f, 0.22f, 0f), new Vector3(w + 1.4f, 0.44f, d + 1.4f), Vector3.zero, darkStone);

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f, sz = (i < 2) ? -1f : 1f;

            for (int q = 0; q < 3; q++)
            {
                Box(p, new Vector3(sx * (w / 2f + 0.35f), 0.55f + q * 0.55f, sz * (d / 2f + 0.35f)),
                    new Vector3(1.0f, 0.5f, 1.0f), new Vector3(0f, Rand(rng, -6f, 6f), 0f),
                    q == 1 ? stone : darkStone);
            }
        }

        Box(p, new Vector3(0f, 0.47f, 0f), new Vector3(w, 0.1f, d), Vector3.zero, wood);

        // four walls
        Wall(p, rng, new Vector3(0f, 0.5f, -d / 2f), w, h, 0f, WallOpening.Door);
        Wall(p, rng, new Vector3(0f, 0.5f, d / 2f), w, h, 180f, WallOpening.Window);
        Wall(p, rng, new Vector3(-w / 2f, 0.5f, 0f), d, h, 90f, WallOpening.Window);
        Wall(p, rng, new Vector3(w / 2f, 0.5f, 0f), d, h, 270f, WallOpening.None);

        float eave = 0.5f + h;
        float ridge = eave + 3.1f;              // steeper than a barn
        float overhang = 0.55f;
        float run = d / 2f + overhang;

        // Pitch comes from the geometry rather than a guessed constant, so the
        // rafters and the thatch actually lie on the roof line.
        float pitch = Mathf.Atan2(ridge - eave, run) * Mathf.Rad2Deg;
        float slope = Mathf.Sqrt(run * run + (ridge - eave) * (ridge - eave));

        // Gable ends, sliced along z and cut to the roof line. Built as
        // horizontal courses instead, the corners of the steps stand proud of
        // the thatch and show as blocks sitting on the roof.
        for (int side = -1; side <= 1; side += 2)
        {
            if (leftRoofGone && side < 0) continue;

            const int slices = 9;

            for (int i = 0; i < slices; i++)
            {
                float z = Mathf.Lerp(-d / 2f, d / 2f, (i + 0.5f) / slices);
                float roofY = ridge - Mathf.Abs(z) * (ridge - eave) / run;
                float height = roofY - eave;

                if (height <= 0.05f) continue;

                Box(p, new Vector3(side * (w / 2f - 0.08f), eave + height * 0.5f, z),
                    new Vector3(0.34f, height, d / slices + 0.02f), Vector3.zero, plaster);
            }
        }

        Box(p, new Vector3(0f, ridge, 0f), new Vector3(w + 1.0f, 0.26f, 0.26f), Vector3.zero, timber);

        for (int side = -1; side <= 1; side += 2)
        {
            Box(p, new Vector3(0f, Mathf.Lerp(eave, ridge, 0.5f), side * run * 0.5f),
                new Vector3(w + 1.0f, 0.2f, 0.2f), Vector3.zero, timber, solid: false);
        }

        // rafters along the true roof line, some gone on the collapsed side
        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(-w / 2f - 0.35f, w / 2f + 0.35f, i / 8f);

            for (int side = -1; side <= 1; side += 2)
            {
                bool gone = leftRoofGone && side < 0 && i < 5;
                if (gone && rng.Next(4) != 0) continue;

                Box(p, new Vector3(x, (eave + ridge) * 0.5f, side * run * 0.5f),
                    new Vector3(0.17f, 0.17f, slope),
                    new Vector3(side * pitch, 0f, 0f), timber, solid: false);
            }
        }

        // thatch laid in courses up the slope, each overlapping the one below
        for (int side = -1; side <= 1; side += 2)
        {
            bool gone = leftRoofGone && side < 0;
            int courses = 4;

            for (int c = 0; c < courses; c++)
            {
                float t = (c + 0.5f) / courses;

                float z = Mathf.Lerp(side * run, 0f, t);
                float y = Mathf.Lerp(eave, ridge, t) + 0.12f;

                float width = gone ? w * 0.44f : w + 0.9f;
                float xoff = gone ? w * 0.28f : 0f;

                Box(p, new Vector3(xoff, y, z),
                    new Vector3(width, 0.3f, slope / courses + 0.28f),
                    new Vector3(side * pitch, 0f, 0f), thatch);
            }
        }

        Box(p, new Vector3(0f, ridge + 0.2f, 0f), new Vector3(w + 0.5f, 0.26f, 0.62f), Vector3.zero, thatch, false);

        // Chimney, tall enough to clear the ridge. It has to be built from the
        // roof height, or raising the roof leaves it poking through the thatch.
        Vector3 stack = new Vector3(w / 2f - 1.2f, 0f, d / 2f - 1.4f);
        float stackTop = ridge + 1.5f;
        int stackBlocks = Mathf.CeilToInt((stackTop - 0.7f) / 0.85f);

        for (int i = 0; i < stackBlocks; i++)
        {
            float taper = i > stackBlocks - 4 ? 0.85f : 1f;
            Box(p, stack + Vector3.up * (0.7f + i * 0.85f), new Vector3(1.6f * taper, 0.85f, 1.6f * taper),
                new Vector3(0f, Rand(rng, -4f, 4f), 0f), i % 2 == 0 ? stone : darkStone);
        }

        Box(p, stack + Vector3.up * (0.7f + stackBlocks * 0.85f), new Vector3(1.9f, 0.3f, 1.9f),
            Vector3.zero, darkStone, false);
        Box(p, stack + new Vector3(0f, 1.0f, -0.85f), new Vector3(1.1f, 1.3f, 0.4f), Vector3.zero, iron, false);
        Rubble(p, rng, stack + new Vector3(0f, 0.5f, -1.1f), 0.4f, 4, 0.4f);

        // what is left of the furniture
        Box(p, new Vector3(-2.2f, 1.35f, 1.0f), new Vector3(2.6f, 0.14f, 1.3f),
            new Vector3(0f, Rand(rng, -12f, 12f), 7f), wood, false);
        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f, sz = (i < 2) ? -1f : 1f;
            Box(p, new Vector3(-2.2f + sx * 1.1f, 0.95f, 1.0f + sz * 0.5f),
                new Vector3(0.14f, 0.85f, 0.14f), Vector3.zero, wood, false);
        }

        Cyl(p, new Vector3(2.4f, 0.95f, -1.6f), new Vector3(0.9f, 0.55f, 0.9f),
            new Vector3(0f, 0f, Rand(rng, 8f, 20f)), wood, false);

        // outside: fallen thatch, planks, a woodpile, ivy up the standing gable
        Rubble(p, rng, new Vector3(leftRoofGone ? -w * 0.4f : w * 0.4f, 0f, 0f), 3.0f, 9, 0.7f);

        for (int i = 0; i < 5; i++)
        {
            Box(p, new Vector3(Rand(rng, -w * 0.7f, w * 0.7f), 0.52f, Rand(rng, -d * 0.8f, d * 0.8f)),
                new Vector3(Rand(rng, 1.2f, 2.6f), 0.12f, 0.26f),
                new Vector3(0f, Rand(rng, 0f, 360f), Rand(rng, -4f, 4f)), timber, false);
        }

        for (int i = 0; i < 7; i++)
        {
            Cyl(p, new Vector3(-w / 2f - 1.5f, 0.65f + (i / 3) * 0.42f, -1.4f + (i % 3) * 0.45f),
                new Vector3(0.4f, 0.9f, 0.4f), new Vector3(0f, 0f, 90f), wood, false);
        }

        Ivy(p, rng, new Vector3(w / 2f - 0.1f, 0.6f, 1.2f), 3.2f, 7);
    }

    private enum WallOpening { None, Door, Window }

    /// <summary>Plaster panels held in a timber frame, with an opening cut into it.</summary>
    private static void Wall(Transform p, System.Random rng, Vector3 centre, float length, float height,
        float yaw, WallOpening opening)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 along = rot * Vector3.right;
        Vector3 euler = new Vector3(0f, yaw, 0f);

        float gapStart = opening == WallOpening.None ? 0f : length * 0.5f - (opening == WallOpening.Door ? 0.9f : 1.1f);
        float gapWidth = opening == WallOpening.None ? 0f : (opening == WallOpening.Door ? 1.8f : 2.2f);
        float gapLow = opening == WallOpening.Door ? 0f : 1.35f;
        float gapHigh = opening == WallOpening.Door ? 2.5f : 2.65f;

        void Panel(float from, float to, float low, float high, Material m, bool solid = true)
        {
            if (to - from <= 0.02f || high - low <= 0.02f) return;

            float mid = (from + to) * 0.5f - length * 0.5f;
            Box(p, centre + along * mid + Vector3.up * ((low + high) * 0.5f),
                new Vector3(to - from, high - low, 0.36f), euler, m, solid);
        }

        // the plaster itself
        Panel(0f, gapStart, 0f, height, plaster);
        Panel(gapStart + gapWidth, length, 0f, height, plaster);
        Panel(gapStart, gapStart + gapWidth, 0f, gapLow, plaster);
        Panel(gapStart, gapStart + gapWidth, gapHigh, height, plaster);

        // frame, standing proud of the panels
        void Beam(float from, float to, float low, float high)
        {
            float mid = (from + to) * 0.5f - length * 0.5f;
            Box(p, centre + along * mid + Vector3.up * ((low + high) * 0.5f),
                new Vector3(Mathf.Max(0.26f, to - from), Mathf.Max(0.26f, high - low), 0.5f), euler, timber, false);
        }

        Beam(0f, length, 0f, 0.28f);                 // sill plate
        Beam(0f, length, height - 0.3f, height);     // top plate
        Beam(0f, 0.3f, 0f, height);                  // corner posts
        Beam(length - 0.3f, length, 0f, height);

        int studs = Mathf.Max(2, Mathf.RoundToInt(length / 1.9f));

        for (int i = 1; i < studs; i++)
        {
            float x = length * i / studs;
            if (opening != WallOpening.None && x > gapStart - 0.3f && x < gapStart + gapWidth + 0.3f) continue;
            Beam(x - 0.13f, x + 0.13f, 0.2f, height - 0.2f);
        }

        // opening surround
        if (opening != WallOpening.None)
        {
            Beam(gapStart - 0.26f, gapStart, gapLow, gapHigh);
            Beam(gapStart + gapWidth, gapStart + gapWidth + 0.26f, gapLow, gapHigh);
            Beam(gapStart - 0.3f, gapStart + gapWidth + 0.3f, gapHigh, gapHigh + 0.3f);

            if (opening == WallOpening.Window)
            {
                Beam(gapStart - 0.3f, gapStart + gapWidth + 0.3f, gapLow - 0.22f, gapLow);

                // a shutter, hanging off one hinge
                float mid = gapStart + gapWidth * 0.5f - length * 0.5f;
                Box(p, centre + along * (mid - gapWidth * 0.45f) + Vector3.up * (gapLow + 0.7f) - rot * Vector3.forward * 0.35f,
                    new Vector3(gapWidth * 0.5f, 1.15f, 0.1f),
                    new Vector3(0f, yaw + Rand(rng, 22f, 46f), Rand(rng, -9f, 9f)), wood, false);
            }
            else
            {
                // the door, off its hinges and leaning
                float mid = gapStart + gapWidth * 0.5f - length * 0.5f;
                Box(p, centre + along * (mid + 0.75f) + Vector3.up * 1.05f - rot * Vector3.forward * 0.75f,
                    new Vector3(1.5f, 2.2f, 0.12f),
                    new Vector3(0f, yaw + Rand(rng, 20f, 40f), Rand(rng, 12f, 22f)), wood, false);
            }
        }
    }

    // -------------------------------------------------------------- the tower

    /// <summary>
    /// Round, coursed, and collapsed down one side. Every block is nudged off
    /// true, which is most of what separates a ruin from a stack of cubes.
    /// </summary>
    private static void BuildTower(Transform p, System.Random rng)
    {
        const float radius = 3.6f;
        const int perRing = 13;
        const float ringHeight = 0.78f;
        int rings = 14 + rng.Next(4);

        float blockWidth = 2f * Mathf.PI * radius / perRing * 1.2f;

        // plinth and buttresses
        Cyl(p, new Vector3(0f, 0.22f, 0f), new Vector3(radius * 2.7f, 0.22f, radius * 2.7f), Vector3.zero, darkStone);

        for (int i = 0; i < 4; i++)
        {
            float a = (i / 4f) * Mathf.PI * 2f + 0.5f;
            Box(p, new Vector3(Mathf.Cos(a) * (radius + 0.5f), 1.1f, Mathf.Sin(a) * (radius + 0.5f)),
                new Vector3(1.5f, 2.2f, 1.5f), new Vector3(0f, -a * Mathf.Rad2Deg, Rand(rng, -3f, 3f)), darkStone);
        }

        for (int r = 0; r < rings; r++)
        {
            float y = 0.44f + r * ringHeight;
            float ruin = Mathf.InverseLerp(rings * 0.5f, rings, r);
            float stagger = (r % 2) * 0.5f / perRing;

            for (int i = 0; i < perRing; i++)
            {
                float frac = i / (float)perRing + stagger;

                if (r < 4 && frac > 0.44f && frac < 0.60f) continue;             // doorway
                if (r > 6 && r % 5 == 0 && (i == 3 || i == 9)) continue;         // openings
                if (ruin > 0f && frac > 0.60f && frac < 0.60f + ruin * 0.45f) continue;

                float a = frac * Mathf.PI * 2f;
                float rr = radius + Rand(rng, -0.05f, 0.05f);

                var pos = new Vector3(Mathf.Cos(a) * rr, y + ringHeight * 0.5f + Rand(rng, -0.02f, 0.02f),
                                      Mathf.Sin(a) * rr);

                // Enough variation to look hand laid, not so much that the
                // courses stop reading as courses.
                Box(p, pos, new Vector3(blockWidth * Rand(rng, 0.95f, 1.04f), ringHeight * Rand(rng, 0.93f, 1.0f), 0.85f),
                    new Vector3(Rand(rng, -1.5f, 1.5f), -a * Mathf.Rad2Deg + Rand(rng, -2.5f, 2.5f), Rand(rng, -1.5f, 1.5f)),
                    rng.Next(9) == 0 ? mossStone : (rng.Next(4) == 0 ? darkStone : stone));
            }
        }

        // arched doorway, built from stepped voussoirs
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Lerp(0.435f, 0.605f, i / 4f) * Mathf.PI * 2f;
            float lift = Mathf.Sin(i / 4f * Mathf.PI) * 0.42f;

            Box(p, new Vector3(Mathf.Cos(a) * radius, 3.5f + lift, Mathf.Sin(a) * radius),
                new Vector3(0.9f, 0.55f, 0.95f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f), stone);
        }

        float topY = 0.44f + rings * ringHeight;

        // crenellations on the standing half
        for (int i = 0; i < perRing; i++)
        {
            float frac = i / (float)perRing;
            if (frac > 0.55f) continue;
            if (i % 2 == 1) continue;

            float a = frac * Mathf.PI * 2f;
            Box(p, new Vector3(Mathf.Cos(a) * radius, topY + 0.4f, Mathf.Sin(a) * radius),
                new Vector3(blockWidth * 0.75f, 0.8f, 0.8f),
                new Vector3(0f, -a * Mathf.Rad2Deg + Rand(rng, -4f, 4f), 0f), stone);
        }

        // broken floor joists poking out of the wall, halfway up
        float joistY = 0.44f + (rings / 2) * ringHeight;

        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Lerp(0.05f, 0.5f, i / 4f) * Mathf.PI * 2f;
            Box(p, new Vector3(Mathf.Cos(a) * radius * 0.55f, joistY, Mathf.Sin(a) * radius * 0.55f),
                new Vector3(radius * 1.1f, 0.22f, 0.22f),
                new Vector3(0f, -a * Mathf.Rad2Deg + 90f, Rand(rng, -4f, 4f)), timber, false);
        }

        SpiralStair(p, radius - 1.4f, 0.44f, topY - 1.0f, 0.4f, 200f, darkStone);

        // the collapsed side, on the ground
        Rubble(p, rng, new Vector3(Mathf.Cos(4.5f) * (radius + 2.4f), 0f, Mathf.Sin(4.5f) * (radius + 2.4f)),
               3.2f, 16, 0.95f);
        Rubble(p, rng, Vector3.zero, radius + 3.6f, 8, 0.7f);

        Ivy(p, rng, new Vector3(Mathf.Cos(1.4f) * radius, 0.4f, Mathf.Sin(1.4f) * radius), topY * 0.65f, 10);
    }

    // ------------------------------------------------------------- the circle

    /// <summary>Irregular stones, tapered and tilted, around a cracked altar.</summary>
    private static void BuildCircle(Transform p, System.Random rng)
    {
        const int count = 12;
        const float radius = 8.5f;

        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f;
            var at = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            float deg = -a * Mathf.Rad2Deg;

            bool fallen = rng.Next(5) == 0;

            if (fallen)
            {
                Box(p, at + Vector3.up * 0.5f, new Vector3(1.4f, 3.6f, 1.0f),
                    new Vector3(Rand(rng, 74f, 88f), deg, Rand(rng, -8f, 8f)), mossStone);
                Sph(p, at + Vector3.up * 0.12f, new Vector3(2.6f, 0.3f, 2.2f), Vector3.zero, soil);
                continue;
            }

            float h = Rand(rng, 4.2f, 6.0f);
            float tilt = Rand(rng, -5f, 5f);

            // two stacked blocks, the upper narrower, so the stone tapers
            Box(p, at + Vector3.up * h * 0.32f, new Vector3(Rand(rng, 1.4f, 1.8f), h * 0.64f, Rand(rng, 0.9f, 1.2f)),
                new Vector3(tilt * 0.4f, deg + Rand(rng, -8f, 8f), tilt), stone);

            Box(p, at + Vector3.up * (h * 0.78f), new Vector3(Rand(rng, 1.1f, 1.5f), h * 0.34f, Rand(rng, 0.8f, 1.05f)),
                new Vector3(tilt * 0.6f, deg + Rand(rng, -10f, 10f), tilt * 1.3f), rng.Next(4) == 0 ? mossStone : stone);

            // kerb stone at the foot
            Sph(p, at + Vector3.up * 0.16f, new Vector3(2.1f, 0.42f, 1.7f),
                new Vector3(0f, Rand(rng, 0f, 360f), 0f), darkStone);

            // lintels bridging some pairs
            if (i % 3 == 0)
            {
                float a2 = (i + 1) / (float)count * Mathf.PI * 2f;
                var mid = (at + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius)) * 0.5f;

                Box(p, mid.normalized * radius + Vector3.up * (h - 0.35f),
                    new Vector3(4.7f, 0.75f, 1.15f),
                    new Vector3(0f, deg - 15f, Rand(rng, -2f, 2f)), darkStone);
            }
        }

        // altar, cracked in two
        Box(p, new Vector3(0f, 0.3f, 0f), new Vector3(4.6f, 0.6f, 3.2f), Vector3.zero, darkStone);
        Box(p, new Vector3(-1.05f, 0.78f, 0f), new Vector3(2.2f, 0.36f, 2.6f), new Vector3(0f, 3f, -1.5f), stone);
        Box(p, new Vector3(1.15f, 0.74f, 0.1f), new Vector3(2.1f, 0.36f, 2.6f), new Vector3(0f, -4f, 2.5f), stone);

        Rubble(p, rng, Vector3.zero, radius * 1.15f, 10, 0.55f);
    }

    // --------------------------------------------------------- the watchtower

    /// <summary>Braced timber legs, a planked deck, and a shingled roof.</summary>
    private static void BuildWatchtower(Transform p, System.Random rng)
    {
        const float spread = 2.8f;
        float height = 9.5f + rng.Next(3);

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f, sz = (i < 2) ? -1f : 1f;

            Cyl(p, new Vector3(sx * spread * 0.5f, height * 0.5f, sz * spread * 0.5f),
                new Vector3(0.4f, height * 0.5f, 0.4f), new Vector3(sz * 2.5f, 0f, -sx * 2.5f), timber);

            // a stone pad under each leg
            Sph(p, new Vector3(sx * spread * 0.5f, 0.12f, sz * spread * 0.5f),
                new Vector3(1.1f, 0.35f, 1.1f), Vector3.zero, darkStone);
        }

        // horizontal ties and diagonal bracing on every face
        for (int level = 1; level <= 2; level++)
        {
            float y = height * level / 3f;

            for (int face = 0; face < 4; face++)
            {
                bool alongX = face < 2;
                float s = (face % 2 == 0) ? -1f : 1f;

                Vector3 pos = alongX ? new Vector3(0f, y, s * spread * 0.5f)
                                     : new Vector3(s * spread * 0.5f, y, 0f);
                Vector3 size = alongX ? new Vector3(spread + 0.5f, 0.2f, 0.2f)
                                      : new Vector3(0.2f, 0.2f, spread + 0.5f);

                Box(p, pos, size, Vector3.zero, wood, false);

                float diag = Mathf.Sqrt(spread * spread + (height / 3f) * (height / 3f));
                Vector3 dpos = pos + Vector3.up * (height / 6f);
                Vector3 dsize = alongX ? new Vector3(diag, 0.16f, 0.16f) : new Vector3(0.16f, 0.16f, diag);
                float angle = Mathf.Atan2(height / 3f, spread) * Mathf.Rad2Deg * (level % 2 == 0 ? 1f : -1f);

                Box(p, dpos, dsize, alongX ? new Vector3(0f, 0f, angle) : new Vector3(angle, 0f, 0f), wood, false);
            }
        }

        // planked deck
        Box(p, new Vector3(0f, height - 0.12f, 0f), new Vector3(5.8f, 0.18f, 5.8f), Vector3.zero, timber);

        for (int i = 0; i < 9; i++)
        {
            float z = Mathf.Lerp(-2.75f, 2.75f, i / 8f);
            Box(p, new Vector3(0f, height + 0.04f, z), new Vector3(5.8f, 0.1f, 0.56f),
                new Vector3(0f, 0f, 0f), wood, false);
        }

        // rail posts and two rails, open where the stair arrives
        for (int face = 0; face < 4; face++)
        {
            bool alongX = face < 2;
            float s = (face % 2 == 0) ? -1f : 1f;

            if (alongX && s < 0f) continue;

            for (int rail = 0; rail < 2; rail++)
            {
                float y = height + 0.55f + rail * 0.5f;
                Vector3 pos = alongX ? new Vector3(0f, y, s * 2.8f) : new Vector3(s * 2.8f, y, 0f);
                Vector3 size = alongX ? new Vector3(5.8f, 0.14f, 0.14f) : new Vector3(0.14f, 0.14f, 5.8f);
                Box(p, pos, size, Vector3.zero, wood, false);
            }

            for (int post = 0; post < 3; post++)
            {
                float t = Mathf.Lerp(-2.6f, 2.6f, post / 2f);
                Vector3 pos = alongX ? new Vector3(t, height + 0.6f, s * 2.8f) : new Vector3(s * 2.8f, height + 0.6f, t);
                Box(p, pos, new Vector3(0.16f, 1.2f, 0.16f), Vector3.zero, timber, false);
            }
        }

        // roof posts, rafters, and overlapping shingles
        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f, sz = (i < 2) ? -1f : 1f;
            Box(p, new Vector3(sx * 2.3f, height + 1.4f, sz * 2.3f), new Vector3(0.2f, 2.6f, 0.2f), Vector3.zero, timber);
        }

        Box(p, new Vector3(0f, height + 3.55f, 0f), new Vector3(6.2f, 0.2f, 0.2f), Vector3.zero, timber, false);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int c = 0; c < 3; c++)
            {
                float t = c / 3f;
                Box(p, new Vector3(0f, height + 2.85f + t * 0.62f, side * (2.2f - t * 1.9f)),
                    new Vector3(6.2f, 0.14f, 1.5f), new Vector3(side * 30f, 0f, 0f), wood, false);
            }
        }

        // things left on the deck
        Cyl(p, new Vector3(1.9f, height + 0.45f, 1.7f), new Vector3(0.8f, 0.45f, 0.8f), Vector3.zero, wood, false);
        Box(p, new Vector3(-1.8f, height + 0.35f, -1.5f), new Vector3(0.9f, 0.6f, 0.9f),
            new Vector3(0f, Rand(rng, 0f, 40f), 0f), wood, false);
        Cyl(p, new Vector3(2.3f, height + 2.5f, -2.3f), new Vector3(0.25f, 0.3f, 0.25f), Vector3.zero, iron, false);

        SpiralStair(p, 2.9f, 0.4f, height + 0.15f, 0.4f, 180f, wood);

        Rubble(p, rng, Vector3.zero, spread * 1.8f, 5, 0.4f);
    }
}
