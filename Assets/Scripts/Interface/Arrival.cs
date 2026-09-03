using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The first few minutes, once ever. A page at the start says what the book is
/// for, one animal is put out in front so there is something to point at, and
/// after that nothing is said until the moment it would be useful: when there
/// is something worth drawing in view, and when the first drawing lands.
///
/// Nothing here explains anything twice. The composition hints already talk you
/// through a drawing once the glass is up, and the book itself lists what is
/// still blank, so this only has to get somebody as far as holding the key.
/// </summary>
public class Arrival : MonoBehaviour
{
    private const string TaughtKey = "tileworld.taught";

    private enum Step { Waiting, Reading, Looking, Drawing, Done }

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private GameObject panel;
    private Step step = Step.Waiting;
    private float since;
    private float said;
    private float tried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (PlayerPrefs.GetInt(TaughtKey, 0) == 1) return;

        if (FindFirstObjectByType<Arrival>() == null)
        {
            new GameObject("Arrival (runtime)").AddComponent<Arrival>();
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

        // Somebody who has already drawn something does not need telling, but
        // this is not marked as taught: they may yet start a world of their own
        // from nothing, and the page belongs at the start of that one.
        if (FieldGuide.Entries > 0)
        {
            enabled = false;
            return;
        }

        FieldGuide.Filled += OnFilled;
    }

    private void OnDestroy()
    {
        FieldGuide.Filled -= OnFilled;
    }

    private void Update()
    {
        if (player == null)
        {
            player = world.PlayerTransform;
            eye = Camera.main;
            return;
        }

        since += Time.deltaTime;

        switch (step)
        {
            case Step.Waiting:

                // let the ground finish arriving before saying anything
                if (since < 1.2f || ScreenState.Current != ScreenState.Screen.None) return;

                Open();
                break;

            case Step.Reading:

                // Keep trying while the page is covering the screen. Whether a
                // kind suits the ground just there is a matter of the hour and
                // the tile, so one go at it is not a promise of anything.
                if (Time.time - tried > 0.4f)
                {
                    tried = Time.time;
                    if (!AnythingNear(32f)) Wildlife.BringOneClose();
                }

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
                {
                    Close();
                }

                break;

            case Step.Looking:

                if (InView() && Time.time - said > 12f)
                {
                    Notices.Show("Something to draw. Hold F while you look at it.");
                    said = Time.time;
                }

                break;
        }
    }

    /// <summary>Is there anything at all within reach, in view or not?</summary>
    private bool AnythingNear(float within)
    {
        if (player == null) return false;

        foreach (var animal in FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(animal.transform.position, player.position) < within) return true;
        }

        return false;
    }

    /// <summary>Is there an animal in front of them, near enough to draw?</summary>
    private bool InView()
    {
        if (eye == null) eye = Camera.main;
        if (eye == null) return false;

        foreach (var animal in FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            Vector3 to = animal.transform.position - eye.transform.position;
            float away = to.magnitude;

            if (away > 34f || away < 2f) continue;
            if (Vector3.Dot(eye.transform.forward, to / away) < 0.55f) continue;

            return true;
        }

        return false;
    }

    private void OnFilled(Subject subject, FieldGuide.Study study)
    {
        if (study != FieldGuide.Study.Sketch || step == Step.Done) return;

        step = Step.Done;
        Taught();

        // show them where it went, once
        Invoke(nameof(OpenTheBook), 2.4f);
    }

    private void OpenTheBook()
    {
        if (ScreenState.Current != ScreenState.Screen.None) return;

        FieldGuideScreen.Show();
        Notices.Show("Your drawing is in the book. G opens it.");
    }

    private static void Taught()
    {
        PlayerPrefs.SetInt(TaughtKey, 1);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------- paper

    private void Open()
    {
        step = Step.Reading;

        Build();

        ScreenState.Open(ScreenState.Screen.Arrival);

        // put something out in front while the page is covering the screen, so
        // it is standing there when they look up rather than arriving in view
        Wildlife.BringOneClose();
    }

    private void Close()
    {
        step = Step.Looking;
        said = Time.time - 9f;

        if (panel != null) Destroy(panel);

        ScreenState.Close(ScreenState.Screen.Arrival);
        Ambience.Instance?.Click();
    }

    private void Build()
    {
        var canvasGo = new GameObject("Arrival Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = canvasGo;

        var shadeGo = new GameObject("Shade");
        shadeGo.transform.SetParent(canvasGo.transform, false);

        var shade = shadeGo.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.62f);

        var shadeRect = shadeGo.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;

        int wide = 840, tall = 560;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(canvasGo.transform, false);

        var paper = cardGo.AddComponent<RawImage>();

        // made at the size it is shown at: stretched, the border thickens
        paper.texture = ParchmentPanel.Create(wide, tall);

        var card = cardGo.GetComponent<RectTransform>();
        card.sizeDelta = new Vector2(wide, tall);
        card.anchoredPosition = Vector2.zero;

        ParchmentPanel.Shade(card);

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        Line(card, font, "The field book", 26f, FontStyles.Bold, new Vector2(0f, 196f), 44f);

        Line(card, font,
            "It was somebody else's first. They drew what they\n"
            + "found, and wrote down where they found it.\n"
            + "Most of the pages are still empty.\n\n"
            + "Hold F to draw whatever you are looking at.\n"
            + "Press G to open the book.",
            21f, FontStyles.Normal, new Vector2(0f, 24f), 300f);

        Line(card, font, "press space", 17f, FontStyles.Italic, new Vector2(0f, -206f), 40f);
    }

    private static void Line(RectTransform card, TMP_FontAsset font, string what, float size,
                             FontStyles style, Vector2 at, float tall)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(card, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = what;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = style == FontStyles.Italic ? ParchmentPanel.InkFaint : ParchmentPanel.Ink;
        text.lineSpacing = 14f;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(700f, tall);
        rect.anchoredPosition = at;
    }
}
