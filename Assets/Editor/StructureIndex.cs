using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Writes down which of the pack's pieces the structures are built from.</summary>
public static class StructureIndex
{
    private const string P = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Prefabs/";
    private const string Written = "Assets/Resources/Structures.asset";

    [MenuItem("Tools/Tile World/Index the structure pieces")]
    public static void Go()
    {
        var asset = AssetDatabase.LoadAssetAtPath<Structures>(Written);
        bool fresh = asset == null;
        if (fresh) asset = ScriptableObject.CreateInstance<Structures>();

        asset.Tower = One("Tower/Tower");
        asset.Stair = One("Passages/Climbe");
        asset.Fences = Many("Environment/Fence", "Environment/Fence_0", "Environment/Fence_1", "Environment/Fence_2", "Environment/Fence_3");
        asset.Lamp = One("Environment/Lamp");
        asset.Busts = Many("Busts/Bust_0", "Busts/Bust_1", "Busts/Bust_2", "Busts/Bust_3", "Busts/Bust_4", "Busts/Bust_5");
        asset.Boxes = Many("Environment/Box", "Environment/Box_0", "Environment/Box_1");
        asset.Chest = One("Environment/Chest");
        asset.Timber = One("Environment/Timber");
        asset.Signboard = One("Environment/Singboard");

        if (fresh) AssetDatabase.CreateAsset(asset, Written);
        else EditorUtility.SetDirty(asset);

        AssetDatabase.SaveAssets();
        Debug.Log("STRUCTURES indexed: tower " + (asset.Tower != null) + ", stair " + (asset.Stair != null)
            + ", fences " + asset.Fences.Length + ", busts " + asset.Busts.Length);
    }

    private static GameObject One(string name)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(P + name + ".prefab");
        if (go == null) Debug.LogError("STRUCTURES missing " + name);
        return go;
    }

    private static GameObject[] Many(params string[] names)
    {
        var list = new List<GameObject>();
        foreach (string n in names) { var go = One(n); if (go != null) list.Add(go); }
        return list.ToArray();
    }
}
