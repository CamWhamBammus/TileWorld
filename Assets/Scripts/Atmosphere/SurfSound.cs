using UnityEngine;

/// <summary>
/// The sea heard: a swell of noise that rises and falls on about the
/// wash's period, louder the nearer the beach. Made in code like the rest.
/// </summary>
public class SurfSound : MonoBehaviour
{
    private static readonly int WashTime = Shader.PropertyToID("_WashTime");
    /// <summary>How loud the surf is now, for the probes.</summary>
    public static float Level { get; private set; }

    private AudioSource source;
    private ChunkManager world;
    private float next, nearest = 999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<SurfSound>() == null) new GameObject("Surf (runtime)").AddComponent<SurfSound>();
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = Build(28f, 91);
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.playOnAwake = false;
    }

    private void Update()
    {
        Shader.SetGlobalFloat(WashTime, Surf.Now);
        if (world == null) { world = FindFirstObjectByType<ChunkManager>(); if (world == null) return; }
        var player = world.PlayerTransform;
        if (player == null) return;

        // the nearest strand tile, looked for twice a second
        if (Time.time > next)
        {
            next = Time.time + 0.5f;
            int px = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize), pz = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);
            nearest = 999f;
            for (int r = 0; r <= 30 && nearest > 900f; r += 2)
            for (int dx = -r; dx <= r && nearest > 900f; dx += 2)
            for (int dz = -r; dz <= r; dz += 2)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                if (Surf.IsStrand(px + dx, pz + dz, world.WorldSeed)) { nearest = r * WorldGrid.TileSize; break; }
            }
        }

        float want = nearest > 900f ? 0f : 0.5f * Mathf.Clamp01(1f - nearest / 70f);
        Level = Mathf.Lerp(Level, want, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        source.volume = Level;
        if (Level > 0.01f && !source.isPlaying) source.Play();
        else if (Level <= 0.01f && source.isPlaying) source.Pause();
    }

    /// <summary>Surf: two swells a little out of step, noise shaped low and high, in fast and out slow.</summary>
    private static AudioClip Build(float seconds, int seed)
    {
        const int rate = 22050;
        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        var rng = new System.Random(seed);
        float lowA = 0f, bandA = 0f, lowB = 0f, bandB = 0f;
        float fA = 2f * Mathf.Sin(3.1415926f * 420f / rate), fB = 2f * Mathf.Sin(3.1415926f * 2600f / rate);

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowA += fA * bandA; float highA = white - lowA - 1.3f * bandA; bandA += fA * highA;
            lowB += fB * bandB; float highB = white - lowB - 1.0f * bandB; bandB += fB * highB;
            // two waves, fourteen seconds apart, each rising quickly and falling away
            float c1 = Mathf.Repeat(t / 14f, 1f), c2 = Mathf.Repeat(t / 14f + 0.45f, 1f);
            float s1 = c1 < 0.3f ? Mathf.SmoothStep(0f, 1f, c1 / 0.3f) : 1f - Mathf.SmoothStep(0f, 1f, (c1 - 0.3f) / 0.7f);
            float s2 = c2 < 0.3f ? Mathf.SmoothStep(0f, 1f, c2 / 0.3f) : 1f - Mathf.SmoothStep(0f, 1f, (c2 - 0.3f) / 0.7f);
            float swell = 0.25f + 0.75f * Mathf.Max(s1, s2 * 0.6f);
            data[i] = (bandA * 1.1f + bandB * 0.35f * (0.5f + s1)) * swell;
        }

        float peak = 0.0001f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        int fade = Mathf.RoundToInt(rate * 0.05f);
        for (int i = 0; i < samples; i++)
        {
            float edge = Mathf.Min(1f, Mathf.Min(i, samples - 1 - i) / (float)fade);
            data[i] = data[i] / peak * 0.8f * edge;
        }

        var clip = AudioClip.Create("Surf", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
