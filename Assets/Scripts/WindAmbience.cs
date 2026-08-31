using UnityEngine;

/// <summary>
/// Wind, synthesised rather than shipped. There is no wind recording in the
/// project and no way for me to add one, but wind is filtered noise, which is
/// a few lines and no download.
///
/// It rises with altitude and with the weather, so climbing a mountain or
/// walking into an overcast patch is something you can hear.
/// </summary>
public class WindAmbience : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.30f;
    [SerializeField] private int seconds = 8;

    private ChunkManager world;
    private Transform player;
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<WindAmbience>() == null)
        {
            new GameObject("Wind (runtime)").AddComponent<WindAmbience>();
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
        source.clip = Build();
        source.loop = true;
        source.volume = 0f;
        source.spatialBlend = 0f;
        source.Play();
    }

    /// <summary>
    /// Low passed noise with a slow swell. The filter is what turns hiss into
    /// wind; the swell is what stops it sounding like a broken speaker.
    /// </summary>
    private AudioClip Build()
    {
        const int rate = 44100;
        int samples = rate * seconds;

        var data = new float[samples];
        var rng = new System.Random(4242);

        float low = 0f, lower = 0f;

        for (int i = 0; i < samples; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            // two poles of low pass, so it is a rush of air and not static
            low += (white - low) * 0.045f;
            lower += (low - lower) * 0.10f;

            float t = i / (float)rate;
            float swell = 0.55f
                        + 0.30f * Mathf.Sin(t * 0.21f * Mathf.PI * 2f)
                        + 0.15f * Mathf.Sin(t * 0.07f * Mathf.PI * 2f + 1.3f);

            data[i] = lower * 5.5f * swell;
        }

        // taper the seam so the loop does not click
        int blend = rate / 2;

        for (int i = 0; i < blend; i++)
        {
            float k = i / (float)blend;
            data[i] = Mathf.Lerp(data[samples - blend + i], data[i], k);
        }

        var clip = AudioClip.Create("Wind", samples, 1, rate, false);
        clip.SetData(data, 0);

        return clip;
    }

    private void Update()
    {
        if (player == null || source == null) return;

        int seed = world.WorldSeed;
        int tileX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);

        float relief = Mathf.Clamp01(WorldHeight.HeightAt(tileX, tileZ, seed) / WorldHeight.MaxRelief);
        float weather = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;

        // quiet in a sheltered valley, loud on an exposed ridge
        float target = maximumVolume * Mathf.Clamp01(0.18f + relief * 0.95f + weather * 0.45f);

        source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime * 0.25f);
        source.pitch = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(relief * 0.7f + weather * 0.5f));
    }
}
