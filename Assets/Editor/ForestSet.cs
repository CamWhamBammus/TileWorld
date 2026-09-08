using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The forest's own ground and trees: five floor tiles and six trees built in
/// Blender by Tools/forest_tiles.py and Tools/forest_trees.py, their colours
/// painted into blank cells of the pack's palette so they draw with the same
/// material as everything else. The tiles become definitions 35 to 39 in the
/// library; the trees go into the flora for the undergrowth to plant.
/// </summary>
public static class ForestSet
{
    private const string Folder = "Assets/Tiles/Forest";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string FloraAsset = "Assets/Resources/Flora.asset";

    /// <summary>The first block id the forest floor takes: the stone owns 30 to 34.</summary>
    public const int FirstId = 35;
    public const int Variants = 5;

    private static readonly string[] TreeFiles = { "Oak 0", "Oak 1", "Beech 0", "Birch 0", "Birch 1", "Sapling 0" };

    [MenuItem("Tools/Tile World/Build the forest set")]
    public static void Go()
    {
        var paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);
        if (paint == null) { Debug.LogError("FOREST no paint at " + Paint); return; }

        for (int i = 0; i < Variants; i++) Settle(Folder + "/Forest Tile " + i + ".fbx");
        foreach (var name in TreeFiles) Settle(Folder + "/" + name + ".fbx");
        AssetDatabase.Refresh();

        // ---- the floor: a prefab and a definition each, painted with the pack's material
        var made = new List<TileDefinition>();

        for (int i = 0; i < Variants; i++)
        {
            string fbx = Folder + "/Forest Tile " + i + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogError("FOREST no model at " + fbx); continue; }

            var root = new GameObject("Forest Tile " + i);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            foreach (var r in part.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;

            string path = Built + "/Forest Tile " + i + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            int id = FirstId + i;
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
            Debug.Log("FOREST tile " + id + " | mesh " + (mesh == null ? "none" : mesh.vertexCount + " verts, "
                + mesh.bounds.min.ToString("F2") + " to " + mesh.bounds.max.ToString("F2"))
                + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
        }

        // and into the library, without disturbing what is already in it
        var library = AssetDatabase.LoadAssetAtPath<TileLibrary>(Library);
        if (library == null) { Debug.LogError("FOREST no tile library"); return; }

        var serialized = new SerializedObject(library);
        var list = serialized.FindProperty("definitions");
        foreach (var def in made)
        {
            bool already = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == def) already = true;
            if (already) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = def;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);

        // ---- the trees: into the flora, on their feet
        var flora = AssetDatabase.LoadAssetAtPath<Flora>(FloraAsset);
        if (flora == null) { Debug.LogError("FOREST no flora at " + FloraAsset); return; }

        var trees = new List<Flora.Sprout>();
        var said = new System.Text.StringBuilder();
        foreach (var name in TreeFiles)
        {
            var mesh = FirstMesh(Folder + "/" + name + ".fbx");
            if (mesh == null) { Debug.LogError("FOREST no mesh in " + name); continue; }
            trees.Add(new Flora.Sprout
            {
                Name = name, Mesh = mesh,
                Size = mesh.bounds.size.y, Wide = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z), Foot = -mesh.bounds.min.y,
                Colour = new Color(0.38f, 0.57f, 0.21f)
            });
            said.Append(" " + name + " (" + mesh.vertexCount + " verts, " + mesh.bounds.size.y.ToString("F1") + " tall, foot " + (-mesh.bounds.min.y).ToString("F2") + ")");
        }
        flora.ForestTrees = trees.ToArray();
        EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("FOREST " + made.Count + " floor tiles in the library, ids " + FirstId + " to " + (FirstId + made.Count - 1)
            + " | " + trees.Count + " trees in the flora:" + said);
    }

    /// <summary>A model as the game needs it: readable, with no materials of its own and nothing animated.</summary>
    private static void Settle(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { Debug.LogError("FOREST nothing imported at " + path); return; }
        bool changed = false;
        if (!importer.isReadable) { importer.isReadable = true; changed = true; }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
        if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    private static Mesh FirstMesh(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m) return m;
        return null;
    }

    public static void Batch()
    {
        Go();
        EditorApplication.Exit(0);
    }
}
