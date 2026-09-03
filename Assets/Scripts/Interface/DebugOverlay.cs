using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// F3 shows what the world is actually doing: frame time, how many chunks are
/// resident and drawn, how much geometry is going out, and where the player is.
/// Tuning draw distance by feel is guesswork without it.
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private ChunkManager world;
    private Transform player;
    private TMP_Text text;
    private GameObject panel;

    private float accumulated;
    private int frames;
    private float shownFps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<DebugOverlay>() == null)
        {
            new GameObject("Debug Overlay (runtime)").AddComponent<DebugOverlay>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();
        if (world != null) player = world.PlayerTransform;

        var canvasGo = new GameObject("Debug Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var background = panel.AddComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0f, 0f, 0f, 0.5f);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -80f);
        rect.sizeDelta = new Vector2(360f, 210f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);

        text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = 17f;
        text.color = new Color(0.86f, 0.88f, 0.84f);
        text.alignment = TextAlignmentOptions.TopLeft;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 12f);
        textRect.offsetMax = new Vector2(-14f, -12f);

        panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            panel.SetActive(!panel.activeSelf);
        }

        accumulated += Time.unscaledDeltaTime;
        frames++;

        if (accumulated >= 0.4f)
        {
            shownFps = frames / accumulated;
            accumulated = 0f;
            frames = 0;
        }

        if (!panel.activeSelf || world == null) return;

        var stats = world.Stats;
        Vector3 at = player != null ? player.position : Vector3.zero;
        var chunk = WorldGrid.WorldToChunk(at);

        text.text =
            Mathf.RoundToInt(shownFps) + " fps   " + (1000f / Mathf.Max(1f, shownFps)).ToString("F1") + " ms\n\n" +
            "chunks  " + stats.drawn + " drawn of " + stats.visible + "\n" +
            "        " + stats.resident + " resident, " + stats.pending + " queued\n" +
            "draws   " + stats.drawCalls + " calls, " + stats.instances + " instances\n" +
            "verts   ~" + (stats.instances * 3861 / 1000000f).ToString("F1") + " M\n\n" +
            "grid    " + chunk.x + ", " + chunk.y + "\n" +
            "height  " + at.y.ToString("F1") + "m\n" +
            "seed    " + world.WorldSeed
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            + "\n\nF8      show the first page again"
            + "\nshift-F8  put this world back to nothing"
#endif
            ;
    }
}
