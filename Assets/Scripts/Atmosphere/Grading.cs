using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The picture, graded. The world was drawn raw: palette colour under a
/// light, no tonemapping, so a sunset clipped to white and midday was flat.
/// This puts a volume over the camera -- tonemapping, a little bloom on the
/// sun and the water's glints, a soft vignette, and a colour grade -- and
/// drives the grade from the clock and the weather: warm at dawn and dusk,
/// cool and washed in the rain, blue-white in the snow country, a lift of
/// blue in the shadows at night. Built in code like everything else, so no
/// scene is edited and the title's camera is left alone.
/// </summary>
public class Grading : MonoBehaviour
{
    public static Grading Instance { get; private set; }

    /// <summary>Switched off, the picture is what it was: for comparing.</summary>
    public static bool Enabled = true;

    private Volume volume;
    private VolumeProfile profile;
    private Tonemapping tone;
    private Bloom bloom;
    private Vignette vignette;
    private ColorAdjustments colour;
    private WhiteBalance balance;
    private LiftGammaGain lift;
    private DepthOfField dof;
    private FilmGrain grain;
    private LensDistortion lens;
    private ChromaticAberration fringe;

    /// <summary>Whether the depth of field is on, and how far it is focused, for the probes.</summary>
    public bool Focused => dof != null && dof.active;
    public float FocusedAt => dof != null ? dof.focusDistance.value : 0f;
    /// <summary>Whether the camera is under the water, for the probes.</summary>
    public bool Submerged { get; private set; }
    private Camera view;
    private ChunkManager world;
    private float air, wet, dust;

    /// <summary>What the grade is doing now, for the probes.</summary>
    public float Saturation => colour != null ? colour.saturation.value : 0f;
    public float Temperature => balance != null ? balance.temperature.value : 0f;
    public float Exposure => colour != null ? colour.postExposure.value : 0f;
    public bool PostProcessing => view != null && view.GetUniversalAdditionalCameraData().renderPostProcessing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Grading>() == null) new GameObject("Grading (runtime)").AddComponent<Grading>();
    }

    private void Awake()
    {
        Instance = this;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Grade (runtime)";
        tone = profile.Add<Tonemapping>(true);
        bloom = profile.Add<Bloom>(true);
        vignette = profile.Add<Vignette>(true);
        colour = profile.Add<ColorAdjustments>(true);
        balance = profile.Add<WhiteBalance>(true);
        lift = profile.Add<LiftGammaGain>(true);
        dof = profile.Add<DepthOfField>(true);
        grain = profile.Add<FilmGrain>(true);
        lens = profile.Add<LensDistortion>(true);
        fringe = profile.Add<ChromaticAberration>(true);

        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focalLength.Override(70f);
        dof.aperture.Override(4.5f);
        dof.active = false;
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0f);
        lens.intensity.Override(0f);
        fringe.intensity.Override(0f);

        tone.mode.Override(TonemappingMode.Neutral);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.65f);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.05f, 0.06f, 0.08f));

        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.profile = profile;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (profile != null) Destroy(profile);
    }

    private void LateUpdate()
    {
        if (view == null)
        {
            view = Camera.main;
            if (view == null) return;

            var data = view.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.requiresColorOption = CameraOverrideOption.On;

            // room for a sky: the clouds are a kilometre off, and the stars were past the old far plane
            if (view.farClipPlane < 3000f) view.farClipPlane = 3000f;
        }

        volume.weight = Enabled ? 1f : 0f;
        if (!Enabled) return;

        var clock = TimeOfDay.Instance;
        float normalized = clock != null ? clock.Normalized : 0.5f;
        float overcast = clock != null ? clock.Overcast : 0f;
        float height = Mathf.Sin((normalized * 360f - 90f) * Mathf.Deg2Rad);    // the sun's, -1 to 1
        float day = Mathf.Clamp01(height * 3.2f);
        float horizon = 1f - Mathf.Clamp01(Mathf.Abs(height) * 3.5f);         // near the horizon, either way
        float goldHour = horizon * day;                                          // low sun, still up
        float night = 1f - day;

        if (world == null) world = FindFirstObjectByType<ChunkManager>();

        // The country you are standing in, and what its air is like. The snow country already
        // graded itself blue-white; the rest of the world all shared one set of numbers, so a
        // jungle at noon and a plain at noon were lit identically and only the ground told them
        // apart. Three kinds of air: cold, humid and dry.
        var country = world != null
            ? Regions.CharacterAtTile(Mathf.RoundToInt(view.transform.position.x / WorldGrid.TileSize),
                                      Mathf.RoundToInt(view.transform.position.z / WorldGrid.TileSize), world.WorldSeed)
            : Regions.Character.Lowland;

        float cold = country == Regions.Character.Snow ? 1f : 0f;
        float humid = country == Regions.Character.Jungle ? 1f : 0f;
        float dry = country == Regions.Character.Savanna || country == Regions.Character.Desert ? 1f : 0f;

        // eased, or the grade snaps as you cross a border
        air = Mathf.MoveTowards(air, cold, Time.deltaTime * 0.5f);
        wet = Mathf.MoveTowards(wet, humid, Time.deltaTime * 0.5f);
        dust = Mathf.MoveTowards(dust, dry, Time.deltaTime * 0.5f);
        cold = air; humid = wet; dry = dust;

        float rain = Rain.Intensity;

        // exposure: a touch up by day, down at night, down again under cloud
        colour.postExposure.Override(Mathf.Lerp(-0.15f, 0.12f, day) - overcast * 0.12f);

        // contrast and saturation: clear days sing, rain washes out, snow is spare
        colour.contrast.Override(Mathf.Lerp(4f, 10f, day) - overcast * 8f);
        colour.saturation.Override(Mathf.Lerp(-6f, 8f, day) - overcast * 22f - cold * 10f + goldHour * 6f
                                   + humid * 7f - dry * 5f);

        // the filter: warm at the gold hour, blue-white in the cold, grey-blue in the rain
        Color filter = Color.white;
        filter = Color.Lerp(filter, new Color(1f, 0.93f, 0.84f), goldHour * 0.8f);
        filter = Color.Lerp(filter, new Color(0.90f, 0.95f, 1.05f), cold * 0.7f);
        filter = Color.Lerp(filter, new Color(0.90f, 0.94f, 1.0f), rain * 0.6f);
        filter = Color.Lerp(filter, new Color(0.86f, 0.90f, 1.0f), night * 0.5f);
        // a jungle is green even in the air; a plain is bleached and dusty
        filter = Color.Lerp(filter, new Color(0.93f, 1.02f, 0.92f), humid * 0.55f);
        filter = Color.Lerp(filter, new Color(1.04f, 0.99f, 0.88f), dry * 0.50f);
        colour.colorFilter.Override(filter);

        // white balance: warmer low sun, colder snow and rain
        balance.temperature.Override(goldHour * 10f - cold * 12f - rain * 5f + dry * 8f - humid * 3f);
        balance.tint.Override(cold * -2f + humid * 4f);

        // the shadows lifted a little blue at night, so the dark is not black
        lift.lift.Override(new Vector4(-0.01f * night, 0f, 0.03f * night, 0f));
        lift.gamma.Override(new Vector4(0f, 0f, 0.01f * cold, 0f));
        lift.gain.Override(new Vector4(1f + goldHour * 0.03f, 1f, 1f + cold * 0.02f, 0f));

        // bloom: more at the gold hour, when the sun and the water glint
        bloom.intensity.Override(0.3f + goldHour * 0.35f - overcast * 0.15f);

        // the vignette closes a little at night and in a downpour
        vignette.intensity.Override(0.2f + night * 0.08f + rain * 0.08f + humid * 0.07f);

        // grain: a little at night and in the rain, none in the sun
        grain.intensity.Override(night * 0.22f + rain * 0.12f);

        // the glass up: everything but the subject softens
        float focus = Sketching.FocusDistance;
        bool focusing = Sketching.Working && focus > 0.5f;
        dof.active = focusing;
        if (focusing) dof.focusDistance.Override(focus);

        // under the water: blue-green, bent at the edges, colour bleeding
        Submerged = view.transform.position.y < WaterSurface.Level;

        // The air itself, written after the clock has had its say and only when the head is
        // above water, since under it the water writes its own. A jungle closes in and a plain
        // opens out, which does as much for telling them apart as the ground does.
        if (!Submerged)
        {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor,
                Color.Lerp(new Color(0.58f, 0.66f, 0.55f), new Color(0.80f, 0.76f, 0.64f), dry),
                Mathf.Max(humid, dry) * 0.35f);
            RenderSettings.fogEndDistance *= Mathf.Lerp(1f, 0.62f, humid) * Mathf.Lerp(1f, 1.18f, dry);
        }
        if (Submerged)
        {
            colour.colorFilter.Override(new Color(0.55f, 0.82f, 0.86f));
            colour.saturation.Override(-14f);
            lens.intensity.Override(-0.28f);
            fringe.intensity.Override(0.4f);
            vignette.intensity.Override(0.4f);
        }
        else
        {
            lens.intensity.Override(0f);
            fringe.intensity.Override(0f);
        }
    }
}
