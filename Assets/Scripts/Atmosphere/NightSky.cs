using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The night sky: a few thousand stars in one mesh on a sphere round the
/// camera, the brightest few big and the rest small, a Milky Way as a band
/// of faint ones with a soft glow behind it, each star twinkling on its own
/// phase, and a shooting star now and then. Drawn with the glow shader so
/// the fog does not take them -- the old stars sat past the fog and were
/// never seen.
/// </summary>
public class NightSky : MonoBehaviour
{
    private const float Distance = 900f;

    /// <summary>For the probes: how many stars, how bright the night is, and whether one is falling.</summary>
    public static int Stars { get; private set; }
    public static float Night { get; private set; }
    public static bool Shooting { get; private set; }
    public static int ShotSoFar { get; private set; }

    private Transform view;
    private Mesh stars, band;
    private Material starPaint, bandPaint, streakPaint;
    private Transform streak;
    private float nextStreak, streakSince = -1f;
    private Vector3 streakFrom, streakTo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<NightSky>() == null) new GameObject("Night sky (runtime)").AddComponent<NightSky>();
    }

    private void Awake()
    {
        var glow = Resources.Load<Material>("Glow");
        if (glow == null) { enabled = false; return; }
        starPaint = new Material(glow); starPaint.SetFloat("_TwinkleRate", 2.3f); starPaint.SetFloat("_Strength", 1.8f);
        bandPaint = new Material(glow); bandPaint.SetFloat("_Strength", 0.7f);
        streakPaint = new Material(glow); streakPaint.SetFloat("_Strength", 2.5f);
        Build();
        nextStreak = Time.time + Random.Range(10f, 30f);
    }

    /// <summary>The sky's stars, seeded, on the upper hemisphere; the Milky Way along a tilted circle.</summary>
    private void Build()
    {
        var rng = new System.Random(8675309);
        var verts = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>(); var tris = new List<int>();

        // the band's axis: a tilted great circle
        Vector3 bandAxis = new Vector3(0.55f, 0.6f, 0.58f).normalized;

        void Star(Vector3 dir, float size, Color c)
        {
            // a small quad facing inward, its softness from uv.x, its twinkle phase in uv.y
            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized; if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, dir).normalized;
            Vector3 at = dir * Distance;
            float phase = (float)rng.NextDouble();
            int v = verts.Count;
            verts.Add(at - right * size - up * size); verts.Add(at + right * size - up * size); verts.Add(at + right * size + up * size); verts.Add(at - right * size + up * size);
            for (int k = 0; k < 4; k++) cols.Add(c);
            uvs.Add(new Vector2(0f, phase)); uvs.Add(new Vector2(1f, phase)); uvs.Add(new Vector2(1f, phase)); uvs.Add(new Vector2(0f, phase));
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
        }

        Vector3 Dir(float minY)
        {
            float u = (float)rng.NextDouble(), w = (float)rng.NextDouble();
            float theta = u * Mathf.PI * 2f, phi = Mathf.Acos(Mathf.Lerp(minY, 1f, w));
            return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
        }

        // the sky's own: most faint and small, a few bright and big, a few with a colour
        for (int i = 0; i < 900; i++)
        {
            Vector3 d = Dir(0.03f);
            float bright = Mathf.Pow((float)rng.NextDouble(), 3.5f);
            float size = Mathf.Lerp(1.2f, 4.2f, bright);
            Color c = Color.Lerp(new Color(0.8f, 0.86f, 1f), Color.white, bright);
            if (rng.NextDouble() < 0.06) c = new Color(1f, 0.85f, 0.7f);
            if (rng.NextDouble() < 0.04) c = new Color(0.75f, 0.85f, 1f);
            c.a = Mathf.Lerp(0.35f, 1f, bright);
            Star(d, size, c);
        }

        // the Milky Way: many faint stars within a few degrees of the band
        for (int i = 0; i < 1400; i++)
        {
            Vector3 d = Dir(-0.2f);
            Vector3 onBand = Vector3.ProjectOnPlane(d, bandAxis).normalized;
            float spread = Mathf.Pow((float)rng.NextDouble(), 1.6f) * 0.16f;
            d = (onBand + bandAxis * (((float)rng.NextDouble() * 2f - 1f) * spread)).normalized;
            if (d.y < 0.02f) continue;
            float bright = Mathf.Pow((float)rng.NextDouble(), 4f);
            var c = new Color(0.85f, 0.9f, 1f, Mathf.Lerp(0.18f, 0.6f, bright));
            Star(d, Mathf.Lerp(0.9f, 2.2f, bright), c);
        }

        stars = new Mesh { name = "stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        stars.SetVertices(verts); stars.SetColors(cols); stars.SetUVs(0, uvs); stars.SetTriangles(tris, 0);
        stars.RecalculateBounds();
        Stars = verts.Count / 4;

        // and the glow behind the band: soft wide quads along it
        verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 u0 = Vector3.Cross(bandAxis, Vector3.up).normalized, v0 = Vector3.Cross(bandAxis, u0);
            Vector3 d = (u0 * Mathf.Cos(a) + v0 * Mathf.Sin(a)).normalized;
            if (d.y < -0.1f) continue;
            float alpha = 0.07f * Mathf.Clamp01((d.y + 0.1f) * 4f) * (0.6f + 0.4f * Mathf.PerlinNoise(a * 3f, 0.5f));
            Vector3 right = Vector3.Cross(d, bandAxis).normalized, up = bandAxis;
            Vector3 at = d * (Distance * 1.02f);
            float w = 55f, h = 105f;
            int v = verts.Count;
            verts.Add(at - right * w - up * h); verts.Add(at + right * w - up * h); verts.Add(at + right * w + up * h); verts.Add(at - right * w + up * h);
            var c = new Color(0.7f, 0.78f, 1f, alpha);
            for (int k = 0; k < 4; k++) cols.Add(c);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
        }
        band = new Mesh { name = "milky way" };
        band.SetVertices(verts); band.SetColors(cols); band.SetUVs(0, uvs); band.SetTriangles(tris, 0);
        band.RecalculateBounds();

        streak = Glows.Disc("shooting star", streakPaint);
        streak.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (view == null) { var cam = Camera.main; if (cam == null) return; view = cam.transform; }

        float night = 0f;
        if (TimeOfDay.Instance != null)
        {
            float t = TimeOfDay.Instance.Normalized;
            night = Mathf.Clamp01(Mathf.InverseLerp(0.24f, 0.15f, t) + Mathf.InverseLerp(0.75f, 0.84f, t));
            night *= 1f - TimeOfDay.Instance.Overcast * 0.92f;
        }
        Night = night;
        if (night < 0.02f) { Shooting = false; return; }

        starPaint.SetColor("_Color", new Color(1f, 1f, 1f, night));
        bandPaint.SetColor("_Color", new Color(1f, 1f, 1f, night));
        var bounds = new Bounds(view.position, Vector3.one * (Distance * 2.2f));
        var sp = new RenderParams(starPaint) { worldBounds = bounds, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        var bp = new RenderParams(bandPaint) { worldBounds = bounds, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        var at = Matrix4x4.Translate(view.position);
        Graphics.RenderMesh(bp, band, 0, at);
        Graphics.RenderMesh(sp, stars, 0, at);

        // a shooting star: a bright disc that races across and is gone
        if (streakSince < 0f && Time.time > nextStreak)
        {
            Vector3 from = new Vector3(Random.Range(-1f, 1f), Random.Range(0.35f, 0.9f), Random.Range(-1f, 1f)).normalized;
            Vector3 side = Vector3.Cross(from, Vector3.up).normalized;
            Vector3 to = (from + side * Random.Range(-0.35f, 0.35f) + Vector3.down * Random.Range(0.05f, 0.25f)).normalized;
            streakFrom = from; streakTo = to; streakSince = Time.time;
            ShotSoFar++;
        }

        if (streakSince >= 0f)
        {
            float k = (Time.time - streakSince) / 0.7f;
            if (k >= 1f) { streakSince = -1f; nextStreak = Time.time + Random.Range(18f, 55f); streak.gameObject.SetActive(false); Shooting = false; }
            else
            {
                Shooting = true;
                streak.gameObject.SetActive(true);
                Vector3 d = Vector3.Slerp(streakFrom, streakTo, k);
                streak.position = view.position + d * (Distance * 0.95f);
                streak.rotation = Quaternion.LookRotation(-d, Vector3.up);
                float fade = Mathf.Sin(k * Mathf.PI);
                streak.localScale = new Vector3(6f, 6f, 1f) * (1f + fade);
                streakPaint.SetColor("_Color", new Color(1f, 0.97f, 0.9f, fade * night));
            }
        }
    }
}
