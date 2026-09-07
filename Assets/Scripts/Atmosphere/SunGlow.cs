using UnityEngine;

/// <summary>
/// The sun as a thing in the sky rather than a light: a bright core and a
/// warm halo far off along the sun's line, the halo swelling as it nears
/// the horizon, and when the sun is in the frame a soft flare of a few
/// ghost discs strung along the line through the middle of the picture.
/// All with the glow shader, so the fog never dims it.
/// </summary>
public class SunGlow : MonoBehaviour
{
    private const float Distance = 880f;

    /// <summary>How much flare is showing, for the probes.</summary>
    public static float Flare { get; private set; }

    private Camera view;
    private Transform core, halo;
    private Transform[] ghosts;
    private Material corePaint, haloPaint;
    private Material[] ghostPaints;
    private static readonly float[] GhostAt = { 0.32f, 0.58f, 0.85f, 1.35f };
    private static readonly float[] GhostSize = { 0.05f, 0.03f, 0.08f, 0.045f };
    private static readonly Color[] GhostTint = { new Color(1f, 0.8f, 0.55f), new Color(0.6f, 1f, 0.7f), new Color(0.7f, 0.75f, 1f), new Color(1f, 0.7f, 0.9f) };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<SunGlow>() == null) new GameObject("Sun glow (runtime)").AddComponent<SunGlow>();
    }

    private void Awake()
    {
        var glow = Resources.Load<Material>("Glow");
        if (glow == null) { enabled = false; return; }
        corePaint = new Material(glow); core = Glows.Disc("sun core", corePaint);
        haloPaint = new Material(glow); halo = Glows.Disc("sun halo", haloPaint);
        ghosts = new Transform[GhostAt.Length]; ghostPaints = new Material[GhostAt.Length];
        for (int i = 0; i < ghosts.Length; i++) { ghostPaints[i] = new Material(glow); ghosts[i] = Glows.Disc("flare ghost", ghostPaints[i]); }
    }

    private void LateUpdate()
    {
        if (view == null) { view = Camera.main; if (view == null) return; }
        var sun = RenderSettings.sun;
        if (sun == null || TimeOfDay.Instance == null) return;

        float height = Mathf.Sin((TimeOfDay.Instance.Normalized * 360f - 90f) * Mathf.Deg2Rad);
        float up = Mathf.Clamp01((height + 0.04f) * 12f);          // showing from just under the horizon
        float horizon = 1f - Mathf.Clamp01(Mathf.Abs(height) * 3.5f);
        float clear = 1f - TimeOfDay.Instance.Overcast;
        float show = up * Mathf.Lerp(0.35f, 1f, clear);

        Vector3 toward = -sun.transform.forward;
        Vector3 at = view.transform.position + toward * Distance;
        var face = Quaternion.LookRotation(-toward, Vector3.up);
        Color warm = Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(1f, 0.62f, 0.35f), horizon);

        core.position = at; core.rotation = face;
        core.localScale = Vector3.one * (Distance * Mathf.Lerp(0.024f, 0.04f, horizon));
        corePaint.SetColor("_Color", new Color(warm.r, warm.g, warm.b, show));
        corePaint.SetFloat("_Strength", 3f);

        halo.position = at; halo.rotation = face;
        halo.localScale = Vector3.one * (Distance * Mathf.Lerp(0.14f, 0.34f, horizon));
        haloPaint.SetColor("_Color", new Color(warm.r, warm.g, warm.b, show * Mathf.Lerp(0.16f, 0.3f, horizon)));
        haloPaint.SetFloat("_Strength", 1.2f);

        // the flare: ghosts along the line from the sun through the middle, when the sun is in the frame
        Vector3 vp = view.WorldToViewportPoint(at);
        bool inFrame = vp.z > 0f && vp.x > -0.15f && vp.x < 1.15f && vp.y > -0.15f && vp.y < 1.15f;
        float centred = inFrame ? 1f - Mathf.Clamp01((new Vector2(vp.x, vp.y) - new Vector2(0.5f, 0.5f)).magnitude / 0.75f) : 0f;
        Flare = show * clear * centred * 0.9f;

        for (int i = 0; i < ghosts.Length; i++)
        {
            bool on = Flare > 0.02f;
            if (ghosts[i].gameObject.activeSelf != on) ghosts[i].gameObject.SetActive(on);
            if (!on) continue;
            Vector2 g = new Vector2(0.5f, 0.5f) + (new Vector2(0.5f, 0.5f) - new Vector2(vp.x, vp.y)) * GhostAt[i];
            Vector3 world = view.ViewportToWorldPoint(new Vector3(g.x, g.y, 40f));
            ghosts[i].position = world;
            ghosts[i].rotation = Quaternion.LookRotation(world - view.transform.position, Vector3.up);
            ghosts[i].localScale = Vector3.one * (40f * GhostSize[i] * 1.6f);
            var tint = GhostTint[i];
            ghostPaints[i].SetColor("_Color", new Color(tint.r, tint.g, tint.b, Flare * 0.14f));
            ghostPaints[i].SetFloat("_Strength", 1f);
        }
    }
}
