using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The reef verge, built by Tools/reef_verge.py: five tiles graded from sand
/// into coral floor, definitions 195 to 199. The mixed ground covers borders
/// between countries; this is not one. A reef stops where the water stops
/// being a metre deep, which is a line of depth inside one country, so it is
/// graded by depth rather than by how near a border a tile is.
/// </summary>
public static class ReefVergeSet
{
    private const string Folder = "Assets/Tiles/ReefVerge";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstVergeId = 195, Variants = 5;
    private const int Steps = 5;

    [MenuItem("Tools/Tile World/Build the reef verge")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("REEF VERGE no paint"); return; }

        var names = new List<string>();
        for (int k = 0; k < Steps; k++) names.Add("Reef Verge " + k);

        foreach (var n in names) ForestSet.Settle(Folder + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int at = 0; at < names.Count; at++)
        {
            string name = names[at];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("REEF VERGE no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstVergeId + at;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
        }

        var library = AssetDatabase.LoadAssetAtPath<TileLibrary>(Library);
        var serialized = new SerializedObject(library);
        var list = serialized.FindProperty("definitions");
        foreach (var def in made)
        {
            bool already = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == def) already = true;
            if (already) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = def;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("REEF VERGE " + made.Count + " tiles in the library, " + FirstVergeId + " to " + (FirstVergeId + made.Count - 1));
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
