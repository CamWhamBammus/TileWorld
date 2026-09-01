using UnityEngine;

/// <summary>
/// Drifting lights that come out after dark and settle in the lowlands. Night
/// was navigable but empty; this gives it something of its own rather than
/// being daytime with the lights turned down.
/// </summary>
public class Fireflies : MonoBehaviour
{
    [SerializeField] private int count = 42;
    [SerializeField] private float radius = 26f;
    [SerializeField] private float driftSpeed = 0.35f;

    [Tooltip("They thin out with altitude, so peaks stay bare.")]
    [SerializeField] private float highestRelief = 0.45f;

    private ChunkManager world;
    private Transform player;
    private Transform[] flies;
    private Vector3[] offsets;
    private float[] phases;
    private Material glow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Fireflies>() == null)
        {
            new GameObject("Fireflies (runtime)").AddComponent<Fireflies>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null)
        {
            enabled = false;
            return;
        }

        player = world.PlayerTransform;

        Shader lit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        glow = new Material(lit);
        glow.SetColor("_BaseColor", new Color(0.95f, 0.92f, 0.55f));
        glow.color = new Color(0.95f, 0.92f, 0.55f);

        flies = new Transform[count];
        offsets = new Vector3[count];
        phases = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "firefly";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Random.Range(0.10f, 0.19f);
            go.GetComponent<MeshRenderer>().sharedMaterial = glow;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            flies[i] = go.transform;
            offsets[i] = Random.insideUnitSphere * radius;
            offsets[i].y = Random.Range(0.6f, 3.4f);
            phases[i] = Random.Range(0f, 20f);
        }
    }

    private void Update()
    {
        if (player == null) return;

        float night = 0f;

        if (TimeOfDay.Instance != null)
        {
            float t = TimeOfDay.Instance.Normalized;
            night = (t < 0.22f || t > 0.80f) ? 1f : 0f;
        }

        // only in the lowlands
        int seed = world.WorldSeed;
        int tileX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);
        float relief = WorldHeight.HeightAt(tileX, tileZ, seed) / WorldHeight.MaxRelief;

        bool active = night > 0.5f && relief < highestRelief;

        for (int i = 0; i < flies.Length; i++)
        {
            var fly = flies[i];

            if (!active)
            {
                if (fly.gameObject.activeSelf) fly.gameObject.SetActive(false);
                continue;
            }

            if (!fly.gameObject.activeSelf) fly.gameObject.SetActive(true);

            float t = Time.time * driftSpeed + phases[i];

            var drift = new Vector3(Mathf.Sin(t * 1.1f) * 1.6f, Mathf.Sin(t * 0.7f) * 0.7f, Mathf.Cos(t * 0.9f) * 1.6f);
            Vector3 at = player.position + offsets[i] + drift;

            // sit above the ground rather than inside a hill
            int fx = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
            int fz = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
            at.y = WorldHeight.SurfaceY(fx, fz, seed) + offsets[i].y;

            fly.position = at;

            // a slow blink, so they read as alive
            float pulse = 0.55f + 0.45f * Mathf.Sin(t * 2.3f + phases[i]);
            fly.localScale = Vector3.one * Mathf.Lerp(0.06f, 0.19f, pulse);

            // wrap any that drift too far from the player
            Vector3 away = at - player.position;
            away.y = 0f;

            if (away.sqrMagnitude > radius * radius * 1.6f)
            {
                offsets[i] = Random.insideUnitSphere * radius;
                offsets[i].y = Random.Range(0.6f, 3.4f);
            }
        }
    }
}
