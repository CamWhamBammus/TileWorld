using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Captures a view for the title from wherever the camera is: six faces,
/// each rendered with the camera turned that way and a frame allowed to
/// pass first, since the world only submits what the camera can see. The
/// world is drawn further than it ever is in play and the fog is held to the
/// sky's colour at the horizon, so the drawn edge fades into sky rather than
/// standing against it. The faces go to a folder beside the saves; the
/// editor's "Bake the title panorama" takes them from there.
/// </summary>
public static class PanoramaCapture
{
    public static string Folder => Path.Combine(Application.persistentDataPath, "title-views");

    public static IEnumerator Capture(Camera cam, string name, System.Action<string> report)
    {
        var world = Object.FindFirstObjectByType<ChunkManager>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // draw further, for the shot alone
        int radiusBefore = world != null ? world.ViewRadius : 4;
        if (world != null)
        {
            typeof(ChunkManager).GetField("viewRadius", flags).SetValue(world, 11);
            typeof(ChunkManager).GetMethod("RefreshVisibleChunks", flags).Invoke(world, new object[] { true });
        }
        report?.Invoke("Dev: drawing the country out to the horizon...");
        yield return new WaitForSecondsRealtime(12f);

        var hidden = new System.Collections.Generic.List<Behaviour>();
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.enabled) { c.enabled = false; hidden.Add(c); }
        var renderers = new System.Collections.Generic.List<Renderer>();
        var player = world != null ? world.PlayerTransform : null;
        if (player != null) foreach (var r in player.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; renderers.Add(r); }

        var keepPos = cam.transform.position; var keepRot = cam.transform.rotation;
        float keepFov = cam.fieldOfView, keepFar = cam.farClipPlane;
        var follow = cam.GetComponent("SimpleFollowCamera") as Behaviour;
        bool followWas = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;

        var tod = TimeOfDay.Instance;
        float fogStart = 0f, fogEnd = 0f;
        bool wasPaused = tod != null && tod.Paused;
        if (tod != null)
        {
            tod.Paused = true;   // one sun for all six faces
            fogStart = (float)typeof(TimeOfDay).GetField("clearFogStart", flags).GetValue(tod);
            fogEnd = (float)typeof(TimeOfDay).GetField("clearFogEnd", flags).GetValue(tod);
            typeof(TimeOfDay).GetField("clearFogStart", flags).SetValue(tod, 50f);
            typeof(TimeOfDay).GetField("clearFogEnd", flags).SetValue(tod, 200f);
        }
        cam.farClipPlane = 240f;
        cam.fieldOfView = 90f;
        cam.aspect = 1f;
        yield return null;

        // the sky's colour at the horizon, from a small look forward
        var small = new RenderTexture(256, 256, 24); small.Create();
        var peek = new Texture2D(256, 256, TextureFormat.RGB24, false);
        Color sky = Color.gray; float best = -1f;
        foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
        {
            cam.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return null;
            cam.targetTexture = small; cam.Render();
            RenderTexture.active = small; peek.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); peek.Apply();
            RenderTexture.active = null; cam.targetTexture = null;
            foreach (int row in new[] { 128 - 14, 128 + 14 })
            {
                Color sum = Color.black; int n = 0;
                for (int x = 16; x < 240; x += 4) { sum += peek.GetPixel(x, row); n++; }
                var c = sum / n;
                if (c.grayscale > best) { best = c.grayscale; sky = c; }
            }
        }
        small.Release();
        if (tod != null) tod.LockFog(sky);
        yield return null; yield return null;

        Directory.CreateDirectory(Folder);
        var face = new RenderTexture(2048, 2048, 24); face.Create();
        var tex = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
        var looks = new[]
        {
            Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, -90f, 0f),
            Quaternion.Euler(-90f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(0f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f),
        };
        for (int f = 0; f < 6; f++)
        {
            cam.transform.rotation = looks[f];
            yield return null; yield return null;
            cam.targetTexture = face; cam.Render();
            RenderTexture.active = face;
            tex.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Folder, name + "-" + f + ".png"), tex.EncodeToPNG());
        }
        face.Release();

        // everything back as it was
        if (tod != null)
        {
            tod.Paused = wasPaused;
            tod.UnlockFog();
            typeof(TimeOfDay).GetField("clearFogStart", flags).SetValue(tod, fogStart);
            typeof(TimeOfDay).GetField("clearFogEnd", flags).SetValue(tod, fogEnd);
        }
        cam.ResetAspect();
        cam.fieldOfView = keepFov; cam.farClipPlane = keepFar;
        cam.transform.position = keepPos; cam.transform.rotation = keepRot;
        if (follow != null) follow.enabled = followWas;
        foreach (var r in renderers) r.enabled = true;
        foreach (var c in hidden) c.enabled = true;
        if (world != null) world.SetViewRadius(radiusBefore);

        report?.Invoke("Dev: title view '" + name + "' captured. Bake it in the editor: Tools > Tile World > Bake the title panorama.");
    }

    /// <summary>How many views are captured and waiting.</summary>
    public static int Captured()
    {
        if (!Directory.Exists(Folder)) return 0;
        return Directory.GetFiles(Folder, "*-0.png").Length;
    }

    public static void Clear()
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
    }
}
