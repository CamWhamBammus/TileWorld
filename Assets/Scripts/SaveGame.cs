using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The only things worth keeping. Terrain, tiles and landmark placement are
/// all functions of the seed, so a save is the seed plus what the player has
/// personally found: where they have been, what they have seen from a height,
/// which landmarks they have reached, the time of day and where they stood.
/// </summary>
[System.Serializable]
public class SaveData
{
    public int seed;
    public float timeOfDay = 0.30f;
    public Vector3 playerPosition;
    public float playerYaw;

    public List<Vector2Int> visited = new List<Vector2Int>();
    public List<Vector2Int> surveyed = new List<Vector2Int>();
    public List<Vector2Int> landmarkChunks = new List<Vector2Int>();
    public List<int> landmarkKinds = new List<int>();
}

public static class SaveGame
{
    private const string FileName = "tileworld-save.json";

    public static SaveData Data { get; private set; }
    public static bool HasSave => Data != null;

    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>
    /// Read before the scene loads, so ChunkManager can pick the saved seed up
    /// in its own Start rather than generating a world and replacing it.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Data = null;

        try
        {
            if (!System.IO.File.Exists(Path)) return;

            var json = System.IO.File.ReadAllText(Path);
            var data = JsonUtility.FromJson<SaveData>(json);

            if (data != null && data.seed != 0)
            {
                Data = data;
                Debug.Log("[Save] Loaded world " + data.seed + " with " + data.visited.Count + " chunks charted.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] Could not read the save, starting fresh: " + e.Message);
        }
    }

    public static void Write(SaveData data)
    {
        try
        {
            System.IO.File.WriteAllText(Path, JsonUtility.ToJson(data));
            Data = data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] Could not write the save: " + e.Message);
        }
    }

    public static void Delete()
    {
        try
        {
            if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
            Data = null;
            Debug.Log("[Save] Save cleared.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] Could not clear the save: " + e.Message);
        }
    }
}
