using UnityEngine;

/// <summary>
/// Drives the sun, the sky, the ambient light and the weather.
///
/// The scene uses Unity's procedural skybox with ambient light set to follow
/// it, so most of this is just moving one directional light correctly and
/// letting the sky respond. The skybox is instanced at runtime rather than
/// edited in place, because the default one is a shared built-in asset and
/// writing to it would leak changes back into the editor.
/// </summary>
public class TimeOfDay : MonoBehaviour
{
    [Header("Cycle")]
    [Tooltip("Real minutes for one full day and night.")]
    [SerializeField, Range(2f, 90f)] private float dayLengthMinutes = 20f;

    [Tooltip("0 is midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset.")]
    [SerializeField, Range(0f, 1f)] private float startTime = 0.30f;

    [Tooltip("Compass direction the sun tracks along.")]
    [SerializeField] private float sunYaw = 150f;

    [SerializeField] private bool paused = false;

    [Header("Weather")]
    [SerializeField] private bool weather = true;

    [Tooltip("Real minutes for weather to drift from clear to overcast and back.")]
    [SerializeField, Range(1f, 40f)] private float weatherPeriodMinutes = 7f;

    [Header("Look")]
    [SerializeField] private float noonIntensity = 1.25f;
    [Tooltip("Night has to stay navigable. Fully dark looks good in a screenshot and is unplayable.")]
    [SerializeField] private float moonIntensity = 0.42f;
    [SerializeField] private float clearFogEnd = 430f;
    [SerializeField] private float overcastFogEnd = 160f;

    public static TimeOfDay Instance { get; private set; }

    /// <summary>0 is midnight, 0.5 is noon.</summary>
    public float Normalized { get; private set; }

    /// <summary>0 clear, 1 fully overcast.</summary>
    public float Overcast { get; private set; }

    private Light sun;
    private Light moon;
    private Material sky;
    private float weatherSeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<TimeOfDay>() == null)
        {
            new GameObject("Time of Day (runtime)").AddComponent<TimeOfDay>();
        }
    }

    private void Awake()
    {
        Instance = this;
        Normalized = startTime;
        weatherSeed = Random.Range(0f, 500f);
    }

    private void Start()
    {
        sun = FindSun();

        if (sun == null)
        {
            Debug.LogWarning("[TimeOfDay] No directional light in the scene, so there is no sun to move.");
            enabled = false;
            return;
        }

        RenderSettings.sun = sun;

        var moonGo = new GameObject("Moon (runtime)");
        moonGo.transform.SetParent(transform, false);
        moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.62f, 0.70f, 0.95f);
        moon.intensity = 0f;
        moon.shadows = LightShadows.None;

        // Instance of the procedural sky, so driving it cannot dirty the asset.
        Shader procedural = Shader.Find("Skybox/Procedural");

        if (procedural != null)
        {
            sky = new Material(procedural);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            RenderSettings.skybox = sky;
        }

        SilenceOtherSuns();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;

        Apply();
    }

    private void Update()
    {
        if (!paused && dayLengthMinutes > 0f)
        {
            Normalized = Mathf.Repeat(Normalized + Time.deltaTime / (dayLengthMinutes * 60f), 1f);
        }

        if (weather)
        {
            float t = Time.time / (weatherPeriodMinutes * 60f);
            float n = Mathf.PerlinNoise(weatherSeed + t, weatherSeed - t * 0.6f);

            // Pushed toward clear, so overcast is weather rather than the norm.
            Overcast = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.85f, n));
        }
        else
        {
            Overcast = 0f;
        }

        Apply();
    }

    private void Apply()
    {
        // Midnight has the sun straight down, noon straight up.
        float elevation = Normalized * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(elevation, sunYaw, 0f);
        moon.transform.rotation = Quaternion.Euler(elevation + 180f, sunYaw, 0f);

        // How far above the horizon the sun is, 0 at the horizon.
        float height = Mathf.Sin(elevation * Mathf.Deg2Rad);
        float day = Mathf.Clamp01(height * 3.2f);          // quick fade through dusk
        float horizon = Mathf.Clamp01(1f - Mathf.Abs(height) * 4f);

        Color noon = new Color(1f, 0.97f, 0.90f);
        Color low = new Color(1f, 0.62f, 0.35f);           // the colour of low sun
        Color sunColour = Color.Lerp(noon, low, horizon);

        float clouds = 1f - Overcast * 0.68f;

        sun.color = Color.Lerp(sunColour, new Color(0.82f, 0.84f, 0.88f), Overcast * 0.6f);
        sun.intensity = day * noonIntensity * clouds;
        sun.enabled = sun.intensity > 0.005f;
        sun.shadowStrength = Mathf.Lerp(0.85f, 0.35f, Overcast);

        moon.intensity = Mathf.Clamp01(-height * 2.5f) * moonIntensity * Mathf.Lerp(1f, 0.45f, Overcast);
        moon.enabled = moon.intensity > 0.005f;

        // Ambient comes from the sky, so this scales the whole scene's floor light.
        RenderSettings.ambientIntensity = Mathf.Lerp(0.30f, 1f, day) * Mathf.Lerp(1f, 0.75f, Overcast);
        RenderSettings.reflectionIntensity = RenderSettings.ambientIntensity;

        if (sky != null)
        {
            // Thicker atmosphere at the horizon reddens sunrise and sunset.
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.0f, 2.1f, horizon) + Overcast * 0.4f);
            sky.SetFloat("_Exposure", Mathf.Lerp(0.55f, 1.3f, day) * Mathf.Lerp(1f, 0.62f, Overcast));

            // deep blue after dark rather than black, so there is still a horizon
            Color dayTint = Color.Lerp(new Color(0.45f, 0.60f, 0.78f), new Color(0.55f, 0.56f, 0.58f), Overcast);
            sky.SetColor("_SkyTint", Color.Lerp(new Color(0.16f, 0.22f, 0.42f), dayTint, day));
            sky.SetColor("_GroundColor", new Color(0.28f, 0.30f, 0.26f));
        }

        // Fog follows the sky so the horizon never cuts a hard line.
        Color fogDay = Color.Lerp(new Color(0.66f, 0.776f, 0.882f), new Color(0.96f, 0.72f, 0.52f), horizon * 0.8f);
        Color fogNight = new Color(0.07f, 0.09f, 0.15f);

        RenderSettings.fogColor = Color.Lerp(fogNight, fogDay, day);
        RenderSettings.fogStartDistance = Mathf.Lerp(110f, 25f, Overcast);
        RenderSettings.fogEndDistance = Mathf.Lerp(clearFogEnd, overcastFogEnd, Overcast) * Mathf.Lerp(0.55f, 1f, day);
    }

    /// <summary>
    /// Picks the light to drive. The scene has two directional lights, and
    /// choosing purely by intensity was luck: this cycle turns the sun's
    /// intensity down to nothing every night, so on a reload the wrong one
    /// could be picked and then never move. A named light wins, then a
    /// non-runtime one, and intensity only breaks a tie.
    /// </summary>
    private Light FindSun()
    {
        Light best = null;
        int bestScore = int.MinValue;

        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || light == moon) continue;

            int score = 0;
            if (light.name.Contains("Directional")) score += 100;
            if (light.name.Contains("Sun")) score += 100;
            if (!light.name.Contains("runtime")) score += 10;
            if (light.shadows != LightShadows.None) score += 5;

            if (score > bestScore || (score == bestScore && best != null && light.intensity > best.intensity))
            {
                best = light;
                bestScore = score;
            }
        }

        if (best != null)
        {
            Debug.Log("[TimeOfDay] Driving the light named '" + best.name + "'.");
        }

        return best;
    }

    /// <summary>Any other directional lights would fight the cycle, so they are turned off.</summary>
    private void SilenceOtherSuns()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || light == sun || light == moon) continue;
            if (!light.enabled) continue;

            light.enabled = false;
            Debug.Log("[TimeOfDay] Disabled a second directional light ('" + light.name + "') so it does not fight the cycle.");
        }
    }

    public float DayLengthMinutes => dayLengthMinutes;

    /// <summary>Changes how long a day takes while running.</summary>
    public void SetDayLength(float minutes)
    {
        dayLengthMinutes = Mathf.Clamp(minutes, 2f, 90f);
    }

    /// <summary>Puts the clock back to a saved time.</summary>
    public void SetTime(float normalized)
    {
        Normalized = Mathf.Repeat(normalized, 1f);
        if (sun != null) Apply();
    }

    /// <summary>Something readable for the map header.</summary>
    public string Label()
    {
        float t = Normalized;

        if (t < 0.22f || t >= 0.96f) return "night";
        if (t < 0.30f) return "dawn";
        if (t < 0.45f) return "morning";
        if (t < 0.55f) return "midday";
        if (t < 0.70f) return "afternoon";
        if (t < 0.80f) return "dusk";
        return "evening";
    }

    /// <summary>A clock reading, for anything that wants one.</summary>
    public string Clock()
    {
        int minutes = Mathf.RoundToInt(Normalized * 24f * 60f);
        return (minutes / 60 % 24).ToString("00") + ":" + (minutes % 60).ToString("00");
    }
}
