using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Takes the mushrooms out of the tile pack and writes them down where the game
/// can find them.
///
/// The pack holds two hundred and sixty five meshes and the ground uses ninety
/// two of them; the rest came with it and have never been put to anything. The
/// names are no help -- everything in there is called Cylinder or Cube and a
/// number -- so they are picked out by what they are: a couple of hundred
/// vertices, about a third of a unit tall, and near enough as wide as they are
/// high. That is a mushroom and nothing else in the pack is.
/// </summary>
public static class FloraIndex
{
    private const string Pack = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Isometric Tiles Pack.fbx";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string Written = "Assets/Resources/Flora.asset";

    [MenuItem("Tools/Tile World/Index the pack's flora")]
    public static void Go()
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(Pack).OfType<Mesh>().ToList();

        var taken = new HashSet<Mesh>();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null) continue;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) taken.Add(mf.sharedMesh);
        }

        var mushrooms = new List<Flora.Sprout>();

        foreach (var mesh in all.Where(m => !taken.Contains(m)).OrderBy(m => m.name))
        {
            if (mesh.vertexCount < 150 || mesh.vertexCount > 220) continue;

            var box = mesh.bounds;

            if (box.size.y < 0.25f || box.size.y > 0.50f) continue;
            if (box.size.x < 0.15f || box.size.x > 0.35f) continue;

            mushrooms.Add(new Flora.Sprout { Name = mesh.name, Mesh = mesh, Size = box.size.y });
        }

        var flora = AssetDatabase.LoadAssetAtPath<Flora>(Written);
        bool fresh = flora == null;

        if (fresh) flora = ScriptableObject.CreateInstance<Flora>();

        flora.Mushrooms = mushrooms.ToArray();
        flora.Paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);

        if (fresh) AssetDatabase.CreateAsset(flora, Written);
        else EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();

        Debug.Log("FLORA " + mushrooms.Count + " mushrooms taken from the pack: "
            + string.Join(", ", mushrooms.Select(m => m.Name)));
    }

    /// <summary>For running it without opening the editor.</summary>
    public static void Batch()
    {
        Go();
        EditorApplication.Exit(0);
    }
}
