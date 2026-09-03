using System.Collections.Generic;
using System.IO;
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

    private const string Sheet = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Texture.png";

    private static Texture2D palette;

    /// <summary>
    /// The pack's palette, loaded from the file itself rather than through the
    /// importer, so it can be read whatever the import settings say.
    /// </summary>
    private static Texture2D Palette()
    {
        if (palette != null) return palette;

        palette = new Texture2D(2, 2);
        palette.LoadImage(File.ReadAllBytes(Sheet));

        return palette;
    }

    /// <summary>The colour a model mostly is, taken off the palette.</summary>
    private static Color ColourOf(Mesh mesh)
    {
        var uv = mesh.uv;
        var tris = mesh.triangles;
        var verts = mesh.vertices;

        if (uv == null || uv.Length == 0) return Color.grey;

        double sumU = 0, sumV = 0, weight = 0;

        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];

            float area = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).magnitude * 0.5f;

            if (area < 1e-7f) continue;

            Vector2 middle = (uv[a] + uv[b] + uv[c]) / 3f;

            sumU += middle.x * area;
            sumV += middle.y * area;
            weight += area;
        }

        if (weight <= 0) return Color.grey;

        return Palette().GetPixelBilinear((float)(sumU / weight), (float)(sumV / weight));
    }

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

        // Snowy trees are made here rather than shipped: the same narrow models
        // with their green pointed at the white of the palette. Any made last
        // time are cleared out first, or they pile up inside the asset.
        if (!fresh)
        {
            foreach (var held in AssetDatabase.LoadAllAssetsAtPath(Written))
                if (held is Mesh old) Object.DestroyImmediate(old, true);
        }

        if (fresh) AssetDatabase.CreateAsset(flora, Written);

        var snowy = new List<Flora.Sprout>();

        foreach (var tree in flora.Trees)
        {
            if (tree.Mesh == null || tree.Wide > 1.0f) continue;      // the narrow ones

            var white = Whitened(tree.Mesh);

            if (white == null) continue;

            AssetDatabase.AddObjectToAsset(white, flora);

            var under = tree;
            under.Mesh = white;
            under.Name = tree.Name + " (snow)";

            snowy.Add(under);
        }

        flora.SnowTrees = snowy.ToArray();

        // Conifers, built here because the pack has none. Their corners are
        // pointed at the same sheet as everything else, so they draw in the
        // same batch as the models that came with it.
        var pines = new List<Flora.Sprout>();
        var snowPines = new List<Flora.Sprout>();

        for (int shape = 0; shape < 5; shape++)
        {
            foreach (bool underSnow in new[] { false, true })
            {
                var mesh = PineTrees.Build(shape, underSnow);

                AssetDatabase.AddObjectToAsset(mesh, flora);

                var one = new Flora.Sprout
                {
                    Name = mesh.name,
                    Mesh = mesh,
                    Size = mesh.bounds.size.y,
                    Wide = mesh.bounds.size.x,
                    Foot = -mesh.bounds.min.y,
                    Colour = underSnow ? Color.white : new Color(0.11f, 0.22f, 0.07f)
                };

                (underSnow ? snowPines : pines).Add(one);
            }
        }

        flora.Pines = pines.ToArray();
        flora.SnowPines = snowPines.ToArray();

        EditorUtility.SetDirty(flora);
        AssetDatabase.SaveAssets();

        Debug.Log("FLORA " + flora.Mushrooms.Length + " mushrooms, " + flora.Cacti.Length + " cactus, "
            + flora.Palms.Length + " palms, " + flora.Stones.Length + " sand stones, "
            + flora.Boulders.Length + " boulders, " + flora.Trees.Length + " trees, "
            + flora.DeadTrees.Length + " dead trees, " + flora.Reeds.Length + " reeds"
            + " | paint " + (flora.Paint == null ? "MISSING" : flora.Paint.name));

        int bright = 0;

        foreach (var one in flora.Boulders)
        {
            var c = one.Colour;
            float most = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float least = Mathf.Min(c.r, Mathf.Min(c.g, c.b));

            bool colourful = most - least > 0.16f;

            if (colourful) bright++;

            Debug.Log("FLORA stone " + one.Name + " #" + ColorUtility.ToHtmlStringRGB(c)
                + (colourful ? "  BRIGHT" : ""));
        }

        Debug.Log("FLORA of " + flora.Boulders.Length + " stones, " + bright + " are bright rather than stone-coloured");
        Debug.Log("FLORA " + flora.SnowTrees.Length + " trees turned to snow, "
            + flora.Pines.Length + " pines built and " + flora.SnowPines.Length + " of them snowed on"
            + (flora.Pines.Length > 0
                ? " | a pine is " + flora.Pines[0].Size.ToString("F2") + " tall and "
                  + flora.Pines[0].Mesh.vertexCount + " corners"
                : ""));
    }

    /// <summary>
    /// The same model with the green taken out of it.
    ///
    /// Colour in this pack is not painted on, it is pointed at: every model
    /// samples one shared palette, and what a face looks like is decided by
    /// where its corners land on that sheet. So a tree is turned to snow by
    /// moving the corners that land on a green to the white instead. The trunk
    /// is left where it is, because trunks are not white.
    /// </summary>
    private static Mesh Whitened(Mesh from)
    {
        var uv = from.uv;

        if (uv == null || uv.Length == 0) return null;

        // well inside the white of the sheet, clear of every swatch edge
        var white = new Vector2(0.52f, 0.92f);

        var moved = new Vector2[uv.Length];
        int changed = 0;

        for (int i = 0; i < uv.Length; i++)
        {
            Color c = Palette().GetPixelBilinear(uv[i].x, uv[i].y);

            bool green = c.g > c.r + 0.05f && c.g > c.b + 0.05f;

            moved[i] = green ? white : uv[i];

            if (green) changed++;
        }

        if (changed == 0) return null;

        var mesh = Object.Instantiate(from);
        mesh.name = from.name + " Snow";
        mesh.uv = moved;

        return mesh;
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
                Wide = mesh.bounds.size.x,

                // whatever the model's own idea of its origin is, this is the
                // lift that puts the bottom of it on the ground
                Foot = -mesh.bounds.min.y,
                Colour = ColourOf(mesh)
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
