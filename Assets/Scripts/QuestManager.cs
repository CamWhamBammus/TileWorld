using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private TMP_Text questText;

    [Header("Quest Goals")]
    [SerializeField] private int chunksNeededForMainQuest = 9;
    [SerializeField] private int chunksNeededForDeepSurvey = 15;
    [SerializeField] private int chunkDistanceGoal = 3;

    [Tooltip("Landmarks to find for the survey quest.")]
    [SerializeField] private int landmarksNeeded = 3;

    [Tooltip("Towers to climb and survey from.")]
    [SerializeField] private int surveysNeeded = 2;

    [Tooltip("Height above the valley floor to reach, in metres.")]
    [SerializeField] private float heightGoal = 26f;

    private Vector2Int currentChunk;

    private bool completedMainQuest;
    private bool completedDistanceQuest;
    private bool completedFourDirectionsQuest;
    private bool completedDeepSurveyQuest;
    private bool completedLandmarkQuest;
    private bool completedHighPlaceQuest;
    private bool completedSummitQuest;
    private int surveysMade;
    private float highestReached;

    private void Start()
    {
        if (playerTransform == null)
        {
            Debug.LogError("[QuestManager] Missing playerTransform. Drag PlayerArmature into this slot.");
            enabled = false;
            return;
        }

        if (questText == null)
        {
            Debug.LogError("[QuestManager] Missing questText. Drag your Quest Menu text into this slot.");
            enabled = false;
            return;
        }

        currentChunk = GetChunkFromPosition(playerTransform.position);
        ExplorationLog.Visit(currentChunk);

        LandmarkSpawner.Surveyed += OnSurveyed;

        StylePanel();

        UpdateQuests();
        UpdateQuestUI();
    }

    /// <summary>
    /// Puts the quest log on the same chart paper as the map, and gives it a
    /// fixed card the text has to fit inside. It was a transparent rectangle
    /// you could read the forest through, and the last two quests ran off the
    /// bottom of it.
    /// </summary>
    private void StylePanel()
    {
        var panel = questText.rectTransform.parent as RectTransform;

        if (panel == null)
        {
            panel = questText.rectTransform;
        }

        // a dimmed backdrop, so the log reads as a screen rather than an overlay
        var shadeGo = new GameObject("Backdrop");
        shadeGo.transform.SetParent(panel, false);
        shadeGo.transform.SetAsFirstSibling();

        var shade = shadeGo.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.45f);
        shade.raycastTarget = false;

        var shadeRect = shadeGo.GetComponent<RectTransform>();
        shadeRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadeRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadeRect.pivot = new Vector2(0.5f, 0.5f);
        shadeRect.sizeDelta = new Vector2(6000f, 6000f);
        shadeRect.anchoredPosition = Vector2.zero;

        // the card itself
        var existing = panel.GetComponent<Image>();
        if (existing != null) existing.enabled = false;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(panel, false);
        cardGo.transform.SetSiblingIndex(1);

        var card = cardGo.AddComponent<RawImage>();
        card.texture = ParchmentPanel.Create(512, 640);
        card.raycastTarget = false;

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = PanelSize;
        cardRect.anchoredPosition = Vector2.zero;

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = PanelSize;

        // text inside the rules, and shrunk to fit rather than allowed to spill
        var textRect = questText.rectTransform;
        textRect.SetAsLastSibling();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = PanelSize - new Vector2(96f, 96f);

        questText.alignment = TextAlignmentOptions.TopLeft;
        questText.enableWordWrapping = true;
        questText.overflowMode = TextOverflowModes.Overflow;
        questText.enableAutoSizing = true;
        questText.fontSizeMin = 11f;
        questText.fontSizeMax = 21f;
        questText.color = Color.white;   // the markup carries the colour
    }

    private static readonly Vector2 PanelSize = new Vector2(720f, 880f);

    public int SurveysMade => surveysMade;
    public float HighestReached => highestReached;

    /// <summary>
    /// Puts back the progress a save keeps. Chunk counts recompute themselves
    /// from the chart, but these two do not exist anywhere else.
    /// </summary>
    public void RestoreProgress(int surveys, float highest)
    {
        surveysMade = Mathf.Max(surveysMade, surveys);
        highestReached = Mathf.Max(highestReached, highest);

        UpdateQuests();
        UpdateQuestUI();
    }

    private void OnDestroy()
    {
        LandmarkSpawner.Surveyed -= OnSurveyed;
    }

    private void OnSurveyed(LandmarkKind kind, int charted)
    {
        if (Landmarks.SurveyHeight(kind) <= 0f)
        {
            return;   // only climbing something counts for this
        }

        surveysMade++;
        UpdateQuests();
        UpdateQuestUI();
    }

    private void Update()
    {
        TrackAltitude();

        Vector2Int newChunk = GetChunkFromPosition(playerTransform.position);

        if (newChunk != currentChunk)
        {
            currentChunk = newChunk;

            if (ExplorationLog.Visit(currentChunk))
            {
                Debug.Log("[QuestManager] Discovered new chunk: " + currentChunk);
            }

            UpdateQuests();
            UpdateQuestUI();
        }
    }

    /// <summary>
    /// Keeps the highest ground reached. Read from the terrain rather than the
    /// player's transform, so standing on top of a tower does not count as
    /// having climbed a mountain.
    /// </summary>
    private void TrackAltitude()
    {
        var world = ChunkManagerRef;

        if (world == null) return;

        int tileX = Mathf.RoundToInt(playerTransform.position.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(playerTransform.position.z / WorldGrid.TileSize);

        float here = WorldHeight.HeightAt(tileX, tileZ, world.WorldSeed);

        if (here > highestReached)
        {
            highestReached = here;

            if (!completedSummitQuest && highestReached >= heightGoal)
            {
                completedSummitQuest = true;
                Notices.Show("Quest complete: The Roof of the World");
            }
        }
    }

    private ChunkManager cachedWorld;

    private ChunkManager ChunkManagerRef
    {
        get
        {
            if (cachedWorld == null) cachedWorld = FindFirstObjectByType<ChunkManager>();
            return cachedWorld;
        }
    }

    private Vector2Int GetChunkFromPosition(Vector3 position)
    {
        return WorldGrid.WorldToChunk(position);
    }

    private void UpdateQuests()
    {
        if (!completedMainQuest && ExplorationLog.Count >= chunksNeededForMainQuest)
        {
            completedMainQuest = true;
            Notices.Show("Quest complete: Map the Unknown");
        }

        if (!completedDeepSurveyQuest && ExplorationLog.Count >= chunksNeededForDeepSurvey)
        {
            completedDeepSurveyQuest = true;
            Notices.Show("Quest complete: Deep Forest Survey");
        }

        if (!completedDistanceQuest && GetChunkDistanceFromOrigin(currentChunk) >= chunkDistanceGoal)
        {
            completedDistanceQuest = true;
            Notices.Show("Quest complete: Walk Beyond the Origin");
        }

        if (!completedLandmarkQuest && LandmarkLog.Count >= landmarksNeeded)
        {
            completedLandmarkQuest = true;
            Notices.Show("Quest complete: Marks on the Land");
        }

        if (!completedHighPlaceQuest && surveysMade >= surveysNeeded)
        {
            completedHighPlaceQuest = true;
            Notices.Show("Quest complete: The High Places");
        }

        if (!completedFourDirectionsQuest && HasVisitedAllFourDirections())
        {
            completedFourDirectionsQuest = true;
            Notices.Show("Quest complete: Compass Survey");
        }
    }

    private int GetChunkDistanceFromOrigin(Vector2Int chunk)
    {
        return WorldGrid.RingDistanceFromOrigin(chunk);
    }

    private bool HasVisitedAllFourDirections()
    {
        bool east = false;
        bool west = false;
        bool north = false;
        bool south = false;

        foreach (Vector2Int chunk in ExplorationLog.Visited)
        {
            if (chunk.x > 0) east = true;
            if (chunk.x < 0) west = true;
            if (chunk.y > 0) north = true;
            if (chunk.y < 0) south = true;
        }

        return east && west && north && south;
    }

    // Palette shared with the map, so the two screens look like one game.
    // Inks for a light card. The old values were for a dark panel and are
    // close to invisible on paper.
    private const string Done = "#4A6B33";
    private const string Open = "#4A4032";
    private const string Dim = "#8A7C63";
    private const string Head = "#33291D";

    private static string Bullet(bool complete)
    {
        return complete
            ? "<color=" + Done + ">\u25CF</color>"
            : "<color=" + Dim + ">\u25CB</color>";
    }

    /// <summary>A filled bar in text, so progress reads at a glance.</summary>
    private static string Bar(int have, int need, int width = 14)
    {
        have = Mathf.Clamp(have, 0, need);
        int filled = need <= 0 ? width : Mathf.RoundToInt(width * (have / (float)need));

        return "<color=" + Done + ">" + new string('\u2588', filled) + "</color>" +
               "<color=" + Dim + ">" + new string('\u2591', width - filled) + "</color>";
    }

    private static string Quest(bool complete, string title, string detail, string progress)
    {
        string body =
            "<line-height=115%>" + Bullet(complete) + "  <b><color=" +
            (complete ? Done : Head) + ">" + title + "</color></b>\n" +
            "<indent=22px><size=88%><color=" + Dim + ">" + detail + "</color></size>\n";

        if (!string.IsNullOrEmpty(progress))
        {
            body += "<size=88%>" + progress + "</size>\n";
        }

        return body + "</indent></line-height>\n";
    }

    private void UpdateQuestUI()
    {
        int discovered = ExplorationLog.Count;
        int distance = GetChunkDistanceFromOrigin(currentChunk);

        string log =
            "<size=145%><b><color=" + Head + ">QUEST LOG</color></b></size>\n" +
            "<color=" + Dim + ">\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500</color>\n\n";

        log += Quest(completedMainQuest, "Map the Unknown",
            "Explore " + chunksNeededForMainQuest + " unique chunks.",
            Bar(discovered, chunksNeededForMainQuest) + "  <color=" + Open + ">" +
            Mathf.Min(discovered, chunksNeededForMainQuest) + " / " + chunksNeededForMainQuest + "</color>");

        log += Quest(completedDistanceQuest, "Walk Beyond the Origin",
            "Reach chunk distance " + chunkDistanceGoal + " from spawn.",
            Bar(distance, chunkDistanceGoal) + "  <color=" + Open + ">" +
            Mathf.Min(distance, chunkDistanceGoal) + " / " + chunkDistanceGoal + "</color>");

        log += Quest(completedFourDirectionsQuest, "Compass Survey",
            "Discover chunks east, west, north and south of spawn.",
            Compass());

        log += Quest(completedLandmarkQuest, "Marks on the Land",
            "Find " + landmarksNeeded + " landmarks left by those who came before.",
            Bar(LandmarkLog.Count, landmarksNeeded) + "  <color=" + Open + ">" +
            Mathf.Min(LandmarkLog.Count, landmarksNeeded) + " / " + landmarksNeeded + "</color>");

        log += Quest(completedHighPlaceQuest, "The High Places",
            "Climb " + surveysNeeded + " towers and chart the land from them.",
            Bar(surveysMade, surveysNeeded) + "  <color=" + Open + ">" +
            Mathf.Min(surveysMade, surveysNeeded) + " / " + surveysNeeded + "</color>");

        log += Quest(completedSummitQuest, "The Roof of the World",
            "Stand " + Mathf.RoundToInt(heightGoal) + "m above the valley floor.",
            Bar(Mathf.RoundToInt(highestReached), Mathf.RoundToInt(heightGoal)) + "  <color=" + Open + ">" +
            Mathf.RoundToInt(Mathf.Min(highestReached, heightGoal)) + " / " + Mathf.RoundToInt(heightGoal) + "m</color>");

        log += Quest(completedDeepSurveyQuest, "Deep Forest Survey",
            "Discover " + chunksNeededForDeepSurvey + " total chunks.",
            Bar(discovered, chunksNeededForDeepSurvey) + "  <color=" + Open + ">" +
            Mathf.Min(discovered, chunksNeededForDeepSurvey) + " / " + chunksNeededForDeepSurvey + "</color>");

        log +=
            "<color=" + Dim + ">\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500</color>\n" +
            "<size=88%><color=" + Dim + ">grid</color> <color=" + Open + ">" +
            currentChunk.x + ", " + currentChunk.y + "</color>" +
            "    <color=" + Dim + ">charted</color> <color=" + Open + ">" + discovered + "</color>" +
            "    <color=" + Dim + ">M for the map</color></size>";

        questText.text = log;
    }

    /// <summary>The four headings, lit as each is reached.</summary>
    private string Compass()
    {
        bool east = false, west = false, north = false, south = false;

        foreach (Vector2Int chunk in ExplorationLog.Visited)
        {
            if (chunk.x > 0) east = true;
            if (chunk.x < 0) west = true;
            if (chunk.y > 0) north = true;
            if (chunk.y < 0) south = true;
        }

        return Tick("N", north) + "  " + Tick("E", east) + "  " + Tick("S", south) + "  " + Tick("W", west);
    }

    private static string Tick(string label, bool reached)
    {
        return "<color=" + (reached ? Done : Dim) + ">" + (reached ? "\u25CF" : "\u25CB") + " " + label + "</color>";
    }
}
