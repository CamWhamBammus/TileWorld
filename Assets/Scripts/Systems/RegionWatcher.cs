using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notices when the player crosses into a region they have not been in before,
/// and keeps the list of the ones they have. Crossing a border is the moment a
/// name is worth saying.
/// </summary>
public class RegionWatcher : MonoBehaviour
{
    private static readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

    public static IReadOnlyCollection<Vector2Int> Visited => visited;
    public static int Count => visited.Count;

    /// <summary>The region the player is standing in, for anything that wants to show it.</summary>
    public static Regions.Region Current { get; private set; }
    public static bool HasCurrent { get; private set; }

    private ChunkManager world;
    private Transform player;
    private Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int lastChunk = new Vector2Int(int.MinValue, int.MinValue);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        visited.Clear();
        HasCurrent = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<RegionWatcher>() == null)
        {
            new GameObject("Regions (runtime)").AddComponent<RegionWatcher>();
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
    }

    private void Update()
    {
        if (player == null) return;

        // Which region you are in is not which square of the ruled grid you are in. The border
        // wanders by up to fourteen tiles, so keying off the straight cell left the name and the
        // description of the country you had just left showing for a strip nearly thirty metres
        // wide, and announced the same place twice when you crossed a grid line without leaving
        // it. The region carries the cell it was actually taken from; key off that.
        //
        // Guarded on the chunk first, because naming a region builds a string and this is Update.
        Vector2Int chunk = WorldGrid.WorldToChunk(player.position);

        if (chunk == lastChunk) return;

        lastChunk = chunk;

        var region = Regions.At(chunk, world.WorldSeed);
        Current = region;
        HasCurrent = true;

        if (region.Cell == lastCell) return;

        lastCell = region.Cell;

        if (visited.Add(region.Cell))
        {
            Notices.Show("You come into " + region.Name);
        }
    }
}
