using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The reef, built by Tools/reef_tiles.py and Tools/reef_corals.py: five floor
/// tiles for the warm shallows, definitions 95 to 99, and six coral that stand
/// up off them, which go into the flora for the undergrowth to plant under the
/// water. Painted with the pack's material like the rest.
/// </summary>
public static class ReefSet
{
    private const string Tiles = "Assets/Tiles/Reef";
    private const string Coral = "Assets/Tiles/Coral";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    public const int FirstReefId = 95, Variants = 5;

    private static readonly string[] Corals =
        { "Brain Coral", "Staghorn Coral", "Table Coral", "Sea Fan", "Barrel Sponge", "Pillar Coral" };

    [MenuItem("Tools/Tile World/Build the reef set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("REEF no paint"); return; }
        for (int i = 0; i < Variants; i++) ForestSet.Settle(Tiles + "/Reef Tile " + i + ".fbx");
        foreach (var name in Corals) ForestSet.Settle(Coral + "/" + name + ".fbx");
        AssetDatabase.Refresh();

        var made = new List<TileDefinition>();
        for (int i = 0; i < Variants; i++)
        {
            string name = "Reef Tile " + i;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("REEF no model for " + name); continue; }
            var root = new GameObject(name);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Built + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            int id = FirstReefId + i;
            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();
            def.blockID = id; def.prefab = saved; def.BuildFromPrefab();
            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset"); else EditorUtility.SetDirty(def);
            made.Add(def);
            var mesh = def.MeshGetter();
            Debug.Log("REEF tile " + id + " " + name + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts")
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
        if (flora == null) { Debug.LogError("REEF no flora"); return; }
        var grown = new List<Flora.Sprout>();
        foreach (var name in Corals)
        {
            var mesh = ForestSet.FirstMesh(Coral + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("REEF no mesh in " + name); continue; }
            grown.Add(new Flora.Sprout
            {
                Name = name, Mesh = mesh, Size = mesh.bounds.size.y,
                Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z),
                Foot = -mesh.bounds.min.y, Colour = new Color(0.85f, 0.45f, 0.55f)
            });
        }
        flora.Corals = grown.ToArray();
        EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("REEF " + made.Count + " tiles in the library, " + FirstReefId + " to " + (FirstReefId + Variants - 1)
                  + ", and " + flora.Corals.Length + " coral in the flora");
    }

    public static void Batch() { Go(); EditorApplication.Exit(0); }
}
