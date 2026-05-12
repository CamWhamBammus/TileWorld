using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkMap
{
	
	private Dictionary<Vector2Int, Chunk> chunkMap = new Dictionary<Vector2Int, Chunk>();

	public Dictionary<Vector2Int, Chunk> ChunkMapGetter()
	{
		return chunkMap;
	}
}
