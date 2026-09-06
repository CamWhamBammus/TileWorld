using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the six panorama faces the probe wrote into a cubemap and a skybox
/// material for the title to turn through.
/// </summary>
public static class MakePanorama
{
    [MenuItem("Tools/Tile World/Bake the title panorama")]
    public static void Go()
    {
        // Views captured in play wait beside the saves; brought in, they
        // replace whatever is baked now.
        string captured = System.IO.Path.Combine(Application.persistentDataPath, "title-views");
        if (System.IO.Directory.Exists(captured))
        {
            var faces0 = System.IO.Directory.GetFiles(captured, "*-0.png");
            if (faces0.Length > 0)
            {
                foreach (string old in System.IO.Directory.GetFiles("Assets/Resources/Title"))
                    if (System.IO.Path.GetFileName(old).StartsWith("pano_") || System.IO.Path.GetFileName(old).StartsWith("View-"))
                        AssetDatabase.DeleteAsset(old.Replace("\\", "/"));
                var order = new System.Collections.Generic.List<string>();
                foreach (string f0 in faces0)
                {
                    string name = System.IO.Path.GetFileName(f0); name = name.Substring(0, name.Length - 6);
                    order.Add(name);
                }
                order.Sort((a, b) => System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(captured, a + "-0.png")).CompareTo(System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(captured, b + "-0.png"))));
                for (int i = 0; i < order.Count; i++)
                    for (int f = 0; f < 6; f++)
                    {
                        string src = System.IO.Path.Combine(captured, order[i] + "-" + f + ".png");
                        var bytes = System.IO.File.ReadAllBytes(src);
                        // faces come out of a 2D render upright; the cubemap wants them the other way up
                        var tex = new Texture2D(2, 2); tex.LoadImage(bytes);
                        var flipped = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
                        var px = tex.GetPixels32();
                        var outPx = new Color32[px.Length];
                        for (int y = 0; y < tex.height; y++) System.Array.Copy(px, y * tex.width, outPx, (tex.height - 1 - y) * tex.width, tex.width);
                        flipped.SetPixels32(outPx); flipped.Apply();
                        System.IO.File.WriteAllBytes("Assets/Resources/Title/pano_" + (i + 1) + "-" + order[i] + "_" + f + ".jpg", flipped.EncodeToJPG(92));
                    }
                AssetDatabase.Refresh();
                Debug.Log("PANO brought in " + order.Count + " captured view(s): " + string.Join(", ", order));
            }
        }

        // whatever views are here, by the first face of each
        var names = new System.Collections.Generic.List<string>();
        foreach (string f in System.IO.Directory.GetFiles("Assets/Resources/Title", "pano_*_0.jpg"))
        {
            string n = System.IO.Path.GetFileName(f);
            names.Add(n.Substring(5, n.Length - 5 - 6));
        }
        names.Sort();
        foreach (string name in names) Bake(name);
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static void Bake(string name)
    {
        var faces = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            string path = "Assets/Resources/Title/pano_" + name + "_" + i + ".jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.Log("PANO no faces for " + name); return; }
            if (!importer.isReadable || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize < 1024)
            {
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            faces[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        int size = faces[0].width;
        var cube = new Cubemap(size, TextureFormat.RGB24, false);
        for (int i = 0; i < 6; i++) cube.SetPixels(faces[i].GetPixels(), (CubemapFace)i);
        cube.Apply(false, false);
        // compressed, or a 1024 cubemap is 36 MB on disk and a 2048 one 144
        EditorUtility.CompressCubemapTexture(cube, TextureFormat.DXT1, TextureCompressionQuality.Best);
        AssetDatabase.CreateAsset(cube, "Assets/Resources/Title/View-" + name + ".cubemap");

        var mat = new Material(Shader.Find("Skybox/Cubemap"));
        mat.SetTexture("_Tex", cube);
        // snow at noon blows out; the night is drawn a touch up, so it reads behind the paper
        mat.SetFloat("_Exposure", name.Contains("noon") ? 0.80f : name.Contains("night") ? 1.25f : 1.0f);
        AssetDatabase.CreateAsset(mat, "Assets/Resources/Title/View-" + name + ".mat");
        Debug.Log("PANO baked " + name + " at " + size);
    }
}
