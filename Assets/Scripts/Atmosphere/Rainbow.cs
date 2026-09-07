using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rainbow after rain, when the sun comes out: a band of colour forty-two
/// degrees round the point opposite the sun, red outside and violet in,
/// with a fainter second bow outside it the other way round. It shows while
/// the ground is still wet and the sun is low enough, and the terrain
/// hides the part below the horizon by itself.
/// </summary>
public class Rainbow : MonoBehaviour
{
    private const float Distance = 720f;

    /// <summary>How much rainbow there is, for the probes.</summary>
    public static float Strength { get; private set; }

    private Mesh bow;
    private Material paint;
    private ChunkManager world;
    private float shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Rainbow>() == null) new GameObject("Rainbow (runtime)").AddComponent<Rainbow>();
    }

    private void Awake()
    {
        var glow = Resources.Load<Material>("Glow");
        if (glow == null) { enabled = false; return; }
        paint = new Material(glow);
        paint.SetFloat("_Strength", 0.8f);
        bow = Build();
    }

    /// <summary>The two bows as rings of quads about +z, colour by radius.</summary>
    private static Mesh Build()
    {
        var verts = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        Color[] bands = { new Color(1f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.2f), new Color(0.2f, 0.9f, 0.3f), new Color(0.15f, 0.55f, 1f), new Color(0.45f, 0.2f, 0.9f) };

        void Ring(float innerDeg, float outerDeg, bool reversed, float alpha)
        {
            const int segments = 96;
            int steps = bands.Length;
            for (int b = 0; b < steps; b++)
            {
                float a0 = Mathf.Lerp(outerDeg, innerDeg, b / (float)steps) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(outerDeg, innerDeg, (b + 1) / (float)steps) * Mathf.Deg2Rad;
                Color c = bands[reversed ? steps - 1 - b : b]; c.a = alpha;
                for (int i = 0; i < segments; i++)
                {
                    float p0 = i / (float)segments * Mathf.PI * 2f, p1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                    Vector3 P(float ang, float ph) => new Vector3(Mathf.Cos(ph) * Mathf.Sin(ang), Mathf.Sin(ph) * Mathf.Sin(ang), Mathf.Cos(ang)) * Distance;
                    int v = verts.Count;
                    verts.Add(P(a0, p0)); verts.Add(P(a0, p1)); verts.Add(P(a1, p1)); verts.Add(P(a1, p0));
                    for (int k = 0; k < 4; k++) cols.Add(c);
                    // uv.x across the band so the glow shader softens only the outer and inner edges of the whole bow
                    float ux0 = b == 0 ? 0.2f : 0.5f, ux1 = b == steps - 1 ? 0.8f : 0.5f;
                    uvs.Add(new Vector2(ux0, 0f)); uvs.Add(new Vector2(ux0, 0f)); uvs.Add(new Vector2(ux1, 0f)); uvs.Add(new Vector2(ux1, 0f));
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
                }
            }
        }

        Ring(40.4f, 42.4f, false, 0.28f);
        Ring(50.4f, 53.4f, true, 0.09f);

        var m = new Mesh { name = "rainbow", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    private void LateUpdate()
    {
        var view = Camera.main;
        var sun = RenderSettings.sun;
        if (view == null || sun == null || TimeOfDay.Instance == null) return;
        if (world == null) world = FindFirstObjectByType<ChunkManager>();

        float height = Mathf.Sin((TimeOfDay.Instance.Normalized * 360f - 90f) * Mathf.Deg2Rad);
        float wet = world != null ? world.Wetness : 0f;
        float sunRight = Mathf.InverseLerp(0.02f, 0.12f, height) * (1f - Mathf.InverseLerp(0.55f, 0.68f, height));   // up, and under forty-two degrees
        float want = Mathf.InverseLerp(0.15f, 0.5f, wet) * sunRight * (1f - Mathf.InverseLerp(0.3f, 0.6f, TimeOfDay.Instance.Overcast)) * (1f - Mathf.InverseLerp(0.1f, 0.4f, Rain.Intensity));
        shown = Mathf.MoveTowards(shown, want, Time.deltaTime * 0.25f);
        Strength = shown;
        if (shown < 0.01f) return;

        paint.SetColor("_Color", new Color(1f, 1f, 1f, shown));
        Vector3 eye = view.transform.position;
        var rp = new RenderParams(paint) { worldBounds = new Bounds(eye, Vector3.one * (Distance * 2.2f)), shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMesh(rp, bow, 0, Matrix4x4.TRS(eye, Quaternion.LookRotation(sun.transform.forward, Vector3.up), Vector3.one));
    }
}
