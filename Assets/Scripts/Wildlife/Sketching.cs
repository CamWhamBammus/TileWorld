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
    [Tooltip("How close you have to be to draw anything worth keeping.")]
    [SerializeField] private float reach = 22f;

    [Tooltip("And how close you have to be for what it is doing to count.")]
    [SerializeField] private float watching = 34f;

    [SerializeField] private float seconds = 4.2f;

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private Animal subject;
    private float progress;

    private GameObject panel;
    private TMP_Text label;
    private RectTransform fill;
    private TMP_FontAsset font;

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

        // Nothing is drawn with a screen open in front of your face.
        if (ScreenState.WantsCursor)
        {
            Show(false);
            return;
        }

        Notice();

        var candidate = Nearest(reach, true);

        if (candidate == null || !Stalking.Steady)
        {
            // A drawing left half done is worth nothing, but it fades rather
            // than snapping away, so a moment's wobble is not fatal.
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 0.55f);
            subject = progress > 0.01f ? subject : null;

            Show(progress > 0.01f);
            Refresh(candidate == null ? "keep it in sight" : "hold still");

            return;
        }

        if (subject != candidate)
        {
            subject = candidate;
            progress = 0f;
        }

        if (FieldGuide.Has(subject.Kind, FieldGuide.Study.Sketch))
        {
            Show(false);
            return;
        }

        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / seconds);

        Show(true);
        Refresh(null);

        if (progress >= 1f)
        {
            if (FieldGuide.Record(subject.Kind, FieldGuide.Study.Sketch))
            {
                Notices.Show("You draw the " + Fauna.Of(subject.Kind).Name
                           + "  ·  " + FieldGuide.Entries + "/4 entries done");

                Ambience.Instance?.Click(1.15f);
            }

            progress = 0f;
            subject = null;
            Show(false);
        }
    }

    /// <summary>
    /// The two studies that are a matter of being there rather than of holding
    /// still: what the animal was doing, and where you found it.
    /// </summary>
    private void Notice()
    {
        var seen = Nearest(watching, false);

        if (seen == null) return;

        float hour = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;

        if (!FieldGuide.Has(seen.Kind, FieldGuide.Study.Habit)
            && FieldGuide.Habit(seen.Kind, seen.Busy)
            && FieldGuide.Record(seen.Kind, FieldGuide.Study.Habit))
        {
            Notices.Show("Noted: " + FieldGuide.Habit(seen.Kind));
        }

        if (!FieldGuide.Has(seen.Kind, FieldGuide.Study.Country)
            && FieldGuide.Country(seen.Kind, seen.transform.position, world.WorldSeed, hour)
            && FieldGuide.Record(seen.Kind, FieldGuide.Study.Country))
        {
            Notices.Show("Noted: " + FieldGuide.Country(seen.Kind));
        }

        if (FieldGuide.Complete(seen.Kind) && !told.Contains(seen.Kind))
        {
            told.Add(seen.Kind);
            Notices.Show("The " + Fauna.Of(seen.Kind).Name + " entry is finished");
        }
    }

    private readonly System.Collections.Generic.HashSet<FaunaKind> told =
        new System.Collections.Generic.HashSet<FaunaKind>();

    /// <summary>The nearest animal you can actually see, if any.</summary>
    private Animal Nearest(float range, bool forDrawing)
    {
        var camera = Eye();

        if (camera == null) return null;

        Animal best = null;
        float closest = range;

        foreach (var animal in FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            if (forDrawing && animal.Busy == Doing.Fleeing) continue;

            Vector3 head = animal.Head;
            Vector3 to = head - camera.transform.position;
            float distance = to.magnitude;

            if (distance > closest) continue;
            if (Vector3.Dot(camera.transform.forward, to.normalized) < (forDrawing ? 0.62f : 0.5f)) continue;
            if (Physics.Linecast(camera.transform.position, head)) continue;

            closest = distance;
            best = animal;
        }

        return best;
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

        string name = subject != null ? Fauna.Of(subject.Kind).Name : "it";

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
