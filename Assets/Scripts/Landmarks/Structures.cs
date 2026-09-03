using UnityEngine;

/// <summary>
/// The pieces a structure is put together from, taken out of the tile pack.
/// The pack was drawn to stack: a tower stands on one tile, a stair climbs
/// exactly one tile's height over two tiles' run, a fence is a tile's edge in
/// four pieces. This is the shelf the builder takes them off.
/// </summary>
public class Structures : ScriptableObject
{
    public GameObject Tower;

    /// <summary>Climbs one tile in two. The high end is its -z end.</summary>
    public GameObject Stair;

    public GameObject[] Fences;
    public GameObject Lamp;
    public GameObject[] Busts;
    public GameObject[] Boxes;
    public GameObject Chest;
    public GameObject Timber;
    public GameObject Signboard;

    private static Structures loaded;

    public static Structures Get()
    {
        if (loaded == null) loaded = Resources.Load<Structures>("Structures");
        return loaded;
    }
}
