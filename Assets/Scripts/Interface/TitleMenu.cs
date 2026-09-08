using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    /// <summary>A seed being looked at from the new world page, or 0; the backdrop is built from it.</summary>
    public static int PreviewSeed { get; private set; }

    /// <summary>The seed the backdrop is built from.</summary>
    public static int BackdropSeed => PreviewSeed != 0 ? PreviewSeed : Seed;

    /// <summary>The new world page's fields, kept across the reload a preview takes.</summary>
    private class PageStash { public string Name, SeedText; public bool Weather, DayCycle, Animals, Ruins; public int StartAt, DayLength; }
    private static PageStash stash;

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

        if (!found)
        {
            // no beach near the origin: the country from a little above its origin
            eye = new Vector3(0f, WorldHeight.SurfaceY(0, 0, seed) + 4f, 0f);
            look = new Vector3(0f, WorldHeight.SurfaceY(0, 15, seed) + 1f, 30f);
            stand = eye;
            return;
        }

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
    private Animal heron;
    private CanvasGroup cardGroup;
    private RectTransform cardRect;
    private int offset;                       // the first world shown, when there are more than fit
    private float escapeAt = -10f, baseClouds = 0.45f;
    private TMP_Text escapeHint, previewLabel, musicLabel;
    private readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();
    private GameObject renamePage, controlsPage;
    private TMP_InputField renameField;
    private string deletedId; private float deletedAt = -100f;
    private CanvasGroup shadeGroup;
    private Vector3 lastMouse; private float lastTouch;
    private readonly Dictionary<string, TMP_Text> keyLabels = new Dictionary<string, TMP_Text>();
    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private bool listening;

    /// <summary>What the Controls page shows for a key, by name, for the probes.</summary>
    public string KeyShown(string name) => keyLabels.TryGetValue(name, out var label) ? label.text : "";

    /// <summary>How long the title waits untouched before the menu steps aside and the country stands alone.</summary>
    public static float IdleAfter = 75f;

    /// <summary>Whether the menu has stepped aside, for the probes.</summary>
    public bool Hidden { get; private set; }

    /// <summary>The first world shown, for the probes.</summary>
    public int Offset => offset;

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
        if (stash != null)
        {
            // back from a preview: the new page as it was left, over the seed's own country
            weather = stash.Weather; dayCycle = stash.DayCycle; animals = stash.Animals; ruins = stash.Ruins;
            startAt = stash.StartAt; dayLength = stash.DayLength;
            ShowNew();
            nameField.text = stash.Name; seedField.text = stash.SeedText;
            stash = null;
        }
        Settings.Apply();
        AudioListener.volume = 0f;   // the sound comes up with the picture
        lastTouch = Time.unscaledTime;
        lastMouse = Input.mousePosition;
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

            Viewpoint(BackdropSeed, out var at, out var look, out _);
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
        Viewpoint(BackdropSeed, out _, out _, out _, out var strand, out var toSea);
        Surf.WaveDue(strand.x, strand.y, BackdropSeed, 3.8f);

        pad = gameObject.AddComponent<TitlePad>();

        yield return new WaitForSecondsRealtime(0.6f);
        Life(strand, toSea);

        // the curtain: held a moment while the near country builds, then lifted, the sound with it
        yield return new WaitForSecondsRealtime(0.6f);
        StartCoroutine(CardIn());
        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 2.2f)
        {
            Curtain(1f - Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }
        Curtain(0f);
    }

    /// <summary>The paper comes up from a little below and settles, as the curtain lifts.</summary>
    private IEnumerator CardIn()
    {
        yield return new WaitForSecondsRealtime(0.9f);
        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / 0.7f)
        {
            float e = Mathf.SmoothStep(0f, 1f, f);
            cardGroup.alpha = e;
            cardRect.anchoredPosition = new Vector2(0f, -100f - 46f * (1f - e));
            yield return null;
        }
        cardGroup.alpha = 1f;
        cardRect.anchoredPosition = new Vector2(0f, -100f);
        cardGroup.interactable = cardGroup.blocksRaycasts = true;
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
        baseClouds = clouds;

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
        heron = Wildlife.Summon(FaunaKind.Heron, tile + toSea * 9f + along * 2f);
        if (heron != null) heron.Direct("rest");

        printsFrom = tile - toSea * 7f + along * 2.4f;
        printsToward = toSea;
        Prints();
        InvokeRepeating(nameof(Prints), 120f, 120f);
    }

    private Vector3 printsFrom, printsToward;

    /// <summary>Somebody walked down the sand and into the water: a line of boot prints, left and right.</summary>
    private void Prints()
    {
        Vector3 side = Vector3.Cross(Vector3.up, printsToward);
        float yaw = Quaternion.LookRotation(printsToward).eulerAngles.y;
        for (int i = 0; i < 12; i++)
        {
            Vector3 at = printsFrom + printsToward * (i * 0.62f) + side * (i % 2 == 0 ? 0.13f : -0.13f);
            int tx = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
            if (WaterSurface.IsUnderwater(tx, tz, BackdropSeed)) break;
            at.y = WorldHeight.SurfaceY(tx, tz, BackdropSeed);
            Tracks.Boot(at, yaw + Random.Range(-6f, 6f), BackdropSeed);
        }
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
        if (heron != null) heron.Direct("spook");
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
        if (renamePage != null) renamePage.SetActive(false);
        if (controlsPage != null) controlsPage.SetActive(false);
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
        musicLabel.text = Settings.TitleMusic ? "on" : "off";
    }

    private void Update()
    {
        // the cursor is the menu's, whatever the player's controls think
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;

        // the sky is never quite the same: the cloud thickens and thins over minutes
        var tod = TimeOfDay.Instance;
        if (tod != null && viewSet) tod.ForceOvercast(Mathf.Clamp(baseClouds + 0.15f * Mathf.Sin(Time.time / 75f), 0.08f, 0.53f));

        // left alone long enough, the menu steps aside and the country stands alone; anything brings it back
        bool touched = Input.anyKeyDown || (Input.mousePosition - lastMouse).sqrMagnitude > 1f || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
        lastMouse = Input.mousePosition;
        if (touched) { lastTouch = Time.unscaledTime; if (Hidden) StartCoroutine(StepAside(false)); }
        else if (!Hidden && !leaving && viewSet && Time.unscaledTime - lastTouch > IdleAfter) StartCoroutine(StepAside(true));

        // the deleted world can be brought back for ten seconds
        if (deletedId != null && forgetLabel != null)
        {
            float left = deletedAt + 10f - Time.unscaledTime;
            if (left <= 0f) { deletedId = null; forgetLabel.text = "Delete world"; }
            else forgetLabel.text = "Undo delete  (" + Mathf.CeilToInt(left) + ")";
        }

        // while a key is being chosen on the Controls page, the keys mean nothing else
        if (listening || rebinding != null) return;

        // Escape from any other page goes back; from the list, twice quits
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (worldsPage != null && !worldsPage.activeSelf) ShowWorlds();
            else Escape();
        }

        // Enter does the page's thing when nothing is being typed into
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool typing = selected != null && selected.GetComponent<TMP_InputField>() != null && selected.GetComponent<TMP_InputField>().isFocused;
        if (!typing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            if (newPage != null && newPage.activeSelf) CreateAndPlay();
            else if (renamePage != null && renamePage.activeSelf) ApplyRename();
        }
        if (worldsPage != null && worldsPage.activeSelf && Input.GetKeyDown(KeyCode.Delete)) Forget();
        if (escapeHint != null && escapeHint.gameObject.activeSelf && Time.unscaledTime > escapeAt + 2.5f) escapeHint.gameObject.SetActive(false);

        if (worldsPage != null && worldsPage.activeSelf && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) Scroll(Input.mouseScrollDelta.y < 0f ? 1 : -1);

        // on the list, the keys do what the mouse does: up and down choose, Enter plays
        if (worldsPage != null && worldsPage.activeSelf && !leaving)
        {
            if (!typing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Enter(chosen);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Choose(1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Choose(-1);
        }
    }

    /// <summary>Moves the choice up or down the list, and the list with it when it has to.</summary>
    private void Choose(int step)
    {
        var worlds = WorldLibrary.All();
        if (worlds.Count == 0) return;
        int i = chosen != null ? worlds.FindIndex(w => w.id == chosen.id) : 0;
        i = Mathf.Clamp(i + step, 0, worlds.Count - 1);
        chosen = worlds[i];
        pendingForget = null;
        if (i < offset) offset = i;
        if (i >= offset + Shown) offset = i - Shown + 1;
        Refresh();
    }

    /// <summary>Moves the list itself.</summary>
    private void Scroll(int step)
    {
        int count = WorldLibrary.All().Count;
        int was = offset;
        offset = Mathf.Clamp(offset + step, 0, Mathf.Max(0, count - Shown));
        if (offset != was) Refresh();
    }

    /// <summary>The menu fades and stops taking clicks, or comes back.</summary>
    private IEnumerator StepAside(bool aside)
    {
        Hidden = aside;
        cardGroup.interactable = cardGroup.blocksRaycasts = !aside;
        float from = cardGroup.alpha, to = aside ? 0f : 1f;
        for (float f = 0f; f < 1f; f += Time.unscaledDeltaTime / (aside ? 1.6f : 0.5f))
        {
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, f));
            cardGroup.alpha = a;
            if (shadeGroup != null) shadeGroup.alpha = a;
            if (Hidden != aside) yield break;
            yield return null;
        }
        cardGroup.alpha = to;
        if (shadeGroup != null) shadeGroup.alpha = to;
    }

    /// <summary>For the probes: as if something had been touched.</summary>
    private void Touched() { lastTouch = Time.unscaledTime; if (Hidden) StartCoroutine(StepAside(false)); }

    private void ShowRename()
    {
        if (chosen == null) return;
        worldsPage.SetActive(false); newPage.SetActive(false); optionsPage.SetActive(false); controlsPage.SetActive(false);
        renamePage.SetActive(true);
        heading.text = "<size=30><b>RENAME</b></size>\n<size=17><color=#8B7860>" + chosen.name + "</color></size>";
        renameField.text = chosen.name;
        renameField.Select();
    }

    private void ApplyRename()
    {
        if (chosen == null || string.IsNullOrWhiteSpace(renameField.text)) { ShowWorlds(); return; }
        WorldLibrary.Rename(chosen.id, renameField.text);
        ShowWorlds();
    }

    private void ShowControls()
    {
        worldsPage.SetActive(false); newPage.SetActive(false); optionsPage.SetActive(false); renamePage.SetActive(false);
        controlsPage.SetActive(true);
        heading.text = "<size=30><b>CONTROLS</b></size>\n<size=17><color=#8B7860>the keys, and what they do</color></size>";
        RefreshControls();
    }

    /// <summary>Every key button says what it is on now.</summary>
    private void RefreshControls()
    {
        foreach (var b in Keys.Actions) if (keyLabels.TryGetValue(b.Name, out var label)) label.text = Keys.Describe(b).ToUpperInvariant();
        foreach (var n in Keys.Legacy) if (keyLabels.TryGetValue(n.Name, out var label)) label.text = Keys.Get(n.Id).ToString();
    }

    /// <summary>One of the player's actions listens for its new key.</summary>
    private void Rebind(Keys.Bound b)
    {
        if (rebinding != null || listening) return;
        var action = Keys.Asset != null ? Keys.Asset.FindAction(b.Action) : null;
        int index = Keys.BindingIndex(b);
        if (action == null || index < 0) return;
        keyLabels[b.Name].text = "press a key";
        bool wasEnabled = action.enabled;
        action.Disable();
        rebinding = action.PerformInteractiveRebinding(index)
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Pointer>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => { op.Dispose(); rebinding = null; if (wasEnabled) action.Enable(); Keys.Save(); RefreshControls(); })
            .OnCancel(op => { op.Dispose(); rebinding = null; if (wasEnabled) action.Enable(); RefreshControls(); })
            .Start();
    }

    private static KeyCode[] allKeys;

    /// <summary>One of the keyboard keys listens for its new key: the next one pressed, Escape leaving it.</summary>
    private IEnumerator Listen(Keys.Named n)
    {
        if (rebinding != null || listening) yield break;
        if (allKeys == null) allKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
        listening = true;
        keyLabels[n.Name].text = "press a key";
        yield return null;
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) break;
            if (Input.anyKeyDown)
            {
                foreach (var k in allKeys)
                {
                    if (k == KeyCode.None || k == KeyCode.Escape || (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) || !Input.GetKeyDown(k)) continue;
                    Keys.Set(n.Id, k);
                    break;
                }
                break;
            }
            yield return null;
        }
        yield return null;   // the key that chose is not read again by the page
        listening = false;
        RefreshControls();
    }

    /// <summary>Escape on the list: once is a warning, twice within a moment is out.</summary>
    private void Escape()
    {
        if (leaving || worldsPage == null || !worldsPage.activeSelf) return;
        if (Time.unscaledTime < escapeAt + 2.5f) { Quit(); return; }
        escapeAt = Time.unscaledTime;
        if (escapeHint != null) escapeHint.gameObject.SetActive(true);
    }

    /// <summary>The country behind the menu becomes the seed on the new page: the scene is taken down and put up again with it.</summary>
    private void Preview()
    {
        if (leaving) return;
        int seed = ParseSeed();
        stash = new PageStash { Name = nameField.text, SeedText = seedField.text, Weather = weather, DayCycle = dayCycle, Animals = animals, Ruins = ruins, StartAt = startAt, DayLength = dayLength };
        PreviewSeed = seed;
        StartCoroutine(Leave(() => WorldLibrary.LeaveToMenu()));
    }

    private int ParseSeed()
    {
        int seed = 0;
        if (!string.IsNullOrWhiteSpace(seedField.text))
        {
            if (!int.TryParse(seedField.text.Trim(), out seed)) seed = Mathf.Abs(seedField.text.Trim().GetHashCode());
            if (seed == 0) seed = 1;
        }
        return seed != 0 ? seed : Random.Range(1, 99999999);
    }

    // -------------------------------------------------------------- pages

    private void ShowWorlds()
    {
        worldsPage.SetActive(true);
        newPage.SetActive(false);
        if (optionsPage != null) optionsPage.SetActive(false);
        if (renamePage != null) renamePage.SetActive(false);
        if (controlsPage != null) controlsPage.SetActive(false);
        heading.text = "<size=30><b>YOUR WORLDS</b></size>\n<size=17><color=#8B7860>select a world</color></size>";
        Refresh();
    }

    private void ShowNew()
    {
        worldsPage.SetActive(false);
        newPage.SetActive(true);
        if (optionsPage != null) optionsPage.SetActive(false);
        if (renamePage != null) renamePage.SetActive(false);
        if (controlsPage != null) controlsPage.SetActive(false);
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
        offset = Mathf.Clamp(offset, 0, Mathf.Max(0, worlds.Count - Shown));

        if (worlds.Count == 0)
        {
            var none = Label("None", worldsPage.transform, 19f, new Vector2(-190f, 100f), new Vector2(680f, 80f));
            none.text = "No worlds yet.\n<size=80%>Create one and it will show up here.</size>";
            none.color = ParchmentPanel.InkFaint;
            rows.Add(none.gameObject);
        }

        for (int i = offset; i < worlds.Count && i < offset + Shown; i++)
        {
            Row(worlds[i], y);
            y -= 72f;
        }

        // when there are more than fit, the list moves: the wheel, or the arrows past the end
        if (offset > 0)
        {
            var above = Label("Above", worldsPage.transform, 14f, new Vector2(-190f, 206f), new Vector2(680f, 22f));
            above.text = "\u25B2  " + offset + " more above";
            above.color = ParchmentPanel.InkFaint;
            rows.Add(above.gameObject);
        }
        if (worlds.Count > offset + Shown)
        {
            var below = Label("Below", worldsPage.transform, 14f, new Vector2(-190f, y + 22f), new Vector2(680f, 22f));
            below.text = "\u25BC  " + (worlds.Count - offset - Shown) + " more below";
            below.color = ParchmentPanel.InkFaint;
            rows.Add(below.gameObject);
        }

        playLabel.text = chosen != null ? "Play  " + Short(chosen.name, 16) : "Play";
        if (deletedId == null) forgetLabel.text = chosen != null && pendingForget == chosen.id ? "Delete " + Short(chosen.name, 12) + "?" : "Delete world";

        // the chosen world's picture, large, over the buttons
        var big = chosen != null ? PictureOf(chosen) : null;
        if (big != null)
        {
            var bigGo = new GameObject("Chosen picture", typeof(RectTransform));
            bigGo.transform.SetParent(worldsPage.transform, false);
            var bigImage = bigGo.AddComponent<RawImage>();
            bigImage.texture = big;
            bigImage.raycastTarget = false;
            Centre(bigGo.GetComponent<RectTransform>(), new Vector2(370f, 252f), new Vector2(220f, 124f));
            rows.Add(bigGo);
        }
    }

    /// <summary>Time played, said briefly.</summary>
    private static string Played(float seconds)
    {
        if (seconds < 60f) return "under a minute";
        int minutes = Mathf.RoundToInt(seconds / 60f);
        return minutes < 60 ? minutes + " m" : (minutes / 60) + " h " + (minutes % 60) + " m";
    }

    private const int Shown = 6;

    /// <summary>A world's picture, the last thing seen in it, read once from beside its save.</summary>
    private Texture2D PictureOf(WorldSave world)
    {
        if (pictures.TryGetValue(world.id, out var kept)) return kept;
        Texture2D tex = null;
        string path = WorldLibrary.PicturePath(world.id);
        if (System.IO.File.Exists(path))
        {
            try { tex = new Texture2D(2, 2, TextureFormat.RGB24, false); if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Destroy(tex); tex = null; } }
            catch { tex = null; }
        }
        pictures[world.id] = tex;
        return tex;
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

        // the last thing seen in it, at the row's end, when there is one
        var picture = PictureOf(world);
        float textWidth = 640f;
        if (picture != null)
        {
            var frameGo = new GameObject("Picture", typeof(RectTransform));
            frameGo.transform.SetParent(go.transform, false);
            var frame = frameGo.AddComponent<RawImage>();
            frame.texture = picture;
            frame.raycastTarget = false;
            Centre(frameGo.GetComponent<RectTransform>(), new Vector2(282f, 0f), new Vector2(100f, 56f));
            textWidth = 520f;
        }

        var text = Label("Text", go.transform, 19f, new Vector2(14f - (640f - textWidth) * 0.5f, 0f), new Vector2(textWidth, 60f));
        text.alignment = TextAlignmentOptions.Left;
        string setup = (world.weather ? "" : "no weather · ") + (world.dayCycle ? "" : "no day cycle · ") + (world.animals ? "" : "no animals · ") + (world.ruins ? "" : "no ruins · ");
        var startChunk = world.playerPosition != Vector3.zero ? WorldGrid.WorldToChunk(world.playerPosition) : Vector2Int.zero;
        string where = Regions.At(startChunk, world.seed).Name;
        text.text = "<b>" + world.name + "</b>  <size=72%><color=#8B7860>in " + where + "</color></size>"
                  + "\n<size=78%><color=#8B7860>seed " + world.seed
                  + "  ·  " + world.Charted + " charted  ·  " + world.Landmarks + " found"
                  + "  ·  " + Played(world.playedSeconds) + " played  ·  " + setup + WorldLibrary.Ago(world.lastPlayedUtc) + "</color></size>";
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
        // within ten seconds of a deletion, the same button brings the world back
        if (deletedId != null && Time.unscaledTime < deletedAt + 10f)
        {
            if (WorldLibrary.Undelete(deletedId)) { var back = WorldLibrary.All().Find(w => w.id == deletedId); if (back != null) chosen = back; }
            deletedId = null;
            Refresh();
            return;
        }
        if (chosen == null) return;
        if (pendingForget != chosen.id) { pendingForget = chosen.id; Refresh(); return; }
        WorldLibrary.Delete(chosen.id);
        deletedId = chosen.id; deletedAt = Time.unscaledTime;
        pendingForget = null;
        chosen = null;
        Refresh();
    }

    private void CreateAndPlay()
    {
        if (leaving) return;
        var world = WorldLibrary.Create(nameField.text, ParseSeed(), weather, dayCycle, StartHours[startAt], LengthMinutes[dayLength], animals, ruins);
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
        shadeGroup = shadeGo.AddComponent<CanvasGroup>();

        // the paper, under the name
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(canvasGo.transform, false);
        var paper = cardGo.AddComponent<RawImage>();
        paper.texture = ParchmentPanel.Create(1120, 640);
        card = cardGo.transform;
        cardRect = cardGo.GetComponent<RectTransform>();
        Centre(cardRect, new Vector2(0f, -146f), new Vector2(1120f, 640f));
        ParchmentPanel.Shade(cardRect, 46f);
        cardGroup = cardGo.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.interactable = cardGroup.blocksRaycasts = false;   // nothing on it can be pressed before it can be seen

        heading = Label("Heading", card, 24f, new Vector2(-190f, 256f), new Vector2(680f, 90f));
        heading.alignment = TextAlignmentOptions.Left;

        // ---- the worlds page
        worldsPage = new GameObject("Worlds");
        worldsPage.transform.SetParent(card, false);
        Stretch(worldsPage.AddComponent<RectTransform>());

        playLabel = Button("Play", worldsPage.transform, new Vector2(370f, 160f), new Vector2(300f, 58f), () => Enter(chosen));
        Button("New world", worldsPage.transform, new Vector2(370f, 90f), new Vector2(300f, 58f), ShowNew);
        forgetLabel = Button("Delete world", worldsPage.transform, new Vector2(370f, 20f), new Vector2(300f, 50f), Forget);
        Button("Rename world", worldsPage.transform, new Vector2(370f, -50f), new Vector2(300f, 50f), ShowRename);
        Button("Controls", worldsPage.transform, new Vector2(370f, -110f), new Vector2(300f, 50f), ShowControls);
        Button("Options", worldsPage.transform, new Vector2(370f, -170f), new Vector2(300f, 50f), ShowOptions);
        Button("Quit", worldsPage.transform, new Vector2(370f, -240f), new Vector2(300f, 50f), Quit);

        var hint = Label("Hint", worldsPage.transform, 15f, new Vector2(-190f, -280f), new Vector2(680f, 30f));
        hint.text = "Select a world and press Play, or double click it.";
        hint.color = ParchmentPanel.InkFaint;

        escapeHint = Label("Escape hint", worldsPage.transform, 15f, new Vector2(370f, -290f), new Vector2(300f, 30f));
        escapeHint.text = "Press Escape again to quit";
        escapeHint.color = ParchmentPanel.InkFaint;
        escapeHint.gameObject.SetActive(false);

        // ---- the new world page
        newPage = new GameObject("New");
        newPage.transform.SetParent(card, false);
        Stretch(newPage.AddComponent<RectTransform>());

        nameField = Field("Name", "World name (optional)", newPage.transform, new Vector2(-150f, 180f), 28);
        seedField = Field("Seed", "Seed (leave blank for random)", newPage.transform, new Vector2(-150f, 120f), 16);
        Button("Random seed", newPage.transform, new Vector2(230f, 120f), new Vector2(200f, 46f), () => seedField.text = Random.Range(1, 99999999).ToString());
        Button("Preview seed", newPage.transform, new Vector2(230f, 180f), new Vector2(200f, 46f), Preview);
        previewLabel = Label("Previewing", newPage.transform, 14f, new Vector2(230f, 214f), new Vector2(260f, 22f));
        previewLabel.color = ParchmentPanel.InkFaint;
        previewLabel.text = PreviewSeed != 0 ? "behind you: seed " + PreviewSeed : "";

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

        musicLabel = Adjuster(optionsPage.transform, "Title music", ref oy, () => Settings.TitleMusic = !Settings.TitleMusic, () => Settings.TitleMusic = !Settings.TitleMusic);

        var note = Label("Note", optionsPage.transform, 15f, new Vector2(0f, -150f), new Vector2(820f, 60f));
        note.text = "View distance takes effect when a world is entered. Further is slower.";
        note.color = ParchmentPanel.InkFaint;

        Button("Back", optionsPage.transform, new Vector2(0f, -284f), new Vector2(240f, 56f), ShowWorlds);
        optionsPage.SetActive(false);

        // ---- the rename page
        renamePage = new GameObject("Rename");
        renamePage.transform.SetParent(card, false);
        Stretch(renamePage.AddComponent<RectTransform>());
        renameField = Field("New name", "A new name for it", renamePage.transform, new Vector2(-100f, 120f), 28);
        Button("Save the name", renamePage.transform, new Vector2(-150f, -284f), new Vector2(360f, 56f), ApplyRename);
        Button("Back", renamePage.transform, new Vector2(230f, -284f), new Vector2(200f, 56f), ShowWorlds);
        renamePage.SetActive(false);

        // ---- the controls page
        controlsPage = new GameObject("Controls");
        controlsPage.transform.SetParent(card, false);
        Stretch(controlsPage.AddComponent<RectTransform>());
        var how = Label("How", controlsPage.transform, 15f, new Vector2(0f, 226f), new Vector2(900f, 26f));
        how.text = "Click a key to change it, then press the new one. Escape leaves it as it was.";
        how.color = ParchmentPanel.InkFaint;

        // the player's actions down the left, the keys read off the keyboard down the right
        for (int i = 0; i < Keys.Actions.Length; i++)
        {
            var b = Keys.Actions[i]; float ky = 186f - i * 40f;
            var name = Label(b.Name, controlsPage.transform, 19f, new Vector2(-330f, ky), new Vector2(220f, 34f));
            name.alignment = TextAlignmentOptions.Right; name.text = b.Name;
            keyLabels[b.Name] = Button("", controlsPage.transform, new Vector2(-140f, ky), new Vector2(130f, 34f), () => Rebind(b));
        }
        for (int i = 0; i < Keys.Legacy.Length; i++)
        {
            var n = Keys.Legacy[i]; float ky = 186f - i * 40f;
            var name = Label(n.Name, controlsPage.transform, 19f, new Vector2(150f, ky), new Vector2(240f, 34f));
            name.alignment = TextAlignmentOptions.Right; name.text = n.Name;
            keyLabels[n.Name] = Button("", controlsPage.transform, new Vector2(350f, ky), new Vector2(130f, 34f), () => StartCoroutine(Listen(n)));
        }
        var rest = Label("Rest of it", controlsPage.transform, 15f, new Vector2(0f, -150f), new Vector2(900f, 90f));
        rest.text = "Look is the mouse. Walk into deep water to swim. Scroll zooms while drawing; on the map, click to place a marker and right click to take it away. "
                  + "Escape closes what is open, or pauses; the pause menu has Main menu. "
                  + "On this page: the arrows and Enter choose and play a world, Delete deletes it, Escape twice quits.";
        rest.color = ParchmentPanel.InkFaint;
        Button("Reset to defaults", controlsPage.transform, new Vector2(-150f, -284f), new Vector2(360f, 56f), () => { Keys.ResetAll(); RefreshControls(); });
        Button("Back", controlsPage.transform, new Vector2(230f, -284f), new Vector2(200f, 56f), ShowWorlds);
        controlsPage.SetActive(false);

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
        MenuSounds.Attach(button);
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
