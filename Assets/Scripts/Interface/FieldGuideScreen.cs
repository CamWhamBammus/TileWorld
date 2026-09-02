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

    private const int PerPage = 8;

    /// <summary>Contents, then a page each, then however many the notes fill.</summary>
    private static int Pages => 1 + Subject.All().Length + Mathf.Max(1, Mathf.CeilToInt(Notebook.Count / (float)PerPage));

    private void Turn(int by)
    {
        page = Mathf.Clamp(page + by, 0, Pages - 1);
        Ambience.Instance?.Click(1.06f);
    }

    private void Refresh()
    {
        var all = Subject.All();

        if (page == 0) Contents();
        else if (page <= all.Length) Entry(all[page - 1]);
        else Noticed(page - all.Length - 1);

        footer.text = "<color=" + Dim + ">page " + (page + 1) + " of " + Pages
                    + "   ·   left and right arrows turn the page   ·   G closes the book</color>";
    }

    /// <summary>
    /// The running record of what the book has remarked on. Not a list of
    /// things to go and do: a list of things that happened while you were out.
    /// </summary>
    private void Noticed(int sheet)
    {
        contents.SetActive(false);
        plate.gameObject.SetActive(false);

        Room(Shape.Notes);

        heading.text = "<size=125%><b><color=" + Ink + ">NOTICED</color></b></size>\n"
                     + "<size=80%><color=" + Dim + ">" + Notebook.Count + " of "
                     + Notebook.Possible + " things worth remarking on  ·  "
                     + Notebook.Wondering() + "</color></size>";

        var text = new System.Text.StringBuilder();

        if (Notebook.Count == 0)
        {
            text.Append("<color=").Append(Dim)
                .Append(">Nothing yet. It fills itself as you go: the book only writes down what ")
                .Append("happens in front of you, so there is nothing here to go and fetch.</color>");
        }
        else
        {
            int from = sheet * PerPage;

            for (int i = from; i < from + PerPage && i < Notebook.Count; i++)
            {
                var entry = Notebook.All[i];

                text.Append("<color=").Append(Ink).Append(">•  ").Append(entry.Line).Append("</color>\n");
                text.Append("<indent=22px><size=80%><color=").Append(Dim).Append(">")
                    .Append(entry.Where);

                if (!string.IsNullOrEmpty(entry.When)) text.Append(", ").Append(entry.When);

                text.Append("</color></size></indent>\n");
            }
        }

        body.text = text.ToString();
    }

    private enum Shape { Entry, Notes, Contents }

    /// <summary>
    /// Where the text sits, which differs by page: the notes want the whole
    /// sheet, an entry leaves the top of it to the drawing, and the contents
    /// has two rows of thumbnails to keep clear of.
    /// </summary>
    private void Room(Shape shape)
    {
        var rect = body.rectTransform;

        switch (shape)
        {
            case Shape.Notes:
                rect.sizeDelta = new Vector2(800f, 700f);
                rect.anchoredPosition = new Vector2(0f, -140f);
                body.fontSizeMax = 21f;
                break;

            case Shape.Contents:
                rect.sizeDelta = new Vector2(800f, 250f);
                rect.anchoredPosition = new Vector2(0f, -600f);
                body.fontSizeMax = 21f;
                break;

            default:
                rect.sizeDelta = new Vector2(800f, 372f);
                rect.anchoredPosition = new Vector2(0f, -498f);
                body.fontSizeMax = 23f;
                break;
        }

        body.alignment = TextAlignmentOptions.TopLeft;
    }

    private void Contents()
    {
        contents.SetActive(true);
        plate.gameObject.SetActive(false);

        Room(Shape.Contents);

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
                  + "back, and whatever is written there read.\n\nPast those pages the book keeps what it "
                  + "has noticed on its own: " + Notebook.Count + " of " + Notebook.Possible
                  + " so far.</color>";
    }

    private void Entry(Subject subject)
    {
        contents.SetActive(false);

        Room(Shape.Entry);

        var drawing = SketchBook.Of(subject);

        plate.gameObject.SetActive(true);
        plate.texture = drawing != null ? drawing : blank;
        plate.color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);

        bool met = Met(subject) || FieldGuide.Count(subject) > 0;
        bool full = FieldGuide.Complete(subject);
        var wants = FieldGuide.Wants(subject);

        heading.text = "<size=125%><b><color=" + (full ? Done : Ink) + ">"
                     + (met ? subject.Name.ToUpper() : "NOT YET FOUND")
                     + "</color></b></size>\n<size=80%><color=" + Dim + ">"
                     + FieldGuide.Count(subject) + " of " + wants.Length + " done"
                     + (drawing == null ? "  ·  no drawing yet" : "")
                     + "  ·  " + FieldGuide.Where(subject) + "</color></size>";

        var text = new System.Text.StringBuilder();

        foreach (var study in wants)
        {
            bool has = FieldGuide.Has(subject, study);

            // Each part of an entry as its own block: what it is called, whether
            // it is done, what it wants, and - if it is not done - what to
            // actually do about it.
            text.Append("<b><color=").Append(has ? Done : Ink).Append(">")
                .Append(FieldGuide.Title(subject, study)).Append("</color></b>")
                .Append("<size=80%><color=").Append(has ? Done : Dim).Append(">   ")
                .Append(has ? "done" : "not yet").Append("</color></size>\n");

            text.Append("<indent=16px><color=").Append(has ? Dim : Ink).Append(">")
                .Append(FieldGuide.Asks(subject, study)).Append("</color></indent>\n");

            if (!has)
            {
                text.Append("<indent=16px><size=78%><i><color=").Append(Dim).Append(">")
                    .Append(FieldGuide.How(subject, study)).Append("</color></i></size></indent>\n");
            }

            text.Append("\n");
        }

        if (!subject.Wild && FieldGuide.Has(subject, FieldGuide.Study.Inscription))
        {
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
        plateRect.sizeDelta = new Vector2(460f, 345f);
        plateRect.anchoredPosition = new Vector2(0f, -134f);

        // the contents: all eight, small
        contents = new GameObject("Contents");
        contents.transform.SetParent(cardGo.transform, false);

        var contentsRect = contents.AddComponent<RectTransform>();
        contentsRect.anchorMin = Vector2.zero;
        contentsRect.anchorMax = Vector2.one;
        contentsRect.offsetMin = Vector2.zero;
        contentsRect.offsetMax = Vector2.zero;

        Label("Row creatures", contents.transform, 19f, new Vector2(0f, -136f), new Vector2(800f, 28f))
            .text = "<color=" + Dim + ">THE CREATURES OF THIS COUNTRY</color>";

        Label("Row built", contents.transform, 19f, new Vector2(0f, -366f), new Vector2(800f, 28f))
            .text = "<color=" + Dim + ">WHAT WAS BUILT IN IT</color>";

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
            rect.anchoredPosition = new Vector2((column - 1.5f) * 200f, -172f - row * 230f);

            thumbNames[i] = Label("Thumb name " + i, contents.transform, 19f,
                                  new Vector2((column - 1.5f) * 200f, -320f - row * 230f),
                                  new Vector2(196f, 30f));
        }

        body = Label("Body", cardGo.transform, 23f, new Vector2(0f, -640f), new Vector2(800f, 210f));
        body.alignment = TextAlignmentOptions.TopLeft;

        // Nothing should ever run off the paper again: if a page is long, the
        // type comes down a point or two rather than spilling over the edge.
        body.enableAutoSizing = true;
        body.fontSizeMin = 16f;
        body.fontSizeMax = 23f;

        footer = Label("Footer", cardGo.transform, 19f, new Vector2(0f, -872f), new Vector2(800f, 36f));

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
