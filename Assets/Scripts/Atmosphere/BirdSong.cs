using UnityEngine;

/// <summary>
/// Birds in the daytime lowlands, synthesised the same way the wind is. Chirps
/// are short frequency sweeps, which is close enough to a bird at the distance
/// you hear one across a forest.
/// </summary>
public class BirdSong : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float volume = 0.16f;
    [SerializeField] private Vector2 gapSeconds = new Vector2(2.5f, 9f);

    [Tooltip("They keep to the lowlands, below this fraction of full relief.")]
    [SerializeField] private float highestRelief = 0.5f;

    private ChunkManager world;
    private Transform player;
    private AudioSource source;
    private AudioClip[] calls, gulls, whoops, chirrs;
    private float next;

    /// <summary>How many gulls have called, for the probes.</summary>
    public static int Whoops, Chirrs;
    public static int Gulls { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<BirdSong>() == null)
        {
            new GameObject("Birds (runtime)").AddComponent<BirdSong>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();
        onTitle = world == null && TitleMenu.IsUp;

        if (world == null && !onTitle)
        {
            enabled = false;
            return;
        }

        player = world != null ? world.PlayerTransform : null;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        calls = new AudioClip[5];
        for (int i = 0; i < calls.Length; i++) calls[i] = BuildCall(i);
        gulls = new AudioClip[3];
        for (int i = 0; i < gulls.Length; i++) gulls[i] = BuildGull(i);
        whoops = new AudioClip[3];
        for (int i = 0; i < whoops.Length; i++) whoops[i] = BuildWhoop(i);
        chirrs = new AudioClip[3];
        for (int i = 0; i < chirrs.Length; i++) chirrs[i] = BuildChirr(i);

        next = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
    }

    /// <summary>A few notes, each a swept sine under a quick envelope.</summary>
    public static AudioClip BuildCall(int variant)
    {
        const int rate = 44100;
        var rng = new System.Random(1000 + variant);

        int notes = 2 + rng.Next(3);
        float noteLength = 0.07f + (float)rng.NextDouble() * 0.06f;
        int perNote = Mathf.RoundToInt(rate * noteLength);
        int gap = Mathf.RoundToInt(rate * 0.05f);
        int samples = notes * (perNote + gap);

        var data = new float[samples];

        for (int n = 0; n < notes; n++)
        {
            float start = 1800f + (float)rng.NextDouble() * 1800f;
            float end = start + (float)(rng.NextDouble() * 1400.0 - 500.0);
            int offset = n * (perNote + gap);
            float phase = 0f;

            for (int i = 0; i < perNote; i++)
            {
                float t = i / (float)perNote;
                float frequency = Mathf.Lerp(start, end, t);

                phase += 2f * Mathf.PI * frequency / rate;

                // in and out quickly, or it sounds like a test tone
                float envelope = Mathf.Sin(t * Mathf.PI);

                data[offset + i] = Mathf.Sin(phase) * envelope * 0.5f;
            }
        }

        var clip = AudioClip.Create("Bird" + variant, samples, 1, rate, false);
        clip.SetData(data, 0);

        return clip;
    }

    /// <summary>A gull: a cry that climbs and falls, harsh with harmonics, sometimes twice.</summary>
    public static AudioClip BuildGull(int variant)
    {
        const int rate = 44100;
        var rng = new System.Random(2000 + variant);
        int cries = 1 + rng.Next(2);
        float length = 0.38f + (float)rng.NextDouble() * 0.2f;
        int per = Mathf.RoundToInt(rate * length), gap = Mathf.RoundToInt(rate * 0.16f);
        var data = new float[cries * (per + gap)];
        for (int c = 0; c < cries; c++)
        {
            float top = 1500f + (float)rng.NextDouble() * 500f;
            float phase = 0f; int offset = c * (per + gap);
            for (int i = 0; i < per; i++)
            {
                float t = i / (float)per;
                // up quickly to the top, then down and away
                float frequency = t < 0.25f ? Mathf.Lerp(top * 0.62f, top, Mathf.SmoothStep(0f, 1f, t / 0.25f)) : Mathf.Lerp(top, top * 0.55f, (t - 0.25f) / 0.75f);
                frequency *= 1f + 0.02f * Mathf.Sin(t * 70f);
                phase += 2f * Mathf.PI * frequency / rate;
                float envelope = Mathf.Sin(Mathf.Pow(t, 0.7f) * Mathf.PI);
                float s = Mathf.Sin(phase) + 0.55f * Mathf.Sin(phase * 2f) + 0.3f * Mathf.Sin(phase * 3f) + 0.12f * Mathf.Sin(phase * 4f);
                data[offset + i] = s * envelope * 0.28f;
            }
        }
        var clip = AudioClip.Create("Gull" + variant, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// A jungle bird: a low whoop that climbs, repeated two or three times and working itself
    /// up a little each time. Lower and rounder than the woodland calls, which are all up in
    /// the top of the range; a jungle that sang like an English wood would not read as one.
    /// </summary>
    public static AudioClip BuildWhoop(int variant)
    {
        const int rate = 44100;
        var rng = new System.Random(4000 + variant);
        int calls = 2 + rng.Next(3);
        float length = 0.22f + (float)rng.NextDouble() * 0.12f;
        int per = Mathf.RoundToInt(rate * length);
        int gap = Mathf.RoundToInt(rate * (0.10f + (float)rng.NextDouble() * 0.10f));
        var data = new float[calls * (per + gap)];
        float top = 620f + (float)rng.NextDouble() * 380f;
        for (int c = 0; c < calls; c++)
        {
            float phase = 0f; int offset = c * (per + gap);
            float rise = top * (1f + c * 0.06f);
            for (int i = 0; i < per; i++)
            {
                float t = i / (float)per;
                float frequency = Mathf.Lerp(rise * 0.55f, rise, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f)));
                phase += 2f * Mathf.PI * frequency / rate;
                float envelope = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI);
                float s = Mathf.Sin(phase) + 0.45f * Mathf.Sin(phase * 2f) + 0.22f * Mathf.Sin(phase * 3f);
                data[offset + i] = s * envelope * 0.22f;
            }
        }
        var clip = AudioClip.Create("Whoop" + variant, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Insects on a hot plain: not a call but a rattle, a high tone chopped fifty or sixty
    /// times a second, fading in and out over a second or two. It is the sound a dry country
    /// makes when nothing else is making one.
    /// </summary>
    public static AudioClip BuildChirr(int variant)
    {
        const int rate = 44100;
        var rng = new System.Random(3000 + variant);
        float length = 1.1f + (float)rng.NextDouble() * 0.9f;
        int samples = Mathf.RoundToInt(rate * length);
        var data = new float[samples];
        float carrier = 4200f + (float)rng.NextDouble() * 2200f;
        float buzz = 52f + (float)rng.NextDouble() * 30f;
        float phase = 0f, buzzPhase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            phase += 2f * Mathf.PI * carrier / rate;
            buzzPhase += 2f * Mathf.PI * buzz / rate;
            float rattle = 0.5f + 0.5f * Mathf.Sin(buzzPhase);
            rattle *= rattle;                       // sharper: an insect rather than a hum
            float envelope = Mathf.Min(1f, t * 8f) * Mathf.Min(1f, (1f - t) * 5f);
            float s = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 1.5f) * 0.4f;
            data[i] = s * rattle * envelope * 0.26f;
        }
        var clip = AudioClip.Create("Chirr" + variant, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private bool onTitle;

    private void Update()
    {
        if (Time.time < next) return;

        next = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);

        if (onTitle)
        {
            // a bird now and then, somewhere in the picture
            source.pitch = Random.Range(0.85f, 1.2f);
            source.PlayOneShot(calls[Random.Range(0, calls.Length)], volume * Random.Range(0.4f, 0.8f));
            return;
        }

        if (player == null) return;

        int country = world.WorldSeed;
        var here = Regions.CharacterAtTile(
            Mathf.RoundToInt(player.position.x / WorldGrid.TileSize),
            Mathf.RoundToInt(player.position.z / WorldGrid.TileSize), country, false);

        // Daylight only, and not up a mountain -- except in a jungle, which is as loud after
        // dark as before it, and that is half of what makes one.
        float time = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;
        if (here != Regions.Character.Jungle && (time < 0.26f || time > 0.76f)) return;

        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        if (overcast > 0.7f) return;      // they go quiet before rain

        int seed = world.WorldSeed;
        int tileX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);

        if (WorldHeight.HeightAt(tileX, tileZ, seed) / WorldHeight.MaxRelief > highestRelief) return;

        // in the jungle, something whooping in the canopy
        if (here == Regions.Character.Jungle && Random.value < 0.72f)
        {
            source.pitch = Random.Range(0.88f, 1.14f);
            source.PlayOneShot(whoops[Random.Range(0, whoops.Length)], volume * Random.Range(0.6f, 1f));
            Whoops++;
            return;
        }

        // out on the plain, insects in the grass
        if ((here == Regions.Character.Savanna || here == Regions.Character.Desert) && Random.value < 0.66f)
        {
            source.pitch = Random.Range(0.92f, 1.1f);
            source.PlayOneShot(chirrs[Random.Range(0, chirrs.Length)], volume * Random.Range(0.6f, 0.95f));
            Chirrs++;
            return;
        }

        // by the sea it is mostly gulls, crying over the water
        if (Regions.Sea(Regions.CharacterAtTile(tileX, tileZ, seed, false)) && Random.value < 0.65f)
        {
            source.pitch = Random.Range(0.9f, 1.12f);
            source.PlayOneShot(gulls[Random.Range(0, gulls.Length)], volume * Random.Range(0.7f, 1f));
            Gulls++;
            return;
        }

        source.pitch = Random.Range(0.85f, 1.2f);
        source.PlayOneShot(calls[Random.Range(0, calls.Length)], volume * Random.Range(0.6f, 1f));
    }
}
