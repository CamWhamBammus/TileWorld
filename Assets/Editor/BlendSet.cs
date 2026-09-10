using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The mixed ground, built by Tools/blend_tiles.py: ten series of five, one for every pair of
/// the five ground families, definitions 115 to 164. The order here must match the order the
/// script builds them in and the order Chunk works its pair index out in -- sand, grass, dark,
/// rock, snow, taken two at a time in that order.
/// </summary>
public static class BlendSet
{
    private const string Folder = "Assets/Tiles/Blend";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstBlendId = 115, Variants = 5;
    private static readonly string[] Order = { "Sand", "Grass", "Dark", "Rock", "Snow" };

    [MenuItem("Tools/Tile World/Build the mixed ground")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("BLEND no paint"); return; }

        var names = new List<string>();
        for (int i = 0; i < Order.Length; i++)
        for (int j = i + 1; j < Order.Length; j++)
        for (int k = 0; k < Variants; k++)
            names.Add("Blend " + Order[i] + " " + Order[j] + " " + k);

        foreach (var n in names) ForestSet.Settle(Folder + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int at = 0; at < names.Count; at++)
        {
            string name = names[at];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("BLEND no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstBlendId + at;
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
        Debug.Log("BLEND " + made.Count + " tiles in the library, " + FirstBlendId + " to " + (FirstBlendId + made.Count - 1));
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
