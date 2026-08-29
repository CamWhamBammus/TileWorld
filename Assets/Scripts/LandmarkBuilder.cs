using UnityEngine;

/// <summary>
/// Builds the structures. The tile art has no buildings in it, so these are
/// assembled from boxes — but assembled as buildings: walls with doorways and
/// window gaps, roofs that have partly fallen in, floors you can walk on.
///
/// Swapping in bought or modelled assets means replacing Build() alone;
/// placement, discovery, the map and the quests all go through the placement
/// struct and never touch geometry.
/// </summary>
public static class LandmarkBuilder
{
    private static Material stone, darkStone, wood, darkWood, thatch, moss;

    public static GameObject Build(Landmarks.Placement placement, Transform parent)
    {
        EnsureMaterials();

        var root = new GameObject(Landmarks.NameOf(placement.Kind) + " " + placement.Chunk);
        root.transform.SetParent(parent, false);
        root.transform.position = placement.Position;
        root.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);

        int variant = Mathf.Abs(placement.Chunk.x * 31 + placement.Chunk.y * 17);

        switch (placement.Kind)
        {
            case LandmarkKind.AbandonedHouse: BuildHouse(root.transform, variant); break;
            case LandmarkKind.RuinedTower: BuildTower(root.transform, variant); break;
            case LandmarkKind.StoneCircle: BuildCircle(root.transform, variant); break;
            default: BuildWatchtower(root.transform, variant); break;
        }

        return root;
    }

    private static void EnsureMaterials()
    {
        if (stone != null) return;

        stone = Mat(new Color(0.52f, 0.51f, 0.48f));
        darkStone = Mat(new Color(0.36f, 0.35f, 0.34f));
        wood = Mat(new Color(0.42f, 0.30f, 0.19f));
        darkWood = Mat(new Color(0.28f, 0.20f, 0.13f));
        thatch = Mat(new Color(0.55f, 0.45f, 0.24f));
        moss = Mat(new Color(0.33f, 0.40f, 0.26f));
    }

    private static Material Mat(Color c)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(lit);
        m.SetColor("_BaseColor", c);
        m.color = c;
        return m;
    }

    private static Transform Box(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    /// <summary>
    /// A helix of steps. Towers are worth climbing only if they can be climbed,
    /// and a ladder cannot be: the character controller steps up, it does not
    /// grab rungs. Rise per step is kept under the controller's step offset.
    /// </summary>
    private static void SpiralStair(Transform p, float radius, float fromY, float toY,
        float stepRise, float startAngle, Material mat)
    {
        int steps = Mathf.CeilToInt((toY - fromY) / stepRise);
        float perStep = 360f / Mathf.Max(6f, 2f * Mathf.PI * radius / 1.1f);

        for (int i = 0; i < steps; i++)
        {
            float a = (startAngle + i * perStep) * Mathf.Deg2Rad;
            float y = fromY + i * stepRise;

            var pos = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);

            Box(p, pos, new Vector3(1.5f, stepRise, 1.15f),
                new Vector3(0f, -a * Mathf.Rad2Deg, 0f), mat);
        }
    }

    /// <summary>A wall with a gap left in it for a door or window.</summary>
    private static void GappedWall(Transform p, Vector3 centre, float length, float height,
        float thickness, float yaw, float gapStart, float gapWidth, float gapBottom, float gapTop, Material mat)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 along = rot * Vector3.right;

        void Segment(float from, float to, float yLow, float yHigh)
        {
            if (to - from <= 0.01f || yHigh - yLow <= 0.01f) return;

            float mid = (from + to) * 0.5f - length * 0.5f;
            Vector3 pos = centre + along * mid + Vector3.up * ((yLow + yHigh) * 0.5f);

            Box(p, pos, new Vector3(to - from, yHigh - yLow, thickness), new Vector3(0f, yaw, 0f), mat);
        }

        Segment(0f, gapStart, 0f, height);                       // before the gap
        Segment(gapStart + gapWidth, length, 0f, height);        // after it
        Segment(gapStart, gapStart + gapWidth, 0f, gapBottom);   // under a window
        Segment(gapStart, gapStart + gapWidth, gapTop, height);  // lintel above
    }

    /// <summary>Nine by seven, one gable end fallen in, chimney still up.</summary>
    private static void BuildHouse(Transform p, int variant)
    {
        const float w = 9f, d = 7f, h = 3.6f, t = 0.45f;
        bool roofLeftGone = (variant & 1) == 0;

        Box(p, new Vector3(0f, 0.15f, 0f), new Vector3(w + 1.2f, 0.3f, d + 1.2f), Vector3.zero, darkStone);
        Box(p, new Vector3(0f, 0.34f, 0f), new Vector3(w, 0.12f, d), Vector3.zero, wood);

        // front wall with a doorway, back wall with a window
        GappedWall(p, new Vector3(0f, 0f, -d / 2f), w, h, t, 0f, 3.6f, 1.8f, 0f, 2.5f, stone);
        GappedWall(p, new Vector3(0f, 0f, d / 2f), w, h, t, 0f, 3.2f, 2.2f, 1.4f, 2.6f, stone);
        GappedWall(p, new Vector3(-w / 2f, 0f, 0f), d, h, t, 90f, 2.4f, 1.6f, 1.5f, 2.7f, stone);
        GappedWall(p, new Vector3(w / 2f, 0f, 0f), d, h, t, 90f, 3.0f, 1.5f, 1.5f, 2.7f, stone);

        // ridge beam and rafters
        Box(p, new Vector3(0f, h + 1.9f, 0f), new Vector3(w + 0.4f, 0.22f, 0.22f), Vector3.zero, darkWood);

        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-w / 2f + 0.6f, w / 2f - 0.6f, i / 5f);
            Box(p, new Vector3(x, h + 0.95f, -d / 4f), new Vector3(0.18f, 0.18f, d / 2f + 0.6f),
                new Vector3(-38f, 0f, 0f), darkWood);

            if (!(roofLeftGone && i < 3))
            {
                Box(p, new Vector3(x, h + 0.95f, d / 4f), new Vector3(0.18f, 0.18f, d / 2f + 0.6f),
                    new Vector3(38f, 0f, 0f), darkWood);
            }
        }

        // thatch, missing over the collapsed side
        if (!roofLeftGone)
        {
            Box(p, new Vector3(0f, h + 0.95f, -d / 4f), new Vector3(w + 0.6f, 0.25f, d / 1.7f),
                new Vector3(-38f, 0f, 0f), thatch);
        }
        else
        {
            Box(p, new Vector3(w / 4f, h + 0.95f, -d / 4f), new Vector3(w / 2f, 0.25f, d / 1.7f),
                new Vector3(-38f, 0f, 0f), thatch);
        }

        Box(p, new Vector3(0f, h + 0.95f, d / 4f), new Vector3(w + 0.6f, 0.25f, d / 1.7f),
            new Vector3(38f, 0f, 0f), thatch);

        // chimney
        Box(p, new Vector3(w / 2f - 0.9f, h / 2f + 1.6f, d / 2f - 1.1f), new Vector3(1.3f, h + 3.2f, 1.3f),
            Vector3.zero, darkStone);

        // rubble where the roof came down
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.37f;
            Box(p, new Vector3(Mathf.Sin(a) * 3.2f, 0.5f, Mathf.Cos(a) * 2.4f),
                new Vector3(1.1f, 0.35f, 0.8f), new Vector3(0f, a * 40f, 12f), i % 2 == 0 ? stone : moss);
        }
    }

    /// <summary>Round, fifteen high, broken off on one side.</summary>
    private static void BuildTower(Transform p, int variant)
    {
        const float radius = 3.4f;
        const int perRing = 12;
        const float ringHeight = 0.8f;
        int rings = 13 + (variant % 3);

        // Blocks have to span the arc between them or the wall reads as a row
        // of separate columns; the overlap is what makes it a wall.
        float blockWidth = 2f * Mathf.PI * radius / perRing * 1.18f;

        Box(p, new Vector3(0f, 0.2f, 0f), new Vector3(radius * 2.6f, 0.4f, radius * 2.6f), Vector3.zero, darkStone);

        for (int r = 0; r < rings; r++)
        {
            float y = 0.4f + r * ringHeight;

            // the higher it goes the more has fallen away
            float ruin = Mathf.InverseLerp(rings * 0.55f, rings, r);

            // every other course offset half a block, so the joints do not
            // line up into vertical seams
            float stagger = (r % 2) * 0.5f / perRing;

            for (int i = 0; i < perRing; i++)
            {
                float frac = i / (float)perRing + stagger;

                // doorway at the base
                if (r < 3 && frac > 0.44f && frac < 0.58f) continue;

                // arrow slits
                if (r > 5 && r % 4 == 0 && (i == 3 || i == 10)) continue;

                // collapsed side, growing with height
                if (ruin > 0f && frac > 0.62f && frac < 0.62f + ruin * 0.42f) continue;

                float a = frac * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * radius, y + ringHeight * 0.5f, Mathf.Sin(a) * radius);

                Box(p, pos, new Vector3(blockWidth, ringHeight, 0.8f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f),
                    (r + i) % 5 == 0 ? moss : (r % 2 == 0 ? stone : darkStone));
            }
        }

        // A stair up the inside, so the top is somewhere you can actually get to.
        // No floor at the head of it: a slab there would be exactly what the
        // last steps run into.
        float topY = 0.4f + rings * ringHeight;
        SpiralStair(p, radius - 1.35f, 0.4f, topY - 1.0f, 0.4f, 200f, darkStone);

        // fallen blocks around the base
        for (int i = 0; i < 8; i++)
        {
            float a = i * 0.9f + 1.2f;
            float dist = radius + 1.6f + (i % 3) * 1.1f;
            Box(p, new Vector3(Mathf.Cos(a) * dist, 0.45f, Mathf.Sin(a) * dist),
                new Vector3(1.0f, 0.7f, 0.8f), new Vector3(8f, a * 60f, 14f), i % 3 == 0 ? moss : stone);
        }
    }

    /// <summary>Twelve stones around an altar, a few of them down.</summary>
    private static void BuildCircle(Transform p, int variant)
    {
        const int count = 12;
        const float radius = 8.5f;

        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f;
            var at = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            float deg = -a * Mathf.Rad2Deg;

            bool fallen = ((variant + i) % 5) == 0;
            float h = fallen ? 0.8f : Mathf.Lerp(4.2f, 5.6f, ((i * 7) % 5) / 4f);

            if (fallen)
            {
                Box(p, at + Vector3.up * 0.45f, new Vector3(1.3f, h, 3.4f), new Vector3(0f, deg, 78f), moss);
            }
            else
            {
                Box(p, at + Vector3.up * h * 0.5f, new Vector3(1.5f, h, 1.0f), new Vector3(0f, deg, 0f), stone);

                // lintels bridging some pairs
                if (i % 3 == 0)
                {
                    float a2 = (i + 1) / (float)count * Mathf.PI * 2f;
                    var mid = (at + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius)) * 0.5f;
                    Box(p, mid + Vector3.up * (h - 0.2f), new Vector3(4.6f, 0.7f, 1.1f),
                        new Vector3(0f, deg - 15f, 0f), darkStone);
                }
            }
        }

        Box(p, new Vector3(0f, 0.25f, 0f), new Vector3(4.4f, 0.5f, 3.0f), Vector3.zero, darkStone);
        Box(p, new Vector3(0f, 0.62f, 0f), new Vector3(3.6f, 0.3f, 2.4f), new Vector3(0f, 12f, 0f), stone);
    }

    /// <summary>Timber legs, a ladder, and a platform above the canopy.</summary>
    private static void BuildWatchtower(Transform p, int variant)
    {
        const float legSpread = 2.6f;
        float height = 9.5f + (variant % 3);

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f;
            float sz = (i < 2) ? -1f : 1f;

            Box(p, new Vector3(sx * legSpread * 0.5f, height * 0.5f, sz * legSpread * 0.5f),
                new Vector3(0.35f, height, 0.35f), new Vector3(sz * 2.5f, 0f, -sx * 2.5f), darkWood);
        }

        // cross bracing
        for (int level = 1; level <= 2; level++)
        {
            float y = height * level / 3f;
            Box(p, new Vector3(0f, y, -legSpread * 0.5f), new Vector3(legSpread + 0.4f, 0.2f, 0.2f), Vector3.zero, wood);
            Box(p, new Vector3(0f, y, legSpread * 0.5f), new Vector3(legSpread + 0.4f, 0.2f, 0.2f), Vector3.zero, wood);
            Box(p, new Vector3(-legSpread * 0.5f, y, 0f), new Vector3(0.2f, 0.2f, legSpread + 0.4f), Vector3.zero, wood);
        }

        // Deck widened to 5.8 so it reaches the stair that spirals up outside
        // the legs; a narrower one would leave a gap to fall through.
        Box(p, new Vector3(0f, height, 0f), new Vector3(5.8f, 0.3f, 5.8f), Vector3.zero, wood);

        for (int i = 0; i < 4; i++)
        {
            bool alongX = i < 2;
            float s = (i % 2 == 0) ? -1f : 1f;

            // the stair arrives on the -z side, so that rail is left open
            if (alongX && s < 0f) continue;

            if (alongX)
                Box(p, new Vector3(0f, height + 0.9f, s * 2.8f), new Vector3(5.8f, 0.18f, 0.18f), Vector3.zero, darkWood);
            else
                Box(p, new Vector3(s * 2.8f, height + 0.9f, 0f), new Vector3(0.18f, 0.18f, 5.8f), Vector3.zero, darkWood);
        }

        // corner posts, or the roof reads as floating
        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f;
            float sz = (i < 2) ? -1f : 1f;

            Box(p, new Vector3(sx * 2.0f, height + 1.2f, sz * 2.0f),
                new Vector3(0.2f, 2.2f, 0.2f), Vector3.zero, darkWood);
        }

        // two slopes meeting at a ridge, rather than crossing through each other
        Box(p, new Vector3(0f, height + 2.72f, -1.08f), new Vector3(5.2f, 0.2f, 2.9f),
            new Vector3(-32f, 0f, 0f), thatch);
        Box(p, new Vector3(0f, height + 2.72f, 1.08f), new Vector3(5.2f, 0.2f, 2.9f),
            new Vector3(32f, 0f, 0f), thatch);
        Box(p, new Vector3(0f, height + 3.5f, 0f), new Vector3(5.4f, 0.18f, 0.18f), Vector3.zero, darkWood);

        // Stair around the outside, replacing a ladder nothing could climb. It
        // ends level with the deck rather than under it.
        SpiralStair(p, 2.9f, 0.4f, height + 0.15f, 0.4f, 180f, wood);
    }
}
