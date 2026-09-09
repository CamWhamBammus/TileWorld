using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The high country's own ground, built by Tools/peak_tiles.py and
/// Tools/peak_plants.py: five tiles of frost-split rock and thin turf,
/// definitions 105 to 109, and the five things that survive up there, which go
/// into the flora in three bands. The krummholz band tops out over the
/// treeline rule on purpose, so the trees stop where the treeline is.
/// </summary>
public static class VergeSet
{
    private const string Tiles = "Assets/Tiles/Verge";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstVergeId = 110, Variants = 5;


    [MenuItem("Tools/Tile World/Build the verge set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("VERGE no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Tiles + "/Verge Tile " + i + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Verge Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("VERGE no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstVergeId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("VERGE tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts")
                      + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
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
        Debug.Log("VERGE " + made.Count + " tiles in the library, " + FirstVergeId + " to " + (FirstVergeId + Variants - 1)
);
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
