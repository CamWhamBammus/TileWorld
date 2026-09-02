using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drawing what you find. The one thing the game asks you to do that is not
/// walking: get near enough to an animal to draw it, and stay still enough,
/// long enough, that it does not leave before you have finished.
///
/// Everything about it is already in the world — how close a creature lets you
/// come depends on how you approach, and what it happens to be doing while you
/// watch is its own business. The pencil only records it.
/// </summary>
public class Sketching : MonoBehaviour
{
    [Tooltip("How close you have to be to draw an animal worth keeping.")]
    [SerializeField] private float reach = 22f;

    [Tooltip("Structures are big, so they are drawn from further off — and not from under them.")]
    [SerializeField] private float ruinReach = 52f;
    [SerializeField] private float ruinBack = 12f;

    [Tooltip("Close enough to read what somebody cut into the stone.")]
    [SerializeField] private float readable = 6.5f;

    [Tooltip("And how close you have to be for what it is doing to count.")]
    [SerializeField] private float watching = 34f;

    [SerializeField] private float seconds = 4.2f;

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private Quarry subject;
    private float progress;

    /// <summary>Something in front of you worth putting on paper.</summary>
    private struct Quarry
    {
        public Subject What;
        public Transform Body;
        public Animal Creature;

        public bool Any => Body != null;

        public Vector3 Aim => Creature != null ? Creature.Head : Body.position;

        public bool Same(Quarry other) => Body == other.Body;
    }

    private GameObject panel;
    private TMP_Text label;
    private RectTransform fill;
    private TMP_FontAsset font;

    private GameObject sheet;        // the drawing, held up for a moment after
    private RawImage page;
    private TMP_Text caption;
    private float showUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Sketching>() == null)
        {
            new GameObject("Sketching (runtime)").AddComponent<Sketching>();
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

        player = world.PlayerTransform;
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        BuildUi();
    }

    private void Update()
    {
        if (player == null) return;

        Stalking.Watch(player, Time.deltaTime);

        if (sheet != null) sheet.SetActive(Time.time < showUntil && !ScreenState.WantsCursor);

        // Nothing is drawn with a screen open in front of your face.
        if (ScreenState.WantsCursor)
        {
            Show(false);
            return;
        }

        Notice();

        var candidate = Nearest(true);

        if (!candidate.Any || (candidate.What.Wild && !Stalking.Steady))
        {
            // A drawing left half done is worth nothing, but it fades rather
            // than snapping away, so a moment's wobble is not fatal.
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 0.55f);

            if (progress <= 0.01f) subject = default;

            Show(progress > 0.01f);
            Refresh(!candidate.Any ? "keep it in sight" : "hold still");

            return;
        }

        if (!subject.Same(candidate))
        {
            subject = candidate;
            progress = 0f;
        }

        if (FieldGuide.Has(subject.What, FieldGuide.Study.Sketch))
        {
            Show(false);
            return;
        }

        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / seconds);

        Show(true);
        Refresh(null);

        if (progress >= 1f)
        {
            // The drawing is of this one, from here, as it stands.
            var drawing = SketchBook.Draw(subject.What, subject.Body, Eye());

            if (FieldGuide.Record(subject.What, FieldGuide.Study.Sketch))
            {
                Notices.Show("You draw the " + subject.What.Name
                           + "  ·  " + FieldGuide.Entries + "/8 entries done");

                Ambience.Instance?.Click(1.15f);
            }

            if (drawing != null)
            {
                page.texture = drawing;
                caption.text = "the " + subject.What.Name + "  ·  G for the book";
                showUntil = Time.time + 4.5f;
            }

            progress = 0f;
            subject = default;
            Show(false);
        }
    }

    /// <summary>
    /// The two studies that are a matter of being there rather than of holding
    /// still: what the animal was doing, and where you found it.
    /// </summary>
    private void Notice()
    {
        Reading();

        var seen = Nearest(false);

        if (!seen.Any || !seen.What.Wild || seen.Creature == null) return;

        float hour = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;
        var kind = seen.What.Fauna;

        if (!FieldGuide.Has(seen.What, FieldGuide.Study.Habit)
            && FieldGuide.Habit(kind, seen.Creature.Busy)
            && FieldGuide.Record(seen.What, FieldGuide.Study.Habit))
        {
            Notices.Show("Noted: " + FieldGuide.Habit(kind));
        }

        if (!FieldGuide.Has(seen.What, FieldGuide.Study.Country)
            && FieldGuide.Country(kind, seen.Body.position, world.WorldSeed, hour)
            && FieldGuide.Record(seen.What, FieldGuide.Study.Country))
        {
            Notices.Show("Noted: " + FieldGuide.Country(kind));
        }

        Finished(seen.What);
    }

    /// <summary>
    /// Standing close enough to a ruin to read it. What is cut into these
    /// places is the only voice in the world, so it is worth walking in for.
    /// </summary>
    private void Reading()
    {
        foreach (var ruin in FindObjectsByType<LandmarkTag>(FindObjectsSortMode.None))
        {
            var what = Subject.Structure(ruin.Kind);

            if (FieldGuide.Has(what, FieldGuide.Study.Inscription)) continue;
            if (Vector3.Distance(player.position, ruin.transform.position) > readable) continue;

            if (FieldGuide.Record(what, FieldGuide.Study.Inscription))
            {
                Notices.Show("\"" + Inscriptions.For(ruin.Chunk, ruin.Kind, world.WorldSeed) + "\"");
            }

            Finished(what);
        }
    }

    private void Finished(Subject what)
    {
        if (!FieldGuide.Complete(what) || told.Contains(what.Key)) return;

        told.Add(what.Key);
        Notices.Show("The " + what.Name + " entry is finished");
    }

    private readonly System.Collections.Generic.HashSet<string> told =
        new System.Collections.Generic.HashSet<string>();

    /// <summary>
    /// The nearest thing you can actually see and would want to draw, creature
    /// or structure. A ruin has to be far enough off to fit on the page, which
    /// is the opposite of what an animal asks of you.
    /// </summary>
    private Quarry Nearest(bool forDrawing)
    {
        var camera = Eye();
        var best = default(Quarry);

        if (camera == null) return best;

        float closest = float.MaxValue;

        foreach (var animal in FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            if (forDrawing && animal.Busy == Doing.Fleeing) continue;

            float distance = Look(camera, animal.Head, forDrawing ? reach : watching, forDrawing);

            if (distance < 0f || distance >= closest) continue;

            closest = distance;
            best = new Quarry
            {
                What = Subject.Creature(animal.Kind),
                Body = animal.transform,
                Creature = animal
            };
        }

        if (!forDrawing) return best;

        foreach (var ruin in FindObjectsByType<LandmarkTag>(FindObjectsSortMode.None))
        {
            Vector3 middle = Middle(ruin.transform);

            float distance = Look(camera, middle, ruinReach, false);

            if (distance < ruinBack || distance >= closest) continue;

            closest = distance;
            best = new Quarry { What = Subject.Structure(ruin.Kind), Body = ruin.transform };
        }

        return best;
    }

    /// <summary>How far off a thing is, or less than nothing if it cannot be seen.</summary>
    private float Look(Camera camera, Vector3 at, float range, bool strict)
    {
        Vector3 to = at - camera.transform.position;
        float distance = to.magnitude;

        if (distance > range) return -1f;
        if (Vector3.Dot(camera.transform.forward, to.normalized) < (strict ? 0.62f : 0.5f)) return -1f;
        if (Physics.Linecast(camera.transform.position, at)) return -1f;

        return distance;
    }

    private static Vector3 Middle(Transform what)
    {
        var bounds = new Bounds(what.position, Vector3.one * 0.1f);

        foreach (var piece in what.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(piece.bounds);

        return bounds.center;
    }

    private Camera Eye()
    {
        if (eye == null) eye = Camera.main;

        return eye;
    }

    // ------------------------------------------------------------------ the bar

    private void Show(bool on)
    {
        if (panel != null && panel.activeSelf != on) panel.SetActive(on);
    }

    private void Refresh(string trouble)
    {
        if (fill == null || label == null) return;

        fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

        string name = subject.Any ? subject.What.Name : "it";

        label.text = trouble == null
            ? "drawing the " + name
            : "<color=#8B7860>" + trouble + "</color>";
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Sketch Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var card = panel.AddComponent<RawImage>();
        card.texture = ParchmentPanel.Create(160, 40);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(420f, 84f);
        rect.anchoredPosition = new Vector2(0f, 130f);

        // the line the drawing fills in
        var trackGo = new GameObject("Track");
        trackGo.transform.SetParent(panel.transform, false);

        var track = trackGo.AddComponent<RawImage>();
        track.texture = Texture2D.whiteTexture;
        track.color = new Color(0.29f, 0.24f, 0.17f, 0.22f);

        var trackRect = trackGo.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.offsetMin = new Vector2(22f, 16f);
        trackRect.offsetMax = new Vector2(-22f, 0f);
        trackRect.sizeDelta = new Vector2(trackRect.sizeDelta.x, 10f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(trackGo.transform, false);

        var drawn = fillGo.AddComponent<RawImage>();
        drawn.texture = Texture2D.whiteTexture;
        drawn.color = new Color(0.29f, 0.24f, 0.17f, 0.92f);

        fill = fillGo.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        label = Label("Label", panel.transform, 21f, new Vector2(0f, 14f), new Vector2(400f, 44f));

        // the drawing itself, held up for a few seconds once it is finished
        sheet = new GameObject("Sheet");
        sheet.transform.SetParent(canvasGo.transform, false);

        var mount = sheet.AddComponent<RawImage>();
        mount.texture = ParchmentPanel.Create(200, 170);

        var sheetRect = sheet.GetComponent<RectTransform>();
        sheetRect.anchorMin = new Vector2(1f, 0f);
        sheetRect.anchorMax = new Vector2(1f, 0f);
        sheetRect.pivot = new Vector2(1f, 0f);
        sheetRect.sizeDelta = new Vector2(400f, 340f);
        sheetRect.anchoredPosition = new Vector2(-40f, 120f);

        var pageGo = new GameObject("Page");
        pageGo.transform.SetParent(sheet.transform, false);

        page = pageGo.AddComponent<RawImage>();

        var pageRect = pageGo.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0.5f, 1f);
        pageRect.anchorMax = new Vector2(0.5f, 1f);
        pageRect.pivot = new Vector2(0.5f, 1f);
        pageRect.sizeDelta = new Vector2(344f, 258f);
        pageRect.anchoredPosition = new Vector2(0f, -26f);

        caption = Label("Caption", sheet.transform, 19f, new Vector2(0f, 34f), new Vector2(360f, 40f));

        sheet.SetActive(false);
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
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = area;
        rect.anchoredPosition = at;

        return text;
    }
}
