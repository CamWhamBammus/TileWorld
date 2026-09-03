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

    /// <summary>Spans two tiles along its z, deck at 0.99.</summary>
    public GameObject Bridge;

    /// <summary>A flat slab of planks, 2 by 3, its top at 0.98.</summary>
    public GameObject Slab;

    public GameObject[] Fences;
    public GameObject Lamp;
    /// <summary>The folder says busts. Every one of them is a bush.</summary>
    public GameObject[] Bushes;
    public GameObject[] Boxes;
    public GameObject Chest;

    /// <summary>A pair of doors, thin in x, standing 1.65 tall on their pivot's floor.</summary>
    public GameObject Doors;
    /// <summary>A bundle of crossed lumber. Not a log: laid as walls it came out as scaffolding.</summary>
    public GameObject Timber;

    /// <summary>Thin poles, a tile tall or so, their foot a unit below the pivot. Posts and masts.</summary>
    public GameObject[] Poles;
    public GameObject Signboard;

    private static Structures loaded;

    public static Structures Get()
    {
        if (loaded == null) loaded = Resources.Load<Structures>("Structures");
        return loaded;
    }
}
