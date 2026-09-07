using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rain, when the weather has closed in. Overcast could be seen in the light
/// and heard in the wind, but nothing was actually falling.
///
/// The streaks are not objects: their positions live in arrays and they are
/// drawn in one instanced call, so there can be a good many of them for
/// nothing. They ride with the camera in a disc around it, lean with a wind
/// of their own that wanders, and fall to the ground or the water under
/// them -- a streak reaching water rings it. Far more drops land than are
/// drawn, so the water within sight is ringed on its own account, densest
/// near and thinning with distance, as far as the fog.
/// </summary>
public class Rain : MonoBehaviour
{
    [SerializeField] private int drops = 800;
    [SerializeField] private float radius = 24f;
    [SerializeField] private float height = 18f;
    [SerializeField] private float fallSpeed = 24f;
    [Tooltip("Overcast has to be at least this heavy before it rains.")]
    [SerializeField, Range(0f, 1f)] private float threshold = 0.55f;
    [Tooltip("How far out the water is ringed, in metres.")]
    [SerializeField] private float ringReach = 64f;

    /// <summary>How hard it is raining, 0 to 1, for anything that wants to know.</summary>
    public static float Intensity { get; private set; }
    /// <summary>The overcast it takes before anything falls.</summary>
    public static float Threshold { get; private set; } = 0.55f;
    /// <summary>The wind the rain leans with, in metres a second across the ground.</summary>
    public static Vector3 Wind { get; private set; }
    /// <summary>Whether what is falling is snow: it is, in the snow country.</summary>
    public static bool Snowing { get; private set; }

    private Transform view;
    private ChunkManager world;
    private Vector3[] local;        // where each streak is, relative to the camera
    private float[] speed;          // how fast it falls
    private float[] length;         // how long a streak it draws
    private float[] floors;         // the ground or the water under it, in world y (0 = not yet known)
    private readonly List<Matrix4x4> batch = new List<Matrix4x4>(1024);
    private Mesh streak, flake;
    private Material paint, white;
    private float[] sway;           // each flake's own drift, a phase
    private float ringsOwed;
    private float windSeed;

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
        paint = Paint.Flat(new Color(0.80f, 0.86f, 0.92f));

        if (paint == null)
        {
            Debug.LogWarning("[Rain] No paint to draw with, so there will be none.");
            enabled = false;
            return;
        }

        streak = Streak();
        flake = Flake();
        // brighter than white: lit flakes under an overcast sky came out grey
        white = Paint.Flat(new Color(1.7f, 1.7f, 1.75f));
        sway = new float[drops];
        local = new Vector3[drops];
        speed = new float[drops];
        length = new float[drops];
        floors = new float[drops];
        windSeed = Random.value * 100f;

        for (int i = 0; i < drops; i++)
        {
            local[i] = RandomStart();
            speed[i] = fallSpeed * Random.Range(0.85f, 1.15f);
            length[i] = Random.Range(0.4f, 0.8f);
            sway[i] = Random.value * Mathf.PI * 2f;
        }
    }

    private Vector3 RandomStart()
    {
        var flat = Random.insideUnitCircle * radius;
        return new Vector3(flat.x, Random.Range(0f, height), flat.y);
    }

    /// <summary>The ground or the water under a point: whichever is higher, a little under its surface.</summary>
    private float FloorUnder(Vector3 at)
    {
        if (world == null) world = FindFirstObjectByType<ChunkManager>();
        int seed = world != null ? world.WorldSeed : 0;
        int tileX = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tileZ = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        if (WaterSurface.IsUnderwater(tileX, tileZ, seed)) return WaterSurface.Level;

        return WorldHeight.SurfaceY(tileX, tileZ, seed) + 0.05f;
    }

    private void LateUpdate()
    {
        if (local == null) return;

        if (view == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            view = cam.transform;
        }

        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        bool raining = overcast >= threshold;
        float intensity = raining ? Mathf.InverseLerp(threshold, 1f, overcast) : 0f;
        Intensity = intensity;
        Threshold = threshold;

        // a wind of its own, wandering in direction and strength
        float t = Time.time * 0.05f + windSeed;
        float angle = Mathf.PerlinNoise(t, 0.3f) * Mathf.PI * 4f;
        float strength = Mathf.Lerp(1.5f, 5f, Mathf.PerlinNoise(0.7f, t * 1.3f)) * Mathf.Lerp(0.6f, 1f, intensity);
        Wind = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * strength;

        // rides with the camera, so a fixed handful of drops covers any distance
        transform.position = view.position;
        int active = Mathf.RoundToInt(drops * intensity);
        float dt = Time.deltaTime;
        if (world == null) world = FindFirstObjectByType<ChunkManager>();
        int seed = world != null ? world.WorldSeed : 0;

        // in the snow country it snows: slow, swaying, white, and nothing rings
        Snowing = Regions.CharacterAtTile(Mathf.RoundToInt(view.position.x / WorldGrid.TileSize), Mathf.RoundToInt(view.position.z / WorldGrid.TileSize), seed) == Regions.Character.Snow;
        float slow = Snowing ? 0.07f : 1f;

        batch.Clear();

        for (int i = 0; i < active; i++)
        {
            var p = local[i];
            p.y -= speed[i] * slow * dt;

            if (Snowing)
            {
                p.x += (Wind.x * 0.3f + Mathf.Sin(Time.time * 1.1f + sway[i]) * 0.35f) * dt;
                p.z += (Wind.z * 0.3f + Mathf.Cos(Time.time * 0.9f + sway[i] * 1.7f) * 0.35f) * dt;
            }
            else
            {
                p.x += Wind.x * dt;
                p.z += Wind.z * dt;
            }

            // A drop falls to the ground or the water under it, not to a fixed
            // depth below the camera: the camera rides well above the ground,
            // and the rain used to stop in mid-air. One reaching the water
            // rings it.
            if (floors[i] == 0f) floors[i] = FloorUnder(transform.position + p);

            if (transform.position.y + p.y <= floors[i])
            {
                if (!Snowing && floors[i] <= WaterSurface.Level + 0.001f) Splashes.Raindrop(transform.position + p, seed, 1f);
                p = RandomStart();
                floors[i] = FloorUnder(transform.position + p);
            }
            else if (p.x * p.x + p.z * p.z > radius * radius * 1.3f)
            {
                // blown out of the disc: back in at the top
                p = RandomStart();
                floors[i] = FloorUnder(transform.position + p);
            }

            local[i] = p;

            if (Snowing)
            {
                float size = 0.07f + length[i] * 0.09f;
                batch.Add(Matrix4x4.TRS(transform.position + p, Quaternion.Euler(sway[i] * 40f, Time.time * 30f + sway[i] * 90f, 0f), new Vector3(size, size, size)));
            }
            else
            {
                // leant along the way it is falling
                var going = new Vector3(Wind.x, -speed[i], Wind.z);
                var lean = Quaternion.FromToRotation(Vector3.down, going.normalized);
                batch.Add(Matrix4x4.TRS(transform.position + p, lean, new Vector3(1f, length[i], 1f)));
            }
        }

        for (int i = active; i < drops; i++) floors[i] = 0f;

        if (batch.Count > 0)
        {
            var rp = new RenderParams(Snowing ? white : paint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            var mesh = Snowing ? flake : streak;
            for (int from = 0; from < batch.Count; from += 1000)
                Graphics.RenderMeshInstanced(rp, mesh, 0, batch, Mathf.Min(1000, batch.Count - from), from);
        }

        if (Snowing) return;

        // The water within sight is ringed on its own account: far more drops
        // land than are drawn. Densest near the camera and thinning with
        // distance, and bigger further off so a ring still reads out there.
        ringsOwed += dt * 5500f * intensity;

        while (ringsOwed >= 1f)
        {
            ringsOwed -= 1f;

            float r = ringReach * Mathf.Sqrt(Random.value);
            float keep = 1f / (1f + (r / 16f) * (r / 16f));
            if (Random.value > keep) continue;

            float a = Random.value * Mathf.PI * 2f;
            var at = transform.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Splashes.Raindrop(at, seed, 1f + r / 40f);
        }
    }

    /// <summary>A small flat-shaded lump, a unit across, for a flake of snow.</summary>
    private static Mesh Flake()
    {
        Vector3[] tips = { Vector3.right * 0.5f, Vector3.left * 0.5f, Vector3.up * 0.35f, Vector3.down * 0.35f, Vector3.forward * 0.5f, Vector3.back * 0.5f };
        int[][] faces =
        {
            new[] { 2, 0, 4 }, new[] { 2, 4, 1 }, new[] { 2, 1, 5 }, new[] { 2, 5, 0 },
            new[] { 3, 4, 0 }, new[] { 3, 1, 4 }, new[] { 3, 5, 1 }, new[] { 3, 0, 5 }
        };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        foreach (var f in faces)
        {
            int n = verts.Count;
            verts.Add(tips[f[0]]); verts.Add(tips[f[1]]); verts.Add(tips[f[2]]);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
        }

        var m = new Mesh { name = "flake" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>A thin bar a unit long down its own y, for a streak of rain.</summary>
    private static Mesh Streak()
    {
        const float w = 0.016f;
        Vector3[] v =
        {
            new Vector3(-w, -0.5f, -w), new Vector3(w, -0.5f, -w), new Vector3(w, -0.5f, w), new Vector3(-w, -0.5f, w),
            new Vector3(-w, 0.5f, -w), new Vector3(w, 0.5f, -w), new Vector3(w, 0.5f, w), new Vector3(-w, 0.5f, w)
        };
        int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        foreach (var f in faces)
        {
            int n = verts.Count;
            verts.Add(v[f[0]]); verts.Add(v[f[1]]); verts.Add(v[f[2]]); verts.Add(v[f[3]]);
            tris.Add(n); tris.Add(n + 2); tris.Add(n + 1);
            tris.Add(n); tris.Add(n + 3); tris.Add(n + 2);
        }

        var m = new Mesh { name = "streak" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
