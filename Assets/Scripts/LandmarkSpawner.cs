using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Brings landmarks into the world near the player and takes them away again.
/// Placement is deterministic, so a landmark removed and rebuilt is identical —
/// nothing about it needs storing except whether it has been found.
/// </summary>
public class LandmarkSpawner : MonoBehaviour
{
    [Tooltip("Chunks around the player that have their landmark built. Never less than the view radius, or you would see terrain with nothing standing on it.")]
    [SerializeField, Range(1, 8)] private int spawnRadius = 5;

    [Tooltip("How close you must get before it counts as discovered.")]
    [SerializeField] private float discoveryRange = 18f;

    [Tooltip("How close you must be, horizontally, for climbing one to count as surveying from it.")]
    [SerializeField] private float surveyRange = 14f;

    [Tooltip("Stand this close to a landmark and you can rest there until morning.")]
    [SerializeField] private float restRange = 12f;

    [SerializeField] private KeyCode restKey = KeyCode.E;

    private ChunkManager world;
    private Transform player;

    private readonly Dictionary<Vector2Int, GameObject> live = new Dictionary<Vector2Int, GameObject>();
    private readonly List<Vector2Int> scratch = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> surveyedFrom = new HashSet<Vector2Int>();

    private Vector2Int lastChunk = new Vector2Int(int.MinValue, int.MinValue);

    /// <summary>
    /// Structures have to be built at least as far out as the terrain is drawn.
    /// Loading them closer than that means walking through country you can see
    /// across with nothing standing in it, and buildings appearing from nowhere
    /// as you approach.
    /// </summary>
    private int LoadRadius => Mathf.Max(spawnRadius, world != null ? world.ViewRadius : spawnRadius);

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
        CheckRest();
    }

    /// <summary>
    /// Somewhere to wait out the dark. Night is navigable but slow going, and
    /// walking back to a landmark you already found gives it a second use.
    /// </summary>
    private void CheckRest()
    {
        if (!Input.GetKeyDown(restKey) || TimeOfDay.Instance == null) return;

        foreach (var pair in live)
        {
            if (!LandmarkLog.Found.ContainsKey(pair.Key)) continue;

            Vector3 to = pair.Value.transform.position - player.position;
            to.y = 0f;

            if (to.sqrMagnitude > restRange * restRange) continue;

            float now = TimeOfDay.Instance.Normalized;

            if (now > 0.24f && now < 0.72f)
            {
                Notices.Show("No need to rest in daylight");
                return;
            }

            TimeOfDay.Instance.SetTime(0.26f);
            Notices.Show("Rested at the " + Landmarks.NameOf(Landmarks.In(pair.Key, world.WorldSeed).Kind) + " until morning");
            return;
        }
    }

    private void Refresh(Vector2Int centre)
    {
        int seed = world.WorldSeed;

        int radius = LoadRadius;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
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
            Debug.Log("[Landmarks] Built a " + Landmarks.NameOf(placement.Kind) + " at chunk " + index);
        }

        scratch.Clear();

        foreach (var pair in live)
        {
            Vector2Int offset = pair.Key - centre;

            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > radius)
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

        foreach (var pair in live)
        {
            Vector3 basePos = pair.Value.transform.position;

            Vector3 flat = basePos - player.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            var placement = Landmarks.In(pair.Key, seed);

            if (distance <= discoveryRange && LandmarkLog.Discover(pair.Key, placement.Kind))
            {
                Notices.Show("Found a " + Landmarks.NameOf(placement.Kind));
            }

            if (surveyedFrom.Contains(pair.Key) || distance > surveyRange)
            {
                continue;
            }

            // Towers ask you to climb them; the rest only ask you to arrive.
            float climbed = player.position.y - basePos.y;

            if (climbed < Landmarks.SurveyHeight(placement.Kind))
            {
                continue;
            }

            surveyedFrom.Add(pair.Key);
            SurveyFrom(pair.Key, placement.Kind);
        }
    }

    /// <summary>Charts the land around a landmark you have made use of.</summary>
    private void SurveyFrom(Vector2Int centre, LandmarkKind kind)
    {
        int radius = Landmarks.SurveyRadius(kind);
        int charted = 0;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            // a circle, so the revealed area does not look like a stamped square
            if (dx * dx + dz * dz > radius * radius)
            {
                continue;
            }

            if (ExplorationLog.Survey(new Vector2Int(centre.x + dx, centre.y + dz)))
            {
                charted++;
            }
        }

        Surveyed?.Invoke(kind, charted);
        Notices.Show("Surveyed from the " + Landmarks.NameOf(kind) + " — " + charted + " chunks charted");
    }

    /// <summary>Raised when a landmark is used to chart the land, with how much it revealed.</summary>
    public static event System.Action<LandmarkKind, int> Surveyed;

    /// <summary>
    /// Statics survive between play sessions when the editor skips domain
    /// reload, which would leave last run's QuestManager still subscribed.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvent()
    {
        Surveyed = null;
    }
}
