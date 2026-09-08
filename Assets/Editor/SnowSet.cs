using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The snowfields' own ground and trees: five snow tiles from
/// Tools/snow_tiles.py, and the laden spruces, the fir, the bare birch and
/// the young spruce of Tools/snow_trees.py. The tiles become definitions
/// 80 to 84; the trees go into the flora in three sets by height.
/// </summary>
public static class SnowSet
{
    private const string Folder = "Assets/Tiles/Snow";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstId = 80, Variants = 5;
    private static readonly string[] Conifers = { "Snow Spruce 0", "Snow Spruce 1", "Snow Fir 0" };
    private static readonly string[] Birches = { "Snow Birch 0" };
    private static readonly string[] Saplings = { "Snow Spruce 20" };

    [MenuItem("Tools/Tile World/Build the snow set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("SNOW no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Folder + "/Snow Tile " + i + ".fbx");
        foreach (var n in Conifers) ForestSet.Settle(Folder + "/" + n + ".fbx");
        foreach (var n in Birches) ForestSet.Settle(Folder + "/" + n + ".fbx");
        foreach (var n in Saplings) ForestSet.Settle(Folder + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Snow Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("SNOW no model for " + name); continue; }
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
            Debug.Log("SNOW tile " + id + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts") + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
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

        var flora = AssetDatabase.LoadAssetAtPath<Flora>(FloraAsset);
        flora.SnowConifers = Sprouts(Conifers); flora.SnowBirches = Sprouts(Birches); flora.SnowSaplings = Sprouts(Saplings);
        EditorUtility.SetDirty(flora);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SNOW " + made.Count + " tiles in the library, ids " + FirstId + " to " + (FirstId + made.Count - 1) + " | flora: " + flora.SnowConifers.Length + " conifers, " + flora.SnowBirches.Length + " birches, " + flora.SnowSaplings.Length + " saplings");
    }

    private static Flora.Sprout[] Sprouts(string[] names)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Folder + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("SNOW no mesh in " + name); continue; }
            made.Add(new Flora.Sprout { Name = name, Mesh = mesh, Size = mesh.bounds.size.y, Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z), Foot = -mesh.bounds.min.y, Colour = Color.white });
        }
        return made.ToArray();
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
