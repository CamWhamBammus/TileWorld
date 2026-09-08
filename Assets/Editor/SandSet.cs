using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The sand, in two kinds, and what grows on it: five beach tiles and five
/// desert tiles built in Blender by Tools/sand_tiles.py, and the cacti and
/// palms of Tools/desert_flora.py. The tiles become definitions 60 to 64
/// (beach) and 65 to 69 (desert); the plants go into the flora for the
/// undergrowth, in three sets by how tall they stand.
/// </summary>
public static class SandSet
{
    private const string Tiles = "Assets/Tiles/Sand";
    private const string Plants = "Assets/Tiles/Desert";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstBeachId = 60, FirstDesertId = 65, Variants = 5;
    private static readonly string[] Saguaros = { "Saguaro 0", "Saguaro 1" };
    private static readonly string[] Small = { "Barrel Cactus 0", "Barrel Cactus 1", "Prickly Pear 0" };
    private static readonly string[] Palms = { "Palm 0", "Palm 1", "Palm 2" };

    [MenuItem("Tools/Tile World/Build the sand set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("SAND2 no paint"); return; }
        for (int i = 0; i < Variants; i++) { ForestSet.Settle(Tiles + "/Beach Tile " + i + ".fbx"); ForestSet.Settle(Tiles + "/Desert Tile " + i + ".fbx"); }
        foreach (var n in Saguaros) ForestSet.Settle(Plants + "/" + n + ".fbx");
        foreach (var n in Small) ForestSet.Settle(Plants + "/" + n + ".fbx");
        foreach (var n in Palms) ForestSet.Settle(Plants + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        foreach (var (prefix, first) in new[] { ("Beach Tile ", FirstBeachId), ("Desert Tile ", FirstDesertId) })
        for (int i = 0; i < Variants; i++)
        {
            string name = prefix + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("SAND2 no model for " + name); continue; }
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
            Debug.Log("SAND2 tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts") + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
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
        flora.Saguaros = Sprouts(Saguaros, new Color(0.38f, 0.56f, 0.26f));
        flora.SmallCacti = Sprouts(Small, new Color(0.38f, 0.56f, 0.26f));
        flora.BeachPalms = Sprouts(Palms, new Color(0.35f, 0.56f, 0.24f));
        EditorUtility.SetDirty(flora);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SAND2 " + made.Count + " tiles in the library, beach " + FirstBeachId + " to " + (FirstBeachId + 4) + ", desert " + FirstDesertId + " to " + (FirstDesertId + 4)
            + " | flora: " + flora.Saguaros.Length + " saguaros, " + flora.SmallCacti.Length + " small cacti, " + flora.BeachPalms.Length + " palms");
    }

    private static Flora.Sprout[] Sprouts(string[] names, Color colour)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Plants + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("SAND2 no mesh in " + name); continue; }
            made.Add(new Flora.Sprout { Name = name, Mesh = mesh, Size = mesh.bounds.size.y, Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z), Foot = -mesh.bounds.min.y, Colour = colour });
        }
        return made.ToArray();
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
