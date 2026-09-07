using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drifting lights that come out after dark and settle in the lowlands.
/// Each has a place of its own in the world and wanders from it on its
/// own slow course, so they hang where they are as you walk past rather
/// than coming along with you, which is what they used to do. New ones
/// come up ahead as you go and old ones are let go behind. Drawn as soft
/// glows that pulse.
/// </summary>
public class Fireflies : MonoBehaviour
{
    private struct Fly
    {
        public Vector3 At;
        public Vector2 Heading;
        public float Phase, Turn, Height;
    }

    [SerializeField] private int count = 48;
    [SerializeField] private float radius = 30f;
    [Tooltip("They thin out with altitude, so peaks stay bare.")]
    [SerializeField] private float highestRelief = 0.45f;

    /// <summary>For the probes: how many are out, and where the first one is.</summary>
    public static int Out { get; private set; }
    public static Vector3 First { get; private set; }

    private ChunkManager world;
    private Transform player;
    private readonly List<Fly> flies = new List<Fly>();
    private readonly List<Matrix4x4> batch = new List<Matrix4x4>(64);
    private Material glow;
    private Mesh disc;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Fireflies>() == null) new GameObject("Fireflies (runtime)").AddComponent<Fireflies>();
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();
        if (world == null) { enabled = false; return; }
        player = world.PlayerTransform;
        var paint = Resources.Load<Material>("Glow");
        if (paint == null) { enabled = false; return; }
        glow = new Material(paint);
        glow.SetColor("_Color", new Color(0.95f, 0.9f, 0.5f, 1f));
        glow.SetFloat("_Strength", 2.2f);
        glow.enableInstancing = true;
        disc = Glows.DiscMesh();
    }

    private Fly Make(Vector3 near, bool ahead)
    {
        var flat = Random.insideUnitCircle.normalized * Random.Range(ahead ? radius * 0.6f : 4f, radius);
        if (ahead && player != null) { Vector3 f = player.forward; if (Vector2.Dot(flat.normalized, new Vector2(f.x, f.z)) < 0f) flat = -flat; }
        var fly = new Fly
        {
            At = new Vector3(near.x + flat.x, 0f, near.z + flat.y),
            Heading = Random.insideUnitCircle.normalized,
            Phase = Random.Range(0f, 20f),
            Turn = Random.Range(0.4f, 1.1f),
            Height = Random.Range(0.6f, 3f)
        };
        fly.At.y = WaterSurface.WalkingY(Mathf.RoundToInt(fly.At.x / WorldGrid.TileSize), Mathf.RoundToInt(fly.At.z / WorldGrid.TileSize), world.WorldSeed) + fly.Height;
        return fly;
    }

    private void Update()
    {
        if (player == null || glow == null) return;

        float night = 0f;
        if (TimeOfDay.Instance != null)
        {
            float t = TimeOfDay.Instance.Normalized;
            night = Mathf.Clamp01(Mathf.InverseLerp(0.24f, 0.19f, t) + Mathf.InverseLerp(0.78f, 0.83f, t));
        }

        int seed = world.WorldSeed;
        int tileX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);
        float relief = WorldHeight.HeightAt(tileX, tileZ, seed) / WorldHeight.MaxRelief;
        bool active = night > 0.05f && relief < highestRelief && Rain.Intensity < 0.4f;
        int wanted = active ? count : 0;

        // come up a few a second where they are wanted, and go the same way
        if (flies.Count < wanted && Random.value < Time.deltaTime * 8f) flies.Add(Make(player.position, flies.Count > count / 2));
        else if (flies.Count > wanted && Random.value < Time.deltaTime * 6f) flies.RemoveAt(Random.Range(0, flies.Count));

        batch.Clear();
        float dt = Time.deltaTime, now = Time.time;
        var view = Camera.main;

        for (int i = flies.Count - 1; i >= 0; i--)
        {
            var fly = flies[i];

            // its own slow course: a heading that wanders, a height that bobs
            fly.Heading = (fly.Heading + new Vector2(Mathf.PerlinNoise(now * 0.3f, fly.Phase) - 0.5f, Mathf.PerlinNoise(fly.Phase, now * 0.3f) - 0.5f) * (fly.Turn * dt * 4f)).normalized;
            fly.At += new Vector3(fly.Heading.x, 0f, fly.Heading.y) * (0.45f * dt);
            float ground = WaterSurface.WalkingY(Mathf.RoundToInt(fly.At.x / WorldGrid.TileSize), Mathf.RoundToInt(fly.At.z / WorldGrid.TileSize), seed);
            fly.At.y = Mathf.Lerp(fly.At.y, ground + fly.Height + Mathf.Sin(now * 0.7f + fly.Phase) * 0.5f, 1f - Mathf.Exp(-2f * dt));

            // left behind: gone, and another comes up ahead
            Vector3 away = fly.At - player.position; away.y = 0f;
            if (away.sqrMagnitude > radius * radius * 2.2f) { flies[i] = Make(player.position, true); continue; }

            flies[i] = fly;

            // a slow blink, so they read as alive
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.1f + fly.Phase);
            float size = Mathf.Lerp(0.12f, 0.36f, pulse) * night;
            var face = view != null ? Quaternion.LookRotation(fly.At - view.transform.position, Vector3.up) : Quaternion.identity;
            batch.Add(Matrix4x4.TRS(fly.At, face, Vector3.one * size));
        }

        Out = batch.Count;
        First = flies.Count > 0 ? flies[0].At : Vector3.zero;
        if (batch.Count == 0) return;
        var rp = new RenderParams(glow) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMeshInstanced(rp, disc, 0, batch, batch.Count, 0);
    }
}
