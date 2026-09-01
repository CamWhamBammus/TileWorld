using UnityEngine;

/// <summary>
/// Restores a save once the world exists, and writes one back periodically and
/// on the way out.
/// </summary>
public class SaveCoordinator : MonoBehaviour
{
    [SerializeField] private float saveEverySeconds = 30f;

    private ChunkManager world;
    private Transform player;
    private float nextSave;
    private bool restored;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<SaveCoordinator>() == null)
        {
            new GameObject("Save (runtime)").AddComponent<SaveCoordinator>();
        }
    }

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null)
        {
            enabled = false;
            return;
        }

        player = world.PlayerTransform;
        nextSave = Time.time + saveEverySeconds;
    }

    private void Update()
    {
        // Restore on the first frame, once every Start has run and the player
        // has been placed on the terrain.
        if (!restored)
        {
            restored = true;
            Restore();
        }

        if (Time.time >= nextSave)
        {
            nextSave = Time.time + saveEverySeconds;
            Save();
        }
    }

    private void Restore()
    {
        var data = SaveGame.Data;

        if (data == null) return;

        foreach (var chunk in data.visited) ExplorationLog.Visit(chunk);
        foreach (var chunk in data.surveyed) ExplorationLog.Survey(chunk);

        for (int i = 0; i < data.landmarkChunks.Count && i < data.landmarkKinds.Count; i++)
        {
            LandmarkLog.Discover(data.landmarkChunks[i], (LandmarkKind)data.landmarkKinds[i]);
        }

        if (TimeOfDay.Instance != null) TimeOfDay.Instance.SetTime(data.timeOfDay);

        var quests = FindFirstObjectByType<QuestManager>();
        if (quests != null) quests.RestoreProgress(data.surveysMade, data.highestReached);

        if (data.waypointSet) Waypoint.Set(data.waypointChunk);

        if (player != null && data.playerPosition != Vector3.zero)
        {
            var controller = player.GetComponent<CharacterController>();
            bool on = controller != null && controller.enabled;

            if (on) controller.enabled = false;
            player.position = data.playerPosition;
            player.rotation = Quaternion.Euler(0f, data.playerYaw, 0f);
            if (on) controller.enabled = true;
        }

        Debug.Log("[Save] Restored " + ExplorationLog.Count + " charted chunks and "
                + LandmarkLog.Count + " landmarks.");
    }

    private void Save()
    {
        if (world == null) return;

        var data = new SaveData
        {
            seed = world.WorldSeed,
            timeOfDay = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.3f,
            playerPosition = player != null ? player.position : Vector3.zero,
            playerYaw = player != null ? player.eulerAngles.y : 0f
        };

        foreach (var chunk in ExplorationLog.Visited) data.visited.Add(chunk);
        foreach (var chunk in ExplorationLog.Surveyed) data.surveyed.Add(chunk);

        foreach (var pair in LandmarkLog.Found)
        {
            data.landmarkChunks.Add(pair.Key);
            data.landmarkKinds.Add((int)pair.Value);
        }

        var quests = FindFirstObjectByType<QuestManager>();

        if (quests != null)
        {
            data.surveysMade = quests.SurveysMade;
            data.highestReached = quests.HighestReached;
        }

        data.waypointSet = Waypoint.IsSet;
        data.waypointChunk = Waypoint.Chunk;

        SaveGame.Write(data);
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }
}
