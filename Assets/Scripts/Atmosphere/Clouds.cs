using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sky's clouds, which it never had: overcast dimmed the light and
/// tinted the sky and nothing was up there. Three layers, all by the
/// weather. Cumulus: puffy flat-shaded clusters of lumps a few hundred
/// metres up, drifting on the wind, more of them the cloudier it is, lit by
/// the sun and the moon. A sheet: a ceiling over everything when it is
/// heavily overcast, its cover a noise in the shader. Cirrus: a thin high
/// veil in clear weather that takes the sunset. Each cloud is one mesh
/// drawn on its own; there are never more than a hundred.
/// </summary>
public class Clouds : MonoBehaviour
{
    private struct Cloud
    {
        public Vector3 At;
        public int Shape;
        public float Scale, Yaw;
    }

    private const float Altitude = 210f;
    private const float Reach = 1250f;
    private const int Shapes = 24;

    /// <summary>What is up there, for the probes.</summary>
    public static int Cumulus { get; private set; }
    public static float SheetCover { get; private set; }
    public static float CirrusAlpha { get; private set; }

    private readonly List<Cloud> clouds = new List<Cloud>(128);
    private Mesh[] shapes;
    private Mesh sheet;
    private Material lumpPaint, sheetPaint, cirrusPaint;
    private Vector2 drift, cirrusDrift;
    private float owed;
    private System.Random rng = new System.Random(7);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Clouds>() == null) new GameObject("Clouds (runtime)").AddComponent<Clouds>();
    }

    private void Awake()
    {
        var cloud = Resources.Load<Material>("Cloud");
        if (cloud == null) { enabled = false; return; }

        lumpPaint = new Material(cloud);
        lumpPaint.SetFloat("_Mode", 0f);

        sheetPaint = new Material(cloud);
        sheetPaint.SetFloat("_Mode", 1f);
        sheetPaint.SetColor("_Color", new Color(0.86f, 0.88f, 0.92f));
        sheetPaint.SetColor("_Shadow", new Color(0.50f, 0.54f, 0.62f));
        sheetPaint.SetFloat("_Scale", 0.0034f);          // patches a few hundred metres across, so a view holds several
        sheetPaint.SetFloat("_FadeStart", 1300f);
        sheetPaint.SetFloat("_FadeEnd", 2250f);
        sheetPaint.SetColor("_Color", new Color(0.84f, 0.86f, 0.90f));
        sheetPaint.SetColor("_Shadow", new Color(0.34f, 0.38f, 0.47f));

        cirrusPaint = new Material(cloud);
        cirrusPaint.SetFloat("_Mode", 1f);
        cirrusPaint.SetColor("_Color", new Color(1f, 0.98f, 0.96f));
        cirrusPaint.SetColor("_Shadow", new Color(0.9f, 0.9f, 0.95f));
        cirrusPaint.SetFloat("_Scale", 0.0027f);
        cirrusPaint.SetFloat("_Stretch", 0.22f);          // long streaks along the wind
        cirrusPaint.SetFloat("_FadeStart", 1300f);
        cirrusPaint.SetFloat("_FadeEnd", 2250f);

        shapes = new Mesh[Shapes];
        for (int i = 0; i < Shapes; i++) shapes[i] = CloudShape(i * 131 + 7);
        sheet = Disc(2350f, 48);     // its rim stays inside the far plane, so it fades rather than cuts
    }

    private void LateUpdate()
    {
        var view = Camera.main;
        if (view == null || TimeOfDay.Instance == null) return;

        float overcast = TimeOfDay.Instance.Overcast;
        float dt = Time.deltaTime;
        Vector3 eye = view.transform.position;

        // the wind aloft: the rain's, or a breeze, and faster than on the ground
        Vector3 wind = Rain.Wind.sqrMagnitude > 0.01f ? Rain.Wind : new Vector3(0.9f, 0f, 0.5f);
        Vector2 aloft = new Vector2(wind.x, wind.z) * 2.5f;
        drift += aloft * dt;
        cirrusDrift += aloft * 0.4f * dt;

        // ---- cumulus: how many, by the weather
        int wanted = Mathf.RoundToInt(Mathf.Lerp(4f, 95f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.72f, overcast))));
        if (overcast > 0.9f) wanted = Mathf.RoundToInt(wanted * Mathf.InverseLerp(1f, 0.9f, overcast));   // under a full ceiling they are lost in it

        if (clouds.Count < wanted) { owed += dt * 6f; while (owed >= 1f && clouds.Count < wanted) { owed -= 1f; clouds.Add(Make(eye, true)); } }
        else if (clouds.Count > wanted) { owed += dt * 7f; while (owed >= 1f && clouds.Count > wanted) { owed -= 1f; clouds.RemoveAt(rng.Next(clouds.Count)); } }

        var rp = new RenderParams(lumpPaint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };

        for (int i = 0; i < clouds.Count; i++)
        {
            var c = clouds[i];
            c.At += new Vector3(aloft.x, 0f, aloft.y) * dt;

            // gone past the far side of the reach: back in on the near side of the wind
            Vector3 rel = c.At - eye; rel.y = 0f;
            if (rel.magnitude > Reach)
            {
                c = Make(eye, false);
                Vector3 back = -new Vector3(aloft.x, 0f, aloft.y).normalized;
                c.At = eye + back * (Reach * 0.92f) + Vector3.Cross(back, Vector3.up) * (float)(rng.NextDouble() * 2.0 - 1.0) * Reach * 0.8f;
                c.At.y = Altitude + (float)(rng.NextDouble() * 60.0);
            }

            clouds[i] = c;
            rp.worldBounds = new Bounds(c.At, Vector3.one * (c.Scale * 4f));
            Graphics.RenderMesh(rp, shapes[c.Shape], 0, Matrix4x4.TRS(c.At, Quaternion.Euler(0f, c.Yaw, 0f), Vector3.one * c.Scale));
        }

        Cumulus = clouds.Count;

        // ---- the sheet: a ceiling when it is heavily overcast
        SheetCover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.92f, overcast));
        if (SheetCover > 0.01f)
        {
            sheetPaint.SetFloat("_Coverage", Mathf.Lerp(0.2f, 0.98f, SheetCover));
            sheetPaint.SetFloat("_Alpha", Mathf.Clamp01(SheetCover * 1.4f));
            sheetPaint.SetVector("_Drift", new Vector4(drift.x, drift.y, 0f, 0f));
            var sp = new RenderParams(sheetPaint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(eye, Vector3.one * 7000f) };
            Graphics.RenderMesh(sp, sheet, 0, Matrix4x4.TRS(new Vector3(eye.x, Altitude + 130f, eye.z), Quaternion.identity, Vector3.one));
        }

        // ---- cirrus: a thin high veil in clear weather
        CirrusAlpha = 0.24f * Mathf.Clamp01(1f - overcast * 1.6f);
        if (CirrusAlpha > 0.01f)
        {
            cirrusPaint.SetFloat("_Coverage", 0.42f);
            cirrusPaint.SetFloat("_Alpha", CirrusAlpha);
            cirrusPaint.SetVector("_Drift", new Vector4(cirrusDrift.x * 1.0f, cirrusDrift.y * 0.25f, 0f, 0f));
            var cp = new RenderParams(cirrusPaint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(eye, Vector3.one * 7000f) };
            Graphics.RenderMesh(cp, sheet, 0, Matrix4x4.TRS(new Vector3(eye.x, 620f, eye.z), Quaternion.identity, Vector3.one));
        }
    }

    private Cloud Make(Vector3 eye, bool anywhere)
    {
        float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float r = anywhere ? (float)Mathf.Sqrt((float)rng.NextDouble()) * Reach * 0.95f : Reach * 0.9f;
        return new Cloud
        {
            At = new Vector3(eye.x + Mathf.Cos(a) * r, Altitude + (float)(rng.NextDouble() * 60.0), eye.z + Mathf.Sin(a) * r),
            Shape = rng.Next(Shapes),
            Scale = 28f + (float)rng.NextDouble() * 34f,
            Yaw = (float)(rng.NextDouble() * 360.0)
        };
    }

    /// <summary>
    /// A cumulus: a handful of lumps, the big ones in the middle, all set on
    /// a common flat bottom. A unit across, scaled when drawn.
    /// </summary>
    private static Mesh CloudShape(int seed)
    {
        var rng = new System.Random(seed);
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        int lumps = 5 + rng.Next(6);

        for (int l = 0; l < lumps; l++)
        {
            float along = (float)(rng.NextDouble() * 2.0 - 1.0);
            float across = (float)(rng.NextDouble() * 0.9 - 0.45);
            float size = Mathf.Lerp(0.55f, 0.3f, Mathf.Abs(along)) * (0.8f + (float)rng.NextDouble() * 0.5f);
            var centre = new Vector3(along * 0.9f, size * 0.45f, across);
            Lump(verts, norms, tris, centre, new Vector3(size, size * 0.75f, size * 0.9f), rng);
        }

        var m = new Mesh { name = "cloud " + seed };
        m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    /// <summary>A flat-shaded lump: an icosahedron subdivided once, its points jittered, its bottom flattened.</summary>
    private static void Lump(List<Vector3> verts, List<Vector3> norms, List<int> tris, Vector3 centre, Vector3 size, System.Random rng)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] ico =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        int[][] faces =
        {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };

        Vector3 Point(Vector3 p)
        {
            p = p.normalized;
            float jitter = 0.86f + (float)rng.NextDouble() * 0.28f;
            p *= jitter;
            if (p.y < -0.25f) p.y = -0.25f;      // the flat bottom
            return centre + Vector3.Scale(p, size);
        }

        foreach (var f in faces)
        {
            // one subdivision: four faces from each
            Vector3 a = ico[f[0]], b = ico[f[1]], c = ico[f[2]];
            Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
            Vector3[][] sub = { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, c }, new[] { ab, bc, ca } };
            foreach (var s in sub)
            {
                Vector3 p0 = Point(s[0]), p1 = Point(s[1]), p2 = Point(s[2]);
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                if (Vector3.Dot(n, (p0 + p1 + p2) / 3f - centre) < 0f) { var tmp = p1; p1 = p2; p2 = tmp; n = -n; }
                int v = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            }
        }
    }

    /// <summary>A flat disc, for the sheet and the cirrus, facing down as well as up.</summary>
    private static Mesh Disc(float radius, int sides)
    {
        var verts = new List<Vector3> { Vector3.zero }; var norms = new List<Vector3> { Vector3.up }; var tris = new List<int>();
        for (int ring = 1; ring <= 3; ring++)
        {
            float r = radius * ring / 3f;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); norms.Add(Vector3.up);
            }
        }
        for (int i = 0; i < sides; i++) { tris.Add(0); tris.Add(1 + (i + 1) % sides); tris.Add(1 + i); }
        for (int ring = 1; ring < 3; ring++)
        {
            int inner = 1 + (ring - 1) * sides, outer = 1 + ring * sides;
            for (int i = 0; i < sides; i++)
            {
                int a = inner + i, b = inner + (i + 1) % sides, c = outer + i, d = outer + (i + 1) % sides;
                tris.Add(a); tris.Add(d); tris.Add(c); tris.Add(a); tris.Add(b); tris.Add(d);
            }
        }
        var m = new Mesh { name = "cloud sheet" };
        m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }
}
