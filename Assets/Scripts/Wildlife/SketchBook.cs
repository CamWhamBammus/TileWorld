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

    /// <summary>A drawing, and what is worth remembering about the making of it.</summary>
    public class Page
    {
        public Texture2D Paper;
        public float Quality;        // nought to one
        public string Verdict;
        public string When;
        public string Where;
        public bool Empty;
        public float Covered;        // the share of the page the subject took
    }

    private static readonly Dictionary<string, Page> drawings = new Dictionary<string, Page>();

    private static readonly Color Paper = new Color(0.902f, 0.855f, 0.749f);
    private static readonly Color Ink = new Color(0.204f, 0.161f, 0.114f);

    /// <summary>
    /// The drawing under a key. A structure's key is the subject's; a
    /// creature's is the subject's and the plate's, so a deer has a drawing
    /// standing and another lying down.
    /// </summary>
    public static Page Made(string key)
    {
        return drawings.TryGetValue(key, out var found) ? found : null;
    }

    public static Page Made(Subject subject) => subject.Wild ? Best(subject) : Made(subject.Key);

    public static Texture2D Of(string key)
    {
        var page = Made(key);
        return page != null ? page.Paper : null;
    }

    public static Texture2D Of(Subject subject)
    {
        var page = Made(subject);
        return page != null ? page.Paper : null;
    }

    public static bool Has(Subject subject) => Of(subject) != null;

    /// <summary>A creature's best drawing across its plates, for the page and the contents.</summary>
    public static Page Best(Subject subject)
    {
        Page best = null;
        foreach (var plate in Plates.For(subject.Fauna))
        {
            var page = Made(Plates.Key(subject, plate.Id));
            if (page != null && (best == null || page.Quality > best.Quality)) best = page;
        }
        return best;
    }

    /// <summary>Every drawing on the shelf, for writing the save out.</summary>
    public static IEnumerable<KeyValuePair<string, Page>> Shelf => drawings;

    /// <summary>What was thought of a drawing, put back after it is read in.</summary>
    public static void Remember(string key, float quality, string verdict, string when)
    {
        if (!drawings.TryGetValue(key, out var page)) return;

        page.Quality = quality;
        page.Verdict = verdict;
        page.When = when;
    }

    /// <summary>
    /// Draws the animal. It is rendered on its own against nothing, so the
    /// hillside behind it does not end up in the drawing, then reduced to a
    /// line where its edges are and a wash where it is dark.
    /// </summary>
    public static Page Draw(Subject subject, Transform what, Camera eye) => Draw(subject, subject.Key, what, eye);

    /// <summary>
    /// A drawing already made, kept under another key as well: one drawing
    /// of a deer lying down in a herd fills two plates.
    /// </summary>
    public static bool Keep(string key, Page page)
    {
        if (page == null || page.Empty || page.Paper == null) return false;
        var standing = Made(key);
        if (standing != null && standing.Quality >= page.Quality) return false;
        var copy = new Page { Paper = page.Paper, Quality = page.Quality, Verdict = page.Verdict, When = page.When, Where = page.Where, Covered = page.Covered };
        drawings[key] = copy;
        Write(key, page.Paper);
        return true;
    }

    public static Page Draw(Subject subject, string key, Transform what, Camera eye)
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

        // The page takes what you were looking at, from where you were looking
        // at it. Framing it for the player made every drawing the same drawing:
        // it is worth having only if standing in the right place was your doing.
        studio.transform.SetPositionAndRotation(eye.transform.position, eye.transform.rotation);
        studio.fieldOfView = eye.fieldOfView;

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

        var judged = Judge(subject, shot, what, eye);
        var drawing = ToInk(shot);

        Object.Destroy(shot);
        Restored(key, drawing, judged);

        return judged;
    }

    /// <summary>
    /// A worse drawing never replaces a better one. Going back to a subject you
    /// made a poor job of is the whole reason the book says how good it was.
    /// </summary>
    private static void Restored(string key, Texture2D drawing, Page judged)
    {
        var standing = Made(key);

        if (judged.Empty)
        {
            beaten = true;
            Object.Destroy(drawing);
            return;
        }

        judged.Paper = drawing;
        judged.When = TimeOfDay.Instance != null ? TimeOfDay.Instance.Clock() : "";

        if (standing != null && standing.Quality >= judged.Quality)
        {
            judged.Verdict = "worse than the one you have";
            beaten = true;

            Object.Destroy(drawing);
            return;
        }

        beaten = false;
        drawings[key] = judged;

        Write(key, drawing);
    }

    /// <summary>Whether the last drawing was thrown away for being the worse of the two.</summary>
    public static bool Beaten => beaten;

    private static bool beaten;

    /// <summary>
    /// What the drawing is worth. How much of the paper it fills, whether it
    /// sits on the page or runs off the edge of it, and whether you caught the
    /// side of the animal or the back end of it going away.
    /// </summary>
    private static Page Judge(Subject subject, Texture2D shot, Transform what, Camera eye)
    {
        var pixels = shot.GetPixels();

        int covered = 0, minX = Width, maxX = 0, minY = Height, maxY = 0;
        long sumX = 0, sumY = 0;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (pixels[y * Width + x].a < 0.5f) continue;

            covered++;
            sumX += x;
            sumY += y;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        var page = new Page();

        page.Covered = covered / (float)(Width * Height);

        if (covered < Width * Height * 0.0008f)
        {
            page.Quality = 0f;
            page.Verdict = "nothing on the page";
            page.Empty = true;
            return page;
        }

        float fill = covered / (float)(Width * Height);

        // How much paper a thing covers depends on how big it is and how near
        // it will let you come, and those differ by kind: a rabbit at the four
        // paces it allows covers a fraction of what a deer does at eight. Judged
        // against a fixed figure, a rabbit could never be drawn well however
        // carefully you went about it, so each is judged against what is
        // actually possible for it.
        float wanted = Filling(subject);

        float size = Mathf.Clamp01(Mathf.InverseLerp(wanted * 0.28f, wanted, fill))
                   * Mathf.Clamp01(Mathf.InverseLerp(wanted * 5f, wanted * 2.4f, fill));

        bool cut = minX <= 1 || minY <= 1 || maxX >= Width - 2 || maxY >= Height - 2;

        float driftX = Mathf.Abs((sumX / (float)covered) / Width - 0.5f) * 2f;
        float driftY = Mathf.Abs((sumY / (float)covered) / Height - 0.5f) * 2f;
        float centred = 1f - Mathf.Clamp01(Mathf.Max(driftX, driftY));

        // side on tells you what a thing is; head on or going away does not
        Vector3 facing = what.forward;
        facing.y = 0f;
        Vector3 view = eye.transform.forward;
        view.y = 0f;

        float side = facing.sqrMagnitude > 0.001f && view.sqrMagnitude > 0.001f
            ? 1f - Mathf.Abs(Vector3.Dot(facing.normalized, view.normalized))
            : 0.5f;

        // Size multiplies rather than adds: a well composed speck is still a
        // speck, and was scoring half marks when it counted for a share of the
        // total instead of against all of it.
        float judgement = 0.4f + centred * 0.25f + side * 0.35f;

        float quality = judgement * (0.25f + 0.75f * size);

        // Losing a leg off the edge spoils a drawing however well judged the
        // rest of it is, so it costs a good deal rather than a tenth.
        if (cut) quality *= 0.68f;

        page.Quality = Mathf.Clamp01(quality);

        if (cut) page.Verdict = "cut off at the edge";
        else if (fill < wanted * 0.35f) page.Verdict = "too small";
        else if (fill > wanted * 3.5f) page.Verdict = "too close";
        else if (side < 0.3f) page.Verdict = "seen end on";
        else if (page.Quality > 0.78f) page.Verdict = "a good drawing";
        else if (page.Quality > 0.55f) page.Verdict = "a decent drawing";
        else page.Verdict = "a rough drawing";

        return page;
    }

    /// <summary>
    /// The share of the page a well drawn one of these would cover: what it
    /// subtends at the closest it will let you stand. Structures do not run
    /// away, so they are simply asked to fill a good part of the sheet.
    /// </summary>
    public static float Filling(Subject subject)
    {
        // Measured rather than hoped for: a goat at six paces through a thirty
        // degree glass covers three and a half percent of the page, and six
        // paces is about as near as it will have you. Asking for a tenth of the
        // sheet meant every honest drawing was marked down as a small thing at
        // a distance, and looked like an empty page besides.
        return 0.05f;
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

    private static void Write(string key, Texture2D drawing)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Folder, key + ".png"),
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

            // every drawing in the folder, whatever its key; one from before
            // the plates, "c3.png", is the creature's standing plate
            foreach (string path in System.IO.Directory.GetFiles(Folder, "*.png"))
            {
                string key = System.IO.Path.GetFileNameWithoutExtension(path);
                if (key.StartsWith("c") && !key.Contains("-")) key += "-standing";

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (texture.LoadImage(System.IO.File.ReadAllBytes(path)))
                {
                    drawings[key] = new Page { Paper = texture, Quality = 0.5f, Verdict = "" };
                }
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
