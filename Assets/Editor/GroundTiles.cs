using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the grounds the pack does not hand over ready-made and adds them to
/// the tile library: sand for the deserts, stone for the barrens.
///
/// The grass tiles come whole, with their grass and their trees baked in. Sand
/// is supplied in two pieces instead -- a plain block for the body and a
/// separate cap for the top of it -- so a tile has to be put together before
/// the world can use one. The body is narrower than the grid and the cap is
/// wider than it, which is the way round that matters: the cap is what you see.
/// </summary>
public static class GroundTiles
{
    private const string Sand = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Update 1 - Sand/Prefabs/Tiles";
    private const string Built = "Assets/Tiles";
    private const string Defs = "Assets/ScriptableObjects";
    private const string Library = "Assets/ScriptableObjects/TileLibrary.asset";

    /// <summary>The first block id the sand takes. Five grass bands own 0 to 24.</summary>
    public const int FirstId = 25;

    /// <summary>And the stone after it.</summary>
    public const int FirstStoneId = 30;

    public const int Variants = 5;

    private const string Tiles = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Prefabs/Tiles";

    /// <summary>How much wider than its own size a sand piece is laid.</summary>
    private const float Spread = 1.08f;

    [MenuItem("Tools/Tile World/Build the sand and stone tiles")]
    public static void Go()
    {
        if (!AssetDatabase.IsValidFolder(Built)) AssetDatabase.CreateFolder("Assets", "Tiles");

        // A body and a cap for each, taken far enough apart in the pack's own
        // numbering that the five do not look like the same tile twice.
        int[] bodies = { 0, 3, 6, 9, 12 };
        int[] caps = { 1, 5, 9, 14, 18 };

        var made = new List<TileDefinition>();

        for (int i = 0; i < Variants; i++)
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(
                Sand + "/Sand Main Part/Sand Main Part_" + bodies[i] + ".prefab");
            var cap = AssetDatabase.LoadAssetAtPath<GameObject>(
                Sand + "/Top Parts/Top Part_" + caps[i] + ".prefab");

            if (body == null || cap == null)
            {
                Debug.LogError("SAND missing a piece for variant " + i);
                continue;
            }

            string path = Built + "/Sand Tile " + i + ".prefab";

            var root = new GameObject("Sand Tile " + i);

            // Widened across, but not up. The sand body is 1.8 on a grid of 2
            // and the cap's outer edge falls away in a bevel, so laid at their
            // own size the tiles do not meet and the desert comes out as slabs
            // with channels between them. Height is left alone: a tile has to
            // stay exactly two tall or it no longer stacks with the grass.
            var wider = new Vector3(Spread, 1f, Spread);

            var underneath = (GameObject)PrefabUtility.InstantiatePrefab(body);
            underneath.transform.SetParent(root.transform, false);
            underneath.transform.localScale = wider;

            var over = (GameObject)PrefabUtility.InstantiatePrefab(cap);
            over.transform.SetParent(root.transform, false);
            over.transform.localScale = wider;

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

            Debug.Log("SAND tile " + id + " from body " + bodies[i] + " and cap " + caps[i]
                + " | mesh " + (def.MeshGetter() == null ? "none" : def.MeshGetter().vertexCount + " verts")
                + " | paint " + (def.MaterialGetter() == null ? "none" : def.MaterialGetter().name));
        }

        // The stone tiles come whole out of the pack, so unlike the sand they
        // need no assembling -- only a definition each and an id.
        //
        // Which five matters, though. Better than half of the hundred have
        // coloured crystals set into them, and a field of those reads as
        // something growing rather than as rock -- on a snowfield they looked
        // like mushrooms coming up through the snow. So they are measured
        // rather than picked by eye: how much of a tile's surface is a colour
        // rather than a grey, taken off the pack's own palette, and the plain
        // ones are the ones we lay.
        int[] stones = PlainStoneTiles();

        for (int i = 0; i < Variants; i++)
        {
            var whole = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/Stone Tiles/Stone Tile_" + stones[i] + ".prefab");

            if (whole == null) { Debug.LogError("SAND no Stone Tile_" + stones[i]); continue; }

            int id = FirstStoneId + i;

            var def = AssetDatabase.LoadAssetAtPath<TileDefinition>(Defs + "/T" + id + ".asset");
            bool fresh = def == null;

            if (fresh) def = ScriptableObject.CreateInstance<TileDefinition>();

            def.blockID = id;
            def.prefab = whole;
            def.BuildFromPrefab();

            if (fresh) AssetDatabase.CreateAsset(def, Defs + "/T" + id + ".asset");
            else EditorUtility.SetDirty(def);

            made.Add(def);

            Debug.Log("SAND stone tile " + id + " from " + whole.name
                + " | mesh " + (def.MeshGetter() == null ? "none" : def.MeshGetter().vertexCount + " verts"));
        }

        // and into the library, without disturbing what is already in it
        var library = AssetDatabase.LoadAssetAtPath<TileLibrary>(Library);

        if (library == null) { Debug.LogError("SAND no tile library"); return; }

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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("SAND " + made.Count + " sand tiles in the library, ids " + FirstId
            + " to " + (FirstId + Variants - 1));
    }

    /// <summary>The five plainest stone tiles, spread across what the pack offers.</summary>
    private static int[] PlainStoneTiles()
    {
        var palette = new Texture2D(2, 2);
        palette.LoadImage(File.ReadAllBytes(
            "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Texture.png"));

        var plain = new List<int>();

        for (int i = 0; i < 100; i++)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Tiles + "/Stone Tiles/Stone Tile_" + i + ".prefab");

            if (go == null) continue;

            double bright = 0, all = 0;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var verts = mesh.vertices;
                var uv = mesh.uv;
                var tris = mesh.triangles;

                if (uv == null || uv.Length == 0) continue;

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];

                    float area = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).magnitude * 0.5f;
                    if (area < 1e-7f) continue;

                    Vector2 middle = (uv[a] + uv[b] + uv[c]) / 3f;
                    Color col = palette.GetPixelBilinear(middle.x, middle.y);

                    float most = Mathf.Max(col.r, Mathf.Max(col.g, col.b));
                    float least = Mathf.Min(col.r, Mathf.Min(col.g, col.b));

                    all += area;
                    if (most - least > 0.18f) bright += area;
                }
            }

            if (all > 0 && bright / all < 0.002) plain.Add(i);
        }

        Debug.Log("SAND " + plain.Count + " stone tiles carry no colour at all");

        if (plain.Count < Variants) return new[] { 11, 12, 17, 18, 19 };

        // spread across them, so the five are not five of a kind
        var picked = new int[Variants];

        for (int i = 0; i < Variants; i++) picked[i] = plain[i * plain.Count / Variants];

        return picked;
    }

    public static void Batch()
    {
        Go();
        EditorApplication.Exit(0);
    }
}
