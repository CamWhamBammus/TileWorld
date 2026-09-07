using UnityEngine;

/// <summary>
/// Shafts of sun between the trees when the sun is low: long soft beams,
/// each beside a trunk near the camera, running down the light's own line
/// to the ground, drawn with the glow shader and faded by how low the sun
/// is and how clear the sky. Nothing volumetric; a dozen quads that face
/// the camera about the light's axis.
/// </summary>
public class LightShafts : MonoBehaviour
{
    private const int Slots = 14;

    /// <summary>How many beams show now and how strong, for the probes.</summary>
    public static int Count { get; private set; }
    public static float Strength { get; private set; }

    private readonly Vector3[] bases = new Vector3[Slots];
    private readonly float[] widths = new float[Slots];
    private readonly bool[] live = new bool[Slots];
    private Mesh mesh;
    private Material paint;
    private Light sun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<LightShafts>() == null) new GameObject("Light shafts (runtime)").AddComponent<LightShafts>();
    }

    private void Awake()
    {
        var glow = Resources.Load<Material>("Glow");
        if (glow != null) { paint = new Material(glow); paint.SetColor("_Color", Color.white); paint.SetFloat("_Strength", 1f); }
        mesh = new Mesh { name = "light shafts" };
        mesh.MarkDynamic();
    }

    private void LateUpdate()
    {
        var view = Camera.main;
        if (view == null || paint == null || TimeOfDay.Instance == null) { Count = 0; return; }
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null) { Count = 0; return; }

        // low sun, clear sky: the window is a bump from just above the horizon to a hand's breadth up
        float height = Mathf.Sin((TimeOfDay.Instance.Normalized * 360f - 90f) * Mathf.Deg2Rad);
        float window = Mathf.Sin(Mathf.PI * Mathf.Clamp01(Mathf.InverseLerp(0.02f, 0.42f, height)));
        Strength = window * Mathf.Clamp01(1f - TimeOfDay.Instance.Overcast * 1.6f);

        if (Strength < 0.02f) { Count = 0; return; }

        Vector3 eye = view.transform.position;
        Vector3 along = sun.transform.forward;             // the way the light travels

        // slots: each beside a trunk within a stone's throw; rerolled when it falls behind
        int tries = 0;
        for (int i = 0; i < Slots; i++)
        {
            if (live[i] && Vector3.Distance(bases[i], eye) < 34f) continue;
            live[i] = false;
            if (tries++ > 4) continue;

            var flat = Random.insideUnitCircle.normalized * Random.Range(5f, 26f);
            Vector3 at = eye + new Vector3(flat.x, 0f, flat.y);
            if (!Undergrowth.NearestTree(at, 4.5f, out var trunk)) continue;

            Vector3 side = Vector3.Cross(along, Vector3.up).normalized;
            bases[i] = trunk + side * Random.Range(-1.6f, 1.6f) + Vector3.Cross(side, Vector3.up) * Random.Range(-1.2f, 1.2f);
            widths[i] = Random.Range(1.2f, 2.4f);
            live[i] = true;
        }

        // the quads, rebuilt each frame to face the camera about the light's line
        var verts = new Vector3[Slots * 4]; var cols = new Color[Slots * 4]; var uvs = new Vector2[Slots * 4]; var tris = new int[Slots * 6];
        int n = 0;
        Color glow = new Color(1f, 0.92f, 0.72f);

        for (int i = 0; i < Slots; i++)
        {
            if (!live[i]) continue;
            Vector3 bottom = bases[i];
            Vector3 top = bottom - along * 12f;
            Vector3 right = Vector3.Cross(along, eye - (bottom + top) * 0.5f).normalized * (widths[i] * 0.5f);
            float a = 0.62f * Strength;
            int v = n * 4;
            verts[v] = bottom - right; verts[v + 1] = bottom + right; verts[v + 2] = top + right; verts[v + 3] = top - right;
            cols[v] = new Color(glow.r, glow.g, glow.b, a * 0.9f); cols[v + 1] = cols[v];
            cols[v + 2] = new Color(glow.r, glow.g, glow.b, 0f); cols[v + 3] = cols[v + 2];
            uvs[v] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f); uvs[v + 2] = new Vector2(1f, 1f); uvs[v + 3] = new Vector2(0f, 1f);
            int tI = n * 6;
            tris[tI] = v; tris[tI + 1] = v + 2; tris[tI + 2] = v + 1; tris[tI + 3] = v; tris[tI + 4] = v + 3; tris[tI + 5] = v + 2;
            n++;
        }

        Count = n;
        if (n == 0) return;

        mesh.Clear();
        mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateBounds();
        var rp = new RenderParams(paint) { worldBounds = new Bounds(eye, Vector3.one * 120f), shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.identity);
    }
}
