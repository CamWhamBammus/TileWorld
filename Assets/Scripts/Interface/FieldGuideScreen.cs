using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sketchbook, as a book. Everything on one page meant small type running
/// off the bottom of the paper; a page you turn gives each subject the room to
/// be looked at, which is the point of having drawn it.
///
/// The first page is the contents, and after it comes one page per subject:
/// the drawing large, what the entry still wants, and whatever the place had
/// written on it.
/// </summary>
public class FieldGuideScreen : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.G;

    private const string Ink = "#332C22";
    private const string Dim = "#8B7860";
    private const string Done = "#4A5B33";

    private ChunkManager world;
    private GameObject panel;
    private TMP_FontAsset font;
    private bool open;

    private int page;                       // 0 is the contents

    private TMP_Text heading;
    private TMP_Text body;
    private TMP_Text footer;
    private RawImage plate;                 // the big drawing, on a subject's page
    private RawImage[] thumbs;
    private TMP_Text[] thumbNames;
    private GameObject contents;
    private Texture2D blank;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<FieldGuideScreen>() == null)
        {
            new GameObject("Field Guide (runtime)").AddComponent<FieldGuideScreen>();
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

        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        BuildUi();
    }

    private void OnEnable() => ScreenState.Changed += OnScreenChanged;

    private void OnDisable() => ScreenState.Changed -= OnScreenChanged;

    private void OnScreenChanged(ScreenState.Screen screen)
    {
        if (open && screen != ScreenState.Screen.Guide)
        {
            open = false;
            if (panel != null) panel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            panel.SetActive(open);

            if (open)
            {
                page = 0;
                ScreenState.Open(ScreenState.Screen.Guide);
            }
            else
            {
                ScreenState.Close(ScreenState.Screen.Guide);
            }

            Ambience.Instance?.Click();
        }

        if (!open) return;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Turn(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Turn(-1);

        Refresh();
    }

    private void Turn(int by)
    {
        page = Mathf.Clamp(page + by, 0, Subject.All().Length);
        Ambience.Instance?.Click(1.06f);
    }

    private void Refresh()
    {
        if (page == 0) Contents();
        else Entry(Subject.All()[page - 1]);

        footer.text = page == 0
            ? "<color=" + Dim + ">right arrow to turn the page  ·  G to close</color>"
            : "<color=" + Dim + ">page " + page + " of " + Subject.All().Length
              + "  ·  arrows to turn  ·  G to close</color>";
    }

    private void Contents()
    {
        contents.SetActive(true);
        plate.gameObject.SetActive(false);

        heading.text = "<size=125%><b><color=" + Ink + ">FIELD SKETCHBOOK</color></b></size>\n"
                     + "<size=80%><color=" + Dim + ">" + FieldGuide.Entries + " of 8 entries finished, "
                     + FieldGuide.Notes + " of " + FieldGuide.NotesWanted + " notes made</color></size>";

        var all = Subject.All();

        for (int i = 0; i < all.Length; i++)
        {
            var drawing = SketchBook.Of(all[i]);

            thumbs[i].texture = drawing != null ? drawing : blank;
            thumbs[i].color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);

            bool met = Met(all[i]);

            thumbNames[i].text = drawing != null
                ? "<color=" + (FieldGuide.Complete(all[i]) ? Done : Ink) + ">" + all[i].Name + "</color>"
                : "<color=" + Dim + ">" + (met ? all[i].Name : "not yet found") + "</color>";
        }

        body.text = "<color=" + Dim + ">A creature wants drawing from close by, something seen of how it "
                  + "lives, and finding in its own country. A ruin wants drawing whole, from far enough "
                  + "back, and whatever is written there read.</color>";
    }

    private void Entry(Subject subject)
    {
        contents.SetActive(false);

        var drawing = SketchBook.Of(subject);

        plate.gameObject.SetActive(true);
        plate.texture = drawing != null ? drawing : blank;
        plate.color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);

        bool met = Met(subject);
        bool full = FieldGuide.Complete(subject);
        var wants = FieldGuide.Wants(subject);

        heading.text = "<size=125%><b><color=" + (full ? Done : Ink) + ">"
                     + (met || FieldGuide.Count(subject) > 0 ? subject.Name.ToUpper() : "NOT YET FOUND")
                     + "</color></b></size>\n<size=80%><color=" + Dim + ">"
                     + (full ? "entry finished" : FieldGuide.Count(subject) + " of " + wants.Length + " notes")
                     + "</color></size>";

        var text = new System.Text.StringBuilder();

        foreach (var study in wants)
        {
            bool has = FieldGuide.Has(subject, study);

            text.Append("<color=").Append(has ? Done : Dim).Append(">")
                .Append(has ? "•  " : "·  ").Append(FieldGuide.Asks(subject, study)).Append("</color>\n");
        }

        text.Append("\n");

        if (subject.Wild)
        {
            if (full)
            {
                text.Append("<i><color=").Append(Ink).Append(">")
                    .Append(Fauna.Describe(subject.Fauna)).Append("</color></i>");
            }
        }
        else if (FieldGuide.Has(subject, FieldGuide.Study.Inscription))
        {
            // what was cut into the stone, which is the only voice out there
            string line = Written(subject.Landmark);

            if (!string.IsNullOrEmpty(line))
            {
                text.Append("<i><color=").Append(Ink).Append(">\"").Append(line).Append("\"</color></i>");
            }
        }

        body.text = text.ToString();
    }

    /// <summary>Whether this sort has been come across at all.</summary>
    private static bool Met(Subject subject)
    {
        if (subject.Wild) return SightingLog.Has(subject.Fauna);

        foreach (var pair in LandmarkLog.Found)
        {
            if (pair.Value == subject.Landmark) return true;
        }

        return false;
    }

    /// <summary>The inscription at one of the ones you have found of this sort.</summary>
    private string Written(LandmarkKind kind)
    {
        foreach (var pair in LandmarkLog.Found)
        {
            if (pair.Value == kind) return Inscriptions.For(pair.Key, kind, world.WorldSeed);
        }

        return null;
    }

    // ------------------------------------------------------------------- paper

    private void BuildUi()
    {
        var canvasGo = new GameObject("Guide Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var shade = panel.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.55f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(panel.transform, false);

        var paper = cardGo.AddComponent<RawImage>();
        paper.texture = ParchmentPanel.Create(380, 460);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(920f, 940f);

        blank = ParchmentPanel.Create(64, 48);

        heading = Label("Heading", cardGo.transform, 30f, new Vector2(0f, -40f), new Vector2(800f, 84f));

        // the big drawing on a subject's page
        var plateGo = new GameObject("Plate");
        plateGo.transform.SetParent(cardGo.transform, false);

        plate = plateGo.AddComponent<RawImage>();

        var plateRect = plateGo.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0.5f, 1f);
        plateRect.anchorMax = new Vector2(0.5f, 1f);
        plateRect.pivot = new Vector2(0.5f, 1f);
        plateRect.sizeDelta = new Vector2(620f, 465f);
        plateRect.anchoredPosition = new Vector2(0f, -130f);

        // the contents: all eight, small
        contents = new GameObject("Contents");
        contents.transform.SetParent(cardGo.transform, false);

        var contentsRect = contents.AddComponent<RectTransform>();
        contentsRect.anchorMin = Vector2.zero;
        contentsRect.anchorMax = Vector2.one;
        contentsRect.offsetMin = Vector2.zero;
        contentsRect.offsetMax = Vector2.zero;

        thumbs = new RawImage[8];
        thumbNames = new TMP_Text[8];

        for (int i = 0; i < 8; i++)
        {
            int column = i % 4;
            int row = i / 4;

            var thumbGo = new GameObject("Thumb " + i);
            thumbGo.transform.SetParent(contents.transform, false);

            thumbs[i] = thumbGo.AddComponent<RawImage>();

            var rect = thumbGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(190f, 143f);
            rect.anchoredPosition = new Vector2((column - 1.5f) * 200f, -130f - row * 200f);

            thumbNames[i] = Label("Thumb name " + i, contents.transform, 19f,
                                  new Vector2((column - 1.5f) * 200f, -278f - row * 200f),
                                  new Vector2(196f, 30f));
        }

        body = Label("Body", cardGo.transform, 23f, new Vector2(0f, -640f), new Vector2(800f, 210f));
        body.alignment = TextAlignmentOptions.Top;

        footer = Label("Footer", cardGo.transform, 19f, new Vector2(0f, -880f), new Vector2(800f, 40f));

        panel.SetActive(false);
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

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = area;
        rect.anchoredPosition = at;

        return text;
    }
}
