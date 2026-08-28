using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Brings landmarks into the world near the player and takes them away again.
/// Placement is deterministic, so a landmark removed and rebuilt is identical —
/// nothing about it needs storing except whether it has been found.
/// </summary>
public class LandmarkSpawner : MonoBehaviour
{
    [Tooltip("Chunks around the player that have their landmark built. Beyond this they still exist, they are just not loaded.")]
    [SerializeField, Range(1, 5)] private int spawnRadius = 3;

    [Tooltip("How close you must get before it counts as discovered.")]
    [SerializeField] private float discoveryRange = 18f;

    private ChunkManager world;
    private Transform player;

    private readonly Dictionary<Vector2Int, GameObject> live = new Dictionary<Vector2Int, GameObject>();
    private readonly List<Vector2Int> scratch = new List<Vector2Int>();

    private Vector2Int lastChunk = new Vector2Int(int.MinValue, int.MinValue);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<LandmarkSpawner>() != null)
        {
            return;
        }

        new GameObject("Landmarks (runtime)").AddComponent<LandmarkSpawner>();
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
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        Vector2Int chunk = WorldGrid.WorldToChunk(player.position);

        if (chunk != lastChunk)
        {
            lastChunk = chunk;
            Refresh(chunk);
        }

        CheckDiscovery();
    }

    private void Refresh(Vector2Int centre)
    {
        int seed = world.WorldSeed;

        for (int dx = -spawnRadius; dx <= spawnRadius; dx++)
        for (int dz = -spawnRadius; dz <= spawnRadius; dz++)
        {
            var index = new Vector2Int(centre.x + dx, centre.y + dz);

            if (live.ContainsKey(index))
            {
                continue;
            }

            var placement = Landmarks.In(index, seed);

            if (!placement.Exists)
            {
                continue;
            }

            live.Add(index, LandmarkBuilder.Build(placement, transform));
        }

        scratch.Clear();

        foreach (var pair in live)
        {
            Vector2Int offset = pair.Key - centre;

            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > spawnRadius)
            {
                scratch.Add(pair.Key);
            }
        }

        foreach (var index in scratch)
        {
            Destroy(live[index]);
            live.Remove(index);
        }
    }

    private void CheckDiscovery()
    {
        int seed = world.WorldSeed;
        float sqrRange = discoveryRange * discoveryRange;

        foreach (var pair in live)
        {
            if (LandmarkLog.Found.ContainsKey(pair.Key))
            {
                continue;
            }

            Vector3 to = pair.Value.transform.position - player.position;
            to.y = 0f;

            if (to.sqrMagnitude > sqrRange)
            {
                continue;
            }

            var placement = Landmarks.In(pair.Key, seed);

            if (LandmarkLog.Discover(pair.Key, placement.Kind))
            {
                Debug.Log("[Landmarks] Found a " + Landmarks.NameOf(placement.Kind) + " at chunk " + pair.Key);
            }
        }
    }
}
