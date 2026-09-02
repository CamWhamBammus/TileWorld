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

    [Tooltip("Held down to draw. Doing it merely by standing still was not something anybody was going to guess.")]
    [SerializeField] private KeyCode drawKey = KeyCode.F;

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private Quarry subject;
    private float progress;

    // Keeping hold of things for a moment. A branch crossing in front of an
    // animal, or it stepping behind a rock, used to drop the subject, blink the
    // frame off the screen and throw away the drawing you were part way through.
    private float holding;          // how long the subject may be kept while unseen
    private float unsteady;         // how long the player has actually been moving
    private bool drewThisHold;      // one page to a press of the button
    private string hint;            // the standing hint, changed slowly
    private string pending;
    private float pendingSince;
    private float fade;             // what the frame and the bar are showing at

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

    private GameObject finder;       // the frame the page will take
    private GameObject sheet;        // the drawing, held up for a moment after
    private RawImage page;
    private TMP_Text caption;
    private float showUntil;
    private float sheetFade;
    private CanvasGroup sheetShowing;

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

        if (sheet != null)
        {
            bool wanted = Time.time < showUntil && !ScreenState.WantsCursor;

            sheetFade = Mathf.MoveTowards(sheetFade, wanted ? 1f : 0f, Time.deltaTime * 3f);

            // Not GetComponent ?? AddComponent: a component that is not there
            // comes back as a reference to nothing rather than as no reference,
            // so the coalescing operator keeps it and the group is never added.
            if (sheetShowing == null && !sheet.TryGetComponent(out sheetShowing))
            {
                sheetShowing = sheet.AddComponent<CanvasGroup>();
            }

            sheetShowing.alpha = sheetFade;

            bool up = sheetFade > 0.02f;
            if (sheet.activeSelf != up) sheet.SetActive(up);
        }

        // Nothing is drawn with a screen open in front of your face.
        if (ScreenState.WantsCursor)
        {
            Show(false);
            return;
        }

        Notice();

        var candidate = Nearest(true);

        // Whatever is in front of you now, or what was a moment ago.
        if (candidate.Any)
        {
            if (!subject.Same(candidate) && (progress <= 0.02f || !subject.Any || holding <= 0f))
            {
                subject = candidate;
                progress = 0f;
            }

            holding = 1.4f;
        }
        else
        {
            holding -= Time.deltaTime;
        }

        bool have = candidate.Any || (subject.Any && holding > 0f);
        var working = candidate.Any ? candidate : subject;

        Frame(have);

        var candidateStanding = have ? SketchBook.Made(working.What) : null;

        // A stumble should not throw the drawing away; walking off should.
        unsteady = Stalking.Steady ? 0f : unsteady + Time.deltaTime;

        bool steady = !working.What.Wild || unsteady < 0.35f;
        bool drawing = Input.GetKey(drawKey);

        if (!drawing) drewThisHold = false;

        if (!have || !steady || !drawing)
        {
            // A drawing left half done is worth nothing, but it fades rather
            // than snapping away, so a moment's wobble is not fatal.
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 0.5f);

            if (progress <= 0.01f && holding <= 0f) subject = default;

            Show(have);

            if (!have) Refresh("keep it in sight");
            else if (!steady) Refresh("hold still");
            else Refresh("hold " + drawKey + " to draw the " + working.What.Name);

            return;
        }

        subject = working;

        // A subject already drawn can be drawn again: the book keeps whichever
        // of the two is the better, so there is a reason to go back.
        var standing = SketchBook.Made(subject.What);

        string trouble = Composition(subject, standing);

        // One drawing to a press: holding the button down through a finished
        // page should not immediately start another.
        if (drewThisHold)
        {
            Show(true);
            Refresh("let go, then hold " + drawKey + " again to draw it once more");

            return;
        }

        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / seconds);

        Show(true);
        Refresh(trouble ?? (standing != null && !string.IsNullOrEmpty(standing.Verdict)
                            ? "drawing — your best so far was " + standing.Verdict
                            : null));

        if (progress >= 1f)
        {
            // The drawing is of this one, from here, as you framed it.
            var made = SketchBook.Draw(subject.What, subject.Body, Eye());

            drewThisHold = true;

            bool first = FieldGuide.Record(subject.What, FieldGuide.Study.Sketch);

            if (made != null)
            {
                if (SketchBook.Beaten)
                {
                    Notices.Show(made.Verdict + " — the book keeps the better one you had");
                }
                else
                {
                    Notices.Show((first ? "You draw the " + subject.What.Name + " — " : "")
                               + made.Verdict + (first ? "" : ", better than before"));
                }

                Ambience.Instance?.Click(1.15f);

                var kept = SketchBook.Of(subject.What);

                if (kept != null)
                {
                    page.texture = kept;
                    caption.text = made.Verdict + "  ·  G for the book";
                    showUntil = Time.time + 4.5f;
                }
            }

            progress = 0f;
            subject = default;
            Show(false);
        }
    }

    /// <summary>
    /// What is wrong with the picture as it stands, while you can still do
    /// something about it. Null when there is nothing worth saying, which is
    /// the game's way of telling you to hold where you are.
    /// </summary>
    private string Composition(Quarry quarry, SketchBook.Page standing)
    {
        var camera = Eye();

        if (camera == null || !quarry.Any) return null;

        var bounds = new Bounds(quarry.Body.position, Vector3.one * 0.1f);

        foreach (var piece in quarry.Body.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(piece.bounds);

        // the page takes the middle of your view, four across by three down
        float pageHigh = Screen.height;
        float pageWide = pageHigh * 4f / 3f;
        float left = (Screen.width - pageWide) * 0.5f;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z);

            Vector3 dot = camera.WorldToScreenPoint(corner);

            if (dot.z <= 0f) return "it is behind you";

            min = Vector2.Min(min, dot);
            max = Vector2.Max(max, dot);
        }

        if (min.x < left || max.x > left + pageWide || min.y < 0f || max.y > pageHigh)
            return "some of it is off the page";

        float want = SketchBook.Filling(quarry.What);
        float showing = ((max.x - min.x) * (max.y - min.y)) / (pageWide * pageHigh) * 0.35f;

        if (showing < want * 0.35f) return "too far off to draw well";
        if (showing > want * 3.5f) return "too close, it crowds the paper";

        Vector3 facing = quarry.Body.forward;
        facing.y = 0f;
        Vector3 view = camera.transform.forward;
        view.y = 0f;

        if (facing.sqrMagnitude > 0.01f
            && 1f - Mathf.Abs(Vector3.Dot(facing.normalized, view.normalized)) < 0.25f)
        {
            return "get round to one side of it";
        }

        return null;
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

    /// <summary>
    /// How far off a thing is, or less than nothing if it cannot be seen.
    ///
    /// Sighted from the player rather than from the camera. The camera is
    /// behind them, so a line from it to anything in front passes through the
    /// player's own body — which is a collider, and was quietly rejecting most
    /// of what you were plainly looking at.
    /// </summary>
    private float Look(Camera camera, Vector3 at, float range, bool strict)
    {
        Vector3 eyes = player.position + Vector3.up * 1.5f;
        Vector3 to = at - eyes;
        float distance = to.magnitude;

        if (distance > range) return -1f;
        if (Vector3.Dot(camera.transform.forward, (at - camera.transform.position).normalized)
            < (strict ? 0.52f : 0.45f)) return -1f;
        if (Blocked(eyes, at)) return -1f;

        return distance;
    }

    /// <summary>Whether anything but the player themselves is in the way.</summary>
    private bool Blocked(Vector3 from, Vector3 to)
    {
        Vector3 out_ = to - from;
        float reach = out_.magnitude;

        if (reach < 0.01f) return false;

        foreach (var hit in Physics.RaycastAll(from, out_ / reach, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            if (player != null && hit.transform.IsChildOf(player)) continue;

            return true;
        }

        return false;
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

    private CanvasGroup showing;

    private void Show(bool on)
    {
        if (panel == null) return;

        // Brought up and taken away with the frame, rather than blinking on and
        // off every time a branch passes in front of the animal.
        if (showing == null && !panel.TryGetComponent(out showing)) showing = panel.AddComponent<CanvasGroup>();

        showing.alpha = fade;

        bool up = on || fade > 0.02f;

        if (panel.activeSelf != up) panel.SetActive(up);
    }

    private void Refresh(string trouble)
    {
        if (fill == null || label == null) return;

        fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

        // Held for a third of a second before it changes, or it flickers
        // between two true things as the animal shifts its weight.
        if (trouble != pending)
        {
            pending = trouble;
            pendingSince = Time.time;
        }
        else if (Time.time - pendingSince > 0.33f)
        {
            hint = trouble;
        }

        string name = subject.Any ? subject.What.Name : "it";

        label.text = hint == null
            ? "drawing the " + name
            : "<color=#8B7860>" + hint + "</color>";
    }

    /// <summary>The frame, brought up and taken away rather than snapped on and off.</summary>
    private void Frame(bool wanted)
    {
        fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, Time.deltaTime * 4f);

        if (finder == null) return;

        bool on = fade > 0.02f;

        if (finder.activeSelf != on) finder.SetActive(on);

        if (!on) return;

        foreach (var mark in finder.GetComponentsInChildren<RawImage>())
        {
            var colour = mark.color;
            colour.a = 0.55f * fade;
            mark.color = colour;
        }
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

        // The frame the page will take. Corners only: a full box across the
        // middle of the screen would be a nuisance to look through.
        finder = new GameObject("Finder");
        finder.transform.SetParent(canvasGo.transform, false);

        var finderRect = finder.AddComponent<RectTransform>();
        finderRect.anchorMin = new Vector2(0.5f, 0f);
        finderRect.anchorMax = new Vector2(0.5f, 1f);
        finderRect.pivot = new Vector2(0.5f, 0.5f);
        finderRect.offsetMin = new Vector2(0f, 0f);
        finderRect.offsetMax = new Vector2(0f, 0f);

        var fit = finder.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fit.aspectRatio = 4f / 3f;

        for (int corner = 0; corner < 4; corner++)
        {
            float ax = (corner % 2 == 0) ? 0f : 1f;
            float ay = (corner < 2) ? 1f : 0f;

            Bracket(finder.transform, ax, ay, new Vector2(64f, 4f));
            Bracket(finder.transform, ax, ay, new Vector2(4f, 64f));
        }

        finder.SetActive(false);

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

        // Under the drawing, not across it: the label helper measures from the
        // middle of what it is given, and the page fills the top of the sheet.
        caption = Label("Caption", sheet.transform, 19f, new Vector2(0f, -142f), new Vector2(360f, 40f));

        sheet.SetActive(false);
        panel.SetActive(false);
    }

    /// <summary>One arm of a corner mark.</summary>
    private void Bracket(Transform parent, float ax, float ay, Vector2 size)
    {
        var go = new GameObject("Bracket");
        go.transform.SetParent(parent, false);

        var mark = go.AddComponent<RawImage>();
        mark.texture = Texture2D.whiteTexture;
        mark.color = new Color(0.94f, 0.91f, 0.84f, 0.55f);
        mark.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(ax, ay);
        rect.anchorMax = new Vector2(ax, ay);
        rect.pivot = new Vector2(ax, ay);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(ax == 0f ? 18f : -18f, ay == 0f ? 18f : -18f);
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
