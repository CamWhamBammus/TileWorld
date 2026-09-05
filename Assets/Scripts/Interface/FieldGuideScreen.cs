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

    private static FieldGuideScreen instance;

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
    private GameObject strip;
    private RawImage[] plateThumbs;
    private TMP_Text[] plateNames;
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

    /// <summary>Open the book without the key, for the first one that lands.</summary>
    public static void Show()
    {
        if (instance == null || instance.panel == null || instance.open) return;

        instance.open = true;
        instance.page = 0;
        instance.panel.SetActive(true);

        // No click: nobody pressed anything. The book opening itself is the
        // game speaking, and the game does not get to use the sound that means
        // the player did something.
        ScreenState.Open(ScreenState.Screen.Guide);
    }

    private void Start()
    {
        instance = this;
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
        if (Input.GetKeyDown(toggleKey)) Toggle();

        if (!open) return;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Turn(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Turn(-1);

        Refresh();
    }

    /// <summary>Opening or shutting the book, as the key does it.</summary>
    private void Toggle()
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

    private const int PerPage = 8;

    /// <summary>Contents, then a page each.</summary>
    private static int Pages => 1 + Subject.All().Length;

    private void Turn(int by)
    {
        // No sound for turning a page: eleven pages is eleven clicks.
        page = Mathf.Clamp(page + by, 0, Pages - 1);
    }

    private void Refresh()
    {
        var all = Subject.All();

        if (page == 0) Contents();
        else Entry(all[Mathf.Min(page, all.Length) - 1]);

        footer.text = "<color=" + Dim + ">page " + (page + 1) + " of " + Pages
                    + "   ·   arrows turn the page   ·   G closes</color>";
    }

    private enum Shape { Entry, Notes, Contents, Plates }

    /// <summary>
    /// Where the text sits, which differs by page: the notes want the whole
    /// sheet, an entry leaves the top of it to the drawing, and the contents
    /// has four rows of thumbnails to keep clear of.
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

            case Shape.Plates:
                // under the drawing and the strip of plates
                rect.sizeDelta = new Vector2(800f, 220f);
                rect.anchoredPosition = new Vector2(0f, -640f);
                body.fontSizeMax = 19f;
                break;

            case Shape.Contents:
                // under six rows of thumbnails now, so low and short
                rect.sizeDelta = new Vector2(800f, 92f);
                rect.anchoredPosition = new Vector2(0f, -838f);
                body.fontSizeMax = 15f;
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
        if (strip != null) strip.SetActive(false);

        Room(Shape.Contents);

        heading.text = "<size=125%><b><color=" + Ink + ">FIELD SKETCHBOOK</color></b></size>\n"
                     + "<size=80%><color=" + Dim + ">" + FieldGuide.Entries + " of " + Subject.All().Length + " entries finished, "
                     + FieldGuide.Notes + " of " + FieldGuide.NotesWanted + " plates drawn</color></size>";

        var all = Subject.All();

        for (int i = 0; i < all.Length; i++)
        {
            var drawing = SketchBook.Of(all[i]);

            thumbs[i].texture = drawing != null ? drawing : blank;
            thumbs[i].color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);

            bool met = Met(all[i]);

            string tally = FieldGuide.Count(all[i]) + "/" + FieldGuide.Wanted(all[i]);
            thumbNames[i].text = drawing != null
                ? "<color=" + (FieldGuide.Complete(all[i]) ? Done : Ink) + ">" + all[i].Name + "  <size=80%>" + tally + "</size></color>"
                : "<color=" + Dim + ">" + (met ? all[i].Name + "  <size=80%>" + tally + "</size>" : "not yet found") + "</color>";
        }

        body.text = "<color=" + Dim + ">Each creature has its plates: drawn standing, on the move, lying down, and doing whatever it does that "
                  + "nothing else does. Ruins want the whole of them drawn from further back, and the writing read.</color>";
    }

    private void Entry(Subject subject)
    {
        contents.SetActive(false);

        Room(Shape.Entry);

        var made = SketchBook.Made(subject);
        var drawing = made != null ? made.Paper : null;

        plate.gameObject.SetActive(true);
        plate.texture = drawing != null ? drawing : blank;
        plate.color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);

        bool met = Met(subject) || FieldGuide.Count(subject) > 0;
        bool full = FieldGuide.Complete(subject);
        var wants = FieldGuide.Wants(subject);

        heading.text = "<size=125%><b><color=" + (full ? Done : Ink) + ">"
                     + (met ? subject.Name.ToUpper() : "NOT FOUND YET")
                     + "</color></b></size>\n<size=80%><color=" + Dim + ">"
                     + FieldGuide.Count(subject) + " of " + FieldGuide.Wanted(subject) + (subject.Wild ? " plates" : " done")
                     + "  ·  " + FieldGuide.Where(subject) + "</color></size>";

        var text = new System.Text.StringBuilder();

        if (subject.Wild)
        {
            // the plates, in a strip under the drawing: what is drawn as a
            // thumbnail with its name, what is not as a blank with what to do
            strip.SetActive(true);
            var all = Plates.For(subject.Fauna);
            for (int i = 0; i < plateThumbs.Length; i++)
            {
                bool shown = i < all.Length;
                plateThumbs[i].gameObject.SetActive(shown);
                plateNames[i].gameObject.SetActive(shown);
                if (!shown) continue;
                bool has = FieldGuide.HasPlate(subject, all[i].Id);
                var drawn = has ? SketchBook.Of(Plates.Key(subject, all[i].Id)) : null;
                plateThumbs[i].texture = drawn != null ? drawn : blank;
                plateThumbs[i].color = drawn != null ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                plateNames[i].text = has
                    ? "<color=" + Done + ">" + all[i].Label + "</color>"
                    : "<color=" + Dim + ">" + all[i].Label + "</color>";
            }
            // the strip's own x positions, centred on however many there are
            for (int i = 0; i < all.Length; i++)
            {
                float x = (i - (all.Length - 1) * 0.5f) * 150f;
                plateThumbs[i].rectTransform.anchoredPosition = new Vector2(x, -498f);
                plateNames[i].rectTransform.anchoredPosition = new Vector2(x, -598f);
            }

            Room(Shape.Plates);
            foreach (var plate in all)
            {
                bool has = FieldGuide.HasPlate(subject, plate.Id);
                text.Append("<color=").Append(has ? Done : Ink).Append(">").Append(has ? "●  " : "○  ").Append(plate.Label).Append("</color>");
                if (has)
                {
                    string detail = FieldGuide.PlateDetail(subject, plate.Id);
                    if (!string.IsNullOrEmpty(detail)) text.Append("<size=80%><color=").Append(Dim).Append(">   ").Append(detail).Append("</color></size>");
                }
                else text.Append("<size=80%><i><color=").Append(Dim).Append(">   ").Append(plate.Ask).Append("</color></i></size>");
                text.Append("\n");
            }
            body.text = text.ToString();
            return;
        }

        strip.SetActive(false);

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

            if (has)
            {
                // What happened, and where and when, rather than the instruction
                // still standing there under the word done.
                text.Append("<indent=16px><color=").Append(Ink).Append(">")
                    .Append(FieldGuide.Did(subject, study));

                if (study == FieldGuide.Study.Sketch && made != null && !string.IsNullOrEmpty(made.Verdict))
                {
                    text.Append(" — ").Append(made.Verdict);
                }

                text.Append("</color>");

                string detail = FieldGuide.Detail(subject, study);

                if (!string.IsNullOrEmpty(detail))
                {
                    text.Append("<size=80%><color=").Append(Dim).Append(">   ").Append(detail)
                        .Append("</color></size>");
                }

                text.Append("</indent>\n");
            }
            else
            {
                text.Append("<indent=16px><color=").Append(Ink).Append(">")
                    .Append(FieldGuide.Asks(subject, study)).Append("</color></indent>\n");

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
        paper.texture = ParchmentPanel.Create(920, 940);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(920f, 940f);

        ParchmentPanel.Shade(cardRect, 46f);

        blank = ParchmentPanel.Create(190, 143, false);

        heading = Label("Heading", cardGo.transform, 30f, new Vector2(0f, -40f), new Vector2(800f, 84f));

        // a rule under the heading, the way a page of a book is headed
        var ruleGo = new GameObject("Heading rule");
        ruleGo.transform.SetParent(cardGo.transform, false);

        var rule = ruleGo.AddComponent<RawImage>();
        rule.texture = Texture2D.whiteTexture;
        rule.color = new Color(0.29f, 0.24f, 0.17f, 0.35f);
        rule.raycastTarget = false;

        var ruleRect = ruleGo.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0.5f, 1f);
        ruleRect.anchorMax = new Vector2(0.5f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.sizeDelta = new Vector2(660f, 2f);
        ruleRect.anchoredPosition = new Vector2(0f, -122f);

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

        // a creature's plates, under its drawing
        strip = new GameObject("Plates");
        strip.transform.SetParent(cardGo.transform, false);
        var stripRect = strip.AddComponent<RectTransform>();
        stripRect.anchorMin = Vector2.zero; stripRect.anchorMax = Vector2.one; stripRect.offsetMin = Vector2.zero; stripRect.offsetMax = Vector2.zero;
        plateThumbs = new RawImage[6];
        plateNames = new TMP_Text[6];
        for (int i = 0; i < 6; i++)
        {
            var thumbGo = new GameObject("Plate " + i);
            thumbGo.transform.SetParent(strip.transform, false);
            plateThumbs[i] = thumbGo.AddComponent<RawImage>();
            var r = thumbGo.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 1f); r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(136f, 102f);
            plateNames[i] = Label("Plate name " + i, strip.transform, 13f, new Vector2(0f, -598f), new Vector2(146f, 22f));
        }
        strip.SetActive(false);

        // the contents: all eight, small
        contents = new GameObject("Contents");
        contents.transform.SetParent(cardGo.transform, false);

        var contentsRect = contents.AddComponent<RectTransform>();
        contentsRect.anchorMin = Vector2.zero;
        contentsRect.anchorMax = Vector2.one;
        contentsRect.offsetMin = Vector2.zero;
        contentsRect.offsetMax = Vector2.zero;

        // The contents: every creature, then every structure, small. There
        // were eight in two rows of four when this was written; there are
        // nineteen now, so the thumbnails are smaller and five to a row, and
        // the arrays are as long as the book is.
        var all = Subject.All();
        int creatures = 0;
        foreach (var one in all) if (one.Wild) creatures++;

        Label("Row creatures", contents.transform, 19f, new Vector2(0f, -136f), new Vector2(800f, 28f))
            .text = "<color=" + Dim + ">CREATURES</color>";

        // Seven to a row and 104 tall a row: nineteen creatures and fifteen
        // structures are six rows, which is what a 940 card has room for.
        const int Columns = 7;
        const float RowPitch = 104f, ColumnPitch = 112f;
        int creatureRows = Mathf.CeilToInt(creatures / (float)Columns);
        float builtLabelY = -160f - creatureRows * RowPitch - 2f;

        Label("Row built", contents.transform, 19f, new Vector2(0f, builtLabelY), new Vector2(800f, 28f))
            .text = "<color=" + Dim + ">STRUCTURES</color>";

        thumbs = new RawImage[all.Length];
        thumbNames = new TMP_Text[all.Length];

        for (int i = 0; i < all.Length; i++)
        {
            bool wild = i < creatures;
            int within = wild ? i : i - creatures;
            int column = within % Columns;
            int row = within / Columns;

            float top = wild ? -164f : builtLabelY - 28f;
            float y = top - row * RowPitch;

            var thumbGo = new GameObject("Thumb " + i);
            thumbGo.transform.SetParent(contents.transform, false);

            thumbs[i] = thumbGo.AddComponent<RawImage>();

            var rect = thumbGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(100f, 75f);
            rect.anchoredPosition = new Vector2((column - (Columns - 1) * 0.5f) * ColumnPitch, y);

            thumbNames[i] = Label("Thumb name " + i, contents.transform, 12f,
                                  new Vector2((column - (Columns - 1) * 0.5f) * ColumnPitch, y - 77f),
                                  new Vector2(110f, 22f));
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
