using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The jungle, built by Tools/jungle_tiles.py and Tools/jungle_trees.py: five
/// floor tiles for under a closed canopy, definitions 100 to 104, and six
/// plants that stand on them, which go into the flora in four bands so the
/// undergrowth can plant a canopy with layers instead of one height of tree.
/// </summary>
public static class JungleSet
{
    private const string Tiles = "Assets/Tiles/Jungle";
    private const string Trees = "Assets/Tiles/JungleTrees";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstJungleId = 100, Variants = 5;

    private static readonly string[] Giants = { "Jungle Giant" };
    private static readonly string[] Canopy = { "Jungle Tree", "Strangler Fig" };
    private static readonly string[] Canes = { "Bamboo" };
    private static readonly string[] Shade = { "Tree Fern", "Jungle Sapling" };

    [MenuItem("Tools/Tile World/Build the jungle set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("JUNGLE no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Tiles + "/Jungle Tile " + i + ".fbx");
        foreach (var set in new[] { Giants, Canopy, Canes, Shade })
            foreach (var n in set) ForestSet.Settle(Trees + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Jungle Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("JUNGLE no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstJungleId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("JUNGLE tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts")
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

        var flora = AssetDatabase.LoadAssetAtPath<Flora>(FloraAsset);
        if (flora == null) { Debug.LogError("JUNGLE no flora"); return; }
        flora.JungleGiants = Sprouts(Giants, new Color(0.30f, 0.52f, 0.24f));
        flora.JungleTrees = Sprouts(Canopy, new Color(0.26f, 0.48f, 0.22f));
        flora.Bamboo = Sprouts(Canes, new Color(0.62f, 0.72f, 0.34f));
        flora.JungleFerns = Sprouts(Shade, new Color(0.24f, 0.50f, 0.20f));
        EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("JUNGLE " + made.Count + " tiles in the library, " + FirstJungleId + " to " + (FirstJungleId + Variants - 1)
                  + " | flora: " + flora.JungleGiants.Length + " giants, " + flora.JungleTrees.Length + " canopy, "
                  + flora.Bamboo.Length + " bamboo, " + flora.JungleFerns.Length + " in the shade");
    }

    private static Flora.Sprout[] Sprouts(string[] names, Color colour)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Trees + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("JUNGLE no mesh in " + name); continue; }
            made.Add(new Flora.Sprout
            {
                Name = name, Mesh = mesh, Size = mesh.bounds.size.y,
                Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z),
                Foot = -mesh.bounds.min.y, Colour = colour
            });
        }
        return made.ToArray();
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
