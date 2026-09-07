using UnityEngine;

/// <summary>
/// Mist in the morning: a thin white sheet just above the water level,
/// which is also the floor of the low hollows, so it lies on the lakes and
/// in the dips and nowhere the ground rises through it. It forms toward
/// dawn, is thickest as the sun comes up, and is gone by mid-morning;
/// heavier after rain. The cloud shader's sheet mode does the drawing.
/// </summary>
public class Mist : MonoBehaviour
{
    /// <summary>How much mist there is, 0 to 1, for the probes.</summary>
    public static float Strength { get; private set; }

    private Mesh sheet;
    private Material paint;
    private Vector2 drift;
    private ChunkManager world;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<Mist>() == null) new GameObject("Mist (runtime)").AddComponent<Mist>();
    }

    private void Awake()
    {
        var cloud = Resources.Load<Material>("Cloud");
        if (cloud == null) { enabled = false; return; }
        paint = new Material(cloud);
        paint.SetFloat("_Mode", 1f);
        paint.SetColor("_Color", new Color(0.92f, 0.94f, 0.97f));
        paint.SetColor("_Shadow", new Color(0.78f, 0.82f, 0.88f));
        paint.SetFloat("_Scale", 0.035f);
        paint.SetFloat("_Coverage", 0.6f);
        paint.SetFloat("_FadeStart", 45f);
        paint.SetFloat("_FadeEnd", 150f);
        sheet = Disc(260f, 32);
    }

    private void LateUpdate()
    {
        var view = Camera.main;
        if (view == null || TimeOfDay.Instance == null) return;
        if (world == null) world = FindFirstObjectByType<ChunkManager>();

        float t = TimeOfDay.Instance.Normalized;
        float morning = Mathf.InverseLerp(0.19f, 0.245f, t) * (1f - Mathf.InverseLerp(0.285f, 0.35f, t));
        float wet = world != null ? world.Wetness : 0f;
        Strength = Mathf.Clamp01(morning * (0.75f + wet * 0.35f)) * (1f - Rain.Intensity) * (1f - TimeOfDay.Instance.Overcast * 0.4f);

        if (Strength < 0.01f) return;

        Vector3 wind = Rain.Wind.sqrMagnitude > 0.01f ? Rain.Wind : new Vector3(0.9f, 0f, 0.5f);
        drift += new Vector2(wind.x, wind.z) * (Time.deltaTime * 0.25f);
        paint.SetFloat("_Alpha", Strength * 0.42f);
        paint.SetVector("_Drift", new Vector4(drift.x, drift.y, 0f, 0f));

        Vector3 eye = view.transform.position;
        var rp = new RenderParams(paint) { worldBounds = new Bounds(eye, Vector3.one * 600f), shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMesh(rp, sheet, 0, Matrix4x4.TRS(new Vector3(eye.x, WaterSurface.Level + 1.1f, eye.z), Quaternion.identity, Vector3.one));
    }

    private static Mesh Disc(float radius, int sides)
    {
        var verts = new System.Collections.Generic.List<Vector3> { Vector3.zero }; var norms = new System.Collections.Generic.List<Vector3> { Vector3.up }; var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < sides; i++) { float a = i / (float)sides * Mathf.PI * 2f; verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)); norms.Add(Vector3.up); }
        for (int i = 0; i < sides; i++) { tris.Add(0); tris.Add(1 + (i + 1) % sides); tris.Add(1 + i); }
        var m = new Mesh { name = "mist" }; m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0); m.RecalculateBounds(); return m;
    }
}
