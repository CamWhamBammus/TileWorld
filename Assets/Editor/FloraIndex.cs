using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Takes the small standing things out of the tile pack and writes them down
/// where the game can find them.
///
/// This used to pick the mushrooms out by shape, because the pack's meshes are
/// all called Cylinder or Cube and a number and there was nothing else to go
/// on. The update sorted the whole pack into named folders, so they are simply
/// read off the shelf now. It also broke the old trick outright: picking by
/// shape meant skipping any mesh a prefab already used, and the update ships a
/// prefab for every mushroom, so the old rule found none of them.
/// </summary>
public static class FloraIndex
{
    private const string Main = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Prefabs";
    private const string Sand = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Update 1 - Sand/Prefabs";
    private const string Paint = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Main Material.mat";
    private const string Written = "Assets/Resources/Flora.asset";

    [MenuItem("Tools/Tile World/Index the pack's flora")]
    public static void Go()
    {
        var flora = AssetDatabase.LoadAssetAtPath<Flora>(Written);
        bool fresh = flora == null;

        if (fresh) flora = ScriptableObject.CreateInstance<Flora>();

        flora.Mushrooms = FromFolder(Main + "/Mushrooms");
        flora.Cacti = FromFolder(Sand + "/Cactus");
        flora.Palms = FromFolder(Sand + "/Palms");
        flora.Stones = FromFolder(Sand + "/Stones");
        flora.Boulders = FromFolder(Main + "/Stones");
        flora.Trees = FromFolder(Main + "/Trees");
        flora.DeadTrees = FromFolder(Sand + "/Sand Tree");

        // The reeds are the pack's thin standing poles. They are filed under
        // scenery with the boxes and the fences, so they are taken by name.
        flora.Reeds = FromFolder(Main + "/Environment", "Rarefoot");
        flora.Paint = AssetDatabase.LoadAssetAtPath<Material>(Paint);

        if (fresh) AssetDatabase.CreateAsset(flora, Written);
        else EditorUtility.SetDirty(flora);

        AssetDatabase.SaveAssets();

        Debug.Log("FLORA " + flora.Mushrooms.Length + " mushrooms, " + flora.Cacti.Length + " cactus, "
            + flora.Palms.Length + " palms, " + flora.Stones.Length + " sand stones, "
            + flora.Boulders.Length + " boulders, " + flora.Trees.Length + " trees, "
            + flora.DeadTrees.Length + " dead trees, " + flora.Reeds.Length + " reeds"
            + " | paint " + (flora.Paint == null ? "MISSING" : flora.Paint.name));

        foreach (var set in new[] { flora.Mushrooms, flora.Boulders, flora.Trees, flora.DeadTrees, flora.Reeds })
        {
            if (set.Length == 0) continue;

            Debug.Log("FLORA  e.g. " + set[0].Name + " tall " + set[0].Size.ToString("F2")
                + " foot " + set[0].Foot.ToString("F2"));
        }
    }

    /// <summary>Everything with a mesh in a folder, as something that can be stood up.</summary>
    private static Flora.Sprout[] FromFolder(string folder, string named = null)
    {
        var found = new List<Flora.Sprout>();

        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("FLORA no such folder: " + folder);
            return found.ToArray();
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

            if (go == null) continue;
            if (named != null && !go.name.StartsWith(named)) continue;

            var filter = go.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || filter.sharedMesh == null) continue;

            var mesh = filter.sharedMesh;

            found.Add(new Flora.Sprout
            {
                Name = go.name,
                Mesh = mesh,
                Size = mesh.bounds.size.y,

                // whatever the model's own idea of its origin is, this is the
                // lift that puts the bottom of it on the ground
                Foot = -mesh.bounds.min.y
            });
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        return found.ToArray();
    }

    public static void Batch()
    {
        Go();
        EditorApplication.Exit(0);
    }
}
