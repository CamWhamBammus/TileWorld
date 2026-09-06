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
        foreach (string name in new[] { "1-dawn", "2-morning", "3-noon", "4-dusk", "5-night" }) Bake(name);
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
