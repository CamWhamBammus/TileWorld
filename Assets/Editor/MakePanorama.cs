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
        var faces = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            string path = "Assets/Resources/Title/pano_" + i + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (!importer.isReadable || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            faces[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (faces[i] == null) { Debug.LogError("PANO missing " + path); EditorApplication.Exit(1); return; }
        }

        int size = faces[0].width;
        var cube = new Cubemap(size, TextureFormat.RGB24, false);
        for (int i = 0; i < 6; i++) cube.SetPixels(faces[i].GetPixels(), (CubemapFace)i);
        cube.Apply(false, false);
        AssetDatabase.CreateAsset(cube, "Assets/Resources/Title/Panorama.cubemap");

        var shader = Shader.Find("Skybox/Cubemap");
        var mat = new Material(shader);
        mat.SetTexture("_Tex", cube);
        mat.SetFloat("_Exposure", 1.0f);
        AssetDatabase.CreateAsset(mat, "Assets/Resources/Title/Panorama.mat");
        AssetDatabase.SaveAssets();
        Debug.Log("PANO baked " + size + " with " + (shader != null ? shader.name : "no shader"));
        EditorApplication.Exit(0);
    }
}
