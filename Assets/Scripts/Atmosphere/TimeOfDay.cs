using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    [SerializeField] private float clearFogStart = 110f;
    [SerializeField] private float clearFogEnd = 430f;
    [SerializeField] private float overcastFogEnd = 160f;

    public static TimeOfDay Instance { get; private set; }

    /// <summary>0 is midnight, 0.5 is noon.</summary>
    public float Normalized { get; private set; }

    /// <summary>0 clear, 1 fully overcast.</summary>
    public float Overcast { get; private set; }

    private Light sun;
    private Texture2D[] cloudCookies;       // light, medium, heavy: the sun through broken cloud
    private UniversalAdditionalLightData sunData;
    private Vector2 cloudDrift;
    private Transform moonDisc, moonHalo;
    private Material moonPaint, haloPaint;

    /// <summary>Whether cloud shadows are drifting over the land, for the probes.</summary>
    public bool CloudShadows => sun != null && sun.cookie != null;
    /// <summary>How bright the moon shows, 0 to 1, for the probes.</summary>
    public float MoonShowing { get; private set; }
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

        // how the world was set up: a still sky, no weather, a long or short day
        if (WorldLibrary.Current != null)
        {
            var world = WorldLibrary.Current;
            weather = world.weather;
            paused = !world.dayCycle;
            dayLengthMinutes = Mathf.Clamp(world.dayLengthMinutes, 2f, 90f);
            if (!world.dayCycle) Normalized = world.startHour;
        }

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

        if (forcedOvercast >= 0f)
        {
            Overcast = forcedOvercast;
        }
        else if (weather)
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
        // a flash of lightning lights everything for a moment and is gone
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 9f);
        sun.intensity = day * noonIntensity * clouds + flash * 2.2f;
        sun.enabled = sun.intensity > 0.005f;
        sun.shadowStrength = Mathf.Lerp(0.85f, 0.35f, Overcast);

        moon.intensity = Mathf.Clamp01(-height * 2.5f) * moonIntensity * Mathf.Lerp(1f, 0.45f, Overcast);
        moon.enabled = moon.intensity > 0.005f;

        // Ambient comes from the sky, so this scales the whole scene's floor light.
        RenderSettings.ambientIntensity = Mathf.Lerp(0.30f, 1f, day) * Mathf.Lerp(1f, 0.75f, Overcast) + flash * 1.6f;
        RenderSettings.reflectionIntensity = RenderSettings.ambientIntensity;

        if (sky != null)
        {
            // Thicker atmosphere at the horizon reddens sunrise and sunset.
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.0f, 2.1f, horizon) + Overcast * 0.4f);
            sky.SetFloat("_Exposure", Mathf.Lerp(0.55f, 1.3f, day) * Mathf.Lerp(1f, 0.62f, Overcast));
            // the sun swells and softens as it nears the horizon
            sky.SetFloat("_SunSize", Mathf.Lerp(0.035f, 0.065f, horizon));
            sky.SetFloat("_SunSizeConvergence", Mathf.Lerp(6f, 3.2f, horizon));
            // deep blue after dark rather than black, so there is still a horizon;
            // warmer at the gold hour
            Color dayTint = Color.Lerp(new Color(0.45f, 0.60f, 0.78f), new Color(0.55f, 0.56f, 0.58f), Overcast);
            dayTint = Color.Lerp(dayTint, new Color(0.62f, 0.55f, 0.55f), horizon * day * 0.5f);
            sky.SetColor("_SkyTint", Color.Lerp(new Color(0.16f, 0.22f, 0.42f), dayTint, day));
            // the ground under the horizon is the haze's colour, so the horizon is a band and not a line
            Color haze = Color.Lerp(new Color(0.58f, 0.66f, 0.74f), new Color(0.88f, 0.70f, 0.55f), horizon * 0.7f);
            haze = Color.Lerp(haze, new Color(0.50f, 0.52f, 0.55f), Overcast);
            sky.SetColor("_GroundColor", Color.Lerp(new Color(0.10f, 0.12f, 0.18f), haze, day));
        }

        Clouds();
        Moon(height);

        // Fog follows the sky so the horizon never cuts a hard line.
        Color fogDay = Color.Lerp(new Color(0.66f, 0.776f, 0.882f), new Color(0.96f, 0.72f, 0.52f), horizon * 0.8f);
        Color fogNight = new Color(0.07f, 0.09f, 0.15f);

        RenderSettings.fogColor = fogLocked ? fogLockColour : Color.Lerp(fogNight, fogDay, day);
        RenderSettings.fogStartDistance = Mathf.Lerp(clearFogStart, 25f, Overcast);
        RenderSettings.fogEndDistance = Mathf.Lerp(clearFogEnd, overcastFogEnd, Overcast) * Mathf.Lerp(0.55f, 1f, day);
    }

    /// <summary>
    /// The sun through broken cloud: a soft cookie on the light, drifting
    /// with the wind, in one of three weights by how overcast it is. Clear
    /// skies and a full overcast get none: nothing to cast, or all shadow.
    /// </summary>
    private void Clouds()
    {
        if (cloudCookies == null)
        {
            cloudCookies = new[] { CloudCookie(0.3f, 11), CloudCookie(0.48f, 12), CloudCookie(0.66f, 13) };
            sunData = sun.GetUniversalAdditionalLightData();
            sunData.lightCookieSize = new Vector2(260f, 260f);
        }

        Texture2D want = Overcast < 0.15f ? null : Overcast < 0.45f ? cloudCookies[0] : Overcast < 0.75f ? cloudCookies[1] : cloudCookies[2];
        if (sun.cookie != want) sun.cookie = want;

        if (want != null && sunData != null)
        {
            Vector3 wind = Rain.Wind.sqrMagnitude > 0.01f ? Rain.Wind : new Vector3(1.2f, 0f, 0.6f);
            cloudDrift += new Vector2(wind.x, wind.z) * (Time.deltaTime * 0.9f / 260f);
            sunData.lightCookieOffset = cloudDrift;
        }
    }

    /// <summary>A soft cloud pattern, in a tile that wraps: the sun through it is mostly light with darker patches drifting by.</summary>
    private static Texture2D CloudCookie(float contrast, int seed)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "cloud cookie " + seed };
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // fbm on a torus, so the tile wraps without a seam
            float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
            float nx = Mathf.Cos(u) * 1.5f + seed * 3f, ny = Mathf.Sin(u) * 1.5f, nz = Mathf.Cos(v) * 1.5f + seed * 7f, nw = Mathf.Sin(v) * 1.5f;
            float n = 0f, amp = 0.55f, freq = 1f;
            for (int o = 0; o < 4; o++)
            {
                n += amp * (Mathf.PerlinNoise(nx * freq + nz * freq * 0.7f, ny * freq + nw * freq * 0.7f) - 0.5f);
                amp *= 0.5f; freq *= 2.1f;
            }
            float light = Mathf.Clamp01(1f - contrast * 1.6f * Mathf.Max(0f, n + 0.15f) * 3f);
            light = Mathf.Lerp(1f - contrast, 1f, light);
            pixels[y * size + x] = new Color(light, light, light, 1f);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>
    /// The moon: a disc and a soft halo, far off in the moon light's
    /// direction, showing by night and fading under cloud. Drawn with the
    /// glow shader so the fog does not take it.
    /// </summary>
    private void Moon(float height)
    {
        var view = Camera.main;
        if (view == null) return;

        if (moonDisc == null)
        {
            var glow = Resources.Load<Material>("Glow");
            if (glow == null) return;
            moonPaint = new Material(glow);
            haloPaint = new Material(glow);
            moonDisc = MoonQuad("Moon disc (runtime)", moonPaint);   // the moon light is "Moon (runtime)" already
            moonHalo = MoonQuad("Moon halo (runtime)", haloPaint);
        }

        float night = Mathf.Clamp01(-height * 2.5f);
        MoonShowing = night * (1f - Overcast * 0.85f);
        Vector3 toward = -moon.transform.forward;
        float far = Mathf.Min(850f, view.farClipPlane * 0.9f);

        moonDisc.position = view.transform.position + toward * far;
        moonDisc.rotation = Quaternion.LookRotation(-toward, Vector3.up);
        moonDisc.localScale = Vector3.one * (far * 0.032f);
        moonHalo.position = moonDisc.position;
        moonHalo.rotation = moonDisc.rotation;
        moonHalo.localScale = Vector3.one * (far * 0.09f);

        moonPaint.SetColor("_Color", new Color(0.96f, 0.97f, 1f, MoonShowing));
        moonPaint.SetFloat("_Strength", 2.4f);
        haloPaint.SetColor("_Color", new Color(0.7f, 0.78f, 1f, MoonShowing * 0.16f));
        haloPaint.SetFloat("_Strength", 1.2f);
        moonDisc.gameObject.SetActive(MoonShowing > 0.01f);
        moonHalo.gameObject.SetActive(MoonShowing > 0.01f);

        // and the water knows where it is
        Shader.SetGlobalVector("_MoonDir", toward);
        Shader.SetGlobalColor("_MoonColor", moon.color * (MoonShowing * 1.6f));
    }

    private static Transform MoonQuad(string name, Material paint)
    {
        var go = new GameObject(name);
        var mesh = new Mesh { name = name };
        // a disc, as a fan, with alpha full at the middle and gone at the rim
        const int sides = 24;
        var verts = new Vector3[sides + 1]; var cols = new Color[sides + 1]; var uvs = new Vector2[sides + 1]; var tris = new int[sides * 3];
        verts[0] = Vector3.zero; cols[0] = Color.white; uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
            cols[i + 1] = new Color(1f, 1f, 1f, 0f);
            uvs[i + 1] = new Vector2(0.5f, 0.5f);
            tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % sides + 1;
        }
        mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = paint;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
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

    // The fog held to one colour: for a panorama, the sky's own colour at the
    // horizon, so what fades into it fades into sky and not into a haze of
    // a slightly different shade that stands against the sky like a hill.
    private bool fogLocked;
    private Color fogLockColour;
    public void LockFog(Color colour) { fogLocked = true; fogLockColour = colour; }
    public void UnlockFog() { fogLocked = false; }

    /// <summary>The clock held or let go: for a capture, so six faces see one sun.</summary>
    public bool Paused { get => paused; set => paused = value; }

    // The weather forced, for the dev tools and for tests: negative is no forcing.
    private float forcedOvercast = -1f;
    public void ForceOvercast(float overcast) { forcedOvercast = overcast; }

    /// <summary>Whether the sky is being held at a level rather than being its own.</summary>
    public bool OvercastHeld => forcedOvercast >= 0f;

    private float flash;

    /// <summary>Lightning: everything lit for a moment. Strength 0 to 1.</summary>
    public void Flash(float strength) { flash = Mathf.Max(flash, Mathf.Clamp01(strength)); }

    /// <summary>How much of a flash is still lighting things, for the probes.</summary>
    public float Flashing => flash;

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
