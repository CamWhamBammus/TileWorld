using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The map the game is named after: everywhere you have been, drawn as a grid
/// of chunks shaded by terrain height, with unexplored ground left blank.
///
/// It builds its own canvas at runtime rather than living in the scene, so the
/// feature is self-contained — drop the script in and it works. Move it onto a
/// scene object later if you want to art-direct it.
/// </summary>
public class WorldMap : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    [Header("Look")]
    [SerializeField] private int textureSize = 512;
    [SerializeField, Range(2, 24)] private int maxCellPixels = 14;
    [SerializeField] private int marginChunks = 2;

    // Unexplored ground is blank paper, not a black void — the map reads as a
    // chart being filled in rather than a grid floating in space.
    private static readonly Color Paper = new Color(0.902f, 0.855f, 0.749f);
    private static readonly Color PaperDark = new Color(0.847f, 0.788f, 0.671f);
    private static readonly Color Ink = new Color(0.286f, 0.227f, 0.169f);
    private static readonly Color InkFaint = new Color(0.545f, 0.470f, 0.373f);
    private static readonly Color Player = new Color(0.706f, 0.208f, 0.153f);
    private static readonly Color Mark = new Color(0.361f, 0.267f, 0.176f);
    private static readonly Color Origin = new Color(0.298f, 0.353f, 0.259f);

    private ChunkManager world;
    private Transform player;

    private Canvas canvas;
    private GameObject panel;
    private RawImage image;
    private TMP_Text titleText;
    private TMP_Text statsText;
    private Texture2D texture;
    private Color32[] pixels;

    private bool open;
    private bool dirty = true;
    private int lastDrawnCount = -1;
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    // Bounds of the drawn area, in chunks, and where it lands on the texture.
    private Vector2Int min, max;
    private int cell;
    private Vector2Int drawOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<WorldMap>() != null)
        {
            return;
        }

        var go = new GameObject("World Map (runtime)");
        go.AddComponent<WorldMap>();
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null)
        {
            Debug.LogWarning("[WorldMap] No ChunkManager in the scene — the map has nothing to draw.");
            enabled = false;
            return;
        }

        player = world.PlayerTransform;
        BuildUi();
        ExplorationLog.ChunkDiscovered += OnDiscovered;
        Debug.Log("[WorldMap] Press " + toggleKey + " for the map.");
    }

    private void OnDestroy()
    {
        ExplorationLog.ChunkDiscovered -= OnDiscovered;
    }

    private void OnDiscovered(Vector2Int chunk)
    {
        dirty = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            panel.SetActive(open);

            if (open)
            {
                dirty = true;
            }
        }

        if (!open)
        {
            return;
        }

        // Redraw when the picture would actually change, not every frame.
        Vector2Int chunk = player != null ? WorldGrid.WorldToChunk(player.position) : Vector2Int.zero;

        if (dirty || ExplorationLog.Count != lastDrawnCount || chunk != lastPlayerChunk)
        {
            Redraw();
            lastDrawnCount = ExplorationLog.Count;
            lastPlayerChunk = chunk;
            dirty = false;
        }
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Map Canvas");
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("Map Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        // RawImage with an explicit texture rather than Image with no sprite,
        // which does not reliably draw anything.
        var backdrop = panel.AddComponent<RawImage>();
        backdrop.texture = Texture2D.whiteTexture;
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var imageGo = new GameObject("Map Image");
        imageGo.transform.SetParent(panel.transform, false);

        texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        pixels = new Color32[textureSize * textureSize];

        image = imageGo.AddComponent<RawImage>();
        image.texture = texture;

        var imageRect = imageGo.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(820f, 820f);
        imageRect.anchoredPosition = new Vector2(0f, -18f);

        titleText = MakeLabel("Title", panel.transform, 34f, FontStyles.Bold,
            new Vector2(0f, 468f), new Vector2(820f, 48f), TextAlignmentOptions.Center);
        titleText.characterSpacing = 12f;
        titleText.text = "<color=#F0E6D2>THE SURVEY</color>";

        statsText = MakeLabel("Stats", panel.transform, 20f, FontStyles.Normal,
            new Vector2(0f, -458f), new Vector2(880f, 64f), TextAlignmentOptions.Center);

        panel.SetActive(false);
    }

    private TMP_Text MakeLabel(string name, Transform parent, float size, FontStyles style,
        Vector2 position, Vector2 area, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = new Color(0.94f, 0.90f, 0.82f);
        text.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = area;
        rect.anchoredPosition = position;

        return text;
    }

    private void Redraw()
    {
        ComputeBounds();
        DrawPaper();

        int seed = world.WorldSeed;

        // seen from a tower, but not walked: drawn washed out toward the paper
        foreach (Vector2Int chunk in ExplorationLog.Surveyed)
        {
            DrawCell(chunk, Color.Lerp(TerrainColour(chunk, seed), Paper, 0.55f));
        }

        foreach (Vector2Int chunk in ExplorationLog.Visited)
        {
            DrawCell(chunk, TerrainColour(chunk, seed));
        }

        DrawSurveyLines();
        DrawFrame();
        DrawCompass();

        DrawLandmarks();

        if (ExplorationLog.HasVisited(Vector2Int.zero))
        {
            DrawSpawn(WorldGrid.ChunkCenter(Vector2Int.zero));
        }

        if (player != null)
        {
            DrawHeading(player);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        UpdateStats();
    }

    private Color TerrainColour(Vector2Int chunk, int seed)
    {
        Vector3 centre = WorldGrid.ChunkCenter(chunk);
        int tileX = Mathf.RoundToInt(centre.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(centre.z / WorldGrid.TileSize);

        return HeightColour(WorldHeight.SurfaceY(tileX, tileZ, seed) - WorldHeight.BaseSurfaceY);
    }

    private void UpdateStats()
    {
        Vector2Int chunk = player != null ? WorldGrid.WorldToChunk(player.position) : Vector2Int.zero;
        int seed = world.WorldSeed;
        float here = WorldHeight.SurfaceY(
            Mathf.RoundToInt(player.position.x / WorldGrid.TileSize),
            Mathf.RoundToInt(player.position.z / WorldGrid.TileSize), seed) - WorldHeight.BaseSurfaceY;

        statsText.text =
            "<color=#D8CDB4>" + ExplorationLog.Count + " chunks charted</color>" +
            "   <color=#8A7E68>|</color>   " +
            "<color=#D8CDB4>grid " + chunk.x + ", " + chunk.y + "</color>" +
            "   <color=#8A7E68>|</color>   " +
            "<color=#D8CDB4>elevation " + here.ToString("F1") + "m</color>" +
            "   <color=#8A7E68>|</color>   " +
            "<color=#D8CDB4>" + LandmarkLog.Count + " landmarks</color>" +
            (TimeOfDay.Instance != null
                ? "   <color=#8A7E68>|</color>   <color=#D8CDB4>" + TimeOfDay.Instance.Clock() +
                  " <color=#8A7E68>" + TimeOfDay.Instance.Label() + "</color></color>"
                : "") +
            "\n<size=16><color=#8A7E68>lowland <color=#5C7A45>\u25A0</color>  hills <color=#8A8250>\u25A0</color>  " +
            "slopes <color=#9A907F>\u25A0</color>  peaks <color=#E8E4DC>\u25A0</color>  " +
            "landmark <color=#5C442D>\u25C6</color>     " +
            toggleKey + " to close</color></size>";
    }

    /// <summary>Blank chart paper, with enough grain that it does not read as flat fill.</summary>
    private void DrawPaper()
    {
        for (int y = 0; y < textureSize; y++)
        for (int x = 0; x < textureSize; x++)
        {
            // cheap deterministic speckle
            int h = (x * 73856093) ^ (y * 19349663);
            float n = ((h >> 8) & 0xFF) / 255f;

            Color c = Color.Lerp(Paper, PaperDark, n * 0.16f);
            pixels[y * textureSize + x] = c;
        }
    }

    /// <summary>Faint ruled grid, drawn only across the charted region.</summary>
    private void DrawSurveyLines()
    {
        Color32 c = Color.Lerp(PaperDark, InkFaint, 0.35f);

        int spanX = (max.x - min.x + 1) * cell;
        int spanY = (max.y - min.y + 1) * cell;

        for (int gx = 0; gx <= spanX; gx += cell)
            for (int y = 0; y < spanY; y++) Plot(drawOffset.x + gx, drawOffset.y + y, c);

        for (int gy = 0; gy <= spanY; gy += cell)
            for (int x = 0; x < spanX; x++) Plot(drawOffset.x + x, drawOffset.y + gy, c);
    }

    /// <summary>A double rule inset from the edge, like a printed chart.</summary>
    private void DrawFrame()
    {
        Color32 ink = Ink;
        Color32 faint = InkFaint;

        Rule(6, ink, 2);
        Rule(13, faint, 1);
    }

    private void Rule(int inset, Color32 c, int thickness)
    {
        for (int t = 0; t < thickness; t++)
        {
            int a = inset + t;
            int b = textureSize - 1 - a;

            for (int x = a; x <= b; x++)
            {
                Plot(x, a, c);
                Plot(x, b, c);
            }

            for (int y = a; y <= b; y++)
            {
                Plot(a, y, c);
                Plot(b, y, c);
            }
        }
    }

    /// <summary>North arrow in the top-right, so the chart has an orientation.</summary>
    private void DrawCompass()
    {
        int cx = textureSize - 42;
        int cy = textureSize - 42;

        Color32 ink = Ink;

        for (int y = -16; y <= 16; y++)
        for (int x = -16; x <= 16; x++)
        {
            int d = x * x + y * y;
            if (d <= 256 && d >= 225) Plot(cx + x, cy + y, ink);
        }

        // needle
        FillTriangle(new Vector2(cx, cy + 14), new Vector2(cx - 6, cy - 6), new Vector2(cx + 6, cy - 6), ink);

        // 'N' above the ring
        for (int y = 0; y < 9; y++)
        {
            Plot(cx - 4, cy + 20 + y, ink);
            Plot(cx + 4, cy + 20 + y, ink);
            int diag = Mathf.RoundToInt(Mathf.Lerp(-4f, 4f, y / 8f));
            Plot(cx + diag, cy + 28 - y, ink);
        }
    }

    /// <summary>Anything found gets inked onto the chart, the way a surveyor would.</summary>
    private void DrawLandmarks()
    {
        foreach (var pair in LandmarkLog.Found)
        {
            var placement = Landmarks.In(pair.Key, world.WorldSeed);

            if (!placement.Exists)
            {
                continue;
            }

            Vector2 p = ToPixel(placement.Position);
            int r = Mathf.Max(3, cell / 3);

            // a small diamond, so it reads differently from the round markers
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) <= r)
                {
                    Plot(Mathf.RoundToInt(p.x) + x, Mathf.RoundToInt(p.y) + y, Mark);
                }
            }
        }
    }

    private void DrawSpawn(Vector3 world)
    {
        Vector2 p = ToPixel(world);
        Color32 c = Origin;

        for (int i = -5; i <= 5; i++)
        {
            Plot(Mathf.RoundToInt(p.x) + i, Mathf.RoundToInt(p.y), c);
            Plot(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) + i, c);
        }
    }

    /// <summary>The player as an arrowhead pointing where they are looking.</summary>
    private void DrawHeading(Transform target)
    {
        Vector2 p = ToPixel(target.position);
        Vector3 forward = target.forward;
        Vector2 dir = new Vector2(forward.x, forward.z);

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.up;
        }

        dir.Normalize();
        Vector2 side = new Vector2(-dir.y, dir.x);

        float len = Mathf.Max(11f, cell * 0.9f);
        float wide = len * 0.5f;

        Vector2 tip = p + dir * len;
        Vector2 left = p - dir * (len * 0.35f) + side * wide;
        Vector2 right = p - dir * (len * 0.35f) - side * wide;

        FillTriangle(tip, left, right, Player);
        FillTriangle(tip, left, right, Player);
    }

    private void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 colour)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
        int maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
        int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
        int maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 0.0001f) return;

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float w0 = Edge(b, c, p) / area;
            float w1 = Edge(c, a, p) / area;
            float w2 = Edge(a, b, p) / area;

            if (w0 >= 0f && w1 >= 0f && w2 >= 0f) Plot(x, y, colour);
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p)
    {
        return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    /// <summary>Frames everything discovered, with a margin, and never zooms in past the cell cap.</summary>
    private void ComputeBounds()
    {
        min = new Vector2Int(int.MaxValue, int.MaxValue);
        max = new Vector2Int(int.MinValue, int.MinValue);

        foreach (Vector2Int c in ExplorationLog.Visited)
        {
            min = Vector2Int.Min(min, c);
            max = Vector2Int.Max(max, c);
        }

        foreach (Vector2Int c in ExplorationLog.Surveyed)
        {
            min = Vector2Int.Min(min, c);
            max = Vector2Int.Max(max, c);
        }

        if (ExplorationLog.Count == 0)
        {
            min = max = Vector2Int.zero;
        }

        min -= Vector2Int.one * marginChunks;
        max += Vector2Int.one * marginChunks;

        int span = Mathf.Max(max.x - min.x + 1, max.y - min.y + 1);
        cell = Mathf.Clamp(textureSize / Mathf.Max(1, span), 2, maxCellPixels);

        // Early on, the explored area is far smaller than the texture. Without
        // this the map would be drawn into the bottom-left corner.
        drawOffset = new Vector2Int(
            (textureSize - (max.x - min.x + 1) * cell) / 2,
            (textureSize - (max.y - min.y + 1) * cell) / 2
        );
    }

    private static Color HeightColour(float height)
    {
        float t = Mathf.Clamp01(height / WorldHeight.MaxRelief);

        Color low = new Color(0.361f, 0.478f, 0.271f);
        Color mid = new Color(0.541f, 0.510f, 0.314f);
        Color high = new Color(0.604f, 0.565f, 0.498f);
        Color peak = new Color(0.910f, 0.894f, 0.863f);

        if (t < 0.35f) return Color.Lerp(low, mid, t / 0.35f);
        if (t < 0.70f) return Color.Lerp(mid, high, (t - 0.35f) / 0.35f);
        return Color.Lerp(high, peak, (t - 0.70f) / 0.30f);
    }

    /// <summary>Chunk coordinates to the pixel of that chunk's lower-left corner.</summary>
    private Vector2Int CellOrigin(Vector2Int chunk)
    {
        return new Vector2Int(
            drawOffset.x + (chunk.x - min.x) * cell,
            drawOffset.y + (chunk.y - min.y) * cell
        );
    }

    private void DrawCell(Vector2Int chunk, Color colour)
    {
        Vector2Int o = CellOrigin(chunk);
        Color32 c = colour;

        for (int y = 0; y < cell; y++)
        for (int x = 0; x < cell; x++)
        {
            Plot(o.x + x, o.y + y, c);
        }
    }

    /// <summary>World position to map pixel, at sub-chunk precision.</summary>
    private Vector2 ToPixel(Vector3 world)
    {
        float chunkX = world.x / WorldGrid.ChunkWorldSize;
        float chunkZ = world.z / WorldGrid.ChunkWorldSize;

        return new Vector2(
            drawOffset.x + (chunkX - min.x) * cell,
            drawOffset.y + (chunkZ - min.y) * cell
        );
    }

    private void Plot(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= textureSize || y >= textureSize) return;
        pixels[y * textureSize + x] = c;
    }
}
