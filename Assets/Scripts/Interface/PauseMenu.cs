using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escape pauses. There was no way to stop, change anything, or leave without
/// killing the process, and the settings that matter most for how the game runs
/// were only reachable by selecting an object in the inspector.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>Read by the camera, which should not keep turning while paused.</summary>
    public static bool Paused { get; private set; }

    private ChunkManager world;
    private TimeOfDay clock;
    private GameObject panel;
    private TMP_Text readout;
    private TMP_FontAsset font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        Paused = false;

        if (FindFirstObjectByType<PauseMenu>() == null)
        {
            new GameObject("Pause (runtime)").AddComponent<PauseMenu>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();
        clock = FindFirstObjectByType<TimeOfDay>();
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        BuildUi();
    }

    /// <summary>Lets the world list hide the pause card while it is over it.</summary>
    public void ShowPanel(bool on)
    {
        if (panel != null) panel.SetActive(on);
    }

    private void Update()
    {
        // The world list has its own Escape, and keeps it for a frame after it
        // closes so the same press does not unpause behind it.
        if (WorldsScreen.Blocking) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Escape backs out of whatever is open before it pauses, which is
            // what everyone expects it to do.
            if (!Paused && ScreenState.WantsCursor)
            {
                ScreenState.Open(ScreenState.Screen.None);
                ScreenState.Close(ScreenState.Screen.None);
            }
            else
            {
                Toggle(!Paused);
            }
        }

        if (Paused)
        {
            Refresh();
        }
    }

    private void Toggle(bool on)
    {
        Paused = on;
        panel.SetActive(on);

        // Time.timeScale of zero also stops the day cycle, which is the point.
        Time.timeScale = on ? 0f : 1f;

        if (on) ScreenState.Open(ScreenState.Screen.Pause);
        else ScreenState.Close(ScreenState.Screen.Pause);

        Ambience.Instance?.Click(on ? 0.9f : 1.1f);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Pause Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

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

        var card = cardGo.AddComponent<RawImage>();
        card.texture = ParchmentPanel.Create(560, 640);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 640f);

        ParchmentPanel.Shade(cardRect, 38f);

        readout = Label("Readout", cardGo.transform, 21f, new Vector2(0f, 150f), new Vector2(470f, 300f));
        readout.alignment = TextAlignmentOptions.Top;

        Button("Worlds", new Vector2(0f, -30f), OpenWorlds);
        Button("Draw further", new Vector2(0f, -92f), () => Adjust(+1));
        Button("Draw closer", new Vector2(0f, -154f), () => Adjust(-1));
        Button("Longer days", new Vector2(0f, -216f), () => AdjustDay(+5f));
        Button("Shorter days", new Vector2(0f, -278f), () => AdjustDay(-5f));

        panel.SetActive(false);
        Refresh();

        void Button(string text, Vector2 at, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(text);
            go.transform.SetParent(cardGo.transform, false);

            var image = go.AddComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.color = new Color(0.29f, 0.24f, 0.17f, 0.85f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(380f, 56f);
            rect.anchoredPosition = at;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var label = Label(text + " label", go.transform, 20f, Vector2.zero, new Vector2(380f, 56f));
            label.text = text;
            label.color = new Color(0.94f, 0.90f, 0.82f);
            label.alignment = TextAlignmentOptions.Center;
        }
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

    private void OpenWorlds()
    {
        WorldsScreen.Instance?.Open();
    }

    private void Adjust(int by)
    {
        if (world == null) return;

        world.SetViewRadius(world.ViewRadius + by);
        Refresh();
    }

    private void AdjustDay(float by)
    {
        if (clock == null) return;

        clock.SetDayLength(clock.DayLengthMinutes + by);
        Refresh();
    }

    private void Refresh()
    {
        if (readout == null) return;

        string seed = world != null ? world.WorldSeed.ToString() : "-";
        string radius = world != null ? world.ViewRadius.ToString() : "-";
        string day = clock != null ? clock.DayLengthMinutes.ToString("F0") : "-";
        string time = clock != null ? clock.Clock() + " " + clock.Label() : "-";

        string here = WorldLibrary.HasCurrent ? WorldLibrary.Current.name : "unnamed";

        readout.text =
            "<size=140%><b>PAUSED</b></size>\n\n" +
            "<size=110%>" + here + "</size>\n" +
            "seed " + seed + "\n" +
            time + "\n\n" +
            "draw distance " + radius + " chunks (" + (int)(int.Parse(radius == "-" ? "0" : radius) * 30) + "m)\n" +
            "day length " + day + " min\n\n" +
            "<size=85%>Esc to go back  ·  M map  ·  G sketchbook  ·  J journal  ·  F to draw</size>";
    }
}
