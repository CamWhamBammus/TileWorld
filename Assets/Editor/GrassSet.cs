using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The open country's own ground: five meadows in each of the three shades
/// the world lays by height, built in Blender by Tools/grass_tiles.py on
/// the same terms as the forest floor and painted with the pack's material.
/// The tiles become definitions 40 to 54 in the library: pale 40 to 44,
/// light 45 to 49, dark 50 to 54.
/// </summary>
public static class GrassSet
{
    private const string Folder = "Assets/Tiles/Grass";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";

    public const int FirstId = 40;
    public const int Variants = 5;
    private static readonly string[] Shades = { "pale", "light", "dark" };

    [MenuItem("Tools/Tile World/Build the grass set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("GRASS no paint at " + Paint); return; }

        foreach (var shade in Shades) for (int i = 0; i < Variants; i++) ForestSet.Settle(Folder + "/Grass " + shade + " " + i + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int s = 0; s < Shades.Length; s++)
        for (int i = 0; i < Variants; i++)
        {
            string name = "Grass " + Shades[s] + " " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("GRASS no model for " + name); continue; }

            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);

            int id = FirstId + s * Variants + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id;
            def.prefab = saved;
            def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset");
            else EditorUtility.SetDirty(def);
            made.Add(def);

            var mesh = def.MeshGetter();
            Debug.Log("GRASS tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts, "
                + mesh.bounds.min.ToString("F2") + " to " + mesh.bounds.max.ToString("F2")) + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
        }

        var library = AssetDatabase.LoadAssetAtPath<TileLibrary>(Library);
        if (library == null) { Debug.LogError("GRASS no tile library"); return; }
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
        Debug.Log("GRASS " + made.Count + " tiles in the library, ids " + FirstId + " to " + (FirstId + made.Count - 1));
    }

    public static void Batch()
    {
        Go();
        EditorApplication.Exit(0);
    }
}
