using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The rock, in two kinds from one script, Tools/rock_tiles.py: five stone
/// tiles for the barrens and the beds of deep and frozen water, and five
/// scree tiles for the steep faces. Definitions 70 to 74 (stone) and 75 to
/// 79 (scree), painted with the pack's material like the rest.
/// </summary>
public static class RockSet
{
    private const string Folder = "Assets/Tiles/Rock";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstStoneId = 70, FirstScreeId = 75, Variants = 5;

    [MenuItem("Tools/Tile World/Build the rock set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("ROCK no paint"); return; }
        for (int i = 0; i < Variants; i++) { ForestSet.Settle(Folder + "/Stone Tile " + i + ".fbx"); ForestSet.Settle(Folder + "/Scree Tile " + i + ".fbx"); }
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        foreach (var (prefix, first) in new[] { ("Stone Tile ", FirstStoneId), ("Scree Tile ", FirstScreeId) })
        for (int i = 0; i < Variants; i++)
        {
            string name = prefix + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("ROCK no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + " (ours).prefab");
            Object.DestroyImmediate(root);
            int id = first + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("ROCK tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts") + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
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
        Debug.Log("ROCK " + made.Count + " tiles in the library, stone " + FirstStoneId + " to " + (FirstStoneId + 4) + ", scree " + FirstScreeId + " to " + (FirstScreeId + 4));
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
