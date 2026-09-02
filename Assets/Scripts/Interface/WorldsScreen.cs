using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shelf of worlds, on the same paper as the map. A world is a seed and
/// what you have found in it, so making one is cheap and keeping several costs
/// nothing: the point of this screen is that leaving one to try another no
/// longer means losing the first.
/// </summary>
public class WorldsScreen : MonoBehaviour
{
    private const int RowsShown = 6;

    public static WorldsScreen Instance { get; private set; }

    private static bool open;
    private static int closedFrame = -1;

    /// <summary>
    /// True while this screen owns the keyboard, and for the frame after it
    /// closes, so the Escape that shut it does not also unpause behind it.
    /// </summary>
    public static bool Blocking => open || Time.frameCount <= closedFrame;

    private GameObject panel;
    private Transform card;
    private TMP_Text heading;
    private TMP_InputField nameField;
    private TMP_InputField seedField;
    private TMP_FontAsset font;

    private readonly List<GameObject> rows = new List<GameObject>();
    private string pendingDelete;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        open = false;
        closedFrame = -1;

        if (FindFirstObjectByType<WorldsScreen>() == null)
        {
            new GameObject("Worlds (runtime)").AddComponent<WorldsScreen>();
        }
    }

    private void Start()
    {
        Instance = this;
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        BuildUi();
    }

    private void Update()
    {
        if (!open) return;

        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Open()
    {
        open = true;
        pendingDelete = null;
        panel.SetActive(true);

        // The pause card would otherwise sit behind this one.
        FindFirstObjectByType<PauseMenu>()?.ShowPanel(false);

        Refresh();
        Ambience.Instance?.Click(0.9f);
    }

    public void Close()
    {
        open = false;
        closedFrame = Time.frameCount;
        panel.SetActive(false);

        FindFirstObjectByType<PauseMenu>()?.ShowPanel(true);
        Ambience.Instance?.Click(1.1f);
    }

    /// <summary>Redraws the list, which changes whenever a world is made or forgotten.</summary>
    private void Refresh()
    {
        foreach (var row in rows) Destroy(row);
        rows.Clear();

        var worlds = WorldLibrary.All();

        heading.text = "<size=150%><b>WORLDS</b></size>\n<size=85%>"
                     + (worlds.Count == 1 ? "one world" : worlds.Count + " worlds")
                     + " on the shelf</size>";

        float y = 280f;

        for (int i = 0; i < worlds.Count && i < RowsShown; i++)
        {
            Row(worlds[i], y);
            y -= 84f;
        }

        if (worlds.Count > RowsShown)
        {
            var more = Label("More", card, 17f, new Vector2(0f, y + 24f), new Vector2(700f, 30f));
            more.text = "and " + (worlds.Count - RowsShown) + " older, kept but not shown";
            more.color = ParchmentPanel.InkFaint;
            rows.Add(more.gameObject);
        }
    }

    private void Row(WorldSave world, float y)
    {
        bool here = WorldLibrary.Current != null && WorldLibrary.Current.id == world.id;

        var go = new GameObject("World " + world.name);
        go.transform.SetParent(card, false);
        rows.Add(go);

        var image = go.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = here ? new Color(0.29f, 0.24f, 0.17f, 0.22f) : new Color(0.29f, 0.24f, 0.17f, 0.10f);

        var rect = go.GetComponent<RectTransform>();
        Centre(rect, new Vector2(0f, y), new Vector2(700f, 76f));

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => Enter(world));

        var text = Label("Text", go.transform, 20f, new Vector2(-40f, 0f), new Vector2(560f, 70f));
        text.alignment = TextAlignmentOptions.Left;
        text.text = "<b>" + world.name + "</b>" + (here ? "  <size=80%>· here now</size>" : "")
                  + "\n<size=80%><color=#8B786" + "0>seed " + world.seed
                  + "  ·  " + world.Charted + " charted"
                  + "  ·  " + world.Landmarks + " found"
                  + "  ·  " + WorldLibrary.Ago(world.lastPlayedUtc) + "</color></size>";

        // Forgetting a world cannot be undone, so it takes two presses.
        bool asking = pendingDelete == world.id;

        var forget = Button(asking ? "sure?" : "forget", go.transform, new Vector2(300f, 0f), new Vector2(96f, 44f), () =>
        {
            if (asking)
            {
                WorldLibrary.Delete(world.id);
                pendingDelete = null;

                if (here)
                {
                    // The ground you are standing on was just thrown out.
                    var next = WorldLibrary.All();
                    Enter(next.Count > 0 ? next[0] : WorldLibrary.Create(null, 0));
                    return;
                }
            }
            else
            {
                pendingDelete = world.id;
            }

            Refresh();
        });

        forget.GetComponent<RawImage>().color = asking
            ? new Color(0.45f, 0.20f, 0.14f, 0.90f)
            : new Color(0.29f, 0.24f, 0.17f, 0.55f);
    }

    private void Enter(WorldSave world)
    {
        if (WorldLibrary.Current != null && WorldLibrary.Current.id == world.id)
        {
            Close();
            return;
        }

        WorldLibrary.Enter(world);
    }

    private void CreateWorld()
    {
        int seed = 0;

        if (seedField != null && !string.IsNullOrWhiteSpace(seedField.text))
        {
            // Any text can be a seed; if it is not a number, its letters are.
            if (!int.TryParse(seedField.text.Trim(), out seed))
            {
                seed = Mathf.Abs(seedField.text.Trim().GetHashCode());
            }

            if (seed == 0) seed = 1;
        }

        var world = WorldLibrary.Create(nameField != null ? nameField.text : null, seed);

        WorldLibrary.Enter(world);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Worlds Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 620;              // above the pause menu

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var shade = panel.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.62f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(panel.transform, false);

        var paper = cardGo.AddComponent<RawImage>();
        paper.texture = ParchmentPanel.Create(800, 860);

        // Adding a UI graphic swaps the plain Transform for a RectTransform and
        // throws the old one out, so the card is only worth holding on to once
        // it carries the paper: keep the earlier one and everything hung on it
        // is parented to a transform that no longer exists.
        card = cardGo.transform;

        Centre(cardGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(800f, 860f));

        ParchmentPanel.Shade(cardGo.GetComponent<RectTransform>(), 42f);

        heading = Label("Heading", card, 24f, new Vector2(0f, 356f), new Vector2(700f, 90f));

        var rule = new GameObject("New world");
        rule.transform.SetParent(card, false);
        var ruleText = rule.AddComponent<TextMeshProUGUI>();
        ruleText.font = font;
        ruleText.fontSize = 19f;
        ruleText.color = ParchmentPanel.InkFaint;
        ruleText.alignment = TextAlignmentOptions.Center;
        ruleText.raycastTarget = false;
        ruleText.text = "— break new ground —";
        Centre(rule.GetComponent<RectTransform>(), new Vector2(0f, -196f), new Vector2(700f, 30f));

        nameField = Field("Name", "a name, or leave it to the land", new Vector2(0f, -244f), 28);
        seedField = Field("Seed", "a seed, or leave it to chance", new Vector2(0f, -300f), 16);

        Button("Create world", card, new Vector2(0f, -362f), new Vector2(420f, 54f), CreateWorld);

        var hint = Label("Hint", card, 16f, new Vector2(0f, -410f), new Vector2(700f, 30f));
        hint.text = "Esc to go back  ·  progress in this world is saved before you leave it";
        hint.color = ParchmentPanel.InkFaint;

        panel.SetActive(false);
    }

    private static void Centre(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = at;
    }

    private TMP_Text Label(string name, Transform parent, float size, Vector2 at, Vector2 area)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.20f, 0.16f, 0.11f);
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;

        Centre(go.GetComponent<RectTransform>(), at, area);

        return text;
    }

    private GameObject Button(string text, Transform parent, Vector2 at, Vector2 size,
                              UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = new Color(0.29f, 0.24f, 0.17f, 0.85f);

        Centre(go.GetComponent<RectTransform>(), at, size);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var label = Label(text + " label", go.transform, size.y > 48f ? 20f : 16f, Vector2.zero, size);
        label.text = text;
        label.color = new Color(0.94f, 0.90f, 0.82f);

        return go;
    }

    /// <summary>
    /// A text box, assembled by hand. Built inactive so the input field is not
    /// enabled before it has been told where its own text lives.
    /// </summary>
    private TMP_InputField Field(string name, string placeholder, Vector2 at, int limit)
    {
        var go = new GameObject(name);
        go.transform.SetParent(card, false);
        go.SetActive(false);

        var background = go.AddComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0.98f, 0.96f, 0.90f, 0.75f);

        Centre(go.GetComponent<RectTransform>(), at, new Vector2(420f, 46f));

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(go.transform, false);
        var viewport = viewportGo.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12f, 4f);
        viewport.offsetMax = new Vector2(-12f, -4f);
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

    private TMP_Text Stretched(string name, RectTransform parent, Color colour)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 19f;
        text.color = colour;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return text;
    }
}
