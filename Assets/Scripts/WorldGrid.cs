using UnityEngine;

/// <summary>
/// The one place that knows how big the world grid is.
///
/// These numbers used to be copied into ChunkManager, Chunk, QuestManager and
/// ChunkExplorationObjective separately, which meant changing the chunk size in
/// one of them silently desynced the others: the world would generate on one
/// grid while the quest log counted chunks on another.
/// </summary>
public static class WorldGrid
{
    public const int TilesPerChunk = 15;
    public const int TileSize = 2;
    public const int ChunkWorldSize = TilesPerChunk * TileSize;

    /// <summary>Which chunk a world position falls in.</summary>
    public static Vector2Int WorldToChunk(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / ChunkWorldSize),
            Mathf.FloorToInt(position.z / ChunkWorldSize)
        );
    }

    /// <summary>World-space centre of a chunk, on the ground plane.</summary>
    public static Vector3 ChunkCenter(Vector2Int chunk)
    {
        return new Vector3(
            chunk.x * ChunkWorldSize + ChunkWorldSize * 0.5f,
            0f,
            chunk.y * ChunkWorldSize + ChunkWorldSize * 0.5f
        );
    }

    /// <summary>Chunks away from spawn, measured as a square ring (Chebyshev distance).</summary>
    public static int RingDistanceFromOrigin(Vector2Int chunk)
    {
        return Mathf.Max(Mathf.Abs(chunk.x), Mathf.Abs(chunk.y));
    }
}
