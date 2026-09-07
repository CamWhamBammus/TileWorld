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
        if (TitleMenu.IsUp) return;
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
        if (open) RefreshWeather();
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

        if (frozenLabel != null)
        {
            bool found = NearestFrozen(out _, out float away);
            frozenLabel.text = found
                ? "Frozen lake   <size=15>" + Mathf.RoundToInt(away) + " m</size>"
                : "Frozen lake   <size=15>none near</size>";
        }

        if (wipeLabel != null) wipeLabel.text = "Put this world back to nothing";
    }

    private TMP_Text frozenLabel;

    /// <summary>The nearest frozen tile: water in the snow country.</summary>
    private bool NearestFrozen(out Vector2Int tile, out float away)
    {
        tile = default;
        away = 0f;
        if (player == null) return false;

        int seed = world.WorldSeed;
        int fromX = Mathf.RoundToInt(player.position.x / WorldGrid.TileSize);
        int fromZ = Mathf.RoundToInt(player.position.z / WorldGrid.TileSize);
        const int Step = 3;
        const int Rings = 170;

        for (int r = 0; r < Rings; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            int tx = fromX + dx * Step, tz = fromZ + dz * Step;
            if (!WaterSurface.IsFrozen(tx, tz, seed)) continue;
            tile = new Vector2Int(tx, tz);
            away = Vector2.Distance(new Vector2(fromX, fromZ), new Vector2(tx, tz)) * WorldGrid.TileSize;
            return true;
        }

        return false;
    }

    private void GoToFrozen()
    {
        if (!NearestFrozen(out var tile, out _) || body == null)
        {
            Notices.Show("Dev: no frozen lake within reach.");
            return;
        }

        int seed = world.WorldSeed;

        // stand on the bank and look out over the ice
        for (int step = 1; step < 40; step++)
        for (int side = 0; side < 8; side++)
        {
            float a = side / 8f * Mathf.PI * 2f;
            int dx = tile.x + Mathf.RoundToInt(Mathf.Cos(a) * step);
            int dz = tile.y + Mathf.RoundToInt(Mathf.Sin(a) * step);
            if (WaterSurface.IsUnderwater(dx, dz, seed)) continue;

            body.enabled = false;
            player.position = new Vector3(dx * WorldGrid.TileSize, WorldHeight.SurfaceY(dx, dz, seed) + 1.4f, dz * WorldGrid.TileSize);
            player.rotation = Quaternion.LookRotation(new Vector3(tile.x - dx, 0f, tile.y - dz).normalized);
            body.enabled = true;
            Toggle(false);
            Notices.Show("Dev: on the bank of a frozen lake.");
            return;
        }
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
        cardRect.sizeDelta = new Vector2(940f, 1040f);

        heading = Label("Heading", cardGo.transform, 26f, new Vector2(0f, 438f), new Vector2(880f, 90f));

        // Two pages: the places, and the animals. The tabs sit either side
        // of the heading; whichever is not showing is simply switched off.
        pages = new GameObject[3];

        for (int p = 0; p < 3; p++)
        {
            pages[p] = new GameObject(p == 0 ? "Places" : p == 1 ? "Animals" : "Weather");
            pages[p].transform.SetParent(cardGo.transform, false);
            var pr = pages[p].AddComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        }
        // the tabs, two a side of the heading, clear of its text
        Button("Places", cardGo.transform, new Vector2(-380f, 448f), new Vector2(150f, 44f), () => ShowPage(0));
        Button("Animals", cardGo.transform, new Vector2(-380f, 398f), new Vector2(150f, 44f), () => ShowPage(1));
        Button("Weather", cardGo.transform, new Vector2(380f, 448f), new Vector2(150f, 44f), () => ShowPage(2));
        var placesGo = pages[0];
        heading.color = new Color(0.85f, 0.87f, 0.90f);

        Label("Go", placesGo.transform, 17f, new Vector2(0f, 378f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST";

        var kinds = (Regions.Character[])System.Enum.GetValues(typeof(Regions.Character));

        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];

            float x = (i % 3 - 1) * 300f;
            float y = 330f - (i / 3) * 56f;

            labels[kind] = Button(kind.ToString(), placesGo.transform,
                new Vector2(x, y), new Vector2(285f, 52f), () => GoTo(kind));
        }

        Label("Water", placesGo.transform, 17f, new Vector2(0f, 108f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST WATER   (a lake is not a region, it is a thing inside one)";

        var bodies = (WaterSurface.Body[])System.Enum.GetValues(typeof(WaterSurface.Body));

        for (int i = 0; i < bodies.Length; i++)
        {
            var kind = bodies[i];
            waters[kind] = Button(kind.ToString(), placesGo.transform,
                new Vector2((i - 1.5f) * 222f, 66f), new Vector2(212f, 52f), () => GoToWater(kind));
        }

        frozenLabel = Button("Frozen lake", placesGo.transform, new Vector2(1.5f * 222f, 66f), new Vector2(212f, 52f), GoToFrozen);

        Label("Built", placesGo.transform, 17f, new Vector2(0f, 16f), new Vector2(880f, 30f))
            .text = "GO TO THE NEAREST STRUCTURE";

        var built = (LandmarkKind[])System.Enum.GetValues(typeof(LandmarkKind));

        for (int i = 0; i < built.Length; i++)
        {
            var kind = built[i];

            // twenty-one kinds now, with the small finds: six rows, closer set
            Button(Landmarks.NameOf(kind), placesGo.transform,
                new Vector2((i % 4 - 1.5f) * 222f, -26f - (i / 4) * 50f), new Vector2(212f, 46f), () => GoToStructure(kind));
        }

        Label("Keeping", placesGo.transform, 17f, new Vector2(0f, -334f), new Vector2(880f, 30f))
            .text = "THE FIRST FEW MINUTES";

        Button("Show the opening again", placesGo.transform,
            new Vector2(-230f, -378f), new Vector2(430f, 52f), () =>
            {
                Arrival.Replay();
                Toggle(false);
                Notices.Show("Dev: showing the first page again.");
            });

        wipeLabel = Button("Put this world back to nothing", placesGo.transform,
            new Vector2(230f, -378f), new Vector2(430f, 52f), Wipe);

        Label("Foot", placesGo.transform, 15f, new Vector2(0f, -450f), new Vector2(880f, 40f))
            .text = "F8 or Escape closes this. Editor and development builds only.";

        BuildAnimals(pages[1].transform);
        BuildWeather(pages[2].transform);
        ShowPage(0);

        panel.SetActive(false);
    }

    private GameObject[] pages;
    private FaunaKind lastKind = FaunaKind.Deer;
    private bool slow;

    private void ShowPage(int which)
    {
        if (pages == null) return;
        for (int p = 0; p < pages.Length; p++) pages[p].SetActive(p == which);
    }

    /// <summary>
    /// The animals page: put any kind down in front of you, tell whatever is
    /// near you what to do, stage a hunt or a herd, set the hour, slow time.
    /// Testing an animal meant finding one first; this is for not having to.
    /// </summary>
    private void BuildAnimals(Transform page)
    {
        Label("Put", page, 17f, new Vector2(0f, 378f), new Vector2(880f, 30f))
            .text = "PUT ONE IN FRONT OF ME";

        var kinds = (FaunaKind[])System.Enum.GetValues(typeof(FaunaKind));
        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            Button(Fauna.Of(kind).Name, page,
                new Vector2((i % 5 - 2f) * 176f, 336f - (i / 5) * 50f), new Vector2(168f, 46f), () => Summon(kind, false));
        }

        Label("Stage", page, 17f, new Vector2(0f, 120f), new Vector2(880f, 30f))
            .text = "STAGE";

        Button("A company of the last kind", page, new Vector2(-330f, 78f), new Vector2(210f, 46f), () => Company(lastKind));
        Button("A deer herd, with young", page, new Vector2(-110f, 78f), new Vector2(210f, 46f), () => Company(FaunaKind.Deer));
        Button("A fox after a rabbit", page, new Vector2(110f, 78f), new Vector2(210f, 46f), Hunt);
        Button("A wolf pair", page, new Vector2(330f, 78f), new Vector2(210f, 46f), () => { Summon(FaunaKind.Wolf, false); Summon(FaunaKind.Wolf, false); });

        Label("Tell", page, 17f, new Vector2(0f, 26f), new Vector2(880f, 30f))
            .text = "TELL EVERYTHING WITHIN 40 M TO";

        string[] orders = { "walk", "run", "rest", "graze", "alert", "spook", "hunt" };
        for (int i = 0; i < orders.Length; i++)
        {
            string order = orders[i];
            Button(order, page, new Vector2((i - 3f) * 125f, -16f), new Vector2(118f, 46f), () => Tell(order));
        }

        Button("Take every animal away", page, new Vector2(-230f, -90f), new Vector2(430f, 52f), () =>
        {
            Wildlife.ClearAll();
            Notices.Show("Dev: the country is empty.");
        });

        Button("Go to the nearest animal", page, new Vector2(230f, -90f), new Vector2(430f, 52f), GoToAnimal);

        Label("Foot2", page, 15f, new Vector2(0f, -380f), new Vector2(880f, 40f))
            .text = "Animals put down here are real ones: they behave, and the book counts them. The hour and the weather are on the Weather page.";
    }

    private TMP_Text weatherLabel;

    /// <summary>
    /// The weather page: the sky held at a level, from clear to a downpour,
    /// or let go to be its own; the hour; the clock slowed. Rain falls once
    /// the overcast is past the rain's threshold, and the water shows it.
    /// </summary>
    private void BuildWeather(Transform page)
    {
        Label("Sky", page, 17f, new Vector2(0f, 378f), new Vector2(880f, 30f))
            .text = "HOLD THE SKY AT";

        (string, float)[] skies = { ("clear", 0f), ("cloudy", 0.4f), ("light rain", 0.65f), ("rain", 0.8f), ("downpour", 1f) };

        for (int i = 0; i < skies.Length; i++)
        {
            var sky = skies[i];
            Button(sky.Item1, page, new Vector2((i - 2f) * 176f, 336f), new Vector2(168f, 46f), () =>
            {
                TimeOfDay.Instance?.ForceOvercast(sky.Item2);
                Notices.Show("Dev: the sky held at " + sky.Item1 + ".");
            });
        }

        Button("Let the weather be its own again", page, new Vector2(-230f, 280f), new Vector2(430f, 46f), () =>
        {
            TimeOfDay.Instance?.ForceOvercast(-1f);
            Notices.Show("Dev: the weather is its own again.");
        });

        Button("A minute of rain, then let go", page, new Vector2(230f, 280f), new Vector2(430f, 46f), () =>
        {
            StartCoroutine(Shower(60f));
            Toggle(false);
            Notices.Show("Dev: a minute of rain.");
        });

        weatherLabel = Label("Now", page, 17f, new Vector2(0f, 226f), new Vector2(880f, 30f));

        Label("Hour", page, 17f, new Vector2(0f, 150f), new Vector2(880f, 30f))
            .text = "THE HOUR, AND THE CLOCK";

        (string, float)[] hours = { ("dawn", 0.24f), ("noon", 0.50f), ("dusk", 0.74f), ("night", 0.95f) };

        for (int i = 0; i < hours.Length; i++)
        {
            var hour = hours[i];
            Button(hour.Item1, page, new Vector2((i - 2f) * 176f, 108f), new Vector2(168f, 46f), () =>
            {
                if (TimeOfDay.Instance != null) TimeOfDay.Instance.SetTime(hour.Item2);
                Notices.Show("Dev: " + hour.Item1 + ".");
            });
        }

        slowLabel = Button("slow time", page, new Vector2(2f * 176f, 108f), new Vector2(168f, 46f), () =>
        {
            slow = !slow;
            Time.timeScale = slow ? 0.25f : 1f;
            slowLabel.text = slow ? "normal time" : "slow time";
        });

        Label("Foot3", page, 15f, new Vector2(0f, -380f), new Vector2(880f, 40f))
            .text = "Rain falls once the overcast is past " + Rain.Threshold.ToString("F2") + ". A held sky holds until you let it go. Stand by a lake to see the rain ring it. In the snow country it snows instead, and the lakes are ice.";
    }

    private System.Collections.IEnumerator Shower(float seconds)
    {
        TimeOfDay.Instance?.ForceOvercast(0.85f);
        yield return new WaitForSeconds(seconds);
        TimeOfDay.Instance?.ForceOvercast(-1f);
        Notices.Show("Dev: the rain let go.");
    }

    private void RefreshWeather()
    {
        if (weatherLabel == null) return;

        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        bool held = TimeOfDay.Instance != null && TimeOfDay.Instance.OvercastHeld;

        weatherLabel.text = "overcast " + overcast.ToString("F2")
                          + (Rain.Intensity > 0f ? ", raining (" + Mathf.RoundToInt(Rain.Intensity * 100f) + "%)" : ", dry")
                          + (held ? " -- held" : " -- its own");
    }

    private TMP_Text slowLabel;

    /// <summary>A spot ten metres ahead, on ground, or as near as can be found.</summary>
    private Vector3 Ahead(float metres, float aside = 0f)
    {
        var cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : player.forward;
        forward.y = 0f; forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 at = player.position + forward * metres + right * aside;
        int tx = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
        at.y = Mathf.Max(WorldHeight.SurfaceY(tx, tz, world.WorldSeed), WaterSurface.Level - 0.5f);
        return at;
    }

    private void Summon(FaunaKind kind, bool young)
    {
        lastKind = kind;
        var an = Wildlife.Summon(kind, Ahead(10f, Random.Range(-3f, 3f)), young);
        if (an != null) an.Direct("graze");
        Toggle(false);
        Notices.Show("Dev: a " + Fauna.Of(kind).Name + ", ahead of you.");
    }

    private void Company(FaunaKind kind)
    {
        int count = Mathf.Max(2, Fauna.Company(kind));
        Animal leader = null;
        for (int i = 0; i < count; i++)
        {
            bool young = i > 0 && i == count - 1;
            var an = Wildlife.Summon(kind, Ahead(12f + Random.Range(-2f, 2f), (i - (count - 1) * 0.5f) * 3f), young);
            if (an == null) continue;
            if (leader == null) leader = an; else an.Leader = leader;
            an.Direct("graze");
        }
        Toggle(false);
        Notices.Show("Dev: " + count + " " + Fauna.Of(kind).Name + (count > 1 ? "s" : "") + ", ahead of you.");
    }

    private void Hunt()
    {
        // both beyond the fox's notice of you, or it stands watching you
        // instead of hunting; the rabbit a little nearer, off to one side
        var rabbit = Wildlife.Summon(FaunaKind.Rabbit, Ahead(22f, 4f));
        var fox = Wildlife.Summon(FaunaKind.Fox, Ahead(30f, -4f));
        if (rabbit != null) rabbit.Direct("graze");
        if (fox != null) fox.Direct("hunt");
        Toggle(false);
        Notices.Show("Dev: a fox after a rabbit.");
    }

    private void Tell(string order)
    {
        int told = 0;
        foreach (var an in Wildlife.Near(player.position, 40f)) { an.Direct(order); told++; }
        Toggle(false);
        Notices.Show("Dev: " + told + " told to " + order + ".");
    }

    private void GoToAnimal()
    {
        Animal nearest = null; float best = float.MaxValue;
        foreach (var an in Wildlife.Near(player.position, 2000f))
        {
            float d = Vector3.Distance(an.transform.position, player.position);
            if (d > 3f && d < best) { best = d; nearest = an; }
        }
        if (nearest == null) { Notices.Show("Dev: nothing about."); return; }

        var controller = player.GetComponent<CharacterController>();
        Vector3 to = player.position - nearest.transform.position; to.y = 0f;
        Vector3 stand = nearest.transform.position + (to.sqrMagnitude < 0.1f ? Vector3.back : to.normalized) * 12f;
        int tx = Mathf.RoundToInt(stand.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(stand.z / WorldGrid.TileSize);
        if (controller != null) controller.enabled = false;
        player.position = new Vector3(stand.x, Mathf.Max(WorldHeight.SurfaceY(tx, tz, world.WorldSeed), WaterSurface.Level) + 1.2f, stand.z);
        player.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
        if (controller != null) controller.enabled = true;
        Toggle(false);
        Notices.Show("Dev: a " + Fauna.Of(nearest.Kind).Name + ", " + Mathf.RoundToInt(best) + " m off.");
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

            save.plateSubjects.Clear();
            save.plateIds.Clear();
            save.plateWhere.Clear();

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
