using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    
    private const int TilesPerChunk = 15;
    private const int TileSize = 2;
    private const int ChunkWorldSize = TilesPerChunk * TileSize;
    private bool hasLoggedFirstDrawSummary = false;
    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private TileLibrary tileLibrary;

    [Header("Build Safety")]
    [SerializeField] private Material overrideDrawMaterial;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private bool drawDebugGizmos = true;

    private Vector2Int playerChunk;
    private readonly HashSet<Chunk> loadedChunks = new HashSet<Chunk>();
    private readonly ChunkMap chunkMap = new ChunkMap();

    private bool hasLoggedStartup;
    private bool hasLoggedMissingReferences;

    private void Start()
    {
        Debug.Log("[ChunkManager] Start() called. ChunkManager is active.");

        ValidateReferences();

        if (playerTransform != null)
        {
            Debug.Log("[ChunkManager] Player Transform assigned: " + playerTransform.name);
        }

        if (tileLibrary != null)
        {
            Debug.Log("[ChunkManager] Tile Library assigned: " + tileLibrary.name);
        
        }

        hasLoggedStartup = true;
    }

    private void Update()
    {
        Debug.Log("[ChunkManager] UPDATE IS RUNNING");
        if (!ValidateReferences())
        {
            return;
        }

        UpdatePlayerChunk();

        loadedChunks.Clear();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var idx = new Vector2Int(playerChunk.x + dx, playerChunk.y + dz);
                loadedChunks.Add(GetOrCreateChunk(idx));
            }
        }

        if (debugMode && logEveryFrame)
        {
            Debug.Log(
                "[ChunkManager] Player position: " + playerTransform.position +
                " | Player chunk: " + playerChunk +
                " | Loaded chunks: " + loadedChunks.Count +
                " | Total created chunks: " + chunkMap.ChunkMapGetter().Count
            );
        }

        DrawChunks();
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (playerTransform == null)
        {
            valid = false;

            if (!hasLoggedMissingReferences)
            {
                Debug.LogError("[ChunkManager] Missing Player Transform. Drag PlayerArmature into the Player Transform slot on WorldCreator.");
            }
        }

        if (tileLibrary == null)
        {
            valid = false;

            if (!hasLoggedMissingReferences)
            {
                Debug.LogError("[ChunkManager] Missing Tile Library. Drag your TileLibrary asset into the Tile Library slot on WorldCreator.");
            }
        }

        if (!valid)
        {
            hasLoggedMissingReferences = true;
        }

        return valid;
    }

    private void UpdatePlayerChunk()
    {
        var p = playerTransform.position;

        playerChunk = new Vector2Int(
            Mathf.FloorToInt(p.x / ChunkWorldSize),
            Mathf.FloorToInt(p.z / ChunkWorldSize)
        );
    }

    private Chunk GetOrCreateChunk(Vector2Int index)
    {
        if (!chunkMap.ChunkMapGetter().TryGetValue(index, out var chunk))
        {
            chunk = new Chunk(index);
            chunkMap.ChunkMapGetter().Add(index, chunk);

            if (debugMode)
            {
                Debug.Log("[ChunkManager] Created new chunk at index: " + index);
            }
        }

        return chunk;
    }

    private void DrawChunks()
    {
        if (loadedChunks.Count == 0)
        {
            if (debugMode)
            {
                Debug.LogWarning("[ChunkManager] No loaded chunks to draw.");
            }

            return;
        }

        int drawCalls = 0;
        int skippedMissingTileDefinitions = 0;
        int skippedMissingMesh = 0;
        int skippedMissingMaterial = 0;
        int totalInstances = 0;

        foreach (var chunk in loadedChunks)
        {
            foreach (int id in chunk.idToListOfTransforms.Keys)
            {
                if (!tileLibrary.TryGet(id, out var def))
                {
                    skippedMissingTileDefinitions++;

                    if (debugMode)
                    {
                        Debug.LogWarning("[ChunkManager] TileLibrary does not contain a TileDefinition for block ID: " + id);
                    }

                    continue;
                }

                if (def == null)
                {
                    skippedMissingTileDefinitions++;

                    if (debugMode)
                    {
                        Debug.LogWarning("[ChunkManager] TileDefinition is null for block ID: " + id);
                    }

                    continue;
                }

                Mesh mesh = def.MeshGetter();
                Material material = overrideDrawMaterial != null ? overrideDrawMaterial : def.MaterialGetter();
                if (debugMode && !hasLoggedFirstDrawSummary)
                {
                    Debug.Log("[ChunkManager] Using material: " + material.name + " | Override assigned: " + (overrideDrawMaterial != null));
                }
                if (material != null)
                {
                    material.enableInstancing = true;
                }

                if (mesh == null)
                {
                    skippedMissingMesh++;

                    if (debugMode)
                    {
                        Debug.LogWarning("[ChunkManager] Missing mesh for block ID: " + id + ". Check this TileDefinition's prefab.");
                    }

                    continue;
                }

                if (material == null)
                {
                    skippedMissingMaterial++;

                    if (debugMode)
                    {
                        Debug.LogWarning("[ChunkManager] Missing material for block ID: " + id + ". Check this TileDefinition's prefab/material.");
                    }

                    continue;
                }

                var matrices = chunk.idToListOfTransforms[id];

                if (matrices == null || matrices.Count == 0)
                {
                    if (debugMode)
                    {
                        Debug.LogWarning("[ChunkManager] No matrices/transforms for block ID: " + id);
                    }

                    continue;
                }

                const int Max = 1023;

                for (int i = 0; i < matrices.Count; i += Max)
                {
                    int count = Mathf.Min(Max, matrices.Count - i);

                    Graphics.DrawMeshInstanced(
                        mesh,
                        0,
                        material,
                        matrices.GetRange(i, count)
                    );

                    drawCalls++;
                    totalInstances += count;
                }
            }
        }

        if (debugMode && (logEveryFrame || !hasLoggedFirstDrawSummary))
        {
            Debug.Log(
                "[ChunkManager] Draw summary | Draw calls: " + drawCalls +
                " | Instances: " + totalInstances +
                " | Missing tile defs: " + skippedMissingTileDefinitions +
                " | Missing meshes: " + skippedMissingMesh +
                " | Missing materials: " + skippedMissingMaterial
            );

            hasLoggedFirstDrawSummary = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || playerTransform == null)
        {
            return;
        }

        Vector3 center = new Vector3(
            playerChunk.x * ChunkWorldSize + ChunkWorldSize / 2f,
            0f,
            playerChunk.y * ChunkWorldSize + ChunkWorldSize / 2f
        );

        Gizmos.DrawWireCube(center, new Vector3(ChunkWorldSize, 0.1f, ChunkWorldSize));
    }
}