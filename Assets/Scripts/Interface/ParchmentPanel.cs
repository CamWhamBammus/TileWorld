using UnityEngine;

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

    public static Texture2D Create(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[width * height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int h = (x * 73856093) ^ (y * 19349663);
            float n = ((h >> 8) & 0xFF) / 255f;
            px[y * width + x] = Color.Lerp(Paper, PaperDark, n * 0.16f);
        }

        Rule(px, width, height, 6, 2, Ink);
        Rule(px, width, height, 13, 1, InkFaint);

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
