#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A panel of things for working on the game rather than playing it: F8 opens
/// it, and it is compiled into the editor and development builds only, so it
/// cannot appear in anything shipped.
///
/// Mostly it is here to answer "go and look at it": a change to the snowfields
/// is no use if finding a snowfield is a ten minute walk. Every region in the
/// world is a button, with how far off the nearest one is written on it.
/// </summary>
public class DevTools : MonoBehaviour
{
    [SerializeField] private KeyCode openKey = KeyCode.F8;

    private ChunkManager world;
    private Transform player;
    private CharacterController body;

    private GameObject panel;
    private TMP_Text heading;
    private TMP_FontAsset font;

    private readonly Dictionary<Regions.Character, TMP_Text> labels =
        new Dictionary<Regions.Character, TMP_Text>();

    private readonly Dictionary<WaterSurface.Body, TMP_Text> waters =
        new Dictionary<WaterSurface.Body, TMP_Text>();

    private TMP_Text wipeLabel;
    private float askedAt = -99f;
    private bool open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<DevTools>() == null)
        {
            new GameObject("Dev Tools (runtime)").AddComponent<DevTools>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null) { enabled = false; return; }

        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        Build();
    }

    private void Update()
    {
        if (player == null && world != null) player = world.PlayerTransform;
        if (player != null && body == null) body = player.GetComponent<CharacterController>();

        if (Input.GetKeyDown(openKey)) Toggle(!open);

        if (open && Input.GetKeyDown(KeyCode.Escape)) Toggle(false);
    }

    private void Toggle(bool on)
    {
        open = on;

        if (panel != null) panel.SetActive(on);

        if (on)
        {
            ScreenState.Open(ScreenState.Screen.Dev);
            Refresh();
        }
        else
        {
            ScreenState.Close(ScreenState.Screen.Dev);
        }
    }

    /// <summary>Where the nearest region of a given sort is, and how far.</summary>
    private bool Nearest(Regions.Character want, out Vector2Int chunk, out float away)
    {
        chunk = default;
        away = 0f;

        if (player == null) return false;

        int seed = world.WorldSeed;
        var home = WorldGrid.WorldToChunk(player.position);

        for (int r = 0; r < 40; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;

            var at = new Vector2Int(home.x + dx * Regions.ChunksAcross, home.y + dz * Regions.ChunksAcross);

            if (Regions.CharacterAt(at, seed) != want) continue;

            var cell = Regions.CellOf(at);

            chunk = new Vector2Int(
                cell.x * Regions.ChunksAcross + Regions.ChunksAcross / 2,
                cell.y * Regions.ChunksAcross + Regions.ChunksAcross / 2);

            away = Vector3.Distance(player.position, WorldGrid.ChunkCenter(chunk));

            return true;
        }

        return false;
    }

    /// <summary>
    /// The nearest water of a given sort.
    ///
    /// Regions are found by walking out over regions, but a lake is not a
    /// region -- it is a thing inside one, and a pond is smaller still. So this
    /// walks out over tiles instead, asking the cheap question first: whether a
    /// tile is under water at all costs one look at the ground, and only the
    /// wet ones are then asked what sort of water they are.
    /// </summary>
    private bool NearestWater(WaterSurface.Body want, out Vector2Int tile, out float away)
    {
        tile = default;
        away = 0f;

        if (player == null) return false;

        int seed = world.WorldSeed;

        int fromX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int fromZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);

        const int Step = 3;
        const int Rings = 90;

        for (int r = 0; r < Rings; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;

            int tx = fromX + dx * Step, tz = fromZ + dz * Step;

            if (!WaterSurface.IsUnderwater(tx, tz, seed)) continue;
            if (WaterSurface.BodyAt(tx, tz, seed) != want) continue;

            tile = new Vector2Int(tx, tz);
            away = Vector2.Distance(new Vector2(fromX, fromZ), new Vector2(tx, tz)) * WorldGrid.TileSize;

            return true;
        }

        return false;
    }

    private void GoToWater(WaterSurface.Body want)
    {
        if (!NearestWater(want, out var tile, out _) || body == null)
        {
            Notices.Show("Dev: no " + want + " within reach.");
            return;
        }

        int seed = world.WorldSeed;

        // stand on the bank and look out over it
        for (int step = 1; step < 40; step++)
        for (int side = 0; side < 8; side++)
        {
            float a = side / 8f * Mathf.PI * 2f;

            int dx = tile.x + Mathf.RoundToInt(Mathf.Cos(a) * step);
            int dz = tile.y + Mathf.RoundToInt(Mathf.Sin(a) * step);

            if (WaterSurface.IsUnderwater(dx, dz, seed)) continue;

            body.enabled = false;
            player.position = new Vector3(dx * WorldGrid.TileSize,
                WorldHeight.SurfaceY(dx, dz, seed) + 1.4f, dz * WorldGrid.TileSize);
            player.rotation = Quaternion.LookRotation(
                new Vector3(tile.x - dx, 0f, tile.y - dz).normalized);
            body.enabled = true;

            Toggle(false);
            Notices.Show("Dev: on the bank of a " + want.ToString().ToLowerInvariant());
            return;
        }

        Notices.Show("Dev: found a " + want + " but no bank to stand on.");
    }

    /// <summary>
    /// The nearest structure of a kind, walking out over chunks. You arrive at
    /// the foot of its stair, facing it.
    /// </summary>
    private void GoToStructure(LandmarkKind want)
    {
        if (player == null || body == null) return;

        int seed = world.WorldSeed;
        var home = WorldGrid.WorldToChunk(player.position);

        for (int r = 0; r < 60; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;

            var at = Landmarks.In(new Vector2Int(home.x + dx, home.y + dz), seed);
            if (!at.Exists || at.Kind != want) continue;

            // the stair comes down the structure's +x side, before it is turned
            Vector3 toFoot = Quaternion.Euler(0f, at.Yaw, 0f) * new Vector3(Landmarks.All(want).Ahead * 2f + 5f, 0f, 0f);
            Vector3 stand = at.Position + toFoot;

            int tx = Mathf.RoundToInt(stand.x / WorldGrid.TileSize);
            int tz = Mathf.RoundToInt(stand.z / WorldGrid.TileSize);

            body.enabled = false;
            player.position = new Vector3(stand.x, WorldHeight.SurfaceY(tx, tz, seed) + 1.2f, stand.z);
            player.rotation = Quaternion.LookRotation(-toFoot.normalized, Vector3.up);
            body.enabled = true;

            Toggle(false);
            Notices.Show("Dev: the " + Landmarks.NameOf(want) + " in " + Regions.At(at.Chunk, seed).Name);
            return;
        }

        Notices.Show("Dev: no " + Landmarks.NameOf(want) + " within sixty chunks.");
    }

    private void GoTo(Regions.Character want)
    {
        if (!Nearest(want, out var chunk, out _) || body == null)
        {
            Notices.Show("Dev: no " + want + " within reach.");
            return;
        }

        int seed = world.WorldSeed;

        Vector3 middle = WorldGrid.ChunkCenter(chunk);

        int cx = Mathf.RoundToInt(middle.x / WorldGrid.TileSize);
        int cz = Mathf.RoundToInt(middle.z / WorldGrid.TileSize);

        // dry ground near the middle of it, so you do not arrive underwater
        for (int ring = 0; ring < 30; ring++)
        for (int ox = -ring; ox <= ring; ox++)
        for (int oz = -ring; oz <= ring; oz++)
        {
            if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oz)) != ring) continue;

            int tx = cx + ox, tz = cz + oz;

            if (WaterSurface.IsUnderwater(tx, tz, seed)) continue;
            if (WorldHeight.SurfaceY(tx, tz, seed) < WaterSurface.Level + 0.4f) continue;

            body.enabled = false;
            player.position = new Vector3(tx * WorldGrid.TileSize,
                WorldHeight.SurfaceY(tx, tz, seed) + 1.4f, tz * WorldGrid.TileSize);
            body.enabled = true;

            Toggle(false);
            Notices.Show("You come into " + Regions.At(chunk, seed).Name);
            return;
        }

        Notices.Show("Dev: found a " + want + " but nowhere dry to stand in it.");
    }

    private void Refresh()
    {
        if (player == null) return;

        int seed = world.WorldSeed;
        var here = Regions.At(WorldGrid.WorldToChunk(player.position), seed);

        if (heading != null)
        {
            heading.text = "Dev tools\n<size=17>standing in " + here.Name + ", which is "
                         + Regions.Describe(here.Character) + "</size>";
        }

        foreach (var pair in labels)
        {
            bool found = Nearest(pair.Key, out _, out float away);

            pair.Value.text = found
                ? pair.Key + "   <size=15>" + Mathf.RoundToInt(away) + " m</size>"
                : pair.Key + "   <size=15>none near</size>";
        }

        foreach (var pair in waters)
        {
            bool found = NearestWater(pair.Key, out _, out float away);

            pair.Value.text = found
                ? pair.Key + "   <size=15>" + Mathf.RoundToInt(away) + " m</size>"
                : pair.Key + "   <size=15>none near</size>";
        }

        if (wipeLabel != null) wipeLabel.text = "Put this world back to nothing";
    }

    private void Build()
    {
        var canvasGo = new GameObject("Dev Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var shade = panel.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.6f);

        var full = panel.GetComponent<RectTransform>();
        full.anchorMin = Vector2.zero;
        full.anchorMax = Vector2.one;
        full.offsetMin = Vector2.zero;
        full.offsetMax = Vector2.zero;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(panel.transform, false);

        var card = cardGo.AddComponent<RawImage>();
        card.texture = Texture2D.whiteTexture;

        // deliberately not the game's parchment: this is not part of the game
        card.color = new Color(0.11f, 0.12f, 0.14f, 0.97f);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(940f, 1000f);

        heading = Label("Heading", cardGo.transform, 26f, new Vector2(0f, 438f), new Vector2(880f, 90f));
        heading.color = new Color(0.85f, 0.87f, 0.90f);

        Label("Go", cardGo.transform, 17f, new Vector2(0f, 378f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST";

        var kinds = (Regions.Character[])System.Enum.GetValues(typeof(Regions.Character));

        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];

            float x = (i % 3 - 1) * 300f;
            float y = 330f - (i / 3) * 56f;

            labels[kind] = Button(kind.ToString(), cardGo.transform,
                new Vector2(x, y), new Vector2(285f, 52f), () => GoTo(kind));
        }

        Label("Water", cardGo.transform, 17f, new Vector2(0f, 108f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST WATER   (a lake is not a region, it is a thing inside one)";

        var bodies = (WaterSurface.Body[])System.Enum.GetValues(typeof(WaterSurface.Body));

        for (int i = 0; i < bodies.Length; i++)
        {
            var kind = bodies[i];

            waters[kind] = Button(kind.ToString(), cardGo.transform,
                new Vector2((i - 1) * 300f, 66f), new Vector2(285f, 52f), () => GoToWater(kind));
        }

        Label("Built", cardGo.transform, 17f, new Vector2(0f, 16f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST STRUCTURE";

        var built = (LandmarkKind[])System.Enum.GetValues(typeof(LandmarkKind));

        for (int i = 0; i < built.Length; i++)
        {
            var kind = built[i];

            Button(Landmarks.NameOf(kind), cardGo.transform,
                new Vector2((i % 3 - 1) * 300f, -26f - (i / 3) * 56f), new Vector2(285f, 52f), () => GoToStructure(kind));
        }

        Label("Keeping", cardGo.transform, 17f, new Vector2(0f, -210f), new Vector2(880f, 30f))
            .text = "THE FIRST FEW MINUTES";

        Button("Show the opening again", cardGo.transform,
            new Vector2(-230f, -260f), new Vector2(430f, 52f), () =>
            {
                Arrival.Replay();
                Toggle(false);
                Notices.Show("Dev: showing the first page again.");
            });

        wipeLabel = Button("Put this world back to nothing", cardGo.transform,
            new Vector2(230f, -260f), new Vector2(430f, 52f), Wipe);

        Label("Foot", cardGo.transform, 15f, new Vector2(0f, -330f), new Vector2(880f, 40f))
            .text = "F8 or Escape closes this. Editor and development builds only.";

        panel.SetActive(false);
    }

    private void Wipe()
    {
        // losing a world's drawings cannot be undone, so it is asked for twice
        if (Time.unscaledTime - askedAt > 4f)
        {
            askedAt = Time.unscaledTime;
            wipeLabel.text = "Press again to wipe it";
            return;
        }

        askedAt = -99f;

        Erase();

        Arrival.Replay();
        Toggle(false);
        Notices.Show("Dev: this world is back to nothing.");
    }

    /// <summary>Everything this world has found, out of the save and off the disk.</summary>
    private static void Erase()
    {
        var save = WorldLibrary.Current;

        if (save != null)
        {
            save.bookSubjects.Clear();
            save.bookStudies.Clear();
            save.bookWhere.Clear();

            save.guideKinds.Clear();
            save.guideStudies.Clear();

            save.drawingKeys.Clear();
            save.drawingQuality.Clear();
            save.drawingVerdict.Clear();
            save.drawingWhen.Clear();

            save.noticed.Clear();
            save.noticedWhere.Clear();
            save.noticedWhen.Clear();

            save.creaturesSeen.Clear();
            save.creatureChunks.Clear();

            WorldLibrary.Write(save);

            string drawings = System.IO.Path.Combine(Application.persistentDataPath, "drawings", save.id);

            if (System.IO.Directory.Exists(drawings))
            {
                try { System.IO.Directory.Delete(drawings, true); }
                catch (System.Exception e) { Debug.LogWarning("[Dev] left the drawings alone: " + e.Message); }
            }
        }

        FieldGuide.Clear();
        Notebook.Clear();
        SketchBook.Clear();
        SightingLog.Clear();
        Stalking.Clear();
    }

    private TMP_Text Button(string text, Transform parent, Vector2 at, Vector2 size,
                            UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = new Color(0.22f, 0.24f, 0.28f, 1f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = at;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var colours = button.colors;
        colours.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        button.colors = colours;

        var label = Label(text + " label", go.transform, 19f, Vector2.zero, size);
        label.text = text;
        label.color = new Color(0.90f, 0.92f, 0.95f);

        return label;
    }

    private TMP_Text Label(string name, Transform parent, float size, Vector2 at, Vector2 area)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.62f, 0.66f, 0.72f);
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = area;
        rect.anchoredPosition = at;

        return text;
    }
}
#endif
