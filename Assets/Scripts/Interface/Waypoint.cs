using UnityEngine;

/// <summary>
/// One marked spot. The map shows where you have been and the compass shows
/// where you are pointing; neither could say "go there", which is the thing
/// you actually want after spotting a gap in the chart.
/// </summary>
public static class Waypoint
{
    public static bool IsSet { get; private set; }
    public static Vector2Int Chunk { get; private set; }

    public static Vector3 Position => WorldGrid.ChunkCenter(Chunk);

    public static void Set(Vector2Int chunk)
    {
        Chunk = chunk;
        IsSet = true;
        Notices.Show("Waypoint set at " + chunk.x + ", " + chunk.y);
    }

    public static void Clear()
    {
        if (!IsSet) return;

        IsSet = false;
        Notices.Show("Waypoint cleared");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsSet = false;
    }
}
