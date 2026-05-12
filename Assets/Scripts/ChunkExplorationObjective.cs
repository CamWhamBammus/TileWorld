using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChunkExplorationObjective : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private TMP_Text chunkCounterText;

    [Header("Chunk Settings")]
    [SerializeField] private int tilesPerChunk = 15;
    [SerializeField] private int tileSize = 2;

    [Header("Objective")]
    [SerializeField] private int chunksNeededToWin = 9;

    private readonly HashSet<Vector2Int> visitedChunks = new HashSet<Vector2Int>();

    private Vector2Int currentChunk;
    private bool objectiveComplete = false;

    private int ChunkWorldSize
    {
        get { return tilesPerChunk * tileSize; }
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            Debug.LogError("[ChunkObjective] Missing playerTransform. Drag PlayerArmature into the slot.");
            enabled = false;
            return;
        }

        if (chunkCounterText == null)
        {
            Debug.LogError("[ChunkObjective] Missing chunkCounterText. Drag your UI text into the slot.");
            enabled = false;
            return;
        }

        UpdateCurrentChunk();
        visitedChunks.Add(currentChunk);
        UpdateUI();
    }

    private void Update()
    {
        if (objectiveComplete)
        {
            return;
        }

        Vector2Int newChunk = GetChunkFromPosition(playerTransform.position);

        if (newChunk != currentChunk)
        {
            currentChunk = newChunk;

            if (!visitedChunks.Contains(currentChunk))
            {
                visitedChunks.Add(currentChunk);
                Debug.Log("[ChunkObjective] New chunk discovered: " + currentChunk);
            }

            UpdateUI();

            if (visitedChunks.Count >= chunksNeededToWin)
            {
                CompleteObjective();
            }
        }
    }

    private void UpdateCurrentChunk()
    {
        currentChunk = GetChunkFromPosition(playerTransform.position);
    }

    private Vector2Int GetChunkFromPosition(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / ChunkWorldSize),
            Mathf.FloorToInt(position.z / ChunkWorldSize)
        );
    }

    private void UpdateUI()
    {
        chunkCounterText.text =
            "Map the Unknown\n" +
            "Chunks Explored: " + visitedChunks.Count + " / " + chunksNeededToWin + "\n" +
            "Current Chunk: " + currentChunk;
    }

    private void CompleteObjective()
    {
        objectiveComplete = true;

        chunkCounterText.text =
            "Survey Complete!\n" +
            "Chunks Explored: " + visitedChunks.Count + " / " + chunksNeededToWin + "\n" +
            "The forest continues beyond your map...";

        Debug.Log("[ChunkObjective] Objective complete!");
    }
}