using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title: the game opens here and asks which world. A list of the
/// worlds kept, with what each has come to; a page for making a new one,
/// with how it should be set up -- name, seed, weather or none, a day that
/// turns or a still sky, the hour it starts at, how long a day is, whether
/// there are animals and ruins in it at all. Choosing enters the world; the
/// pause menu comes back here.
///
/// Behind it is the country itself, live: a beach in a world of its own,
/// drawn as far as the game can draw, with the sea running up the sand and
/// back and clouds going over, and the game's name in blocks at the top.
/// The title is the game scene with nothing chosen: the world under it is
/// built from a fixed seed, the player is put away, the camera is stood on
/// the sand, and none of the game's own interface wakes.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    /// <summary>Whether the title is up: the game is running with no world chosen.</summary>
    public static bool IsUp => !WorldLibrary.HasCurrent;

    /// <summary>The world behind the title, picked for a beach near its origin.</summary>
    public const int Seed = 24;

    /// <summary>
    /// Which title it is by the clock on the wall: a morning, an afternoon, a
    /// dusk or a night. The hour and the clouds behind the title follow it.
    /// </summary>
    public static string WhenNow()
    {
        int h = System.DateTime.Now.Hour;
        return h < 5 || h >= 21 ? "night" : h < 10 ? "morning" : h < 17 ? "afternoon" : "dusk";
    }

    /// <summary>The title that is up, by name; and the camera's rest, for the probes.</summary>
    public string When { get; private set; } = "";
    public Quaternion BaseRotation => baseRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (!IsUp) return;
        if (FindFirstObjectByType<TitleMenu>() == null) new GameObject("Title (runtime)").AddComponent<TitleMenu>();
    }

    /// <summary>
    /// Where the title looks from, in the title's world: on the sand a few
    /// metres back from the nearest strand to the origin, looking out to sea
    /// a little along the shore so the waterline runs across the picture.
    /// <paramref name="stand"/> is where the hidden player goes, which is
    /// what the world is drawn around.
    /// </summary>
    public static void Viewpoint(int seed, out Vector3 eye, out Vector3 look, out Vector3 stand)
        => Viewpoint(seed, out eye, out look, out stand, out _, out _);

    public static void Viewpoint(int seed, out Vector3 eye, out Vector3 look, out Vector3 stand, out Vector2Int strand, out Vector3 toSea)
    {
        int sx = 0, sz = 0; bool found = false;
        for (int r = 0; r < 70 && !found; r++)
        for (int dx = -r; dx <= r && !found; dx++)
        for (int dz = -r; dz <= r && !found; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            if (Surf.IsStrand(dx, dz, seed)) { found = true; sx = dx; sz = dz; }
        }

        Vector2 seaSum = Vector2.zero; int sea = 0;
        if (found)
        {
            for (int dx = -20; dx <= 20; dx++)
            for (int dz = -20; dz <= 20; dz++)
            {
                int tx = sx + dx, tz = sz + dz;
                if (!WaterSurface.IsUnderwater(tx, tz, seed) || WaterSurface.BodyAt(tx, tz, seed) != WaterSurface.Body.Beach) continue;
                seaSum += new Vector2(dx, dz); sea++;
            }
        }
        Vector3 dir = sea > 0 ? new Vector3(seaSum.x, 0f, seaSum.y).normalized : Vector3.forward;
        Vector3 tile = new Vector3(sx * WorldGrid.TileSize, WorldHeight.SurfaceY(sx, sz, seed), sz * WorldGrid.TileSize);
        strand = new Vector2Int(sx, sz);
        toSea = dir;

        stand = tile - dir * 8f + Vector3.up * 1.5f;
        eye = tile - dir * 5f + Vector3.up * 2.6f;
        Vector3 along = Quaternion.Euler(0f, 24f, 0f) * dir;
        look = eye + along * 30f;
        look.y = WaterSurface.Level;
    }

    private TMP_FontAsset font;
    private Transform card, canvasRoot;
    private GameObject worldsPage, newPage, optionsPage;
    private TMP_Text volumeLabel, radiusLabel, lookLabel, fullLabel;
    private readonly List<GameObject> rows = new List<GameObject>();
    private WorldSave chosen;
    private string pendingForget;
    private TMP_Text playLabel, forgetLabel, heading;

    // the new world's settings
    private TMP_InputField nameField, seedField;
    private bool weather = true, dayCycle = true, animals = true, ruins = true;
    private int startAt = 0;          // dawn, noon, dusk, night
    private int dayLength = 1;        // short, ordinary, long
    private TMP_Text weatherLabel, cycleLabel, startLabel, lengthLabel, animalsLabel, ruinsLabel;

    private static readonly string[] StartNames = { "dawn", "noon", "dusk", "night" };
    private static readonly float[] StartHours = { 0.24f, 0.50f, 0.74f, 0.95f };
    private static readonly string[] LengthNames = { "10 minutes", "20 minutes", "40 minutes" };
    private static readonly float[] LengthMinutes = { 10f, 20f, 40f };

    private Camera eye;
    private TitleLogo logo;
    private TitlePad pad;
    private Quaternion baseRotation;
    private bool viewSet, leaving;
    private RawImage curtain;
    private float curtainAlpha = 1f;

    /// <summary>The name in blocks at the top, for the probes.</summary>
    public TitleLogo Logo => logo;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        Build();
        ShowWorlds();
        Settings.Apply();
        AudioListener.volume = 0f;   // the sound comes up with the picture
    }

    private IEnumerator Start()
    {
        // the world's own Start has run by the next frame: the camera is
        // stood on the sand, the hour and the weather held, the name put up
        yield return null;

        eye = Camera.main;
        if (eye != null)
        {
            var follow = eye.GetComponent<SimpleFollowCamera>();
            if (follow != null) follow.enabled = false;

            Viewpoint(Seed, out var at, out var look, out _);
            eye.transform.position = at;
            baseRotation = Quaternion.LookRotation(look - at);
            eye.transform.rotation = baseRotation;
            eye.fieldOfView = 58f;
            eye.farClipPlane = 3000f;
            eye.cullingMask &= ~(1 << TitleLogo.Layer);
            viewSet = true;
        }

        logo = TitleLogo.Make(canvasRoot);
        Look(WhenNow());

        // a wave due as the picture comes up
        Viewpoint(Seed, out _, out _, out _, out var strand, out var toSea);
        Surf.WaveDue(strand.x, strand.y, Seed, 3.8f);

        pad = gameObject.AddComponent<TitlePad>();

        yield return new WaitForSecondsRealtime(0.6f);
        Life(strand, toSea);

        // the curtain: held a moment while the near country builds, then lifted, the sound with it
        yield return new WaitForSecondsRealtime(0.6f);
        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 2.2f)
        {
            Curtain(1f - Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }
        Curtain(0f);
    }

    /// <summary>Sets the hour and the sky for one of the titles: morning, afternoon, dusk or night.</summary>
    public void Look(string when)
    {
        float hour, clouds;
        switch (when)
        {
            case "morning": hour = 0.30f; clouds = 0.22f; break;
            case "dusk": hour = 0.745f; clouds = 0.50f; break;
            case "night": hour = 0.96f; clouds = 0.25f; break;
            default: when = "afternoon"; hour = 0.69f; clouds = 0.45f; break;
        }
        When = when;

        var tod = TimeOfDay.Instance;
        if (tod != null)
        {
            tod.SetTime(hour);
            tod.Paused = true;
            tod.ForceOvercast(clouds);
        }

        // no sun on the letters after dark: they keep a little light of their own
        float daylight = Mathf.Clamp01(Mathf.Min((hour - 0.22f) / 0.06f, (0.78f - hour) / 0.06f));
        if (logo != null) logo.SetDaylight(daylight);
    }

    /// <summary>A few of the beach's own on it: crabs on the sand, a heron in the shallows.</summary>
    private void Life(Vector2Int strand, Vector3 toSea)
    {
        Vector3 tile = new Vector3(strand.x * WorldGrid.TileSize, WaterSurface.Level, strand.y * WorldGrid.TileSize);
        Vector3 along = Vector3.Cross(Vector3.up, toSea);
        Vector3[] sand = { tile - toSea * 1.5f + along * 3.5f, tile - toSea * 2.5f - along * 4f };
        foreach (var at in sand)
        {
            int tx = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
            var crab = Wildlife.Summon(FaunaKind.Crab, new Vector3(at.x, WorldHeight.SurfaceY(tx, tz, Seed) + 0.1f, at.z));
            if (crab != null) crab.Direct("graze");
        }
        var heron = Wildlife.Summon(FaunaKind.Heron, tile + toSea * 9f + along * 2f);
        if (heron != null) heron.Direct("rest");
    }

    private void Curtain(float alpha)
    {
        curtainAlpha = alpha;
        if (curtain != null) curtain.color = new Color(0f, 0f, 0f, alpha);
        AudioListener.volume = Settings.Volume * (1f - alpha);
    }

    /// <summary>How dark the curtain is, 0 to 1, for the probes.</summary>
    public float CurtainAlpha => curtainAlpha;

    /// <summary>Out through the curtain: the picture and the sound go down together, then the thing is done.</summary>
    private IEnumerator Leave(System.Action then)
    {
        leaving = true;
        float from = curtainAlpha;
        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.7f)
        {
            Curtain(Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, f)));
            yield return null;
        }
        Curtain(1f);
        then();
    }

    private void LateUpdate()
    {
        if (eye == null || !viewSet) return;

        // the view leans a little toward the cursor, and rests when the window is not looked at
        Vector2 m = Vector2.zero;
        if (Application.isFocused && Screen.width > 0 && Screen.height > 0)
        {
            m = new Vector2(Mathf.Clamp(Input.mousePosition.x / Screen.width - 0.5f, -0.5f, 0.5f), Mathf.Clamp(Input.mousePosition.y / Screen.height - 0.5f, -0.5f, 0.5f));
        }
        var target = baseRotation * Quaternion.Euler(-m.y * 1.6f, m.x * 2.4f, 0f);
        eye.transform.rotation = Quaternion.Slerp(eye.transform.rotation, target, 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));
    }

    private void ShowOptions()
    {
        worldsPage.SetActive(false);
        newPage.SetActive(false);
        optionsPage.SetActive(true);
        heading.text = "<size=30><b>OPTIONS</b></size>\n<size=17><color=#8B7860>kept between runs</color></size>";
        Options();
    }

    private void Options()
    {
        volumeLabel.text = Mathf.RoundToInt(Settings.Volume * 100f) + "%";
        radiusLabel.text = Settings.ViewRadius + " chunks (" + (Settings.ViewRadius * WorldGrid.ChunkWorldSize) + " m)";
        lookLabel.text = Settings.LookSpeed.ToString("F1") + "x";
        fullLabel.text = Settings.Fullscreen ? "on" : "off";
    }

    private void Update()
    {
        // the cursor is the menu's, whatever the player's controls think
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;

        // Escape from the new world page goes back; from the list, nothing
        if (Input.GetKeyDown(KeyCode.Escape) && ((newPage != null && newPage.activeSelf) || (optionsPage != null && optionsPage.activeSelf))) ShowWorlds();

        // on the list, the keys do what the mouse does: up and down choose, Enter plays
        if (worldsPage != null && worldsPage.activeSelf && !leaving)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Enter(chosen);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Choose(1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Choose(-1);
        }
    }

    /// <summary>Moves the choice up or down the list.</summary>
    private void Choose(int step)
    {
        var worlds = WorldLibrary.All();
        if (worlds.Count == 0) return;
        int i = chosen != null ? worlds.FindIndex(w => w.id == chosen.id) : 0;
        i = Mathf.Clamp(i + step, 0, Mathf.Min(worlds.Count, 6) - 1);
        chosen = worlds[i];
        pendingForget = null;
        Refresh();
    }

    // -------------------------------------------------------------- pages

    private void ShowWorlds()
    {
        worldsPage.SetActive(true);
        newPage.SetActive(false);
        if (optionsPage != null) optionsPage.SetActive(false);
        heading.text = "<size=30><b>YOUR WORLDS</b></size>\n<size=17><color=#8B7860>select a world</color></size>";
        Refresh();
    }

    private void ShowNew()
    {
        worldsPage.SetActive(false);
        newPage.SetActive(true);
        if (optionsPage != null) optionsPage.SetActive(false);
        heading.text = "<size=30><b>NEW WORLD</b></size>\n<size=17><color=#8B7860>world settings</color></size>";
        nameField.text = "";
        seedField.text = Random.Range(1, 99999999).ToString();
        NewWorldSettings();
    }

    private void Refresh()
    {
        foreach (var row in rows) Destroy(row);
        rows.Clear();

        var worlds = WorldLibrary.All();

        // what was played last first, and chosen, unless something else is
        if (chosen == null || !worlds.Exists(w => w.id == chosen.id))
        {
            chosen = worlds.Find(w => w.id == WorldLibrary.LastId) ?? (worlds.Count > 0 ? worlds[0] : null);
        }

        float y = 166f;
        const int Shown = 6;

        if (worlds.Count == 0)
        {
            var none = Label("None", worldsPage.transform, 19f, new Vector2(-190f, 100f), new Vector2(680f, 80f));
            none.text = "No worlds yet.\n<size=80%>Create one and it will show up here.</size>";
            none.color = ParchmentPanel.InkFaint;
            rows.Add(none.gameObject);
        }

        for (int i = 0; i < worlds.Count && i < Shown; i++)
        {
            Row(worlds[i], y);
            y -= 72f;
        }

        if (worlds.Count > Shown)
        {
            var more = Label("More", worldsPage.transform, 15f, new Vector2(-190f, y + 20f), new Vector2(680f, 26f));
            more.text = "and " + (worlds.Count - Shown) + " older, kept but not shown";
            more.color = ParchmentPanel.InkFaint;
            rows.Add(more.gameObject);
        }

        playLabel.text = chosen != null ? "Play  " + Short(chosen.name, 16) : "Play";
        forgetLabel.text = chosen != null && pendingForget == chosen.id ? "Press again to delete" : "Delete world";
    }

    private void Row(WorldSave world, float y)
    {
        bool picked = chosen != null && chosen.id == world.id;

        var go = new GameObject("World " + world.name);
        go.transform.SetParent(worldsPage.transform, false);
        rows.Add(go);

        var image = go.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = picked ? new Color(0.29f, 0.24f, 0.17f, 0.26f) : new Color(0.29f, 0.24f, 0.17f, 0.08f);
        Centre(go.GetComponent<RectTransform>(), new Vector2(-190f, y), new Vector2(680f, 64f));

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colours = button.colors; colours.highlightedColor = new Color(1f, 1f, 1f, 1.4f); button.colors = colours;
        button.onClick.AddListener(() => { if (chosen != null && chosen.id == world.id) Enter(world); else { chosen = world; pendingForget = null; Refresh(); } });

        var text = Label("Text", go.transform, 19f, new Vector2(14f, 0f), new Vector2(640f, 60f));
        text.alignment = TextAlignmentOptions.Left;
        string setup = (world.weather ? "" : "no weather · ") + (world.dayCycle ? "" : "no day cycle · ") + (world.animals ? "" : "no animals · ") + (world.ruins ? "" : "no ruins · ");
        text.text = "<b>" + world.name + "</b>"
                  + "\n<size=78%><color=#8B7860>seed " + world.seed
                  + "  ·  " + world.Charted + " charted  ·  " + world.Landmarks + " found"
                  + "  ·  " + setup + WorldLibrary.Ago(world.lastPlayedUtc) + "</color></size>";
    }

    private void Enter(WorldSave world)
    {
        if (world == null || leaving) return;
        StartCoroutine(Leave(() => WorldLibrary.Enter(world)));
    }

    private static string Short(string name, int most)
        => string.IsNullOrEmpty(name) ? "" : name.Length <= most ? name : name.Substring(0, most - 1) + "\u2026";

    private void Forget()
    {
        if (chosen == null) return;
        if (pendingForget != chosen.id) { pendingForget = chosen.id; Refresh(); return; }
        WorldLibrary.Delete(chosen.id);
        pendingForget = null;
        chosen = null;
        Refresh();
    }

    private void CreateAndPlay()
    {
        int seed = 0;
        if (!string.IsNullOrWhiteSpace(seedField.text))
        {
            if (!int.TryParse(seedField.text.Trim(), out seed)) seed = Mathf.Abs(seedField.text.Trim().GetHashCode());
            if (seed == 0) seed = 1;
        }
        if (leaving) return;
        var world = WorldLibrary.Create(nameField.text, seed, weather, dayCycle, StartHours[startAt], LengthMinutes[dayLength], animals, ruins);
        Enter(world);
    }

    private void NewWorldSettings()
    {
        weatherLabel.text = weather ? "on" : "off";
        cycleLabel.text = dayCycle ? "on" : "off";
        startLabel.text = StartNames[startAt];
        lengthLabel.text = LengthNames[dayLength];
        animalsLabel.text = animals ? "on" : "off";
        ruinsLabel.text = ruins ? "on" : "off";
        lengthLabel.color = dayCycle ? ParchmentPanel.Paper : new Color(0.9f, 0.85f, 0.75f, 0.45f);
    }

    private void Quit()
    {
        if (leaving) return;
        StartCoroutine(Leave(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }));
    }

    // ---------------------------------------------------------------- build

    private void Build()
    {
        var canvasGo = new GameObject("Title Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        // taken after the Canvas is added: adding it swaps the Transform for
        // a RectTransform, and a reference taken before that points at nothing
        canvasRoot = canvasGo.transform;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // a light shade toward the edges, so the paper reads and the country still shows
        var shadeGo = new GameObject("Shade");
        shadeGo.transform.SetParent(canvasGo.transform, false);
        var shade = shadeGo.AddComponent<RawImage>();
        shade.texture = ParchmentPanel.Shadow(64, 64);
        shade.color = new Color(0f, 0f, 0f, 0.26f);
        shade.raycastTarget = false;
        Stretch(shadeGo.GetComponent<RectTransform>());

        // the paper, under the name
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(canvasGo.transform, false);
        var paper = cardGo.AddComponent<RawImage>();
        paper.texture = ParchmentPanel.Create(1120, 640);
        card = cardGo.transform;
        Centre(cardGo.GetComponent<RectTransform>(), new Vector2(0f, -100f), new Vector2(1120f, 640f));
        ParchmentPanel.Shade(cardGo.GetComponent<RectTransform>(), 46f);

        heading = Label("Heading", card, 24f, new Vector2(-190f, 256f), new Vector2(680f, 90f));
        heading.alignment = TextAlignmentOptions.Left;

        // ---- the worlds page
        worldsPage = new GameObject("Worlds");
        worldsPage.transform.SetParent(card, false);
        Stretch(worldsPage.AddComponent<RectTransform>());

        playLabel = Button("Play", worldsPage.transform, new Vector2(370f, 160f), new Vector2(300f, 58f), () => Enter(chosen));
        Button("New world", worldsPage.transform, new Vector2(370f, 90f), new Vector2(300f, 58f), ShowNew);
        forgetLabel = Button("Delete world", worldsPage.transform, new Vector2(370f, 20f), new Vector2(300f, 50f), Forget);
        Button("Options", worldsPage.transform, new Vector2(370f, -170f), new Vector2(300f, 50f), ShowOptions);
        Button("Quit", worldsPage.transform, new Vector2(370f, -240f), new Vector2(300f, 50f), Quit);

        var hint = Label("Hint", worldsPage.transform, 15f, new Vector2(-190f, -280f), new Vector2(680f, 30f));
        hint.text = "Select a world and press Play, or double click it.";
        hint.color = ParchmentPanel.InkFaint;

        // ---- the new world page
        newPage = new GameObject("New");
        newPage.transform.SetParent(card, false);
        Stretch(newPage.AddComponent<RectTransform>());

        nameField = Field("Name", "World name (optional)", newPage.transform, new Vector2(-150f, 180f), 28);
        seedField = Field("Seed", "Seed (leave blank for random)", newPage.transform, new Vector2(-150f, 120f), 16);
        Button("Random seed", newPage.transform, new Vector2(230f, 120f), new Vector2(200f, 46f), () => seedField.text = Random.Range(1, 99999999).ToString());

        float y = 46f;
        weatherLabel = Setting(newPage.transform, "Weather", ref y, () => { weather = !weather; NewWorldSettings(); });
        cycleLabel = Setting(newPage.transform, "Day cycle", ref y, () => { dayCycle = !dayCycle; NewWorldSettings(); });
        startLabel = Setting(newPage.transform, "Start time", ref y, () => { startAt = (startAt + 1) % StartNames.Length; NewWorldSettings(); });
        lengthLabel = Setting(newPage.transform, "Day length", ref y, () => { dayLength = (dayLength + 1) % LengthNames.Length; NewWorldSettings(); });
        animalsLabel = Setting(newPage.transform, "Animals", ref y, () => { animals = !animals; NewWorldSettings(); });
        ruinsLabel = Setting(newPage.transform, "Ruins", ref y, () => { ruins = !ruins; NewWorldSettings(); });

        Button("Create world", newPage.transform, new Vector2(-150f, -284f), new Vector2(360f, 56f), CreateAndPlay);
        Button("Back", newPage.transform, new Vector2(230f, -284f), new Vector2(200f, 56f), ShowWorlds);

        // ---- the options page
        optionsPage = new GameObject("Options");
        optionsPage.transform.SetParent(card, false);
        Stretch(optionsPage.AddComponent<RectTransform>());

        float oy = 150f;
        volumeLabel = Adjuster(optionsPage.transform, "Volume", ref oy, () => Settings.Volume -= 0.1f, () => Settings.Volume += 0.1f);
        radiusLabel = Adjuster(optionsPage.transform, "View distance", ref oy, () => Settings.ViewRadius -= 1, () => Settings.ViewRadius += 1);
        lookLabel = Adjuster(optionsPage.transform, "Mouse look speed", ref oy, () => Settings.LookSpeed -= 0.1f, () => Settings.LookSpeed += 0.1f);
        fullLabel = Adjuster(optionsPage.transform, "Fullscreen", ref oy, () => Settings.Fullscreen = !Settings.Fullscreen, () => Settings.Fullscreen = !Settings.Fullscreen);

        var note = Label("Note", optionsPage.transform, 15f, new Vector2(0f, -140f), new Vector2(820f, 60f));
        note.text = "View distance takes effect when a world is entered. Further is slower.";
        note.color = ParchmentPanel.InkFaint;

        Button("Back", optionsPage.transform, new Vector2(0f, -284f), new Vector2(240f, 56f), ShowWorlds);
        optionsPage.SetActive(false);

        // the build's number, small, in the corner
        var version = Label("Version", canvasGo.transform, 13f, Vector2.zero, new Vector2(420f, 22f));
        var vr = version.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f); vr.pivot = new Vector2(1f, 0f); vr.anchoredPosition = new Vector2(-18f, 12f);
        version.alignment = TextAlignmentOptions.Right;
        version.color = new Color(1f, 1f, 1f, 0.55f);
        version.text = "Tile World " + Application.version;

        // the curtain, over everything but the name: black until the country is ready
        var curtainGo = new GameObject("Curtain", typeof(RectTransform));
        curtainGo.transform.SetParent(canvasGo.transform, false);
        curtain = curtainGo.AddComponent<RawImage>();
        curtain.texture = Texture2D.whiteTexture;
        curtain.color = Color.black;
        curtain.raycastTarget = false;
        Stretch(curtainGo.GetComponent<RectTransform>());
    }

    /// <summary>One setting with a less and a more either side of its value.</summary>
    private TMP_Text Adjuster(Transform page, string name, ref float y, UnityEngine.Events.UnityAction less, UnityEngine.Events.UnityAction more)
    {
        var label = Label(name, page, 19f, new Vector2(-300f, y), new Vector2(300f, 40f));
        label.alignment = TextAlignmentOptions.Right;
        label.text = name;
        Button("−", page, new Vector2(-90f, y), new Vector2(56f, 44f), () => { less(); Options(); });
        var value = Label(name + " value", page, 19f, new Vector2(60f, y), new Vector2(220f, 40f));
        Button("+", page, new Vector2(210f, y), new Vector2(56f, 44f), () => { more(); Options(); });
        y -= 58f;
        return value;
    }

    /// <summary>One setting: its name on the left, what it is set to on a button on the right.</summary>
    private TMP_Text Setting(Transform page, string name, ref float y, UnityEngine.Events.UnityAction toggle)
    {
        var label = Label(name, page, 19f, new Vector2(-300f, y), new Vector2(300f, 40f));
        label.alignment = TextAlignmentOptions.Right;
        label.text = name;
        var value = Button("", page, new Vector2(60f, y), new Vector2(360f, 44f), toggle);
        y -= 54f;
        return value;
    }

    // -------------------------------------------------------------- pieces

    private static void Centre(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = at;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private TMP_Text Label(string name, Transform parent, float size, Vector2 at, Vector2 area)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = ParchmentPanel.Ink;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        Centre(go.GetComponent<RectTransform>(), at, area);
        return text;
    }

    private TMP_Text Button(string text, Transform parent, Vector2 at, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Button " + text);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = new Color(0.29f, 0.24f, 0.17f, 0.82f);
        Centre(go.GetComponent<RectTransform>(), at, size);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colours = button.colors;
        colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colours;
        button.onClick.AddListener(action);
        var label = Label("Label", go.transform, 19f, Vector2.zero, size);
        label.color = ParchmentPanel.Paper;
        label.text = text;
        return label;
    }

    private TMP_InputField Field(string name, string placeholder, Transform parent, Vector2 at, int limit)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        var background = go.AddComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0.98f, 0.96f, 0.90f, 0.75f);
        Centre(go.GetComponent<RectTransform>(), at, new Vector2(520f, 46f));

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(go.transform, false);
        var viewport = viewportGo.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12f, 4f); viewport.offsetMax = new Vector2(-12f, -4f);
        viewportGo.AddComponent<RectMask2D>();

        var text = Stretched("Text", viewport, ParchmentPanel.Ink);
        var hint = Stretched("Placeholder", viewport, ParchmentPanel.InkFaint);
        hint.text = placeholder;

        var field = go.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = hint;
        field.fontAsset = font;
        field.pointSize = 19f;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = limit;
        field.selectionColor = new Color(0.29f, 0.24f, 0.17f, 0.35f);
        field.caretColor = ParchmentPanel.Ink;
        field.customCaretColor = true;
        field.text = "";
        go.SetActive(true);
        return field;
    }

    private TextMeshProUGUI Stretched(string name, RectTransform parent, Color colour)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 19f;
        text.color = colour;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        Stretch(go.GetComponent<RectTransform>());
        return text;
    }
}
