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

    private static readonly Color Unexplored = new Color(0.09f, 0.10f, 0.09f);
    private static readonly Color Grid = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color Player = new Color(1f, 0.86f, 0.35f);
    private static readonly Color Origin = new Color(0.85f, 0.45f, 0.30f);

    private ChunkManager world;
    private Transform player;

    private Canvas canvas;
    private GameObject panel;
    private RawImage image;
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
        imageRect.sizeDelta = new Vector2(880f, 880f);

        panel.SetActive(false);
    }

    private void Redraw()
    {
        ComputeBounds();
        Fill(Unexplored);

        int seed = world.WorldSeed;

        foreach (Vector2Int chunk in ExplorationLog.Visited)
        {
            Vector3 centre = WorldGrid.ChunkCenter(chunk);
            int tileX = Mathf.RoundToInt(centre.x / WorldGrid.TileSize);
            int tileZ = Mathf.RoundToInt(centre.z / WorldGrid.TileSize);

            float height = WorldHeight.SurfaceY(tileX, tileZ, seed) - WorldHeight.BaseSurfaceY;
            DrawCell(chunk, HeightColour(height));
        }

        DrawGrid();

        if (ExplorationLog.HasVisited(Vector2Int.zero))
        {
            DrawMarker(WorldGrid.ChunkCenter(Vector2Int.zero), Origin, 2);
        }

        if (player != null)
        {
            DrawMarker(player.position, Player, 3);
            DrawFacing(player);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
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

        Color low = new Color(0.24f, 0.36f, 0.22f);
        Color mid = new Color(0.47f, 0.50f, 0.30f);
        Color high = new Color(0.62f, 0.60f, 0.56f);
        Color peak = new Color(0.92f, 0.94f, 0.96f);

        if (t < 0.35f) return Color.Lerp(low, mid, t / 0.35f);
        if (t < 0.70f) return Color.Lerp(mid, high, (t - 0.35f) / 0.35f);
        return Color.Lerp(high, peak, (t - 0.70f) / 0.30f);
    }

    private void Fill(Color c)
    {
        Color32 c32 = c;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c32;
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

    private void DrawGrid()
    {
        Color32 c = Grid;
        int spanX = (max.x - min.x + 1) * cell;
        int spanY = (max.y - min.y + 1) * cell;

        for (int gx = 0; gx <= spanX; gx += cell)
            for (int y = 0; y < spanY; y++) Plot(drawOffset.x + gx, drawOffset.y + y, c);

        for (int gy = 0; gy <= spanY; gy += cell)
            for (int x = 0; x < spanX; x++) Plot(drawOffset.x + x, drawOffset.y + gy, c);
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

    private void DrawMarker(Vector3 world, Color colour, int radius)
    {
        Vector2 p = ToPixel(world);
        Color32 c = colour;

        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radius * radius) continue;
            Plot(Mathf.RoundToInt(p.x) + x, Mathf.RoundToInt(p.y) + y, c);
        }
    }

    /// <summary>A stub in the direction the player is looking, so the map has an orientation.</summary>
    private void DrawFacing(Transform target)
    {
        Vector2 from = ToPixel(target.position);
        Vector3 forward = target.forward;
        Vector2 dir = new Vector2(forward.x, forward.z);

        if (dir.sqrMagnitude < 0.0001f)
        {
            return;   // looking straight up or down; no heading to draw
        }

        dir.Normalize();

        Color32 c = Player;
        int length = Mathf.Max(6, cell);

        for (int i = 0; i < length; i++)
        {
            Vector2 p = from + dir * i;
            Plot(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), c);
        }
    }

    private void Plot(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= textureSize || y >= textureSize) return;
        pixels[y * textureSize + x] = c;
    }
}
