using UnityEngine;

/// <summary>
/// The small finds, built from the kit like the ruins. Each is a tile or
/// two across and sits on the ground with no foundation: the tile it was
/// placed on is level, and the ground's own top is a tenth under the root.
/// </summary>
public static partial class LandmarkBuilder
{
    private static float Spread(Job b, float from, float to) => from + (float)b.Rng.NextDouble() * (to - from);

    // ------------------------------------------------------------ Fallen Tree
    /// <summary>
    /// A tree down across the way: either cut, with the stump beside it, or
    /// blown over with its root plate standing on end. The trunk lies along
    /// +x with its branches broken under it and about, and the moss or the
    /// snow of the country on its upper side.
    /// </summary>
    private static void Fallen(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.55f, Weathering = WeatherAt(b) };
        bool cut = b.Rng.Next(2) == 0;
        float r = Spread(b, 0.30f, 0.40f);

        if (cut)
        {
            // the stump, and the splintered top of it where the axe went through
            k.Log(new Vector3(-3.4f, Ground, 0f), new Vector3(-3.4f, 0.75f, 0f), r + 0.03f, Kit.Swatch.Wood, 10);
            k.Cone(new Vector3(-3.4f, 0.75f, 0f), r + 0.03f, 0.35f, Kit.Swatch.EndGrain, 10);
            k.Debris(new Vector3(-2.9f, Ground, 0.4f), 0.8f, 4);
        }
        else
        {
            // the root plate up on end, the hollow it came out of, and the roots
            k.Block(new Vector3(-3.0f, 0.9f, 0f), new Vector3(0.5f, 2.3f, 2.1f), Quaternion.Euler(0f, 0f, -12f), Kit.Swatch.Earth, 0.05f);
            for (int i = 0; i < 5; i++)
            {
                float a = Spread(b, -1.2f, 1.2f);
                var at = new Vector3(-3.2f, 0.9f + Mathf.Sin(a) * 0.9f, Mathf.Cos(a) * 0.9f * (i % 2 == 0 ? 1f : -1f));
                k.Log(at, at + new Vector3(-Spread(b, 0.5f, 1.0f), Spread(b, -0.3f, 0.5f), Spread(b, -0.5f, 0.5f)), 0.07f, Kit.Swatch.OldWood, 6);
            }
            k.Block(new Vector3(-2.2f, Ground - 0.25f, 0f), new Vector3(1.8f, 0.5f, 1.8f), Kit.Swatch.Earth, 0.03f);
        }

        // the trunk, thick then thin, resting on the ground and on a branch
        // that got under it
        k.Log(new Vector3(-2.7f, r, 0.05f), new Vector3(1.6f, r + 0.05f, 0.3f), r, Kit.Swatch.Wood, 10);
        k.Log(new Vector3(1.5f, r * 0.72f + 0.05f, 0.3f), new Vector3(5.6f, r * 0.55f, 0.7f), r * 0.72f, Kit.Swatch.Wood, 8);

        // the branches: broken off, stuck out, or pinned under
        for (int i = 0; i < 6; i++)
        {
            float along = Spread(b, -1.0f, 5.0f);
            float side = b.Rng.Next(2) == 0 ? 1f : -1f;
            float thick = Spread(b, 0.07f, 0.13f);
            float length = Spread(b, 0.9f, 2.2f);
            var root = new Vector3(along, r * 0.9f, 0.2f + along * 0.08f);
            var tip = root + new Vector3(Spread(b, 0.2f, 0.8f) * length, Spread(b, -0.2f, 0.9f) * length, side * Spread(b, 0.5f, 1.0f) * length);
            if (tip.y < thick) tip.y = thick;
            k.Log(root, tip, thick, Kit.Swatch.OldWood, 6);
        }

        for (int i = 0; i < 5; i++) k.Tuft(new Vector3(Spread(b, -3.5f, 5.5f), Ground, Spread(b, -1.8f, 1.8f)), 0.5f);
        k.Debris(new Vector3(2.0f, Ground, -0.6f), 1.6f, 5);

        k.Finish("Fallen tree", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // -------------------------------------------------------------- Dead Fire
    /// <summary>
    /// A ring of stones round cold ash, the charred ends laid pointing in,
    /// a log dragged up to sit on, and a pot left on the stones.
    /// </summary>
    private static void Fire(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.5f, Weathering = WeatherAt(b) };

        int stones = 8 + b.Rng.Next(3);
        for (int i = 0; i < stones; i++)
        {
            float a = i / (float)stones * Mathf.PI * 2f + Spread(b, -0.15f, 0.15f);
            float rad = Spread(b, 0.72f, 0.9f);
            Lying(b, new Vector3(Mathf.Cos(a) * rad, Ground, Mathf.Sin(a) * rad), Spread(b, 0.28f, 0.42f));
        }

        k.Ash(Vector3.zero, 0.4f, 5);

        // the ends, pointing in, blackened to where they were out of the fire
        int ends = 3 + b.Rng.Next(2);
        for (int i = 0; i < ends; i++)
        {
            float a = i / (float)ends * Mathf.PI * 2f + 0.4f;
            var inner = new Vector3(Mathf.Cos(a) * 0.15f, 0.08f, Mathf.Sin(a) * 0.15f);
            var outer = new Vector3(Mathf.Cos(a) * 0.95f, 0.14f, Mathf.Sin(a) * 0.95f);
            k.Log(inner, outer, 0.08f, Kit.Swatch.Char, 6);
        }

        // the log to sit on, on the side away from the wind that day
        float sit = Spread(b, 0f, Mathf.PI * 2f);
        var mid = new Vector3(Mathf.Cos(sit) * 1.9f, 0.19f, Mathf.Sin(sit) * 1.9f);
        var run = new Vector3(-Mathf.Sin(sit), 0f, Mathf.Cos(sit)) * 0.9f;
        k.Log(mid - run, mid + run, 0.19f, Kit.Swatch.OldWood, 8);

        // the pot, and a cup dropped by the log
        var pot = new Vector3(Mathf.Cos(sit + 1.1f) * 0.8f, 0.14f, Mathf.Sin(sit + 1.1f) * 0.8f);
        k.Block(pot, new Vector3(0.30f, 0.28f, 0.30f), Quaternion.Euler(0f, b.Rng.Next(90), 0f), Kit.Swatch.Iron, 0f, true);
        k.Block(mid + run * 0.6f + new Vector3(0f, -0.14f, 0f) + new Vector3(Mathf.Cos(sit), 0f, Mathf.Sin(sit)) * -0.35f,
                new Vector3(0.12f, 0.1f, 0.12f), Kit.Swatch.Iron, 0f, true);

        for (int i = 0; i < 4; i++) k.Tuft(new Vector3(Spread(b, -2.4f, 2.4f), Ground, Spread(b, -2.4f, 2.4f)), 0.4f);

        k.Finish("Dead fire", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ----------------------------------------------------------- Dropped Pack
    /// <summary>
    /// A pack set down against a rock and never picked up: a bedroll tied
    /// to it, a stick laid beside, a cup, and a lantern gone over on its side.
    /// </summary>
    private static void Pack(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.45f, Weathering = WeatherAt(b) };

        Lying(b, new Vector3(0.9f, Ground, 0.7f), Spread(b, 0.9f, 1.3f));

        // the pack, leant back on the rock, its flap open
        var lean = Quaternion.Euler(-22f, 25f, 0f);
        k.Block(new Vector3(0f, 0.30f, 0f), new Vector3(0.52f, 0.58f, 0.30f), lean, Kit.Swatch.Cloth, 0.02f, true);
        k.Block(new Vector3(0f, 0.50f, -0.20f), new Vector3(0.50f, 0.22f, 0.05f), lean * Quaternion.Euler(60f, 0f, 0f), Kit.Swatch.Cloth, 0.02f);
        k.Block(new Vector3(-0.16f, 0.30f, 0.06f), new Vector3(0.06f, 0.58f, 0.32f), lean, Kit.Swatch.DarkWood, 0f, true);
        k.Block(new Vector3(0.16f, 0.30f, 0.06f), new Vector3(0.06f, 0.58f, 0.32f), lean, Kit.Swatch.DarkWood, 0f, true);

        // the bedroll, tied under it, and the stick
        k.Log(new Vector3(-0.32f, 0.13f, -0.05f), new Vector3(0.36f, 0.13f, -0.15f), 0.13f, Kit.Swatch.Cloth, 8);
        k.Log(new Vector3(-0.8f, 0.03f, 0.5f), new Vector3(0.5f, 0.03f, 0.9f), 0.03f, Kit.Swatch.OldWood, 5);

        // the cup, and the lantern on its side, its glass out
        k.Block(new Vector3(-0.5f, 0.05f, -0.4f), new Vector3(0.11f, 0.1f, 0.11f), Kit.Swatch.Iron, 0f, true);
        var over = Quaternion.Euler(0f, b.Rng.Next(360), 90f);
        k.Block(new Vector3(0.6f, 0.12f, -0.7f), new Vector3(0.22f, 0.3f, 0.22f), over, Kit.Swatch.Iron, 0f);
        k.Block(new Vector3(0.6f, 0.12f, -0.7f), new Vector3(0.16f, 0.2f, 0.16f), over, Kit.Swatch.Pane, 0f);

        for (int i = 0; i < 4; i++) k.Tuft(new Vector3(Spread(b, -1.6f, 1.6f), Ground, Spread(b, -1.6f, 1.6f)), 0.4f);

        k.Finish("Dropped pack", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ------------------------------------------------------------------ Snare
    /// <summary>
    /// A sapling bent to a peg, the cord run from its tip to a noose on the
    /// ground across a run through the grass. It has been sprung.
    /// </summary>
    private static void Snare(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.3f, Weathering = WeatherAt(b) };

        // the sapling, bent over in two lengths, and its few leaves
        k.Log(new Vector3(-1.0f, Ground, 0f), new Vector3(-0.45f, 1.35f, 0.1f), 0.05f, Kit.Swatch.Wood, 6);
        k.Log(new Vector3(-0.45f, 1.35f, 0.1f), new Vector3(0.35f, 0.8f, 0.2f), 0.04f, Kit.Swatch.Wood, 6);
        for (int i = 0; i < 4; i++)
        {
            var leafAt = new Vector3(-0.45f + i * 0.2f, 1.36f - i * 0.12f + 0.08f, 0.1f + (i % 2 == 0 ? 0.12f : -0.12f));
            k.Block(leafAt, new Vector3(0.22f, 0.02f, 0.12f), Quaternion.Euler(Spread(b, -30f, 30f), Spread(b, 0f, 360f), 0f), Kit.Swatch.Moss, 0f);
        }

        // the cord down to the noose, and the noose sprung flat
        k.Log(new Vector3(0.35f, 0.8f, 0.2f), new Vector3(0.62f, 0.04f, 0.28f), 0.012f, Kit.Swatch.Cloth, 4);
        k.Ring(new Vector3(0.62f, 0.02f, 0.28f), 0.24f, 0.035f, 0.035f, 12, Kit.Swatch.Iron);

        // the pegs that held it, and the trigger stick lying loose
        k.Post(new Vector3(0.72f, Ground, 0.1f), 0.32f, 0.03f, Kit.Swatch.DarkWood);
        k.Post(new Vector3(0.5f, Ground, 0.5f), 0.28f, 0.03f, Kit.Swatch.DarkWood);
        k.Log(new Vector3(0.3f, 0.02f, -0.3f), new Vector3(0.75f, 0.02f, -0.45f), 0.02f, Kit.Swatch.OldWood, 4);

        // the run: grass either side of a line through it
        for (int i = 0; i < 6; i++)
        {
            float along = -1.6f + i * 0.65f;
            k.Tuft(new Vector3(along * 0.3f + 0.6f, Ground, along + 0.28f + 0.45f), 0.35f);
            k.Tuft(new Vector3(along * 0.3f + 0.6f, Ground, along + 0.28f - 0.45f), 0.35f);
        }

        k.Finish("Snare", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------------- Waymark
    /// <summary>
    /// A small cairn by the way, three courses of stones and a flat one on
    /// top, with a few that have rolled off it lying about the foot.
    /// </summary>
    private static void Waymark(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.35f, Weathering = WeatherAt(b) };

        float[] radius = { 0.62f, 0.42f, 0.24f };
        float[] height = { 0f, 0.42f, 0.78f };
        float[] size = { 0.62f, 0.52f, 0.42f };
        int[] count = { 7, 5, 3 };

        for (int tier = 0; tier < 3; tier++)
        for (int i = 0; i < count[tier]; i++)
        {
            float a = i / (float)count[tier] * Mathf.PI * 2f + tier * 0.7f;
            Lying(b, new Vector3(Mathf.Cos(a) * radius[tier], Ground + height[tier], Mathf.Sin(a) * radius[tier]), size[tier] + Spread(b, -0.06f, 0.06f));
        }

        // the core the courses lean on, and the flat stone on top
        k.Block(new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.6f), Kit.Swatch.DarkStone, 0.02f, true);
        k.Block(new Vector3(0f, 1.2f, 0f), new Vector3(0.66f, 0.13f, 0.56f), Quaternion.Euler(0f, b.Rng.Next(60), 3f), Kit.Swatch.Stone, 0.015f);

        // the ones that came off it
        int off = 2 + b.Rng.Next(3);
        for (int i = 0; i < off; i++)
        {
            float a = Spread(b, 0f, Mathf.PI * 2f);
            float far = Spread(b, 1.0f, 1.7f);
            Lying(b, new Vector3(Mathf.Cos(a) * far, Ground, Mathf.Sin(a) * far), Spread(b, 0.3f, 0.5f));
        }

        for (int i = 0; i < 3; i++) k.Tuft(new Vector3(Spread(b, -1.6f, 1.6f), Ground, Spread(b, -1.6f, 1.6f)), 0.35f);

        k.Finish("Waymark", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ------------------------------------------------------------ Broken Cart
    /// <summary>
    /// A cart down on one side where a wheel came off: the bed tilted to the
    /// ground, the shafts dropped, the wheel lying a little way off, and a
    /// barrel that was too heavy to carry.
    /// </summary>
    private static void Cart(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.6f, Weathering = WeatherAt(b) };

        // the bed, down at the back left where the wheel was
        var tilt = Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis(-14f, Vector3.right) * Quaternion.AngleAxis(6f, Vector3.forward);
        var bedAt = new Vector3(0f, 0.5f, 0f);

        k.Block(bedAt, new Vector3(2.6f, 0.1f, 1.4f), tilt, Kit.Swatch.OldWood, 0.02f);
        k.Block(bedAt + tilt * new Vector3(0f, 0.22f, 0.7f), new Vector3(2.6f, 0.4f, 0.07f), tilt, Kit.Swatch.Plank, 0.02f);
        k.Block(bedAt + tilt * new Vector3(0f, 0.22f, -0.7f), new Vector3(2.6f, 0.4f, 0.07f), tilt, Kit.Swatch.Plank, 0.02f);
        k.Block(bedAt + tilt * new Vector3(1.3f, 0.22f, 0f), new Vector3(0.07f, 0.4f, 1.4f), tilt, Kit.Swatch.Plank, 0.02f);

        // the axle, with the one wheel still on it
        k.Log(bedAt + tilt * new Vector3(-0.3f, -0.12f, -0.9f), bedAt + tilt * new Vector3(-0.3f, -0.12f, 0.9f), 0.07f, Kit.Swatch.DarkWood, 6);
        k.Cartwheel(new Vector3(-0.3f, Ground, -1.0f), 0.55f, 0f);

        // the other wheel, off and lying flat a little way away
        var wheelAt = new Vector3(-1.9f, Ground + 0.05f, 1.6f);
        k.Ring(wheelAt, 0.5f, 0.09f, 0.11f, 14, Kit.Swatch.OldWood);
        k.Block(wheelAt + new Vector3(0f, 0.06f, 0f), new Vector3(0.24f, 0.12f, 0.24f), Kit.Swatch.DarkWood, 0f, true);
        k.Block(wheelAt + new Vector3(0f, 0.05f, 0f), new Vector3(0.95f, 0.06f, 0.08f), Kit.Swatch.OldWood, 0f);
        k.Block(wheelAt + new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.06f, 0.95f), Kit.Swatch.OldWood, 0f);

        // the shafts, dropped to the ground in front
        k.Log(bedAt + tilt * new Vector3(1.3f, 0f, 0.5f), new Vector3(3.2f, 0.06f, 0.55f), 0.05f, Kit.Swatch.DarkWood, 6);
        k.Log(bedAt + tilt * new Vector3(1.3f, 0f, -0.5f), new Vector3(3.2f, 0.06f, -0.45f), 0.05f, Kit.Swatch.DarkWood, 6);

        // the barrel, off the back and left; the load went with them
        k.Barrel(new Vector3(-2.0f, Ground, -0.6f), 0.3f, 0.8f);
        k.Debris(new Vector3(-1.2f, Ground, 0.2f), 1.2f, 4);
        for (int i = 0; i < 5; i++) k.Tuft(new Vector3(Spread(b, -2.6f, 3.2f), Ground, Spread(b, -1.8f, 1.8f)), 0.45f);

        k.Finish("Broken cart", b.Root, Vector3.zero, b.Flora.Paint);
    }
}
