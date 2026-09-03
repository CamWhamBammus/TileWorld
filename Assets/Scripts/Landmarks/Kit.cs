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
    public enum Swatch { Wood, DarkWood, Plank, EndGrain, Stone, DarkStone, Mortar, Plaster, Thatch, Slate, Iron, Pane }

    /// <summary>Where on the palette each swatch is, in the order of the enum.</summary>
    public Vector2[] Where = new Vector2[12];

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

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> faces = new List<int>();
        private readonly List<(Vector3 centre, Vector3 size, Quaternion turn)> solids = new List<(Vector3, Vector3, Quaternion)>();
        private readonly System.Random rng;

        public Builder(int seed) { rng = new System.Random(seed); }

        private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

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
            var uv = At(swatch);
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
            var uv = At(swatch);
            var grain = At(Swatch.EndGrain);

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

            for (float y = radius + lift; y < height; y += pitch)
            {
                float r = radius * Rand(0.92f, 1.05f);
                Log(a + Vector3.up * y, b + Vector3.up * y, r);
            }

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

            for (int row = 0; row < rows; row++)
            {
                float y = rowHeight * (row + 0.5f);
                float x = row % 2 == 0 ? 0f : -Rand(0.25f, 0.45f);

                while (x < length)
                {
                    float len = Mathf.Min(Rand(0.45f, 1.0f), length - Mathf.Max(x, 0f));
                    float start = Mathf.Max(x, 0f), end = Mathf.Min(x + len, length);
                    if (end - start > 0.08f)
                    {
                        var centre = from + along * ((start + end) * 0.5f) + Vector3.up * y;
                        var size = new Vector3(end - start - 0.03f, rowHeight - 0.03f, thickness * Rand(0.94f, 1.04f));
                        var swatch = rng.Next(4) == 0 ? Swatch.DarkStone : Swatch.Stone;
                        Block(centre, size, Quaternion.LookRotation(across, Vector3.up), swatch, 0.012f);
                    }
                    x += len;
                }
            }

            // the mortar behind the joints, one slab a little inside the face
            Block(from + along * length * 0.5f + Vector3.up * height * 0.5f,
                  new Vector3(length, height, thickness * 0.8f), Quaternion.LookRotation(across, Vector3.up), Swatch.Mortar);

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

            Block(from + along * length * 0.5f + Vector3.up * height * 0.5f, new Vector3(length, height, thickness * 0.8f), turn, Swatch.Plaster);

            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.1f));
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
                float h = height * Rand(0.96f, 1.0f);
                Block(from + along * x + Vector3.up * h * 0.5f, new Vector3(plank, h, thickness), turn, rng.Next(3) == 0 ? Swatch.Wood : Swatch.Plank);
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
            var swatch = style == RoofStyle.Thatch ? Swatch.Thatch : style == RoofStyle.Slate ? Swatch.Slate : Swatch.Plank;
            float over = length + overhang * 2f;

            foreach (int side in new[] { -1, 1 })
            {
                var centre = eaveCentre + new Vector3(side * halfW * 0.5f, rise * 0.5f + thick * 0.4f, 0f);
                var turn = Quaternion.Euler(0f, 0f, -side * pitchDegrees);
                Block(centre, new Vector3(slope, thick, over), turn, swatch, style == RoofStyle.Thatch ? 0.03f : 0f);

                if (style == RoofStyle.Plank)
                {
                    // battens across the slope, so it is planks and not a sheet
                    for (float z = -over * 0.5f + 0.4f; z < over * 0.5f; z += 0.9f)
                        Block(centre + turn * new Vector3(0f, thick * 0.6f, 0f) + new Vector3(0f, 0f, z), new Vector3(slope * 0.98f, 0.05f, 0.12f), turn, Swatch.DarkWood);
                }
                if (style == RoofStyle.Thatch)
                {
                    // thatch is laid in courses from the eave up, each lapping
                    // the one below; without them it is a slab the colour of thatch
                    for (float t = -slope * 0.5f + 0.5f; t < slope * 0.5f - 0.2f; t += 0.55f)
                        Block(centre + turn * new Vector3(t, thick * 0.5f, 0f), new Vector3(0.22f, 0.09f, over * 0.985f), turn, Swatch.Wood, 0.012f);
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
            var turn = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 P(float x, float y, float z) => foot + turn * new Vector3(x, y, z);

            float plank = width / 4f;
            for (int i = 0; i < 4; i++)
                Block(P((i - 1.5f) * plank, height * 0.5f, 0.03f), new Vector3(plank - 0.02f, height, 0.06f), turn, i % 2 == 0 ? Swatch.Wood : Swatch.Plank);

            foreach (float y in new[] { height * 0.25f, height * 0.75f })
                Block(P(0f, y, 0.08f), new Vector3(width, 0.12f, 0.05f), turn, Swatch.DarkWood);

            Block(P(-width * 0.5f - 0.06f, height * 0.5f, 0.05f), new Vector3(0.12f, height + 0.12f, 0.16f), turn, Swatch.DarkWood);
            Block(P(width * 0.5f + 0.06f, height * 0.5f, 0.05f), new Vector3(0.12f, height + 0.12f, 0.16f), turn, Swatch.DarkWood);
            Block(P(0f, height + 0.06f, 0.05f), new Vector3(width + 0.24f, 0.12f, 0.16f), turn, Swatch.DarkWood);
            Block(P(width * 0.32f, height * 0.5f, 0.1f), new Vector3(0.05f, 0.05f, 0.08f), turn, Swatch.Iron);
        }

        /// <summary>A window: a dark pane, a frame, a sill, a mullion.</summary>
        public void Window(Vector3 centre, Vector3 facing, float width = 0.8f, float height = 0.9f)
        {
            var turn = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x, y, z);

            Block(P(0f, 0f, 0.02f), new Vector3(width, height, 0.04f), turn, Swatch.Pane);
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
            for (float y = 0f; y < height; y += course)
            {
                float w = width * (1f - 0.12f * (y / height));
                foreach (int side in new[] { 0, 1 })
                {
                    float off = (side == 0 ? -1f : 1f) * w * 0.25f;
                    float shift = ((int)(y / course) % 2 == 0) ? 0f : w * 0.12f;
                    Block(foot + new Vector3(off + shift, y + course * 0.5f, 0f), new Vector3(w * 0.5f - 0.02f, course - 0.02f, w), rng.Next(3) == 0 ? Swatch.DarkStone : Swatch.Stone, 0.01f);
                }
            }
            Block(foot + Vector3.up * (height + 0.06f), new Vector3(width * 1.1f, 0.12f, width * 1.1f), Swatch.DarkStone);
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

            for (int i = 0; i <= posts; i++) Post(from + along * (length * i / posts), height, 0.06f);
            foreach (float y in new[] { height * 0.5f, height - 0.05f })
                Log(from + Vector3.up * y, to + Vector3.up * y, 0.035f, Swatch.Wood, 5);
        }

        /// <summary>Stone flags over an area, with gaps, two greys.</summary>
        public void Pavers(Vector3 centre, float width, float depth, float flag = 0.9f)
        {
            for (float x = -width * 0.5f + flag * 0.5f; x < width * 0.5f; x += flag + 0.05f)
            for (float z = -depth * 0.5f + flag * 0.5f; z < depth * 0.5f; z += flag + 0.05f)
            {
                Block(centre + new Vector3(x + Rand(-0.02f, 0.02f), 0.04f + Rand(0f, 0.02f), z + Rand(-0.02f, 0.02f)),
                      new Vector3(flag * Rand(0.9f, 0.98f), 0.08f, flag * Rand(0.9f, 0.98f)),
                      Quaternion.Euler(0f, Rand(-3f, 3f), 0f), rng.Next(3) == 0 ? Swatch.DarkStone : Swatch.Stone, 0.008f);
            }
        }

        /// <summary>Steps climbing away from a foot, in stone.</summary>
        public void Steps(Vector3 foot, Vector3 direction, int count, float rise, float run, float width)
        {
            var dir = new Vector3(direction.x, 0f, direction.z).normalized;
            var turn = Quaternion.LookRotation(dir, Vector3.up);

            for (int i = 0; i < count; i++)
            {
                var centre = foot + dir * (run * (i + 0.5f)) + Vector3.up * (rise * (i + 0.5f) + rise * i * 0f);
                // each tread is a block from its own top down to the ground, so the flight is solid
                float tall = rise * (i + 1);
                Block(foot + dir * (run * (i + 0.5f)) + Vector3.up * tall * 0.5f, new Vector3(width, tall, run), turn, i % 2 == 0 ? Swatch.Stone : Swatch.DarkStone, 0.01f, true);
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
