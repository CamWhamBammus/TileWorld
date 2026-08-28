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

    private Vector2Int currentChunk;

    private bool completedMainQuest;
    private bool completedDistanceQuest;
    private bool completedFourDirectionsQuest;
    private bool completedDeepSurveyQuest;

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

    private string Checkmark(bool complete)
    {
        return complete ? "✓" : "○";
    }

    private void UpdateQuestUI()
    {
        questText.text =
            "QUEST LOG\n\n" +

            Checkmark(completedMainQuest) + " Map the Unknown\n" +
            "Explore " + chunksNeededForMainQuest + " unique chunks.\n" +
            "Progress: " + Mathf.Min(ExplorationLog.Count, chunksNeededForMainQuest) + " / " + chunksNeededForMainQuest + "\n\n" +

            Checkmark(completedDistanceQuest) + " Walk Beyond the Origin\n" +
            "Reach chunk distance " + chunkDistanceGoal + " from spawn.\n" +
            "Current Distance: " + GetChunkDistanceFromOrigin(currentChunk) + " / " + chunkDistanceGoal + "\n\n" +

            Checkmark(completedFourDirectionsQuest) + " Compass Survey\n" +
            "Discover chunks east, west, north, and south of spawn.\n\n" +

            Checkmark(completedDeepSurveyQuest) + " Deep Forest Survey\n" +
            "Discover " + chunksNeededForDeepSurvey + " total chunks.\n" +
            "Progress: " + Mathf.Min(ExplorationLog.Count, chunksNeededForDeepSurvey) + " / " + chunksNeededForDeepSurvey + "\n\n" +

            "Current Chunk: " + currentChunk + "\n" +
            "Total Chunks Discovered: " + ExplorationLog.Count;
    }
}
