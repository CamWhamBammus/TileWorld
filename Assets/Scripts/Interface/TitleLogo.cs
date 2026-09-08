using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// The game's name in blocks, at the top of the title: the letters are built
/// from cubes the way the country is built from tiles, sand with a green
/// top, lit by the same sun as the beach under them. They stand far above
/// the world on a layer of their own and a second camera draws them, without
/// fog, into a texture that the title lays over the picture; so they are
/// crisp whatever the weather is doing, and they turn and bob a little.
/// </summary>
public class TitleLogo : MonoBehaviour
{
    public const int Layer = 30;

    private Camera cam;
    private RenderTexture picture;
    private Transform root;
    private RawImage image;
    private float t;
    private bool fogWas;

    // the fall: each block starts above its place and drops in, the columns one after another
    private class Falling { public Transform Block; public Vector3 Place; public float Delay, Drop; }
    private readonly List<Falling> falling = new List<Falling>();
    private int blocks, landed;

    // the blocks' own materials, so they can glow when there is no sun on them
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Color> colours = new List<Color>();

    /// <summary>How much of the word has landed, 0 to 1, for the probes.</summary>
    public float Landed => blocks > 0 ? landed / (float)blocks : 0f;

    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
    };

    /// <summary>Puts the name at the top of a canvas.</summary>
    public static TitleLogo Make(Transform canvas)
    {
        // the blocks and their camera live in the world, not under the canvas,
        // which the scaler would shrink them with
        var go = new GameObject("Title logo");
        var logo = go.AddComponent<TitleLogo>();
        logo.Build(canvas);
        return logo;
    }

    /// <summary>Whether the letters have been built and drawn into their picture.</summary>
    public bool Ready => picture != null && root != null && root.childCount > 0;

    private void Build(Transform canvas)
    {
        // ---- the blocks
        root = new GameObject("Title blocks").transform;
        root.SetParent(transform, false);
        root.position = new Vector3(0f, 3000f, 0f);

        var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = probe.GetComponent<MeshFilter>().sharedMesh;
        Destroy(probe);

        var top = Own(new Color(0.47f, 0.64f, 0.31f));
        var sands = new[]
        {
            Own(new Color(0.86f, 0.78f, 0.60f)),
            Own(new Color(0.83f, 0.74f, 0.56f)),
            Own(new Color(0.89f, 0.82f, 0.65f)),
        };

        const string Word = "TILE WORLD";
        int col = 0;
        var cells = new List<Vector2Int>();
        var topmost = new Dictionary<int, int>();

        foreach (char c in Word)
        {
            if (c == ' ') { col += 3; continue; }
            var glyph = Glyphs[c];
            for (int r = 0; r < 7; r++)
            for (int x = 0; x < 5; x++)
            {
                if (glyph[r][x] != '#') continue;
                int cx = col + x, cy = 6 - r;
                cells.Add(new Vector2Int(cx, cy));
                if (!topmost.TryGetValue(cx, out int best) || cy > best) topmost[cx] = cy;
            }
            col += 6;
        }

        int minX = int.MaxValue, maxX = int.MinValue;
        foreach (var cell in cells) { minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x); }
        float centre = (minX + maxX) * 0.5f;
        float halfWidth = (maxX - minX) * 0.5f + 0.5f;
        var rng = new System.Random(17);

        foreach (var cell in cells)
        {
            var block = new GameObject("Block");
            block.layer = Layer;
            block.transform.SetParent(root, false);
            var place = new Vector3(cell.x - centre, cell.y, 0f);
            float drop = 12f + (float)rng.NextDouble() * 5f;
            block.transform.localPosition = place + Vector3.up * drop;
            block.transform.localScale = Vector3.one * 0.94f;
            falling.Add(new Falling { Block = block.transform, Place = place, Delay = 0.3f + (cell.x - minX) * 0.022f + (float)rng.NextDouble() * 0.18f, Drop = drop });
            blocks++;
            block.AddComponent<MeshFilter>().sharedMesh = cube;
            var renderer = block.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = topmost[cell.x] == cell.y ? top : sands[rng.Next(sands.Length)];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---- the camera that draws them, into a picture with nothing behind it
        var camGo = new GameObject("Title logo camera");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << Layer;
        cam.fieldOfView = 15.6f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 400f;
        cam.depth = -5f;
        cam.allowHDR = false;
        // stood back far enough that the word fills the picture's width with a little to spare
        float aspect = 4f;
        float away = halfWidth * 1.16f / (Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect);
        cam.transform.position = root.position + new Vector3(0f, 3.1f + away * 0.04f, -away);
        cam.transform.LookAt(root.position + new Vector3(0f, 3.1f, 0f));
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;
        picture = new RenderTexture(1600, 400, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Title logo" };
        cam.targetTexture = picture;

        RenderPipelineManager.beginCameraRendering += NoFog;
        RenderPipelineManager.endCameraRendering += FogBack;

        // ---- where it goes on the page: a soft shadow, then the picture, top and centre
        var shadowGo = new GameObject("Logo shadow", typeof(RectTransform));
        shadowGo.transform.SetParent(canvas, false);
        var shadow = shadowGo.AddComponent<RawImage>();
        shadow.texture = ParchmentPanel.Shadow(64, 64);
        shadow.color = new Color(0f, 0f, 0f, 0.22f);
        shadow.raycastTarget = false;
        Top(shadowGo.GetComponent<RectTransform>(), new Vector2(0f, -160f), new Vector2(1400f, 400f));

        var imageGo = new GameObject("Logo picture", typeof(RectTransform));
        imageGo.transform.SetParent(canvas, false);
        image = imageGo.AddComponent<RawImage>();
        image.texture = picture;
        image.raycastTarget = false;
        Top(imageGo.GetComponent<RectTransform>(), new Vector2(0f, -160f), new Vector2(1240f, 310f));
    }

    private Material Own(Color colour)
    {
        var m = new Material(Paint.Flat(colour));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        materials.Add(m); colours.Add(colour);
        return m;
    }

    /// <summary>How much sun there is on the letters: with none, they hold a little light of their own so the name still reads.</summary>
    public void SetDaylight(float daylight)
    {
        float glow = (1f - Mathf.Clamp01(daylight)) * 0.5f;
        for (int i = 0; i < materials.Count; i++) materials[i].SetColor("_EmissionColor", colours[i] * glow);
    }

    private static void Top(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = at;
    }

    private void NoFog(ScriptableRenderContext context, Camera which)
    {
        if (which != cam) return;
        fogWas = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    private void FogBack(ScriptableRenderContext context, Camera which)
    {
        if (which != cam) return;
        RenderSettings.fog = fogWas;
    }

    private void Update()
    {
        if (root == null) return;
        t += Time.unscaledDeltaTime;
        root.localRotation = Quaternion.Euler(-7f + Mathf.Sin(t * 0.5f) * 1.5f, Mathf.Sin(t * 0.37f) * 2.5f, 0f);
        root.position = new Vector3(0f, 3000f + Mathf.Sin(t * 0.8f) * 0.18f, 0f);

        if (landed < blocks)
        {
            landed = 0;
            foreach (var f in falling)
            {
                float u = Mathf.Clamp01((t - f.Delay) / 0.75f);
                float eased = 1f - (1f - u) * (1f - u) * (1f - u);
                f.Block.localPosition = f.Place + Vector3.up * f.Drop * (1f - eased);
                if (u >= 1f) landed++;
            }
        }
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= NoFog;
        RenderPipelineManager.endCameraRendering -= FogBack;
        if (picture != null) { picture.Release(); Destroy(picture); }
        foreach (var m in materials) Destroy(m);
    }
}
