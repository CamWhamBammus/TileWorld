using UnityEngine;

/// <summary>
/// The lanterns at a ruin, lit at night: a warm point light and a glow at
/// each lamp the kit hung, for the ones near the camera, flickering a
/// little. Only so many burn at once across the world, since every point
/// light is paid for.
/// </summary>
public class LanternLights : MonoBehaviour
{
    public Vector3[] Lamps;

    /// <summary>How many lanterns are burning now, for the probes.</summary>
    public static int Lit { get; private set; }

    private const int MostLit = 8;
    private const float Reach = 55f;

    private Light[] lights;
    private Transform[] glows;
    private Material[] paints;
    private float next;

    private void Update()
    {
        if (Lamps == null || Lamps.Length == 0) return;
        if (lights == null) { lights = new Light[Lamps.Length]; glows = new Transform[Lamps.Length]; paints = new Material[Lamps.Length]; }

        var view = Camera.main;
        if (view == null) return;

        // lit from a little before sunset to a little after sunrise
        float on = 0f;
        if (TimeOfDay.Instance != null)
        {
            float height = Mathf.Sin((TimeOfDay.Instance.Normalized * 360f - 90f) * Mathf.Deg2Rad);
            on = Mathf.Clamp01(0.5f - height * 4f);
        }

        for (int i = 0; i < Lamps.Length; i++)
        {
            Vector3 at = transform.TransformPoint(Lamps[i]);
            float away = Vector3.Distance(at, view.transform.position);
            bool want = on > 0.05f && away < Reach;

            if (want && lights[i] == null && Lit < MostLit)
            {
                var go = new GameObject("lantern light");
                go.transform.SetParent(transform, false);
                go.transform.position = at;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.color = new Color(1f, 0.76f, 0.48f);
                light.shadows = LightShadows.None;
                lights[i] = light;

                var glow = Resources.Load<Material>("Glow");
                if (glow != null)
                {
                    paints[i] = new Material(glow);
                    glows[i] = Glows.Disc("lantern glow", paints[i]);
                    glows[i].SetParent(transform, false);
                    glows[i].position = at;
                    glows[i].localScale = Vector3.one * 0.9f;
                }

                Lit++;
            }
            else if (!want && lights[i] != null)
            {
                Destroy(lights[i].gameObject);
                lights[i] = null;
                if (glows[i] != null) { Destroy(glows[i].gameObject); glows[i] = null; }
                Lit--;
            }

            if (lights[i] == null) continue;

            // a lantern's flicker: slow noise, a little of it
            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 5f, i * 3.1f);
            lights[i].intensity = 1.9f * on * flicker;

            if (glows[i] != null)
            {
                glows[i].rotation = Quaternion.LookRotation(glows[i].position - view.transform.position, Vector3.up);
                paints[i].SetColor("_Color", new Color(1f, 0.8f, 0.5f, 0.55f * on * flicker));
                paints[i].SetFloat("_Strength", 1.6f);
            }
        }
    }

    private void OnDestroy()
    {
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) Lit--;
    }
}
