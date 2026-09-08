using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The wet dark ground: five marsh tiles built in Blender by
/// Tools/marsh_tiles.py, for the low flats, the reedbeds, the dead woods
/// and the beds of lakes and ponds. Definitions 55 to 59, painted with the
/// pack's material like the rest.
/// </summary>
public static class MarshSet
{
    private const string Folder = "Assets/Tiles/Marsh";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstId = 55;
    public const int Variants = 5;

    [MenuItem("Tools/Tile World/Build the marsh set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("MARSH no paint at " + Paint); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Folder + "/Marsh Tile " + i + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Marsh Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("MARSH no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);

            int id = FirstId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("MARSH tile " + id + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts") + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
        }

        var library = AssetDatabase.LoadAssetAtPath<TileLibrary>(Library);
        if (library == null) { Debug.LogError("MARSH no tile library"); return; }
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
        Debug.Log("MARSH " + made.Count + " tiles in the library, ids " + FirstId + " to " + (FirstId + made.Count - 1));
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
