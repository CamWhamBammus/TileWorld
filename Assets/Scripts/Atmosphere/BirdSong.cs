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
    private AudioClip[] calls;
    private float next;

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

        if (world == null)
        {
            enabled = false;
            return;
        }

        player = world.PlayerTransform;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        calls = new AudioClip[5];
        for (int i = 0; i < calls.Length; i++) calls[i] = BuildCall(i);

        next = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
    }

    /// <summary>A few notes, each a swept sine under a quick envelope.</summary>
    private AudioClip BuildCall(int variant)
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

    private void Update()
    {
        if (player == null || Time.time < next) return;

        next = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);

        // daylight only, and not up a mountain
        float time = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;
        if (time < 0.26f || time > 0.76f) return;

        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        if (overcast > 0.7f) return;      // they go quiet before rain

        int seed = world.WorldSeed;
        int tileX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);

        if (WorldHeight.HeightAt(tileX, tileZ, seed) / WorldHeight.MaxRelief > highestRelief) return;

        source.pitch = Random.Range(0.85f, 1.2f);
        source.PlayOneShot(calls[Random.Range(0, calls.Length)], volume * Random.Range(0.6f, 1f));
    }
}
