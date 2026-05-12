using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    private const int TilesPerChunk  = 15;
    private const int TileSize       = 2;
    private const int ChunkWorldSize = TilesPerChunk * TileSize;

    private const int Categories          = 5;  
    private const int VariantsPerCategory = 5;  
    private const int TotalIds            = Categories * VariantsPerCategory;
    private const float CategoryNoiseScale = 0.08f;
    public Dictionary<int, List<Matrix4x4>> idToListOfTransforms
        = new Dictionary<int, List<Matrix4x4>>();
    private readonly Vector2Int chunkIndex;
    public Chunk(Vector2Int chunkIndex)
    {
        this.chunkIndex = chunkIndex;
        Generate();
    }
    private void Generate()
    {
        for (int tx = 0; tx < TilesPerChunk; tx++)
        for (int ty = 0; ty < TilesPerChunk; ty++)
        {
            int gx = chunkIndex.x * TilesPerChunk + tx;
            int gy = chunkIndex.y * TilesPerChunk + ty;
            float n = Mathf.PerlinNoise(gx * CategoryNoiseScale, gy * CategoryNoiseScale);
            int category = Mathf.Clamp(Mathf.FloorToInt(n * Categories), 0, Categories - 1);
            int variant = Hash2D(gx, gy, 12345) % VariantsPerCategory;
            int id = category * VariantsPerCategory + variant;

            if (!idToListOfTransforms.ContainsKey(id))
                idToListOfTransforms[id] = new List<Matrix4x4>();

            Vector3 pos = new Vector3(
                chunkIndex.x * ChunkWorldSize + tx * TileSize,
                0f,
                chunkIndex.y * ChunkWorldSize + ty * TileSize
            );

            idToListOfTransforms[id].Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        }
    }

    
    private static int Hash2D(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        
        return (int)(h & 0x7FFFFFFF);
    }
}

