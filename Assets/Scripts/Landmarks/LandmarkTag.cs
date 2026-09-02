using UnityEngine;

/// <summary>
/// Says what a built structure is, so anything that comes across one in the
/// world can tell a watchtower from a stone circle without working it out from
/// the seed again.
/// </summary>
public class LandmarkTag : MonoBehaviour
{
    public LandmarkKind Kind { get; private set; }
    public Vector2Int Chunk { get; private set; }

    public static LandmarkTag Attach(GameObject go, LandmarkKind kind, Vector2Int chunk)
    {
        var tag = go.AddComponent<LandmarkTag>();
        tag.Kind = kind;
        tag.Chunk = chunk;

        return tag;
    }
}
