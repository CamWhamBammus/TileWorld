using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The savanna, built by Tools/savanna_tiles.py and Tools/savanna_trees.py:
/// five tiles of dry grassland, definitions 165 to 169, and the five things
/// that stand on it. The acacias go in a band of their own because they are
/// the whole shape of the country and want to be tall; the termite mounds go
/// in one of their own because they are rare and must not be scaled to the
/// height of a tree.
/// </summary>
public static class SavannaSet
{
    private const string Tiles = "Assets/Tiles/Savanna";
    private const string Standing = "Assets/Tiles/SavannaPlants";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstSavannaId = 165, Variants = 5;

    private static readonly string[] Trees = { "Acacia", "Acacia Small" };
    private static readonly string[] Scrub = { "Thorn Bush" };
    private static readonly string[] Grasses = { "Tall Grass" };
    private static readonly string[] Mounds = { "Termite Mound" };

    [MenuItem("Tools/Tile World/Build the savanna set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("SAVANNA no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Tiles + "/Savanna Tile " + i + ".fbx");
        foreach (var set in new[] { Trees, Scrub, Grasses, Mounds })
            foreach (var n in set) ForestSet.Settle(Standing + "/" + n + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Savanna Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("SAVANNA no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstSavannaId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("SAVANNA tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts")
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
        if (flora == null) { Debug.LogError("SAVANNA no flora"); return; }
        flora.Acacias = Sprouts(Trees, new Color(0.30f, 0.48f, 0.24f));
        flora.SavannaScrub = Sprouts(Scrub, new Color(0.42f, 0.50f, 0.30f));
        flora.SavannaGrass = Sprouts(Grasses, new Color(0.78f, 0.72f, 0.44f));
        flora.TermiteMounds = Sprouts(Mounds, new Color(0.69f, 0.38f, 0.24f));
        EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SAVANNA " + made.Count + " tiles in the library, " + FirstSavannaId + " to " + (FirstSavannaId + Variants - 1)
                  + " | flora: " + flora.Acacias.Length + " acacias, " + flora.SavannaScrub.Length
                  + " thorn, " + flora.SavannaGrass.Length + " grass, " + flora.TermiteMounds.Length + " mounds");
    }

    private static Flora.Sprout[] Sprouts(string[] names, Color colour)
    {
        var made = new List<Flora.Sprout>();
        foreach (var name in names)
        {
            var mesh = ForestSet.FirstMesh(Standing + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("SAVANNA no mesh in " + name); continue; }
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
