using UnityEngine;

/// <summary>
/// Sound. The project already owned a music track and a UI click; both sat in
/// the project with neither of the scene's two AudioSources having a clip
/// assigned, so the game was silent apart from footsteps.
///
/// The clips live under Resources so this can find them without a scene
/// reference, which keeps the whole feature in code.
/// </summary>
public class Ambience : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;

    [Tooltip("How much quieter the music sits after dark.")]
    [SerializeField, Range(0f, 1f)] private float nightDuck = 0.45f;

    [SerializeField] private float fadeInSeconds = 4f;

    public static Ambience Instance { get; private set; }

    private AudioSource music;
    private AudioSource effects;
    private AudioClip click;
    private float fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Ambience>() == null)
        {
            new GameObject("Ambience (runtime)").AddComponent<Ambience>();
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        var track = Resources.Load<AudioClip>("Audio/ES_Maps - August Wilhelmsson");
        click = Resources.Load<AudioClip>("Audio/ES_User Interface, Click, Select, Smartphone, Short 01 - Epidemic Sound");

        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        effects.spatialBlend = 0f;

        if (track == null)
        {
            Debug.LogWarning("[Ambience] No music track found under Resources/Audio.");
            return;
        }

        music = gameObject.AddComponent<AudioSource>();
        music.clip = track;
        music.loop = true;
        music.volume = 0f;
        music.spatialBlend = 0f;    // not positional; it is score, not a sound in the world
        music.Play();
    }

    private void Update()
    {
        if (music == null) return;

        if (fade < 1f && fadeInSeconds > 0f)
        {
            fade = Mathf.Clamp01(fade + Time.deltaTime / fadeInSeconds);
        }

        // Quieter after dark, so night feels like a different part of the day.
        float night = 0f;

        if (TimeOfDay.Instance != null)
        {
            float t = TimeOfDay.Instance.Normalized;
            night = (t < 0.24f || t > 0.78f) ? 1f : 0f;
        }

        float target = musicVolume * Mathf.Lerp(1f, 1f - nightDuck, night);
        music.volume = Mathf.MoveTowards(music.volume, target * fade, Time.deltaTime * 0.35f);
    }

    /// <summary>A short click, for opening and closing screens.</summary>
    public void Click(float pitch = 1f)
    {
        if (effects == null || click == null) return;

        effects.pitch = pitch;
        effects.PlayOneShot(click, 0.6f);
    }
}
