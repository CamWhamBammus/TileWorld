using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title: the game opens here, outside any world, and asks which. A
/// list of the worlds kept, with what each has come to; a page for making
/// a new one, with how it should be set up -- name, seed, weather or none,
/// a day that turns or a still sky, the hour it starts at, how long a day
/// is, whether there are animals and ruins in it at all. Choosing enters
/// the world; the pause menu comes back here.
///
/// Drawn the way the book is, on paper, over a photograph of the country.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    /// <summary>Whether the title is up, so nothing of the game's own wakes under it.</summary>
    public static bool IsUp { get; private set; }

    private TMP_FontAsset font;
    private Transform card;
    private GameObject worldsPage, newPage;
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

    private void Awake()
    {
        IsUp = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        Build();
        ShowWorlds();
    }

    private void OnDestroy() { IsUp = false; }

    private void Update()
    {
        // Escape from the new world page goes back; from the list, nothing
        if (Input.GetKeyDown(KeyCode.Escape) && newPage != null && newPage.activeSelf) ShowWorlds();
    }

    // -------------------------------------------------------------- pages

    private void ShowWorlds()
    {
        worldsPage.SetActive(true);
        newPage.SetActive(false);
        heading.text = "<size=30><b>TILE WORLD</b></size>\n<size=17><color=#8B7860>a survey of an empty country</color></size>";
        Refresh();
    }

    private void ShowNew()
    {
        worldsPage.SetActive(false);
        newPage.SetActive(true);
        heading.text = "<size=30><b>NEW WORLD</b></size>\n<size=17><color=#8B7860>how it should be set up</color></size>";
        nameField.text = "";
        seedField.text = Random.Range(1, 99999999).ToString();
        Settings();
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

        float y = 186f;
        const int Shown = 6;

        if (worlds.Count == 0)
        {
            var none = Label("None", worldsPage.transform, 19f, new Vector2(-190f, 120f), new Vector2(680f, 80f));
            none.text = "No worlds yet.\n<size=80%>Make one, and it will be kept here.</size>";
            none.color = ParchmentPanel.InkFaint;
            rows.Add(none.gameObject);
        }

        for (int i = 0; i < worlds.Count && i < Shown; i++)
        {
            Row(worlds[i], y);
            y -= 76f;
        }

        if (worlds.Count > Shown)
        {
            var more = Label("More", worldsPage.transform, 15f, new Vector2(-190f, y + 20f), new Vector2(680f, 26f));
            more.text = "and " + (worlds.Count - Shown) + " older, kept but not shown";
            more.color = ParchmentPanel.InkFaint;
            rows.Add(more.gameObject);
        }

        playLabel.text = chosen != null ? "Play" : "Play";
        forgetLabel.text = chosen != null && pendingForget == chosen.id ? "Press again to forget it" : "Forget this world";
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
        Centre(go.GetComponent<RectTransform>(), new Vector2(-190f, y), new Vector2(680f, 68f));

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colours = button.colors; colours.highlightedColor = new Color(1f, 1f, 1f, 1.4f); button.colors = colours;
        button.onClick.AddListener(() => { if (chosen != null && chosen.id == world.id) Enter(world); else { chosen = world; pendingForget = null; Refresh(); } });

        var text = Label("Text", go.transform, 19f, new Vector2(14f, 0f), new Vector2(640f, 64f));
        text.alignment = TextAlignmentOptions.Left;
        string setup = (world.weather ? "" : "no weather · ") + (world.dayCycle ? "" : "still sky · ") + (world.animals ? "" : "no animals · ") + (world.ruins ? "" : "no ruins · ");
        text.text = "<b>" + world.name + "</b>"
                  + "\n<size=78%><color=#8B7860>seed " + world.seed
                  + "  ·  " + world.Charted + " charted  ·  " + world.Landmarks + " found"
                  + "  ·  " + setup + WorldLibrary.Ago(world.lastPlayedUtc) + "</color></size>";
    }

    private void Enter(WorldSave world)
    {
        if (world == null) return;
        WorldLibrary.Enter(world);
    }

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
        var world = WorldLibrary.Create(nameField.text, seed, weather, dayCycle, StartHours[startAt], LengthMinutes[dayLength], animals, ruins);
        WorldLibrary.Enter(world);
    }

    private void Settings()
    {
        weatherLabel.text = weather ? "weather comes and goes" : "always clear";
        cycleLabel.text = dayCycle ? "the sun moves" : "the sky stands still";
        startLabel.text = StartNames[startAt];
        lengthLabel.text = LengthNames[dayLength];
        animalsLabel.text = animals ? "animals live here" : "no animals";
        ruinsLabel.text = ruins ? "ruins stand here" : "no ruins";
        lengthLabel.color = dayCycle ? ParchmentPanel.Paper : new Color(0.9f, 0.85f, 0.75f, 0.45f);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- build

    private void Build()
    {
        var canvasGo = new GameObject("Title Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // the country behind, darkened toward the edges
        var backGo = new GameObject("Backdrop");
        backGo.transform.SetParent(canvasGo.transform, false);
        var back = backGo.AddComponent<RawImage>();
        var photo = Resources.Load<Texture2D>("Title/backdrop");
        back.texture = photo != null ? photo : Texture2D.blackTexture;
        back.color = new Color(0.62f, 0.62f, 0.62f, 1f);
        Stretch(backGo.GetComponent<RectTransform>());
        var shadeGo = new GameObject("Shade");
        shadeGo.transform.SetParent(canvasGo.transform, false);
        var shade = shadeGo.AddComponent<RawImage>();
        shade.texture = ParchmentPanel.Shadow(64, 64);
        shade.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(shadeGo.GetComponent<RectTransform>());

        // the paper
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(canvasGo.transform, false);
        var paper = cardGo.AddComponent<RawImage>();
        paper.texture = ParchmentPanel.Create(1120, 720);
        card = cardGo.transform;
        Centre(cardGo.GetComponent<RectTransform>(), new Vector2(0f, -20f), new Vector2(1120f, 720f));
        ParchmentPanel.Shade(cardGo.GetComponent<RectTransform>(), 46f);

        heading = Label("Heading", card, 24f, new Vector2(-190f, 296f), new Vector2(680f, 90f));
        heading.alignment = TextAlignmentOptions.Left;

        // ---- the worlds page
        worldsPage = new GameObject("Worlds");
        worldsPage.transform.SetParent(card, false);
        Stretch(worldsPage.AddComponent<RectTransform>());

        var kept = Label("Kept", worldsPage.transform, 16f, new Vector2(-190f, 236f), new Vector2(680f, 26f));
        kept.alignment = TextAlignmentOptions.Left;
        kept.text = "WORLDS KEPT";
        kept.color = ParchmentPanel.InkFaint;

        playLabel = Button("Play", worldsPage.transform, new Vector2(370f, 200f), new Vector2(300f, 58f), () => Enter(chosen));
        Button("New world", worldsPage.transform, new Vector2(370f, 130f), new Vector2(300f, 58f), ShowNew);
        forgetLabel = Button("Forget this world", worldsPage.transform, new Vector2(370f, 60f), new Vector2(300f, 50f), Forget);
        Button("Quit", worldsPage.transform, new Vector2(370f, -230f), new Vector2(300f, 50f), Quit);

        var hint = Label("Hint", worldsPage.transform, 15f, new Vector2(-190f, -300f), new Vector2(680f, 30f));
        hint.text = "Choose a world and press Play, or press its name twice.  Everything in a world comes from its seed.";
        hint.color = ParchmentPanel.InkFaint;

        // ---- the new world page
        newPage = new GameObject("New");
        newPage.transform.SetParent(card, false);
        Stretch(newPage.AddComponent<RectTransform>());

        nameField = Field("Name", "a name, or leave it to the land", newPage.transform, new Vector2(-150f, 220f), 28);
        seedField = Field("Seed", "a seed, or leave it to chance", newPage.transform, new Vector2(-150f, 160f), 16);
        Button("another seed", newPage.transform, new Vector2(230f, 160f), new Vector2(200f, 46f), () => seedField.text = Random.Range(1, 99999999).ToString());

        float y = 80f;
        weatherLabel = Setting(newPage.transform, "Weather", ref y, () => { weather = !weather; Settings(); });
        cycleLabel = Setting(newPage.transform, "Day and night", ref y, () => { dayCycle = !dayCycle; Settings(); });
        startLabel = Setting(newPage.transform, "Starts at", ref y, () => { startAt = (startAt + 1) % StartNames.Length; Settings(); });
        lengthLabel = Setting(newPage.transform, "A day lasts", ref y, () => { dayLength = (dayLength + 1) % LengthNames.Length; Settings(); });
        animalsLabel = Setting(newPage.transform, "Animals", ref y, () => { animals = !animals; Settings(); });
        ruinsLabel = Setting(newPage.transform, "Ruins", ref y, () => { ruins = !ruins; Settings(); });

        Button("Create and play", newPage.transform, new Vector2(-150f, -280f), new Vector2(360f, 58f), CreateAndPlay);
        Button("Back", newPage.transform, new Vector2(230f, -280f), new Vector2(200f, 58f), ShowWorlds);
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
