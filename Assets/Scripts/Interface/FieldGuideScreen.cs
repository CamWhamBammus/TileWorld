using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The book itself. The map says where you have been and the journal says what
/// you found; this says what you have understood, and — more usefully — what
/// you have not, which is the only thing in the game that tells you what to go
/// and do next.
/// </summary>
public class FieldGuideScreen : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.G;

    private ChunkManager world;
    private GameObject panel;
    private TMP_Text body;
    private RawImage[] plates;
    private TMP_Text[] plateNames;
    private TMP_FontAsset font;
    private bool open;

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

    private void OnEnable()
    {
        ScreenState.Changed += OnScreenChanged;
    }

    private void OnDisable()
    {
        ScreenState.Changed -= OnScreenChanged;
    }

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

            if (open) ScreenState.Open(ScreenState.Screen.Guide);
            else ScreenState.Close(ScreenState.Screen.Guide);

            Ambience.Instance?.Click();
        }

        if (open) Refresh();
    }

    private void Refresh()
    {
        if (body == null) return;

        string ink = "#332C22";
        string dim = "#8B7860";
        string done = "#4A5B33";

        var text = new System.Text.StringBuilder();

        text.Append("<size=145%><b><color=").Append(ink).Append(">FIELD GUIDE</color></b></size>\n");
        text.Append("<size=90%><color=").Append(dim).Append(">")
            .Append(FieldGuide.Entries).Append(" of 4 entries finished, ")
            .Append(FieldGuide.Studies).Append(" of 12 notes made</color></size>\n");
        text.Append("<color=").Append(dim).Append(">").Append(new string('─', 34)).Append("</color>\n");

        for (int i = 0; i < 4; i++)
        {
            var kind = (FaunaKind)i;
            bool met = SightingLog.Has(kind);

            if (!met)
            {
                // Nothing given away about something you have never seen.
                text.Append("<color=").Append(dim).Append("><b>—————</b>   not yet seen</color>\n\n");
                continue;
            }

            bool full = FieldGuide.Complete(kind);

            text.Append("<color=").Append(full ? done : ink).Append("><b>")
                .Append(Fauna.Of(kind).Name.ToUpper()).Append("</b></color>");
            text.Append("<size=85%><color=").Append(dim).Append(">   ")
                .Append(full ? "entry finished" : FieldGuide.Count(kind) + " of 3").Append("</color></size>\n");

            for (int s = 0; s < 3; s++)
            {
                var study = (FieldGuide.Study)s;
                bool has = FieldGuide.Has(kind, study);

                text.Append("<indent=18px><size=85%><color=").Append(has ? done : dim).Append(">")
                    .Append(has ? "✓ " : "·  ").Append(FieldGuide.Asks(kind, study))
                    .Append("</color></size></indent>\n");
            }

            if (full)
            {
                text.Append("<indent=18px><size=80%><i><color=").Append(ink).Append(">")
                    .Append(Fauna.Describe(kind)).Append("</color></i></size></indent>\n");
            }

            text.Append("\n");
        }

        text.Append("<color=").Append(dim).Append(">").Append(new string('─', 34)).Append("</color>\n");
        text.Append("<size=85%><color=").Append(dim)
            .Append(">Walk slowly and they let you nearer. Stand still to draw. G to close</color></size>");

        body.text = text.ToString();

        // and the drawings across the top, which are the entries really
        for (int i = 0; i < 4; i++)
        {
            var kind = (FaunaKind)i;
            var drawing = SketchBook.Of(kind);

            plates[i].texture = drawing != null ? drawing : blank;
            plates[i].color = drawing != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);

            plateNames[i].text = drawing != null
                ? Fauna.Of(kind).Name
                : (SightingLog.Has(kind) ? "<color=#8B7860>not drawn</color>" : "<color=#8B7860>—</color>");
        }
    }

    private Texture2D blank;

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
        paper.texture = ParchmentPanel.Create(360, 440);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(760f, 900f);

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(cardGo.transform, false);

        body = bodyGo.AddComponent<TextMeshProUGUI>();
        body.font = font;
        body.fontSize = 23f;
        body.enableAutoSizing = true;
        body.fontSizeMin = 15f;
        body.fontSizeMax = 23f;
        body.color = new Color(0.20f, 0.16f, 0.11f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.raycastTarget = false;

        var bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 0f);
        bodyRect.anchorMax = new Vector2(0.5f, 0f);
        bodyRect.pivot = new Vector2(0.5f, 0f);
        bodyRect.sizeDelta = new Vector2(660f, 560f);
        bodyRect.anchoredPosition = new Vector2(0f, 40f);

        blank = ParchmentPanel.Create(64, 48);

        plates = new RawImage[4];
        plateNames = new TMP_Text[4];

        for (int i = 0; i < 4; i++)
        {
            var plateGo = new GameObject("Plate " + i);
            plateGo.transform.SetParent(cardGo.transform, false);

            plates[i] = plateGo.AddComponent<RawImage>();

            var plateRect = plateGo.GetComponent<RectTransform>();
            plateRect.anchorMin = new Vector2(0.5f, 1f);
            plateRect.anchorMax = new Vector2(0.5f, 1f);
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.sizeDelta = new Vector2(164f, 123f);
            plateRect.anchoredPosition = new Vector2((i - 1.5f) * 172f, -78f);

            plateNames[i] = Label("Plate name " + i, cardGo.transform, 17f,
                                  new Vector2((i - 1.5f) * 172f, -214f), new Vector2(164f, 28f));
        }

        panel.SetActive(false);
    }
}
