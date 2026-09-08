using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The two dark grounds from Tools/dark_tiles.py: five fungal floor tiles
/// (definitions 85 to 89) and five for the dead woods (90 to 94), painted
/// with the pack's material like the rest.
/// </summary>
public static class DarkSet
{
    private const string Folder = "Assets/Tiles/Dark";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstFungalId = 85, FirstDeadId = 90, Variants = 5;

    [MenuItem("Tools/Tile World/Build the dark grounds")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("DARK no paint"); return; }
        for (int i = 0; i < Variants; i++) { ForestSet.Settle(Folder + "/Fungal Tile " + i + ".fbx"); ForestSet.Settle(Folder + "/Dead Tile " + i + ".fbx"); }
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        foreach (var (prefix, first) in new[] { ("Fungal Tile ", FirstFungalId), ("Dead Tile ", FirstDeadId) })
        for (int i = 0; i < Variants; i++)
        {
            string name = prefix + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("DARK no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = first + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("DARK tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts") + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
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
        Debug.Log("DARK " + made.Count + " tiles in the library, fungal " + FirstFungalId + " to " + (FirstFungalId + 4) + ", dead " + FirstDeadId + " to " + (FirstDeadId + 4));
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
