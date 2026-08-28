using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    private Vector2Int currentChunk;

    private bool completedMainQuest;
    private bool completedDistanceQuest;
    private bool completedFourDirectionsQuest;
    private bool completedDeepSurveyQuest;
    private bool completedLandmarkQuest;

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

        UpdateQuests();
        UpdateQuestUI();
    }

    private void Update()
    {
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

    private Vector2Int GetChunkFromPosition(Vector3 position)
    {
        return WorldGrid.WorldToChunk(position);
    }

    private void UpdateQuests()
    {
        if (!completedMainQuest && ExplorationLog.Count >= chunksNeededForMainQuest)
        {
            completedMainQuest = true;
            Debug.Log("[QuestManager] Completed quest: Map the Unknown");
        }

        if (!completedDeepSurveyQuest && ExplorationLog.Count >= chunksNeededForDeepSurvey)
        {
            completedDeepSurveyQuest = true;
            Debug.Log("[QuestManager] Completed quest: Deep Forest Survey");
        }

        if (!completedDistanceQuest && GetChunkDistanceFromOrigin(currentChunk) >= chunkDistanceGoal)
        {
            completedDistanceQuest = true;
            Debug.Log("[QuestManager] Completed quest: Walk Beyond the Origin");
        }

        if (!completedLandmarkQuest && LandmarkLog.Count >= landmarksNeeded)
        {
            completedLandmarkQuest = true;
            Debug.Log("[QuestManager] Completed quest: Marks on the Land");
        }

        if (!completedFourDirectionsQuest && HasVisitedAllFourDirections())
        {
            completedFourDirectionsQuest = true;
            Debug.Log("[QuestManager] Completed quest: Compass Survey");
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
    private const string Done = "#7FA05A";
    private const string Open = "#C9BEA4";
    private const string Dim = "#8A7E68";
    private const string Head = "#F0E6D2";

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
            "<indent=22px><size=17><color=" + Dim + ">" + detail + "</color></size>\n";

        if (!string.IsNullOrEmpty(progress))
        {
            body += "<size=17>" + progress + "</size>\n";
        }

        return body + "</indent></line-height>\n";
    }

    private void UpdateQuestUI()
    {
        int discovered = ExplorationLog.Count;
        int distance = GetChunkDistanceFromOrigin(currentChunk);

        string log =
            "<size=26><b><color=" + Head + ">QUEST LOG</color></b></size>\n" +
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

        log += Quest(completedDeepSurveyQuest, "Deep Forest Survey",
            "Discover " + chunksNeededForDeepSurvey + " total chunks.",
            Bar(discovered, chunksNeededForDeepSurvey) + "  <color=" + Open + ">" +
            Mathf.Min(discovered, chunksNeededForDeepSurvey) + " / " + chunksNeededForDeepSurvey + "</color>");

        log +=
            "<color=" + Dim + ">\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500</color>\n" +
            "<size=17><color=" + Dim + ">grid</color> <color=" + Open + ">" +
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
