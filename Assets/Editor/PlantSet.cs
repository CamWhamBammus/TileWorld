using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The small standing things, built in Blender by Tools/plants.py: five
/// mushrooms, three of the fungal country's big toadstools, four boulders,
/// three desert stones and three dead trees. They go into the flora in
/// sets of their own, which the undergrowth plants in place of the pack's.
/// </summary>
public static class PlantSet
{
    private const string Folder = "Assets/Tiles/Plants";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    private static readonly string[] Mushrooms = { "Mushroom 0", "Mushroom 1", "Mushroom 2", "Mushroom 3", "Mushroom 4" };
    private static readonly string[] Toadstools = { "Toadstool 0", "Toadstool 1", "Toadstool 2" };
    private static readonly string[] Boulders = { "Boulder 0", "Boulder 1", "Boulder 2", "Boulder 3" };
    private static readonly string[] Stones = { "Desert Stone 0", "Desert Stone 1", "Desert Stone 2" };
    private static readonly string[] DeadTrees = { "Dead Tree 0", "Dead Tree 1", "Dead Tree 2" };

    [MenuItem("Tools/Tile World/Index our plants")]
    public static void Go()
    {
        foreach (var set in new[] { Mushrooms, Toadstools, Boulders, Stones, DeadTrees }) foreach (var n in set) ForestSet.Settle(Folder + "/" + n + ".fbx");
        AssetDatabase.Refresh();
        var flora = AssetDatabase.LoadAssetAtPath<Flora>(FloraAsset);
        flora.OurMushrooms = Sprouts(Mushrooms, new Color(0.7f, 0.3f, 0.25f));
        flora.OurToadstools = Sprouts(Toadstools, new Color(0.6f, 0.3f, 0.5f));
        flora.OurBoulders = Sprouts(Boulders, new Color(0.48f, 0.48f, 0.46f));
        flora.OurStones = Sprouts(Stones, new Color(0.72f, 0.62f, 0.44f));
        flora.OurDeadTrees = Sprouts(DeadTrees, new Color(0.48f, 0.42f, 0.36f));
        EditorUtility.SetDirty(flora);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PLANTS in the flora: " + flora.OurMushrooms.Length + " mushrooms, " + flora.OurToadstools.Length + " toadstools, " + flora.OurBoulders.Length + " boulders, " + flora.OurStones.Length + " stones, " + flora.OurDeadTrees.Length + " dead trees");
    }

    private static Flora.Sprout[] Sprouts(string[] names, Color colour)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Folder + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("PLANTS no mesh in " + name); continue; }
            made.Add(new Flora.Sprout { Name = name, Mesh = mesh, Size = mesh.bounds.size.y, Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z), Foot = -mesh.bounds.min.y, Colour = colour });
        }
        return made.ToArray();
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
