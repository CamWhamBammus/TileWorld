using UnityEngine;

/// <summary>
/// Stars. The procedural sky darkens after sunset but has nothing in it, so
/// night was an empty dome. These sit on a sphere around the camera, so they
/// never get closer however far you walk.
/// </summary>
public class Starfield : MonoBehaviour
{
    [SerializeField] private int count = 320;
    [SerializeField] private float distance = 900f;
    [SerializeField] private float size = 2.4f;

    private Transform view;
    private Transform[] stars;
    private float[] twinkle;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Starfield>() == null)
        {
            new GameObject("Starfield (runtime)").AddComponent<Starfield>();
        }
    }

    private void Start()
    {
        // A built player only carries shaders something in it referenced, so
        // the unlit one is often not there and Shader.Find comes back null.
        // Making a material out of that throws, and the throw repeats every
        // frame after it: in a thirty second run of the built game this cost
        // twelve hundred exceptions apiece.
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Unlit/Color");

        if (unlit == null)
        {
            Debug.LogWarning("[Starfield] No shader to draw with, so there will be none.");
            enabled = false;
            return;
        }

        material = new Material(unlit);
        material.SetColor("_BaseColor", Color.white);
        material.color = Color.white;

        stars = new Transform[count];
        twinkle = new float[count];

        var rng = new System.Random(8675309);

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "star";
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // upper hemisphere only; stars below the horizon are just wasted work
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            float theta = u * Mathf.PI * 2f;
            float phi = Mathf.Acos(Mathf.Lerp(0.06f, 1f, v));

            var dir = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Cos(phi),
                Mathf.Sin(phi) * Mathf.Sin(theta));

            go.transform.localPosition = dir * distance;
            go.transform.localScale = Vector3.one * size * Random.Range(0.5f, 1.6f);

            stars[i] = go.transform;
            twinkle[i] = (float)rng.NextDouble() * 12f;
        }
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            view = cam.transform;
        }

        // Ride with the camera so the sky stays put as the player walks.
        transform.position = view.position;

        float night = 0f;

        if (TimeOfDay.Instance != null)
        {
            float t = TimeOfDay.Instance.Normalized;
            night = Mathf.Clamp01(Mathf.InverseLerp(0.24f, 0.14f, t) + Mathf.InverseLerp(0.74f, 0.84f, t));
            night *= 1f - TimeOfDay.Instance.Overcast * 0.9f;   // clouds hide them
        }

        bool show = night > 0.02f;

        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];

            if (star.gameObject.activeSelf != show) star.gameObject.SetActive(show);
            if (!show) continue;

            star.rotation = Quaternion.LookRotation(star.position - view.position);

            float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 1.7f + twinkle[i]);
            star.localScale = Vector3.one * size * flicker * night;
        }
    }
}
