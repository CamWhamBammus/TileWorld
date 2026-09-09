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
public static class PeakSet
{
    private const string Tiles = "Assets/Tiles/Peak";
    private const string Standing = "Assets/Tiles/PeakPlants";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstPeakId = 105, Variants = 5;

    private static readonly string[] Trees = { "Krummholz", "Krummholz Mat" };
    private static readonly string[] Small = { "Cushion Plant", "Mountain Tussock" };
    private static readonly string[] Blocks = { "Erratic" };

    [MenuItem("Tools/Tile World/Build the peak set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("PEAK no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Tiles + "/Peak Tile " + i + ".fbx");
        foreach (var set in new[] { Trees, Small, Blocks })
            foreach (var n in set) ForestSet.Settle(Standing + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Peak Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("PEAK no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstPeakId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("PEAK tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts")
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
        if (flora == null) { Debug.LogError("PEAK no flora"); return; }
        flora.Krummholz = Sprouts(Trees, new Color(0.20f, 0.32f, 0.22f));
        flora.AlpinePlants = Sprouts(Small, new Color(0.42f, 0.46f, 0.30f));
        flora.AlpineStones = Sprouts(Blocks, new Color(0.66f, 0.68f, 0.66f));
        EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PEAK " + made.Count + " tiles in the library, " + FirstPeakId + " to " + (FirstPeakId + Variants - 1)
                  + " | flora: " + flora.Krummholz.Length + " krummholz, " + flora.AlpinePlants.Length
                  + " small, " + flora.AlpineStones.Length + " erratics");
    }

    private static Flora.Sprout[] Sprouts(string[] names, Color colour)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Standing + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("PEAK no mesh in " + name); continue; }
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
