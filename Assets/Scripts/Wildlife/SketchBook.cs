using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The drawings themselves.
///
/// When you finish a sketch the animal is photographed as it stands — from
/// where you were, at the angle you saw it, in the pose it happened to be in —
/// and that picture is worked over into ink and wash on paper. So the deer in
/// your guide is the deer you actually stood in front of, and someone else's
/// guide has a different one.
///
/// They are written out beside the world they were drawn in, so a world you
/// come back to still has its drawings.
/// </summary>
public static class SketchBook
{
    public const int Width = 320;
    public const int Height = 240;

    private static readonly Dictionary<string, Texture2D> drawings =
        new Dictionary<string, Texture2D>();

    private static readonly Color Paper = new Color(0.902f, 0.855f, 0.749f);
    private static readonly Color Ink = new Color(0.204f, 0.161f, 0.114f);

    public static Texture2D Of(Subject subject)
    {
        return drawings.TryGetValue(subject.Key, out var found) ? found : null;
    }

    public static bool Has(Subject subject) => Of(subject) != null;

    /// <summary>
    /// Draws the animal. It is rendered on its own against nothing, so the
    /// hillside behind it does not end up in the drawing, then reduced to a
    /// line where its edges are and a wash where it is dark.
    /// </summary>
    public static Texture2D Draw(Subject subject, Transform what, Camera eye)
    {
        if (what == null || eye == null) return null;

        int layer = SpareLayer();
        var was = new Dictionary<Transform, int>();

        Hide(what, layer, was);

        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };

        var studioGo = new GameObject("sketching camera");
        var studio = studioGo.AddComponent<Camera>();

        studio.CopyFrom(eye);
        studio.targetTexture = target;
        studio.cullingMask = 1 << layer;
        studio.clearFlags = CameraClearFlags.SolidColor;
        studio.backgroundColor = new Color(0f, 0f, 0f, 0f);

        // Frame the whole of it, from where the player was standing. Aiming at
        // the head and guessing a width put half the animal off the page, and
        // a long one like the fox lost its legs.
        var bounds = new Bounds(what.position, Vector3.one * 0.1f);

        foreach (var piece in what.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(piece.bounds);

        Vector3 centre = bounds.center;
        float away = Vector3.Distance(eye.transform.position, centre);
        float radius = bounds.extents.magnitude * 1.12f;      // a margin of paper round it

        studio.transform.position = eye.transform.position;
        studio.transform.rotation = Quaternion.LookRotation(centre - eye.transform.position);
        studio.fieldOfView = Mathf.Clamp(2f * Mathf.Atan2(radius, Mathf.Max(0.5f, away)) * Mathf.Rad2Deg, 4f, 70f);

        studio.Render();

        var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;

        RenderTexture.active = target;
        shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        shot.Apply(false);
        RenderTexture.active = previous;

        studio.targetTexture = null;
        Object.Destroy(studioGo);
        target.Release();
        Object.Destroy(target);

        Restore(was);

        var drawing = ToInk(shot);
        Object.Destroy(shot);

        drawings[subject.Key] = drawing;

        Write(subject, drawing);

        return drawing;
    }

    /// <summary>Ink where the edges are, a wash where the animal is dark, paper elsewhere.</summary>
    private static Texture2D ToInk(Texture2D shot)
    {
        var source = shot.GetPixels();
        var page = new Color[Width * Height];

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = y * Width + x;

            // paper first, with the same grain the map is drawn on
            int h = (x * 73856093) ^ (y * 19349663);
            float grain = ((h >> 8) & 0xFF) / 255f;
            var colour = Color.Lerp(Paper, Paper * 0.94f, grain * 0.5f);

            float here = source[i].a;

            if (here > 0.02f)
            {
                // Two kinds of line: the outside of the animal, where the page
                // stops being animal, and the creases inside it. The animals
                // are flat shaded, so every facet meets its neighbour at a step
                // in brightness, and those steps are the drawing's interior.
                float edge = 0f;
                float crease = 0f;

                Line(source, x, y, i, ref edge, ref crease);

                float shade = 1f - Mathf.Clamp01(source[i].grayscale * 1.5f);

                if (edge > 0.4f)
                {
                    colour = Color.Lerp(colour, Ink, 0.95f);
                }
                else if (crease > 0.055f)
                {
                    colour = Color.Lerp(colour, Ink, Mathf.Clamp01(crease * 7f));
                }
                else if (shade > 0.35f)
                {
                    // hatching rather than a flat fill, so it reads as drawn
                    bool hatch = ((x + y) % (shade > 0.62f ? 3 : 5)) == 0;

                    if (hatch) colour = Color.Lerp(colour, Ink, shade * 0.55f);
                    else colour = Color.Lerp(colour, Ink, shade * 0.14f);
                }
                else
                {
                    colour = Color.Lerp(colour, Ink, 0.06f);
                }
            }

            page[i] = colour;
        }

        var drawing = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        drawing.SetPixels(page);
        drawing.Apply(false);

        return drawing;
    }

    /// <summary>
    /// The strongest change against the four neighbours: in coverage, which
    /// gives the outline, and in brightness where both are on the animal,
    /// which gives the creases.
    /// </summary>
    private static void Line(Color[] source, int x, int y, int i, ref float edge, ref float crease)
    {
        Compare(source, i, x + 1, y, ref edge, ref crease);
        Compare(source, i, x - 1, y, ref edge, ref crease);
        Compare(source, i, x, y + 1, ref edge, ref crease);
        Compare(source, i, x, y - 1, ref edge, ref crease);
    }

    private static void Compare(Color[] source, int i, int x, int y, ref float edge, ref float crease)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            edge = Mathf.Max(edge, source[i].a);
            return;
        }

        var them = source[y * Width + x];
        var us = source[i];

        edge = Mathf.Max(edge, Mathf.Abs(us.a - them.a));

        if (them.a > 0.5f && us.a > 0.5f)
        {
            crease = Mathf.Max(crease, Mathf.Abs(us.grayscale - them.grayscale));
        }
    }

    // --------------------------------------------------------------- the shelf

    private static string Folder
    {
        get
        {
            string id = WorldLibrary.HasCurrent ? WorldLibrary.Current.id : "loose";

            return System.IO.Path.Combine(Application.persistentDataPath, "drawings", id);
        }
    }

    private static void Write(Subject subject, Texture2D drawing)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Folder, subject.Key + ".png"),
                                         drawing.EncodeToPNG());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Sketchbook] Could not keep the drawing: " + e.Message);
        }
    }

    /// <summary>Fetches the drawings made in the world being loaded.</summary>
    public static void Reopen()
    {
        drawings.Clear();

        try
        {
            if (!System.IO.Directory.Exists(Folder)) return;

            foreach (var subject in Subject.All())
            {
                string path = System.IO.Path.Combine(Folder, subject.Key + ".png");

                if (!System.IO.File.Exists(path)) continue;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (texture.LoadImage(System.IO.File.ReadAllBytes(path))) drawings[subject.Key] = texture;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Sketchbook] Could not read the drawings: " + e.Message);
        }
    }

    /// <summary>A layer nothing else is using, so the animal can be drawn alone.</summary>
    private static int SpareLayer()
    {
        for (int i = 31; i >= 8; i--)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        }

        return 31;
    }

    private static void Hide(Transform root, int layer, Dictionary<Transform, int> was)
    {
        was[root] = root.gameObject.layer;
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++) Hide(root.GetChild(i), layer, was);
    }

    private static void Restore(Dictionary<Transform, int> was)
    {
        foreach (var pair in was)
        {
            if (pair.Key != null) pair.Key.gameObject.layer = pair.Value;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        drawings.Clear();
    }
}
