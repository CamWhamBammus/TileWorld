using UnityEngine;

/// <summary>
/// Rain, when the weather has closed in. Overcast could be seen in the light
/// and heard in the wind, but nothing was actually falling.
/// </summary>
public class Rain : MonoBehaviour
{
    [SerializeField] private int drops = 260;
    [SerializeField] private float radius = 14f;
    [SerializeField] private float height = 12f;
    [SerializeField] private float fallSpeed = 26f;

    [Tooltip("Overcast has to be at least this heavy before it rains.")]
    [SerializeField, Range(0f, 1f)] private float threshold = 0.55f;

    private Transform view;
    private Transform[] streaks;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Rain>() == null)
        {
            new GameObject("Rain (runtime)").AddComponent<Rain>();
        }
    }

    private void Start()
    {
        // A built player only carries shaders something in it referenced, so
        // the unlit one is often not there and Shader.Find comes back null.
        // Making a material out of that throws, and the throw repeats every
        // frame after it: in a thirty second run of the built game this cost
        // twelve hundred exceptions apiece.
        Shader unlit = Shaders.First("Universal Render Pipeline/Unlit",
                                   "Universal Render Pipeline/Lit",
                                   "Unlit/Color");

        if (unlit == null)
        {
            Debug.LogWarning("[Rain] No shader to draw with, so there will be none.");
            enabled = false;
            return;
        }

        material = new Material(unlit);

        var colour = new Color(0.72f, 0.80f, 0.86f, 0.5f);
        material.SetColor("_BaseColor", colour);
        material.color = colour;

        streaks = new Transform[drops];

        for (int i = 0; i < drops; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "drop";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.025f, Random.Range(0.35f, 0.7f), 0.025f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            go.transform.localPosition = RandomStart();
            streaks[i] = go.transform;
        }
    }

    private Vector3 RandomStart()
    {
        var flat = Random.insideUnitCircle * radius;
        return new Vector3(flat.x, Random.Range(0f, height), flat.y);
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            view = cam.transform;
        }

        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        bool raining = overcast >= threshold;

        // Rides with the camera, so a fixed handful of drops covers any distance.
        transform.position = view.position;

        int active = raining ? Mathf.RoundToInt(streaks.Length * Mathf.InverseLerp(threshold, 1f, overcast)) : 0;

        for (int i = 0; i < streaks.Length; i++)
        {
            var streak = streaks[i];
            bool on = i < active;

            if (streak.gameObject.activeSelf != on) streak.gameObject.SetActive(on);
            if (!on) continue;

            var local = streak.localPosition;
            local.y -= fallSpeed * Time.deltaTime;

            if (local.y < -3f) local = RandomStart();

            streak.localPosition = local;
        }
    }
}
