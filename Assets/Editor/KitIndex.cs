using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds the building kit's colours on the pack's palette and writes down
/// where they are. Each swatch is the nearest colour the sheet has to the
/// one asked for; the sheet is read from the file so import settings do not
/// matter.
/// </summary>
public static class KitIndex
{
    private const string Sheet = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Texture.png";
    private const string Written = "Assets/Resources/Kit.asset";

    [MenuItem("Tools/Tile World/Index the building kit's colours")]
    public static void Go()
    {
        var palette = new Texture2D(2, 2);
        palette.LoadImage(File.ReadAllBytes(Sheet));

        var asset = AssetDatabase.LoadAssetAtPath<Kit>(Written);
        bool fresh = asset == null;
        if (fresh) asset = ScriptableObject.CreateInstance<Kit>();

        // what each swatch is meant to look like; the sheet decides what it gets
        var wanted = new (Kit.Swatch swatch, Color colour)[]
        {
            (Kit.Swatch.Wood,      new Color(0.56f, 0.40f, 0.21f)),
            (Kit.Swatch.DarkWood,  new Color(0.31f, 0.20f, 0.11f)),
            (Kit.Swatch.Plank,     new Color(0.70f, 0.52f, 0.28f)),
            (Kit.Swatch.EndGrain,  new Color(0.82f, 0.70f, 0.50f)),
            (Kit.Swatch.Stone,     new Color(0.62f, 0.62f, 0.60f)),
            (Kit.Swatch.DarkStone, new Color(0.44f, 0.44f, 0.43f)),
            (Kit.Swatch.Mortar,    new Color(0.30f, 0.30f, 0.29f)),
            (Kit.Swatch.Plaster,   new Color(0.94f, 0.83f, 0.70f)),
            (Kit.Swatch.Thatch,    new Color(0.80f, 0.70f, 0.48f)),
            (Kit.Swatch.Slate,     new Color(0.27f, 0.27f, 0.30f)),
            (Kit.Swatch.Iron,      new Color(0.13f, 0.13f, 0.14f)),
            (Kit.Swatch.Pane,      new Color(0.10f, 0.14f, 0.18f)),
            (Kit.Swatch.Cloth,     new Color(0.62f, 0.16f, 0.14f)),
            (Kit.Swatch.WarmStone, new Color(0.60f, 0.56f, 0.50f)),
            (Kit.Swatch.Water,     new Color(0.25f, 0.45f, 0.60f)),
            (Kit.Swatch.Earth,     new Color(0.32f, 0.26f, 0.19f)),
            (Kit.Swatch.Snow,      new Color(0.94f, 0.94f, 0.94f)),
            (Kit.Swatch.Moss,      new Color(0.19f, 0.31f, 0.19f)),
            (Kit.Swatch.Vine,      new Color(0.13f, 0.50f, 0.19f)),
            (Kit.Swatch.Sand,      new Color(0.82f, 0.70f, 0.56f)),
            (Kit.Swatch.Char,      new Color(0.09f, 0.09f, 0.09f)),
            (Kit.Swatch.OldWood,   new Color(0.44f, 0.38f, 0.30f)),
        };

        asset.Where = new Vector2[wanted.Length];
        var pixels = palette.GetPixels32();
        int w = palette.width;

        foreach (var (swatch, colour) in wanted)
        {
            float best = float.MaxValue; int at = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float dr = pixels[i].r / 255f - colour.r, dg = pixels[i].g / 255f - colour.g, db = pixels[i].b / 255f - colour.b;
                float off = dr * dr + dg * dg + db * db;
                if (off < best) { best = off; at = i; }
            }
            asset.Where[(int)swatch] = new Vector2((at % w + 0.5f) / w, (at / w + 0.5f) / palette.height);
            var got = pixels[at];
            Debug.Log(string.Format("KIT {0,-9} wanted #{1} got #{2:x2}{3:x2}{4:x2}", swatch, ColorUtility.ToHtmlStringRGB(colour).ToLower(), got.r, got.g, got.b));
        }

        if (fresh) AssetDatabase.CreateAsset(asset, Written);
        else EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("KIT written");
    }
}
