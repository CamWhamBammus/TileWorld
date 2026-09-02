using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The chart paper the map is drawn on, as a reusable texture so other screens
/// can sit on the same stock. Generated rather than authored: it is a flat
/// colour with grain and a double rule, which is cheaper to make than to store.
/// </summary>
public static class ParchmentPanel
{
    public static readonly Color Paper = new Color(0.902f, 0.855f, 0.749f);
    public static readonly Color PaperDark = new Color(0.847f, 0.788f, 0.671f);
    public static readonly Color Ink = new Color(0.286f, 0.227f, 0.169f);
    public static readonly Color InkFaint = new Color(0.545f, 0.470f, 0.373f);

    public static Texture2D Create(int width, int height) => Create(width, height, true);

    /// <summary>
    /// A sheet of paper, drawn at the size it will be shown at. Made smaller
    /// and stretched, the rule round the edge comes out fat and the corners
    /// come out blurred.
    /// </summary>
    public static Texture2D Create(int width, int height, bool ornate)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[width * height];

        // A few old marks on the paper, in the same places every time it is
        // made: paper that is perfectly clean does not look like paper.
        var stains = new Vector3[5];

        for (int i = 0; i < stains.Length; i++)
        {
            int h = (i * 73856093) ^ (width * 19349663) ^ (height * 83492791);

            stains[i] = new Vector3(
                Mathf.Abs((h >> 3) % 1000) / 1000f * width,
                Mathf.Abs((h >> 13) % 1000) / 1000f * height,
                Mathf.Lerp(width * 0.05f, width * 0.16f, Mathf.Abs((h >> 23) % 1000) / 1000f));
        }

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int h = (x * 73856093) ^ (y * 19349663);
            float grain = ((h >> 8) & 0xFF) / 255f;

            var colour = Color.Lerp(Paper, PaperDark, grain * 0.16f);

            // darker towards the edges, the way a sheet handled at the corners
            // goes over the years
            float toEdge = Mathf.Min(Mathf.Min(x, width - 1 - x) / (float)width,
                                     Mathf.Min(y, height - 1 - y) / (float)height);

            colour = Color.Lerp(colour, PaperDark, Mathf.Clamp01(1f - toEdge * 6f) * 0.22f);

            foreach (var stain in stains)
            {
                float away = Vector2.Distance(new Vector2(x, y), new Vector2(stain.x, stain.y));

                if (away > stain.z) continue;

                colour = Color.Lerp(colour, PaperDark, (1f - away / stain.z) * 0.10f);
            }

            px[y * width + x] = colour;
        }

        Rule(px, width, height, 6, 2, Ink);
        Rule(px, width, height, 13, 1, InkFaint);

        if (ornate) Corners(px, width, height);

        tex.SetPixels32(px);
        tex.Apply(false);

        return tex;
    }

    /// <summary>
    /// A short stroke in from each corner, where somebody ruling a sheet by
    /// hand would run the lines past one another.
    /// </summary>
    private static void Corners(Color32[] px, int width, int height)
    {
        int reach = Mathf.Clamp(Mathf.Min(width, height) / 7, 6, 26);

        for (int i = 0; i < reach; i++)
        {
            Ink32(px, width, height, 19 + i, 19, i > reach - 4);
            Ink32(px, width, height, 19, 19 + i, i > reach - 4);

            Ink32(px, width, height, width - 20 - i, 19, i > reach - 4);
            Ink32(px, width, height, width - 20, 19 + i, i > reach - 4);

            Ink32(px, width, height, 19 + i, height - 20, i > reach - 4);
            Ink32(px, width, height, 19, height - 20 - i, i > reach - 4);

            Ink32(px, width, height, width - 20 - i, height - 20, i > reach - 4);
            Ink32(px, width, height, width - 20, height - 20 - i, i > reach - 4);
        }
    }

    private static void Ink32(Color32[] px, int width, int height, int x, int y, bool fading)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;

        Color32 was = px[y * width + x];

        px[y * width + x] = Color32.Lerp(was, InkFaint, fading ? 0.35f : 0.85f);
    }

    /// <summary>Lays a shadow under a sheet, behind it in the drawing order.</summary>
    public static GameObject Shade(RectTransform card, float spread = 34f)
    {
        var go = new GameObject("Shadow");
        go.transform.SetParent(card.parent, false);
        go.transform.SetSiblingIndex(card.GetSiblingIndex());

        var image = go.AddComponent<RawImage>();
        image.texture = Shadow(128, 128);
        image.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = card.anchorMin;
        rect.anchorMax = card.anchorMax;
        rect.pivot = card.pivot;
        rect.sizeDelta = card.sizeDelta + new Vector2(spread, spread);
        rect.anchoredPosition = card.anchoredPosition + new Vector2(0f, -spread * 0.22f);

        return go;
    }

    /// <summary>
    /// A shadow to sit a sheet of paper on, so it lies over the world rather
    /// than against it. Soft at the edges, since nothing casts a hard one.
    /// </summary>
    public static Texture2D Shadow(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[width * height];
        float feather = Mathf.Min(width, height) * 0.22f;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float toEdge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
            float strength = Mathf.Clamp01(toEdge / feather);

            px[y * width + x] = new Color(0f, 0f, 0f, strength * strength * 0.42f);
        }

        tex.SetPixels32(px);
        tex.Apply(false);

        return tex;
    }

    private static void Rule(Color32[] px, int width, int height, int inset, int thickness, Color colour)
    {
        Color32 c = colour;

        for (int t = 0; t < thickness; t++)
        {
            int a = inset + t;

            for (int x = a; x < width - a; x++)
            {
                px[a * width + x] = c;
                px[(height - 1 - a) * width + x] = c;
            }

            for (int y = a; y < height - a; y++)
            {
                px[y * width + a] = c;
                px[y * width + (width - 1 - a)] = c;
            }
        }
    }
}
