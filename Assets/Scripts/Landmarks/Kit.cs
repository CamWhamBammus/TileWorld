using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The building kit: walls, roofs, doors, windows, steps and the rest, made
/// as flat-shaded geometry rather than taken from the pack, which has no
/// building parts at all. Colour comes the way everything else's does: the
/// pack draws with one palette texture, and a face is the colour of wherever
/// its UVs point, so every part here points its faces at a swatch -- wood,
/// stone, plaster, thatch -- found on that sheet by KitIndex.
///
/// One structure is one mesh. A builder gathers everything it is told to add
/// and hands back a single object with the pack's material on it and a box
/// collider for each solid part.
/// </summary>
public class Kit : ScriptableObject
{
    public enum Swatch { Wood, DarkWood, Plank, EndGrain, Stone, DarkStone, Mortar, Plaster, Thatch, Slate, Iron, Pane, Cloth, WarmStone, Water, Earth, Snow, Moss, Vine, Sand, Char, OldWood }

    /// <summary>Where on the palette each swatch is, in the order of the enum.</summary>
    public Vector2[] Where = new Vector2[22];

    private static Kit loaded;

    public static Kit Get()
    {
        if (loaded == null) loaded = Resources.Load<Kit>("Kit");
        return loaded;
    }

    public static Vector2 At(Swatch s)
    {
        var kit = Get();
        return kit != null && (int)s < kit.Where.Length ? kit.Where[(int)s] : new Vector2(0.5f, 0.5f);
    }

    // ------------------------------------------------------------ the builder

    public sealed class Builder
    {
        public enum RoofStyle { Plank, Thatch, Slate }

        /// <summary>What the country does to a place left to it.</summary>
        public enum Weather { None, Vines, Snow, Sand, Char }

        /// <summary>
        /// How far gone a structure is, nought to one. Every part consults
        /// it: at nought a place is kept, at one it is barely standing. What
        /// goes is decided by the builder's own random, so a ruin rebuilt is
        /// the same ruin.
        /// </summary>
        public float Decay = 0f;
        public Weather Weathering = Weather.None;

        /// <summary>Whether a thing of this fragility has gone, at this decay.</summary>
        private bool Gone(float fragility) => Decay > 0f && rng.NextDouble() < fragility * Decay;

        /// <summary>A small lean, more the further gone the place is.</summary>
        private float Lean(float most) => Decay <= 0f ? 0f : Rand(-most, most) * Decay;

        /// <summary>
        /// Old wood for what is left standing, once a place is far gone; and
        /// where the place burned, most of the wood is char and the rest is
        /// scorched grey. Stone and plaster are left their colour: a fire does
        /// not blacken a wall's whole face, and blackened they read as iron.
        /// </summary>
        private Swatch Aged(Swatch swatch)
        {
            bool wood = swatch == Swatch.Wood || swatch == Swatch.Plank || swatch == Swatch.DarkWood || swatch == Swatch.OldWood || swatch == Swatch.EndGrain || swatch == Swatch.Thatch;
            if (!wood) return swatch;
            if (Weathering == Weather.Char) return rng.NextDouble() < 0.7 ? Swatch.Char : Swatch.OldWood;
            return Decay > 0.5f && (swatch == Swatch.Plank || swatch == Swatch.Wood) ? Swatch.OldWood : swatch;
        }

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> faces = new List<int>();
        private readonly List<(Vector3 centre, Vector3 size, Quaternion turn)> solids = new List<(Vector3, Vector3, Quaternion)>();
        private readonly System.Random rng;

        public Builder(int seed) { rng = new System.Random(seed); }

        private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Three greys for stonework, mostly the middle one.</summary>
        private Swatch StoneShade()
        {
            int roll = rng.Next(8);
            return roll == 0 ? Swatch.DarkStone : roll <= 2 ? Swatch.WarmStone : Swatch.Stone;
        }

        // ---------------------------------------------------------- primitives

        /// <summary>
        /// One face with its own corners, wound so its normal is the way asked.
        /// Sharing corners between faces would smooth them, and nothing here
        /// is smooth.
        /// </summary>
        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 uv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { (b, d) = (d, b); }

            int at = points.Count;
            points.Add(a); points.Add(b); points.Add(c); points.Add(d);
            for (int i = 0; i < 4; i++) uvs.Add(uv);
            faces.Add(at); faces.Add(at + 1); faces.Add(at + 2);
            faces.Add(at); faces.Add(at + 2); faces.Add(at + 3);
        }

        private void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 uv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { (b, c) = (c, b); }

            int at = points.Count;
            points.Add(a); points.Add(b); points.Add(c);
            for (int i = 0; i < 3; i++) uvs.Add(uv);
            faces.Add(at); faces.Add(at + 1); faces.Add(at + 2);
        }

        /// <summary>A box. Jitter moves its corners a little, for stone.</summary>
        public void Block(Vector3 centre, Vector3 size, Quaternion turn, Swatch swatch, float jitter = 0f, bool solid = false)
        {
            var uv = At(Aged(swatch));
            var h = size * 0.5f;
            var c = new Vector3[8];

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                if (jitter > 0f) corner += new Vector3(Rand(-jitter, jitter), Rand(-jitter, jitter), Rand(-jitter, jitter));
                c[i] = centre + turn * corner;
            }

            Face(c[0], c[1], c[3], c[2], turn * Vector3.back, uv);     // -z
            Face(c[4], c[5], c[7], c[6], turn * Vector3.forward, uv);  // +z
            Face(c[0], c[2], c[6], c[4], turn * Vector3.left, uv);     // -x
            Face(c[1], c[3], c[7], c[5], turn * Vector3.right, uv);    // +x
            Face(c[0], c[1], c[5], c[4], turn * Vector3.down, uv);     // -y
            Face(c[2], c[3], c[7], c[6], turn * Vector3.up, uv);       // +y

            if (solid) solids.Add((centre, size, turn));
        }

        public void Block(Vector3 centre, Vector3 size, Swatch swatch, float jitter = 0f, bool solid = false)
        {
            Block(centre, size, Quaternion.identity, swatch, jitter, solid);
        }

        /// <summary>A log between two points: eight sides, capped in end grain.</summary>
        public void Log(Vector3 from, Vector3 to, float radius, Swatch swatch = Swatch.Wood, int sides = 8)
        {
            var axis = (to - from).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var up = Vector3.Cross(side, axis);
            var uv = At(Aged(swatch));

            // end grain only where the log is wood: the same shape serves as a
            // tower's mortar core, and that showed a pale disc when its top
            // was broken open
            bool wooden = swatch == Swatch.Wood || swatch == Swatch.Plank || swatch == Swatch.DarkWood || swatch == Swatch.OldWood;
            var grain = wooden && Weathering != Weather.Char ? At(Swatch.EndGrain) : uv;

            var a = new Vector3[sides]; var b = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float ang = i / (float)sides * Mathf.PI * 2f;
                var r = (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius;
                a[i] = from + r; b[i] = to + r;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                var outward = (a[i] + a[j]) * 0.5f - from;
                Face(a[i], a[j], b[j], b[i], outward, uv);
                Tri(from, a[j], a[i], -axis, grain);
                Tri(to, b[i], b[j], axis, grain);
            }
        }

        /// <summary>A triangular prism standing on its base: a gable.</summary>
        public void Gable(Vector3 baseCentre, float width, float height, float thickness, Quaternion turn, Swatch swatch)
        {
            var uv = At(swatch);
            Vector3 P(float x, float y, float z) => baseCentre + turn * new Vector3(x, y, z);
            float w = width * 0.5f, t = thickness * 0.5f;

            var l0 = P(-w, 0f, -t); var r0 = P(w, 0f, -t); var top0 = P(0f, height, -t);
            var l1 = P(-w, 0f, t); var r1 = P(w, 0f, t); var top1 = P(0f, height, t);

            Tri(l0, r0, top0, turn * Vector3.back, uv);
            Tri(l1, r1, top1, turn * Vector3.forward, uv);
            Face(l0, top0, top1, l1, turn * new Vector3(-height, w, 0f), uv);
            Face(r0, top0, top1, r1, turn * new Vector3(height, w, 0f), uv);
            Face(l0, r0, r1, l1, turn * Vector3.down, uv);
        }

        // --------------------------------------------------------------- walls

        /// <summary>
        /// A wall of logs from one point to another, stacked to a height. The
        /// logs run past both ends so that two walls meeting at a corner cross
        /// the way a cabin's do; the caller offsets alternate walls by half a
        /// log so they interlock.
        /// </summary>
        public void LogWall(Vector3 from, Vector3 to, float height, float lift = 0f, float radius = 0.17f)
        {
            var along = (to - from).normalized;
            float pitch = radius * 1.85f;
            var a = from - along * radius * 1.6f;
            var b = to + along * radius * 1.6f;

            int course = 0;
            for (float y = radius + lift; y < height; y += pitch, course++)
            {
                float r = radius * Rand(0.92f, 1.05f);

                // the top courses go first, and the ends of them before the middles
                bool high = y > height - pitch * 2.5f;
                if (high && Gone(0.6f))
                {
                    if (Gone(0.5f)) Log(a + Vector3.up * y, Vector3.Lerp(a, b, Rand(0.3f, 0.6f)) + Vector3.up * y, r);
                    continue;
                }
                if (high && Gone(0.35f))
                {
                    // slipped: one end dropped and pushed out
                    var outward = Vector3.Cross(along, Vector3.up) * Rand(-0.3f, 0.3f);
                    Log(a + Vector3.up * y, b + Vector3.up * (y - radius * 1.2f) + outward, r);
                    continue;
                }
                Log(a + Vector3.up * y, b + Vector3.up * y, r);
            }

            if (Gone(0.5f))
            {
                var at = Vector3.Lerp(from, to, Rand(0.2f, 0.8f)) + Vector3.Cross(along, Vector3.up) * Rand(0.6f, 1.4f) * (rng.Next(2) == 0 ? 1f : -1f);
                var turn = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                Log(at + turn * Vector3.forward * 1.2f + Vector3.up * radius, at - turn * Vector3.forward * 1.2f + Vector3.up * radius, radius * 0.9f);
            }

            Climb(from, to, height, radius * 2f);

            solids.Add(((from + to) * 0.5f + Vector3.up * height * 0.5f,
                        new Vector3(Vector3.Distance(from, to), height, radius * 2f),
                        Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up)));
        }

        /// <summary>
        /// A wall of coursed stone: blocks of uneven length, each course
        /// offset from the last, two greys mixed so it is not one slab.
        /// </summary>
        public void StoneWall(Vector3 from, Vector3 to, float height, float thickness = 0.42f, float course = 0.36f)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / course));
            float rowHeight = height / rows;

            // How much of the top has fallen, along the wall: a wandering line,
            // deeper toward the ends, so the top is ragged rather than shaved.
            float fallen = Decay * rows * 0.55f;
            float bite0 = Rand(0.2f, 1f) * fallen, bite1 = Rand(0.2f, 1f) * fallen, biteMid = Rand(0f, 0.6f) * fallen;

            for (int row = 0; row < rows; row++)
            {
                float y = rowHeight * (row + 0.5f);
                float x = row % 2 == 0 ? 0f : -Rand(0.25f, 0.45f);

                while (x < length)
                {
                    float len = Mathf.Min(Rand(0.45f, 1.0f), length - Mathf.Max(x, 0f));
                    float start = Mathf.Max(x, 0f), end = Mathf.Min(x + len, length);
                    x += len;
                    if (end - start <= 0.08f) continue;

                    float t = (start + end) * 0.5f / length;
                    float bite = t < 0.5f ? Mathf.Lerp(bite0, biteMid, t * 2f) : Mathf.Lerp(biteMid, bite1, (t - 0.5f) * 2f);
                    if (row >= rows - bite) { if (Gone(0.9f)) continue; }

                    var centre = from + along * ((start + end) * 0.5f) + Vector3.up * y;
                    var size = new Vector3(end - start - 0.03f, rowHeight - 0.03f, thickness * Rand(0.94f, 1.04f));
                    Block(centre, size, Quaternion.LookRotation(across, Vector3.up), StoneShade(), 0.012f);
                }
            }

            // the mortar behind the joints, one slab a little inside the face,
            // as tall as the lowest the top has fallen to
            float keptHeight = height - Mathf.Max(bite0, Mathf.Max(bite1, biteMid)) * rowHeight;
            Block(from + along * length * 0.5f + Vector3.up * keptHeight * 0.5f,
                  new Vector3(length, keptHeight, thickness * 0.8f), Quaternion.LookRotation(across, Vector3.up), Swatch.Mortar);

            if (fallen > 0.3f)
            {
                Rubble(Vector3.Lerp(from, to, Rand(0.1f, 0.9f)) + across * Rand(-1f, 1f) * (thickness + 0.4f), 0.9f, 3 + (int)(fallen * 3f));
                if (Gone(0.6f)) Rubble(Vector3.Lerp(from, to, Rand(0.1f, 0.9f)) - across * Rand(0.3f, 0.9f) * (thickness + 0.4f), 0.7f, 3);
            }

            Climb(from, to, height, thickness);
            if (Weathering == Weather.Vines || Decay > 0.3f) Moss(from, to, height, thickness);

            solids.Add((from + along * length * 0.5f + Vector3.up * height * 0.5f,
                        new Vector3(length, height, thickness), Quaternion.LookRotation(across, Vector3.up)));
        }

        /// <summary>
        /// Timber framing over plaster: a pale panel, dark posts and plates,
        /// and a brace across each bay.
        /// </summary>
        public void FrameWall(Vector3 from, Vector3 to, float height, float thickness = 0.28f)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);
            var turn = Quaternion.LookRotation(across, Vector3.up);
            float beam = 0.16f;

            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.1f));

            // the plaster, a panel a bay, and the panels fall out first
            for (int i = 0; i < bays; i++)
            {
                float x0 = length * i / bays, x1 = length * (i + 1) / bays;
                if (Gone(0.55f))
                {
                    // a broken panel: what is left of it, low in the bay
                    if (Gone(0.5f)) continue;
                    float keep = Rand(0.25f, 0.6f);
                    Block(from + along * ((x0 + x1) * 0.5f) + Vector3.up * (height * keep * 0.5f), new Vector3(x1 - x0, height * keep, thickness * 0.8f), turn, Swatch.Plaster, 0.02f);
                    continue;
                }
                Block(from + along * ((x0 + x1) * 0.5f) + Vector3.up * height * 0.5f, new Vector3(x1 - x0, height, thickness * 0.8f), turn, Swatch.Plaster);
            }

            for (int i = 0; i <= bays; i++)
            {
                float x = length * i / bays;
                Block(from + along * x + Vector3.up * height * 0.5f, new Vector3(beam, height, thickness), turn, Swatch.DarkWood);
            }
            Block(from + along * length * 0.5f + Vector3.up * (beam * 0.5f), new Vector3(length, beam, thickness), turn, Swatch.DarkWood);
            Block(from + along * length * 0.5f + Vector3.up * (height - beam * 0.5f), new Vector3(length, beam, thickness), turn, Swatch.DarkWood);

            for (int i = 0; i < bays; i++)
            {
                float x0 = length * i / bays + beam, x1 = length * (i + 1) / bays - beam;
                var lo = from + along * x0 + Vector3.up * (beam + 0.05f);
                var hi = from + along * x1 + Vector3.up * (height - beam - 0.05f);
                if (i % 2 == 1) (lo, hi) = (from + along * x1 + Vector3.up * (beam + 0.05f), from + along * x0 + Vector3.up * (height - beam - 0.05f));
                var mid = (lo + hi) * 0.5f;
                var dir = (hi - lo).normalized;
                Block(mid, new Vector3(beam * 0.8f, Vector3.Distance(lo, hi), thickness * 0.95f), Quaternion.LookRotation(across, dir), Swatch.DarkWood);
            }

            Climb(from, to, height, thickness);
            solids.Add((from + along * length * 0.5f + Vector3.up * height * 0.5f, new Vector3(length, height, thickness), turn));
        }

        /// <summary>Upright planks with a rail across the top and bottom.</summary>
        public void PlankWall(Vector3 from, Vector3 to, float height, float thickness = 0.08f)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);
            var turn = Quaternion.LookRotation(across, Vector3.up);

            float plank = 0.24f;
            for (float x = plank * 0.5f; x < length; x += plank + 0.02f)
            {
                if (Gone(0.3f)) continue;
                float h = height * Rand(0.96f, 1.0f) * (Gone(0.3f) ? Rand(0.4f, 0.8f) : 1f);
                var lean = Quaternion.AngleAxis(Lean(9f), across) * turn;
                Block(from + along * x + Vector3.up * h * 0.5f, new Vector3(plank, h, thickness), lean, rng.Next(3) == 0 ? Swatch.Wood : Swatch.Plank);
            }
            foreach (float y in new[] { height * 0.15f, height * 0.85f })
                Block(from + along * length * 0.5f + Vector3.up * y - across * thickness, new Vector3(length, 0.14f, thickness), turn, Swatch.DarkWood);

            solids.Add((from + along * length * 0.5f + Vector3.up * height * 0.5f, new Vector3(length, height, thickness * 2f), turn));
        }

        // --------------------------------------------------------------- roofs

        /// <summary>
        /// A pitched roof over a rectangle, the ridge running along z. Two
        /// slopes, a ridge beam, and in thatch a thicker, rougher pair. Each
        /// slope reaches past the eave line by the overhang.
        /// </summary>
        public void Roof(Vector3 eaveCentre, float width, float length, float pitchDegrees, RoofStyle style, float overhang = 0.35f)
        {
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float halfW = width * 0.5f + overhang;
            float slope = halfW / Mathf.Cos(pitch);
            float rise = halfW * Mathf.Tan(pitch);
            float thick = style == RoofStyle.Thatch ? 0.34f : 0.12f;
            var swatch = style == RoofStyle.Thatch ? Swatch.Thatch : style == RoofStyle.Slate ? Swatch.Slate : Swatch.Wood;
            float over = length + overhang * 2f;

            foreach (int side in new[] { -1, 1 })
            {
                var centre = eaveCentre + new Vector3(side * halfW * 0.5f, rise * 0.5f + thick * 0.4f, 0f);
                var turn = Quaternion.Euler(0f, 0f, -side * pitchDegrees);

                // The covering, in strips from ridge to eave. A roof does not go
                // a strip at a time so much as a section at a time: the timbers
                // under one length of it give and the whole length comes down,
                // so at any decay worth the name each slope loses one run of
                // strips together, and only a strip here and there besides.
                float strip = style == RoofStyle.Thatch ? 0.7f : 0.45f;
                float sectionFrom = 0f, sectionTo = 0f;
                if (Decay > 0.35f && rng.NextDouble() < Decay)
                {
                    float sectionLen = over * Rand(0.25f, 0.45f) * Mathf.Clamp01(Decay + 0.2f);
                    sectionFrom = Rand(-over * 0.5f, over * 0.5f - sectionLen);
                    sectionTo = sectionFrom + sectionLen;
                }
                for (float z = -over * 0.5f + strip * 0.5f; z < over * 0.5f; z += strip)
                {
                    bool inSection = z > sectionFrom && z < sectionTo;
                    bool gone = inSection || Gone(0.12f);
                    // in the fallen section nearly all of each strip is gone,
                    // a stub left at the ridge; elsewhere a lost strip may keep more
                    float keep = !gone ? 1f : inSection ? (Gone(0.7f) ? 0f : Rand(0.1f, 0.3f)) : (Gone(0.5f) ? 0f : Rand(0.3f, 0.7f));
                    if (keep <= 0f) continue;
                    // what is kept is the ridge end; the eave end is what fell
                    var at = centre + turn * new Vector3(-slope * (1f - keep) * 0.5f, 0f, 0f) + new Vector3(0f, 0f, z);
                    Block(at, new Vector3(slope * keep, thick, Mathf.Min(strip, over * 0.5f - z + strip * 0.5f)), turn, swatch, style == RoofStyle.Thatch ? 0.03f : 0f);
                    if (Weathering == Weather.Snow && keep > 0.5f && !Gone(0.3f))
                        Block(at + turn * new Vector3(0f, thick * 0.5f + 0.09f, 0f), new Vector3(slope * keep * 0.96f, 0.18f, Mathf.Min(strip, over * 0.5f - z + strip * 0.5f) * 1.02f), turn, Swatch.Snow, 0.02f);
                }

                if (style == RoofStyle.Plank)
                {
                    // battens across the slope, so it is planks and not a sheet
                    for (float z = -over * 0.5f + 0.4f; z < over * 0.5f; z += 0.9f)
                        if (!Gone(0.25f)) Block(centre + turn * new Vector3(0f, thick * 0.6f, 0f) + new Vector3(0f, 0f, z), new Vector3(slope * 0.98f, 0.05f, 0.12f), turn, Swatch.DarkWood);
                }

                // the rafters under it, which show where the covering has gone
                if (Decay > 0.15f)
                    for (float z = -over * 0.5f + 0.5f; z < over * 0.5f; z += 1.1f)
                        Block(centre + turn * new Vector3(0f, -thick * 0.7f, 0f) + new Vector3(0f, 0f, z), new Vector3(slope * 0.96f, 0.1f, 0.1f), turn, Swatch.DarkWood);
                if (style == RoofStyle.Thatch)
                {
                    // thatch is laid in courses from the eave up, each lapping
                    // the one below; without them it is a slab the colour of thatch
                    for (float t = -slope * 0.5f + 0.5f; t < slope * 0.5f - 0.2f; t += 0.55f)
                        Block(centre + turn * new Vector3(t, thick * 0.5f, 0f), new Vector3(0.3f, 0.06f, over * 0.985f), turn, Swatch.Thatch, 0.04f);

                    // the eave, rounded off with a fatter roll of it
                    Block(centre + turn * new Vector3(slope * 0.5f - 0.1f, -thick * 0.15f, 0f), new Vector3(0.36f, thick * 1.2f, over * 0.99f), turn, Swatch.Thatch, 0.05f);
                }
                if (style == RoofStyle.Slate)
                {
                    for (float s = -slope * 0.5f + 0.35f; s < slope * 0.5f; s += 0.5f)
                        Block(centre + turn * new Vector3(s, thick * 0.55f, 0f), new Vector3(0.06f, 0.04f, over), turn, Swatch.Iron);
                }
            }

            // the ridge
            var ridge = eaveCentre + Vector3.up * (rise + thick * 0.6f);
            if (style == RoofStyle.Thatch)
                Block(ridge, new Vector3(0.7f, 0.3f, over * 0.98f), Swatch.Thatch, 0.03f);
            else
                Log(ridge + Vector3.forward * over * 0.5f, ridge + Vector3.back * over * 0.5f, 0.12f, Swatch.DarkWood);
        }

        /// <summary>The triangle of wall under a gable end, in plaster or stone.</summary>
        public void GableEnd(Vector3 eaveCentre, float width, float pitchDegrees, float z, Swatch swatch, float thickness = 0.3f)
        {
            float rise = (width * 0.5f) * Mathf.Tan(pitchDegrees * Mathf.Deg2Rad);
            Gable(eaveCentre + Vector3.forward * z, width, rise, thickness, Quaternion.identity, swatch);
        }

        // ------------------------------------------------------------ openings

        /// <summary>A door: planks, two bands, a frame; set into a wall face.</summary>
        public void Door(Vector3 foot, Vector3 facing, float width = 1.0f, float height = 1.9f)
        {
            var frameTurn = Quaternion.LookRotation(facing, Vector3.up);
            var turn = frameTurn;

            // Left long enough a door hangs open on one hinge, or comes off
            // and leans where it fell.
            var hinge = foot + frameTurn * new Vector3(-width * 0.5f, 0f, 0f);
            if (Gone(0.5f))
            {
                float swing = Rand(25f, 70f);
                turn = frameTurn * Quaternion.Euler(0f, -swing, 0f);
                if (Gone(0.4f)) turn = turn * Quaternion.Euler(0f, 0f, -Rand(8f, 18f));
                foot = hinge + turn * new Vector3(width * 0.5f, 0f, 0f);
            }
            Vector3 P(float x, float y, float z) => foot + turn * new Vector3(x, y, z);
            Vector3 F(float x, float y, float z) => hinge + frameTurn * new Vector3(x + width * 0.5f, y, z);

            float plank = width / 4f;
            for (int i = 0; i < 4; i++)
                Block(P((i - 1.5f) * plank, height * 0.5f, 0.03f), new Vector3(plank - 0.02f, height, 0.06f), turn, i % 2 == 0 ? Swatch.Wood : Swatch.Plank);

            foreach (float y in new[] { height * 0.25f, height * 0.75f })
                Block(P(0f, y, 0.08f), new Vector3(width, 0.12f, 0.05f), turn, Swatch.DarkWood);

            Block(F(-width * 0.5f - 0.06f, height * 0.5f, 0.05f), new Vector3(0.12f, height + 0.12f, 0.16f), frameTurn, Swatch.DarkWood);
            Block(F(width * 0.5f + 0.06f, height * 0.5f, 0.05f), new Vector3(0.12f, height + 0.12f, 0.16f), frameTurn, Swatch.DarkWood);
            Block(F(0f, height + 0.06f, 0.05f), new Vector3(width + 0.24f, 0.12f, 0.16f), frameTurn, Swatch.DarkWood);
            Block(P(width * 0.32f, height * 0.5f, 0.1f), new Vector3(0.05f, 0.05f, 0.08f), turn, Swatch.Iron);
        }

        /// <summary>A window: a dark pane, a frame, a sill, a mullion.</summary>
        public void Window(Vector3 centre, Vector3 facing, float width = 0.8f, float height = 0.9f)
        {
            var turn = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x, y, z);

            bool glassGone = Gone(0.6f);
            if (!glassGone) Block(P(0f, 0f, 0.02f), new Vector3(width, height, 0.04f), turn, Swatch.Pane);
            else Block(P(0f, 0f, -0.05f), new Vector3(width, height, 0.03f), turn, Swatch.Iron);
            if (Gone(0.5f))
            {
                // one shutter, hanging from its top hinge
                var hang = turn * Quaternion.Euler(0f, 0f, Rand(6f, 22f));
                Block(centre + turn * new Vector3(width * 0.5f + 0.25f, -height * 0.15f, 0.12f), new Vector3(width * 0.5f, height, 0.05f), hang, Swatch.OldWood);
            }
            Block(P(-width * 0.5f, 0f, 0.06f), new Vector3(0.1f, height + 0.1f, 0.12f), turn, Swatch.DarkWood);
            Block(P(width * 0.5f, 0f, 0.06f), new Vector3(0.1f, height + 0.1f, 0.12f), turn, Swatch.DarkWood);
            Block(P(0f, height * 0.5f, 0.06f), new Vector3(width, 0.1f, 0.12f), turn, Swatch.DarkWood);
            Block(P(0f, -height * 0.5f, 0.1f), new Vector3(width + 0.2f, 0.1f, 0.22f), turn, Swatch.Wood);
            Block(P(0f, 0f, 0.05f), new Vector3(0.06f, height, 0.08f), turn, Swatch.DarkWood);
            Block(P(0f, 0f, 0.05f), new Vector3(width, 0.06f, 0.08f), turn, Swatch.DarkWood);
        }

        // ---------------------------------------------------------- the rest

        public void Chimney(Vector3 foot, float height, float width = 0.7f)
        {
            float course = 0.3f;
            float standing = height * (1f - 0.45f * Decay * (float)rng.NextDouble());
            for (float y = 0f; y < standing; y += course)
            {
                float w = width * (1f - 0.12f * (y / height));
                foreach (int side in new[] { 0, 1 })
                {
                    float off = (side == 0 ? -1f : 1f) * w * 0.25f;
                    float shift = ((int)(y / course) % 2 == 0) ? 0f : w * 0.12f;
                    Block(foot + new Vector3(off + shift, y + course * 0.5f, 0f), new Vector3(w * 0.5f - 0.02f, course - 0.02f, w), rng.Next(3) == 0 ? Swatch.DarkStone : Swatch.Stone, 0.01f);
                }
            }
            if (standing > height * 0.9f) Block(foot + Vector3.up * (height + 0.06f), new Vector3(width * 1.1f, 0.12f, width * 1.1f), Swatch.DarkStone);
            else Rubble(foot + new Vector3(Rand(-1f, 1f), 0f, Rand(0.6f, 1.4f)), 0.7f, 4);
            solids.Add((foot + Vector3.up * height * 0.5f, new Vector3(width, height, width), Quaternion.identity));
        }

        public void Post(Vector3 foot, float height, float radius = 0.1f, Swatch swatch = Swatch.DarkWood)
        {
            Log(foot, foot + Vector3.up * height, radius, swatch, 6);
        }

        /// <summary>Posts and two rails between two points.</summary>
        public void Railing(Vector3 from, Vector3 to, float height = 1.0f)
        {
            var along = (to - from).normalized;
            float length = Vector3.Distance(from, to);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / 1.2f));

            var across = Vector3.Cross(along, Vector3.up);
            for (int i = 0; i <= posts; i++)
            {
                var foot = from + along * (length * i / posts);
                if (Gone(0.2f)) { Log(foot, foot + Vector3.up * height * Rand(0.2f, 0.5f), 0.06f, Swatch.DarkWood, 6); continue; }
                Log(foot, foot + Vector3.up * height + across * Lean(0.25f) + along * Lean(0.1f), 0.06f, Swatch.DarkWood, 6);
            }
            foreach (float y in new[] { height * 0.5f, height - 0.05f })
                for (int i = 0; i < posts; i++)
                {
                    if (Gone(0.35f))
                    {
                        // one end down, if it is there at all
                        if (Gone(0.5f)) continue;
                        Log(from + along * (length * i / posts) + Vector3.up * y, from + along * (length * (i + 1) / posts) + Vector3.up * (y - height * 0.45f) + across * 0.15f, 0.035f, Swatch.Wood, 5);
                        continue;
                    }
                    Log(from + along * (length * i / posts) + Vector3.up * y, from + along * (length * (i + 1) / posts) + Vector3.up * y, 0.035f, Swatch.Wood, 5);
                }
        }

        /// <summary>Stone flags over an area, with gaps, two greys.</summary>
        public void Pavers(Vector3 centre, float width, float depth, float flag = 0.9f)
        {
            for (float x = -width * 0.5f + flag * 0.5f; x < width * 0.5f; x += flag + 0.05f)
            for (float z = -depth * 0.5f + flag * 0.5f; z < depth * 0.5f; z += flag + 0.05f)
            {
                var at = centre + new Vector3(x, 0f, z);
                if (Gone(0.12f))
                {
                    // a flag gone, and grass come up where it was
                    if (Weathering == Weather.Vines) Tuft(at, flag * 0.5f);   // only where things grow
                    continue;
                }
                var tilt = Quaternion.Euler(Lean(7f), Rand(-3f, 3f), Lean(7f));
                Block(at + new Vector3(Rand(-0.02f, 0.02f), 0.04f + Rand(0f, 0.02f), Rand(-0.02f, 0.02f)),
                      new Vector3(flag * Rand(0.9f, 0.98f), 0.08f, flag * Rand(0.9f, 0.98f)),
                      tilt, rng.Next(3) == 0 ? Swatch.DarkStone : Swatch.Stone, 0.008f);
                if (Weathering == Weather.Vines && Gone(0.35f))
                    Block(at + Vector3.up * 0.085f, new Vector3(flag * Rand(0.3f, 0.6f), 0.02f, flag * Rand(0.3f, 0.6f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f), Swatch.Moss);
                if (Weathering == Weather.Snow && !Gone(0.5f))
                    Block(at + Vector3.up * 0.13f, new Vector3(flag * Rand(0.6f, 0.95f), 0.1f, flag * Rand(0.6f, 0.95f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f), Swatch.Snow, 0.01f);
                if (Weathering == Weather.Sand && Gone(0.5f))
                    Block(at + Vector3.up * 0.1f, new Vector3(flag * Rand(0.7f, 1.1f), 0.06f, flag * Rand(0.7f, 1.1f)), Quaternion.Euler(0f, Rand(0f, 90f), 0f), Swatch.Sand, 0.015f);
            }
        }

        /// <summary>Steps climbing away from a foot, in stone.</summary>
        public void Steps(Vector3 foot, Vector3 direction, int count, float rise, float run, float width, bool wooden = false)
        {
            var dir = new Vector3(direction.x, 0f, direction.z).normalized;
            var turn = Quaternion.LookRotation(dir, Vector3.up);

            for (int i = 0; i < count; i++)
            {
                var centre = foot + dir * (run * (i + 0.5f)) + Vector3.up * (rise * (i + 0.5f) + rise * i * 0f);
                // each tread is a block from its own top down to the ground, so the flight is solid
                float tall = rise * (i + 1);
                var shade = wooden ? (i % 2 == 0 ? Swatch.Wood : Swatch.Plank) : (i % 2 == 0 ? Swatch.Stone : Swatch.DarkStone);

                // a broken tread: the block is there, but its top is gone to a
                // rough lower surface, so the flight is still climbable at a
                // stumble
                if (Gone(0.3f) && i > 0)
                {
                    float broken = tall - rise * Rand(0.4f, 0.9f);
                    Block(foot + dir * (run * (i + 0.5f)) + Vector3.up * broken * 0.5f, new Vector3(width * Rand(0.7f, 1f), broken, run), turn, wooden ? Swatch.OldWood : Swatch.DarkStone, wooden ? 0.02f : 0.04f, true);
                    if (!wooden) Rubble(foot + dir * (run * (i + 0.5f)) + Vector3.up * broken + new Vector3(0f, 0f, Rand(-width, width) * 0.4f), 0.3f, 2);
                    continue;
                }
                Block(foot + dir * (run * (i + 0.5f)) + Vector3.up * tall * 0.5f, new Vector3(width, tall, run), turn, shade, wooden ? 0f : 0.01f, true);
            }
        }

        // --------------------------------------------------------------- props

        /// <summary>A barrel: a fat log on end with two iron bands.</summary>
        public void Barrel(Vector3 foot, float radius = 0.32f, float height = 0.9f)
        {
            if (Gone(0.35f))
            {
                // on its side, and split
                var turn = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                Log(foot + Vector3.up * radius * 0.9f - turn * Vector3.forward * height * 0.5f, foot + Vector3.up * radius * 0.9f + turn * Vector3.forward * height * 0.5f, radius, Swatch.OldWood, 12);
                Ring(foot + Vector3.up * radius * 0.9f + turn * Vector3.forward * height * 0.28f, radius + 0.02f, 0.06f, 0.05f, 12, Swatch.Iron);
                return;
            }
            Log(foot + Vector3.up * 0.02f, foot + Vector3.up * height, radius, Swatch.Wood, 12);
            foreach (float y in new[] { height * 0.22f, height * 0.78f }) Ring(foot + Vector3.up * y, radius + 0.02f, 0.06f, 0.05f, 12, Swatch.Iron);
            solids.Add((foot + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f), Quaternion.identity));
        }

        /// <summary>A crate: planks, corner posts, a band round the middle.</summary>
        public void Crate(Vector3 foot, float size = 0.7f)
        {
            if (Gone(0.4f))
            {
                // broken open: three sides and the lid off beside it
                var t = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                Block(foot + Vector3.up * 0.04f, new Vector3(size, 0.08f, size), t, Swatch.OldWood);
                Block(foot + t * new Vector3(0f, size * 0.3f, size * 0.5f), new Vector3(size, size * 0.6f, 0.06f), t, Swatch.OldWood);
                Block(foot + t * new Vector3(-size * 0.5f, size * 0.25f, 0f), new Vector3(0.06f, size * 0.5f, size), t, Swatch.OldWood);
                Block(foot + t * new Vector3(size * 0.9f, 0.04f, -size * 0.3f), new Vector3(size, 0.07f, size), t * Quaternion.Euler(0f, 30f, 0f), Swatch.OldWood);
                return;
            }
            var c = foot + Vector3.up * size * 0.5f;
            Block(c, new Vector3(size, size, size), Swatch.Plank);
            float e = 0.07f;
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                Block(c + new Vector3(x * (size * 0.5f), 0f, z * (size * 0.5f)), new Vector3(e, size + 0.02f, e), Swatch.DarkWood);
            Block(c + new Vector3(0f, 0f, 0f), new Vector3(size + 0.04f, 0.08f, size + 0.04f), Swatch.DarkWood);
            solids.Add((c, Vector3.one * size, Quaternion.identity));
        }

        public void Table(Vector3 foot, float width = 1.6f, float depth = 0.8f, float height = 0.8f)
        {
            Block(foot + Vector3.up * (height - 0.04f), new Vector3(width, 0.08f, depth), Swatch.Plank);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                Block(foot + new Vector3(x * (width * 0.5f - 0.1f), (height - 0.08f) * 0.5f, z * (depth * 0.5f - 0.1f)), new Vector3(0.1f, height - 0.08f, 0.1f), Swatch.DarkWood);
            solids.Add((foot + Vector3.up * height * 0.5f, new Vector3(width, height, depth), Quaternion.identity));
        }

        public void Bench(Vector3 foot, float length = 1.4f, float height = 0.45f)
        {
            Block(foot + Vector3.up * (height - 0.03f), new Vector3(length, 0.06f, 0.36f), Swatch.Plank);
            foreach (float x in new[] { -1f, 1f })
                Block(foot + new Vector3(x * (length * 0.5f - 0.15f), (height - 0.06f) * 0.5f, 0f), new Vector3(0.08f, height - 0.06f, 0.3f), Swatch.DarkWood);
        }

        /// <summary>A lantern on a post, its arm out one way.</summary>
        public void Lantern(Vector3 foot, float height = 2.4f, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            var top = foot + Vector3.up * height + new Vector3(Lean(0.35f), 0f, Lean(0.35f));
            Log(foot, top, 0.07f, Swatch.DarkWood, 6);
            var arm = top + Vector3.down * 0.1f;
            Log(arm, arm + turn * Vector3.forward * 0.5f, 0.04f, Swatch.DarkWood, 5);
            var lamp = arm + turn * Vector3.forward * 0.45f + Vector3.down * 0.32f;
            Block(lamp, new Vector3(0.22f, 0.3f, 0.22f), Swatch.Pane);
            Block(lamp + Vector3.up * 0.17f, new Vector3(0.28f, 0.05f, 0.28f), Swatch.Iron);
            Block(lamp + Vector3.down * 0.17f, new Vector3(0.26f, 0.04f, 0.26f), Swatch.Iron);
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                Block(lamp + new Vector3(x * 0.11f, 0f, z * 0.11f), new Vector3(0.03f, 0.3f, 0.03f), Swatch.Iron);
        }

        /// <summary>A trough, with water in it.</summary>
        public void Trough(Vector3 foot, float length = 1.6f, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => foot + turn * new Vector3(x, y, z);
            Block(P(0f, 0.05f, 0f), new Vector3(length, 0.1f, 0.6f), turn, Swatch.DarkWood);
            Block(P(0f, 0.3f, 0.27f), new Vector3(length, 0.5f, 0.06f), turn, Swatch.Wood);
            Block(P(0f, 0.3f, -0.27f), new Vector3(length, 0.5f, 0.06f), turn, Swatch.Wood);
            Block(P(length * 0.5f - 0.03f, 0.3f, 0f), new Vector3(0.06f, 0.5f, 0.6f), turn, Swatch.DarkWood);
            Block(P(-length * 0.5f + 0.03f, 0.3f, 0f), new Vector3(0.06f, 0.5f, 0.6f), turn, Swatch.DarkWood);
            Block(P(0f, 0.42f, 0f), new Vector3(length - 0.1f, 0.02f, 0.48f), turn, Swatch.Water);
            solids.Add((P(0f, 0.3f, 0f), new Vector3(length, 0.6f, 0.6f), turn));
        }

        /// <summary>A board hung from an arm on a post.</summary>
        public void HangingSign(Vector3 foot, float height = 2.6f, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            Post(foot, height, 0.08f);
            var arm = foot + Vector3.up * (height - 0.15f);
            Log(arm, arm + turn * Vector3.forward * 0.9f, 0.05f, Swatch.DarkWood, 5);
            var board = arm + turn * Vector3.forward * 0.55f + Vector3.down * 0.5f;
            Block(board, new Vector3(0.06f, 0.5f, 0.7f), turn, Swatch.Plank);
            Block(board + Vector3.up * 0.3f, new Vector3(0.03f, 0.2f, 0.5f), turn, Swatch.Iron);
        }

        /// <summary>A well: a stone ring, two posts, a crossbar and a small roof.</summary>
        public void Well(Vector3 foot)
        {
            Ring(foot + Vector3.up * 0.25f, 0.75f, 0.5f, 0.28f, 10, Swatch.Stone, true);
            Ring(foot + Vector3.up * 0.75f, 0.75f, 0.5f, 0.28f, 10, Swatch.WarmStone, true);
            Block(foot + Vector3.up * 0.06f, new Vector3(1.0f, 0.06f, 1.0f), Swatch.Water);
            Post(foot + Vector3.left * 0.55f, 2.0f, 0.08f);
            Post(foot + Vector3.right * 0.55f, 2.0f, 0.08f);
            Log(foot + Vector3.left * 0.55f + Vector3.up * 1.7f, foot + Vector3.right * 0.55f + Vector3.up * 1.7f, 0.05f, Swatch.Wood, 6);
            Roof(foot + Vector3.up * 2.0f, 1.2f, 1.2f, 35f, RoofStyle.Plank, 0.2f);
            Barrel(foot + Vector3.up * 1.15f + Vector3.forward * 0.1f, 0.13f, 0.28f);
            solids.Add((foot + Vector3.up * 0.5f, new Vector3(1.6f, 1.0f, 1.6f), Quaternion.identity));
        }

        /// <summary>Logs stacked in rows, cut ends out.</summary>
        public void Woodpile(Vector3 foot, float length = 1.6f, int rows = 4, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            float r = 0.13f;
            int keptRows = Mathf.Max(1, rows - Mathf.RoundToInt(Decay * rows * 0.6f));
            for (int row = 0; row < keptRows; row++)
            {
                int across = 5 - row;
                for (int i = 0; i < across; i++)
                {
                    if (row == keptRows - 1 && Gone(0.5f)) continue;
                    var c = foot + turn * new Vector3((i - (across - 1) * 0.5f) * r * 2.1f, r + row * r * 1.8f, 0f);
                    Log(c + turn * Vector3.back * length * 0.5f, c + turn * Vector3.forward * length * 0.5f, r * Rand(0.85f, 1f), Swatch.Wood, 7);
                }
            }
            for (int i = 0; i < rows - keptRows + 1 && Decay > 0f; i++)
            {
                var c = foot + turn * new Vector3(Rand(-1.2f, 1.2f), r, Rand(-0.8f, 0.8f)) + turn * Vector3.right * Rand(1.2f, 2.2f) * (rng.Next(2) == 0 ? 1f : -1f);
                var t = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                Log(c - t * Vector3.forward * length * 0.5f, c + t * Vector3.forward * length * 0.5f, r * 0.9f, Swatch.OldWood, 7);
            }
            solids.Add((foot + Vector3.up * rows * r, new Vector3(1.4f, rows * r * 2f, length), turn));
        }

        /// <summary>A hay bale, tied twice.</summary>
        public void HayBale(Vector3 foot, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            Block(foot + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 0.6f), turn, Swatch.Thatch, 0.035f);
            foreach (float x in new[] { -0.25f, 0.25f })
                Block(foot + Vector3.up * 0.3f + turn * new Vector3(x, 0f, 0f), new Vector3(0.05f, 0.64f, 0.64f), turn, Swatch.DarkWood);
        }

        /// <summary>A banner hung from an arm on a post.</summary>
        public void Banner(Vector3 foot, float height = 3.0f, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            Post(foot, height, 0.07f);
            var arm = foot + Vector3.up * (height - 0.05f);
            Log(arm, arm + turn * Vector3.forward * 0.8f, 0.04f, Swatch.DarkWood, 5);
            var cloth = arm + turn * Vector3.forward * 0.42f + Vector3.down * 0.7f;
            if (Gone(0.6f))
            {
                // a rag of it, hanging from one corner
                float rag = Rand(0.3f, 0.6f);
                Block(arm + turn * Vector3.forward * 0.15f + Vector3.down * rag * 0.5f, new Vector3(0.03f, rag, 0.25f), turn * Quaternion.Euler(Rand(-20f, 20f), 0f, 0f), Swatch.Cloth);
                return;
            }
            Block(cloth, new Vector3(0.03f, 1.3f, 0.7f), turn, Swatch.Cloth);
            Gable(cloth + Vector3.down * 0.65f, 0.7f, -0.3f, 0.03f, turn * Quaternion.Euler(0f, 90f, 0f), Swatch.Cloth);
        }

        /// <summary>A ladder leaning on whatever is at its top.</summary>
        public void Ladder(Vector3 foot, float height, float yaw = 0f, float lean = 12f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-lean, 0f, 0f);
            Vector3 P(float x, float y) => foot + turn * new Vector3(x, y, 0f);
            foreach (float x in new[] { -0.25f, 0.25f }) Log(P(x, 0f), P(x, height), 0.04f, Swatch.DarkWood, 5);
            for (float y = 0.3f; y < height - 0.1f; y += 0.35f) Log(P(-0.25f, y), P(0.25f, y), 0.03f, Swatch.Wood, 5);
        }

        /// <summary>A cart wheel standing on its rim.</summary>
        public void Cartwheel(Vector3 foot, float radius = 0.55f, float yaw = 0f)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            var hub = foot + Vector3.up * radius;
            int segs = 12;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                var p0 = hub + turn * new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
                var p1 = hub + turn * new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                var mid = (p0 + p1) * 0.5f;
                // each rim piece lies along its chord: turned about the axle
                // by the chord's own angle, in the wheel's frame
                float chord = ((a0 + a1) * 0.5f) * Mathf.Rad2Deg + 90f;
                Block(mid, new Vector3(Vector3.Distance(p0, p1) + 0.02f, 0.08f, 0.1f), turn * Quaternion.Euler(0f, 0f, chord), Swatch.DarkWood);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var tip = hub + turn * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Log(hub, tip, 0.03f, Swatch.Wood, 5);
            }
            Log(hub + turn * Vector3.back * 0.08f, hub + turn * Vector3.forward * 0.08f, 0.09f, Swatch.DarkWood, 8);
        }

        /// <summary>A mound of what the weather brings -- sand or snow -- lying in the open.</summary>
        public void Drift(Vector3 centre, float across, float tall)
        {
            var swatch = Weathering == Weather.Snow ? Swatch.Snow : Swatch.Sand;
            if (Weathering != Weather.Snow && Weathering != Weather.Sand) return;
            var turn = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
            Gable(centre + turn * new Vector3(0f, 0f, 0f), across, tall, across * Rand(0.9f, 1.6f), turn, swatch);
            Gable(centre + turn * new Vector3(across * 0.2f, 0f, across * 0.3f), across * 0.7f, tall * 0.6f, across * Rand(0.8f, 1.2f), turn * Quaternion.Euler(0f, 90f, 0f), swatch);
        }

        /// <summary>Ash and burnt ground, in blotches.</summary>
        public void Ash(Vector3 centre, float radius, int count = 4)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0f, radius);
                float size = Rand(0.6f, 1.6f);
                Block(centre + new Vector3(Mathf.Cos(a) * d, 0.015f, Mathf.Sin(a) * d), new Vector3(size, 0.03f, size * Rand(0.6f, 1.2f)), Quaternion.Euler(0f, Rand(0f, 180f), 0f), i % 3 == 0 ? Swatch.Char : Swatch.DarkStone, 0.02f);
            }
        }

        /// <summary>Broken stone lying where it fell.</summary>
        public void Rubble(Vector3 centre, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0f, radius);
                float size = Rand(0.18f, 0.42f);
                Block(centre + new Vector3(Mathf.Cos(a) * d, size * 0.4f, Mathf.Sin(a) * d), new Vector3(size * Rand(0.8f, 1.6f), size * 0.8f, size), Quaternion.Euler(Rand(-15f, 15f), Rand(0f, 360f), Rand(-15f, 15f)), StoneShade(), 0.02f);
            }
        }

        /// <summary>Wood lying where it fell: planks and lengths of log.</summary>
        public void Debris(Vector3 centre, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0f, radius);
                var at = centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                var t = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                if (rng.Next(2) == 0)
                    Block(at + Vector3.up * 0.04f, new Vector3(Rand(0.6f, 1.6f), 0.07f, 0.24f), t * Quaternion.Euler(Lean(6f), 0f, 0f), Swatch.OldWood);
                else
                    Log(at - t * Vector3.forward * Rand(0.4f, 0.9f) + Vector3.up * 0.1f, at + t * Vector3.forward * Rand(0.4f, 0.9f) + Vector3.up * 0.1f, 0.1f, Swatch.OldWood, 6);
            }
        }

        /// <summary>Grass come up through a floor.</summary>
        public void Tuft(Vector3 at, float spread)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f), d = Rand(0f, spread);
                var foot = at + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float tall = Rand(0.2f, 0.45f);
                Block(foot + Vector3.up * tall * 0.5f, new Vector3(0.05f, tall, 0.14f), Quaternion.Euler(Lean(20f) + Rand(-12f, 12f), Rand(0f, 180f), Rand(-12f, 12f)), Swatch.Vine);
            }
        }

        /// <summary>
        /// What grows up a wall left alone: in the woods, vines with leaves,
        /// climbing from the foot; in snow, a drift against the foot and a cap
        /// along the top; in the desert, sand drifted against it.
        /// </summary>
        private void Climb(Vector3 from, Vector3 to, float height, float thickness)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);

            switch (Weathering)
            {
                case Weather.Vines:
                    for (float x = Rand(0.2f, 0.8f); x < length; x += Rand(0.9f, 1.8f))
                    {
                        if (Gone(0.3f) == false && Decay < 0.2f) continue;
                        float side = rng.Next(2) == 0 ? 1f : -1f;
                        var foot = from + along * x + across * side * (thickness * 0.5f + 0.04f);
                        float reach = height * Rand(0.4f, 1.05f) * Mathf.Clamp01(0.4f + Decay);
                        float wander = 0f;
                        for (float y = 0f; y < reach; y += 0.35f)
                        {
                            wander += Rand(-0.12f, 0.12f);
                            var seg = foot + along * wander + Vector3.up * (y + 0.17f);
                            Block(seg, new Vector3(0.06f, 0.4f, 0.05f), Quaternion.Euler(0f, 0f, Rand(-14f, 14f)) , Swatch.Vine);
                            if (rng.Next(3) > 0) Block(seg + along * Rand(-0.15f, 0.15f) + across * side * 0.06f, new Vector3(0.22f, 0.16f, 0.06f), Quaternion.Euler(Rand(-25f, 25f), Rand(-25f, 25f), Rand(0f, 360f)), rng.Next(4) == 0 ? Swatch.Moss : Swatch.Vine);
                        }
                    }
                    break;

                // A drift is a wedge with its ridge at the wall face, sloping
                // away down to the ground: the gable's width runs across the
                // wall, its length along it. LookRotation at 'along' already
                // puts the gable's length along the wall; a quarter turn added
                // to that laid the wedges flat on the ground, the same mistake
                // the tower's blocks had.
                case Weather.Snow:
                    Block(from + along * length * 0.5f + Vector3.up * (height + 0.08f), new Vector3(length + 0.2f, 0.16f, thickness + 0.2f), Quaternion.LookRotation(across, Vector3.up), Swatch.Snow, 0.02f);
                    // drifts hug the foot of the wall: low, not wide, in a few
                    // lengths rather than one slab the length of the wall
                    foreach (float side in new[] { -1f, 1f })
                        for (float x = Rand(0f, 1.5f); x < length; x += Rand(1.5f, 3.5f))
                        {
                            float run = Mathf.Min(Rand(0.8f, 2.2f), length - x);
                            if (run < 0.4f) continue;
                            Gable(from + along * (x + run * 0.5f) + across * side * (thickness * 0.5f), 1.1f, Rand(0.3f, 0.5f) * (0.5f + Decay), run, Quaternion.LookRotation(along, Vector3.up), Swatch.Snow);
                        }
                    break;

                case Weather.Sand:
                    foreach (float side in new[] { -1f, 1f })
                        for (float x = Rand(0f, 2f); x < length; x += Rand(2f, 4f))
                        {
                            if (Gone(0.5f) == false && Decay < 0.3f) continue;
                            float run = Mathf.Min(Rand(1.0f, 2.4f), length - x);
                            if (run < 0.5f) continue;
                            Gable(from + along * (x + run * 0.5f) + across * side * (thickness * 0.5f), 1.3f, Rand(0.35f, 0.6f) * (0.4f + Decay), run, Quaternion.LookRotation(along, Vector3.up), Swatch.Sand);
                        }
                    break;
            }
        }

        /// <summary>Moss on stone, low down, where it is damp.</summary>
        private void Moss(Vector3 from, Vector3 to, float height, float thickness)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);
            for (float x = Rand(0.1f, 0.5f); x < length; x += Rand(0.5f, 1.2f))
            {
                if (!Gone(0.7f)) continue;
                float side = rng.Next(2) == 0 ? 1f : -1f;
                float h = Rand(0.1f, height * 0.45f);
                Block(from + along * x + across * side * (thickness * 0.5f + 0.012f) + Vector3.up * h * 0.5f, new Vector3(Rand(0.3f, 0.7f), h, 0.02f), Quaternion.LookRotation(across, Vector3.up), Swatch.Moss);
            }
        }

        /// <summary>A ring of blocks round a centre, for bands, wells and towers.</summary>
        public void Ring(Vector3 centre, float radius, float height, float thickness, int count, Swatch swatch, bool jitter = false, float turnBy = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                if (swatch == Swatch.Pane && Gone(0.6f)) continue;   // glass goes first
                float a = (i + turnBy) / count * Mathf.PI * 2f;
                var at = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                var facing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float len = 2f * Mathf.PI * radius / count + thickness * 0.35f;
                Block(at, new Vector3(len, height, thickness), Quaternion.LookRotation(facing, Vector3.up), swatch, jitter ? 0.012f : 0f);
            }
        }

        // -------------------------------------------------------------- towers

        /// <summary>
        /// A round tower of coursed stone: each course a ring of blocks, turned
        /// half a block from the one below, with a slit window or two and a
        /// top of either battlements or a cone of slate.
        /// </summary>
        public void RoundTower(Vector3 foot, float radius, float height, bool battlements, RoofStyle cap = RoofStyle.Slate, float course = 0.45f, bool topless = false)
        {
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / course));
            float rowHeight = height / rows;

            // Short chords, and each block a little longer than its place, so
            // the courses close up into a wall. Laid to the same radius with
            // gaps between, the first try was a stack of loose cubes: every
            // block's corners stood out from the curve on both sides.
            int count = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * radius / 0.5f));

            // The wall itself is the cylinder; the blocks are a skin a few
            // hundredths proud of it, so the joints read as lines. And they
            // lie along the curve: LookRotation at the facing already puts a
            // block's length on the tangent, and a quarter turn on top of
            // that stood every block on end, pointing out of the wall.
            Log(foot, foot + Vector3.up * height, radius - 0.03f, Swatch.Mortar, 32);

            // how far the top has fallen, round the tower: a wandering line
            // a topless tower has lost its whole top: the bite is deep everywhere
            float[] bite = new float[count];
            for (int i = 0; i < count; i++) bite[i] = (topless ? Rand(1.5f, 4f) : Rand(0f, 1f) * Decay * rows * 0.4f);
            for (int i = 0; i < count; i++) bite[i] = (bite[i] + bite[(i + 1) % count] + bite[(i + count - 1) % count]) / 3f;

            for (int row = 0; row < rows; row++)
            {
                float y = rowHeight * (row + 0.5f);
                for (int i = 0; i < count; i++)
                {
                    if (row >= rows - bite[i] && Gone(0.9f)) continue;
                    float a = (i + (row % 2) * 0.5f) / count * Mathf.PI * 2f;
                    float r = radius - 0.02f + Rand(-0.008f, 0.008f);
                    var at = foot + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    var facing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float len = 2f * Mathf.PI * radius / count - 0.04f;
                    var shade = rng.Next(6) == 0 ? Swatch.WarmStone : Swatch.Stone;
                    Block(at, new Vector3(len, rowHeight - 0.04f, 0.16f), Quaternion.LookRotation(facing, Vector3.up), shade, 0.003f);
                }
            }

            // slits, two, a little up the wall
            for (int k = 0; k < 2; k++)
            {
                float a = k * Mathf.PI + 0.6f;
                var facing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var at = foot + facing * (radius + 0.05f) + Vector3.up * height * (0.45f + k * 0.2f);
                Block(at, new Vector3(0.14f, 0.7f, 0.12f), Quaternion.LookRotation(facing, Vector3.up), Swatch.Pane);
                Block(at + Vector3.up * 0.42f, new Vector3(0.34f, 0.12f, 0.16f), Quaternion.LookRotation(facing, Vector3.up), Swatch.DarkStone);
                Block(at + Vector3.down * 0.42f, new Vector3(0.34f, 0.12f, 0.2f), Quaternion.LookRotation(facing, Vector3.up), Swatch.DarkStone);
            }

            var top = foot + Vector3.up * height;
            if (Decay > 0.3f) Rubble(foot + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)).normalized * (radius + Rand(0.6f, 1.6f)), 1.0f, 4 + (int)(Decay * 4f));

            // what grows up it, and the moss low down, as on a wall
            if (Weathering == Weather.Vines)
                for (int v = 0; v < 2 + (int)(Decay * 3f); v++)
                {
                    float a = Rand(0f, Mathf.PI * 2f);
                    float reach = height * Rand(0.3f, 0.9f) * Mathf.Clamp01(0.4f + Decay);
                    for (float y = 0f; y < reach; y += 0.35f)
                    {
                        a += Rand(-0.06f, 0.06f);
                        var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var seg = foot + outward * (radius + 0.06f) + Vector3.up * (y + 0.17f);
                        Block(seg, new Vector3(0.06f, 0.4f, 0.05f), Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, Rand(-14f, 14f)), Swatch.Vine);
                        if (rng.Next(3) > 0) Block(seg + outward * 0.06f, new Vector3(0.22f, 0.16f, 0.06f), Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(Rand(-25f, 25f), Rand(-25f, 25f), Rand(0f, 360f)), rng.Next(4) == 0 ? Swatch.Moss : Swatch.Vine);
                    }
                }
            if (Weathering == Weather.Vines || Decay > 0.3f)
                for (int m = 0; m < 3 + (int)(Decay * 4f); m++)
                {
                    float a = Rand(0f, Mathf.PI * 2f), h = Rand(0.15f, height * 0.25f);
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Block(foot + outward * (radius + 0.012f) + Vector3.up * h * 0.5f, new Vector3(Rand(0.3f, 0.7f), h, 0.02f), Quaternion.LookRotation(outward, Vector3.up), Swatch.Moss);
                }

            if (topless)
            {
                // nothing on it: the core shows above the broken courses
                Rubble(foot + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)).normalized * (radius + Rand(0.8f, 2.0f)), 1.2f, 6);
                solids.Add((foot + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f), Quaternion.identity));
                return;
            }

            if (battlements)
            {
                // a lip, then merlons every other place round it
                Ring(top + Vector3.up * 0.1f, radius + 0.08f, 0.2f, 0.5f, count, Swatch.DarkStone, true);
                for (int i = 0; i < count; i += 3)
                {
                    if (Gone(0.5f)) continue;
                    float a = (i + 0.5f) / count * Mathf.PI * 2f;
                    var facing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Block(top + facing * (radius - 0.02f) + Vector3.up * 0.5f, new Vector3(2f * Mathf.PI * radius / count * 1.6f, 0.6f, 0.3f), Quaternion.LookRotation(facing, Vector3.up), Swatch.Stone, 0.004f);
                }
                Block(top + Vector3.up * 0.06f, new Vector3(radius * 1.4f, 0.12f, radius * 1.4f), Swatch.Plank);
            }
            else
            {
                Ring(top + Vector3.up * 0.08f, radius + 0.1f, 0.16f, 0.5f, count, Swatch.DarkStone, true);
                Cone(top + Vector3.up * 0.16f, radius + 0.35f, radius * 1.7f, cap == RoofStyle.Plank ? Swatch.Plank : Swatch.Slate, 12);
            }

            solids.Add((foot + Vector3.up * height * 0.5f, new Vector3(radius * 2f, height, radius * 2f), Quaternion.identity));
        }

        /// <summary>A cone, sat on its base: a tower's roof.</summary>
        public void Cone(Vector3 baseCentre, float radius, float height, Swatch swatch, int sides = 12)
        {
            var uv = At(swatch);
            var apex = baseCentre + Vector3.up * height;

            // left long enough a cone loses a wedge, the boards under one run
            // of it gone, and the rafters of it are what is left there
            int gapFrom = -1, gapLen = 0;
            if (Decay > 0.4f && rng.NextDouble() < Decay) { gapFrom = rng.Next(sides); gapLen = 2 + rng.Next(Mathf.Max(1, (int)(sides * Decay * 0.3f))); }

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                bool inGap = gapFrom >= 0 && ((i - gapFrom + sides) % sides) < gapLen;
                if (inGap)
                {
                    var rim0 = baseCentre + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                    Log(rim0 + Vector3.up * 0.02f, apex, 0.05f, Swatch.OldWood, 4);
                    continue;
                }
                var p0 = baseCentre + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = baseCentre + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Tri(p0, p1, apex, (p0 + p1) * 0.5f - baseCentre + Vector3.up * radius * 0.5f, uv);
                Tri(p0, p1, baseCentre, Vector3.down, At(Swatch.DarkWood));
            }
            // seams down the cone, like the slate roof's
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var rim = baseCentre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Log(rim + Vector3.up * 0.02f, apex + Vector3.up * 0.02f, 0.03f, Swatch.Iron, 4);
            }
        }

        /// <summary>Merlons along a wall top, for a square tower or a curtain wall.</summary>
        public void MerlonsAlong(Vector3 from, Vector3 to, float thickness = 0.36f)
        {
            var along = (to - from).normalized;
            var across = Vector3.Cross(along, Vector3.up).normalized;
            float length = Vector3.Distance(from, to);
            var turn = Quaternion.LookRotation(across, Vector3.up);
            Block((from + to) * 0.5f + Vector3.up * 0.08f, new Vector3(length + 0.2f, 0.16f, thickness + 0.14f), turn, Swatch.DarkStone);
            for (float x = 0.3f; x < length - 0.2f; x += 1.0f)
            {
                if (Gone(0.5f)) { if (Gone(0.5f)) Block(from + along * x + Vector3.up * 0.2f, new Vector3(0.5f, 0.25f, thickness), turn, StoneShade(), 0.02f); continue; }
                Block(from + along * x + Vector3.up * 0.46f, new Vector3(0.5f, 0.6f, thickness), turn, StoneShade(), 0.012f);
            }
        }

        // ------------------------------------------------------------ finish

        /// <summary>Everything gathered, as one object under the parent.</summary>
        public GameObject Finish(string name, Transform parent, Vector3 localPosition, Material paint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var mesh = new Mesh { name = name };
            if (points.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = paint;

            foreach (var (centre, size, turn) in solids)
            {
                var box = new GameObject("Solid");
                box.transform.SetParent(go.transform, false);
                box.transform.localPosition = centre;
                box.transform.localRotation = turn;
                box.AddComponent<BoxCollider>().size = size;
            }

            return go;
        }
    }
}
