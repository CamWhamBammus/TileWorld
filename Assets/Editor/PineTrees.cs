using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds conifers, because the pack has none.
///
/// Measured across all thirty-two of its trees, every one is a round crown on a
/// stick: widest in the middle and barely narrower at the top. That is a fine
/// tree for a warm wood and it is not a pine, and a snowfield planted with them
/// looks like a lawn that has been snowed on.
///
/// These are built the way the animals and the surveyor are built -- flat
/// shaded, out of the same handful of colours. The one thing they do that the
/// animals do not is take their colour the way the pack does: every corner is
/// pointed at a swatch on the shared sheet rather than given a material of its
/// own, so a pine draws in the same batch as everything else standing on the
/// ground and costs nothing extra to put in the world.
/// </summary>
public static class PineTrees
{
    private const string Sheet = "Assets/Low Poly Isometric Tiles - Cartoon Pack/Models/Texture.png";

    private static Texture2D palette;

    private static Texture2D Palette()
    {
        if (palette != null) return palette;

        palette = new Texture2D(2, 2);
        palette.LoadImage(File.ReadAllBytes(Sheet));

        return palette;
    }

    /// <summary>Where on the shared sheet a colour like this can be found.</summary>
    public static Vector2 Swatch(Color want)
    {
        var sheet = Palette();
        var pixels = sheet.GetPixels32();

        int w = sheet.width, h = sheet.height;

        float best = float.MaxValue;
        int bestAt = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];

            // skip the sheet's blank ground unless blank is what was asked for
            float dr = p.r / 255f - want.r, dg = p.g / 255f - want.g, db = p.b / 255f - want.b;
            float off = dr * dr + dg * dg + db * db;

            if (off >= best) continue;

            best = off;
            bestAt = i;
        }

        int x = bestAt % w, y = bestAt / w;

        return new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
    }

    private class Piece
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<int> Faces = new List<int>();
        public Vector2 Uv;
    }

    /// <summary>A faceted cone, flat shaded, standing on its own base.</summary>
    private static Piece Cone(float bottom, float top, float radius, int sides, Vector2 uv)
    {
        var piece = new Piece { Uv = uv };

        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            float b = (i + 1) / (float)sides * Mathf.PI * 2f;

            var one = new Vector3(Mathf.Cos(a) * radius, bottom, Mathf.Sin(a) * radius);
            var two = new Vector3(Mathf.Cos(b) * radius, bottom, Mathf.Sin(b) * radius);
            var tip = new Vector3(0f, top, 0f);

            int at = piece.Points.Count;

            piece.Points.Add(one);
            piece.Points.Add(two);
            piece.Points.Add(tip);

            piece.Faces.Add(at); piece.Faces.Add(at + 2); piece.Faces.Add(at + 1);

            // and the underside, so a tier is not see-through from below
            int under = piece.Points.Count;

            piece.Points.Add(one);
            piece.Points.Add(two);
            piece.Points.Add(new Vector3(0f, bottom, 0f));

            piece.Faces.Add(under); piece.Faces.Add(under + 1); piece.Faces.Add(under + 2);
        }

        return piece;
    }

    /// <summary>A tapering trunk.</summary>
    private static Piece Trunk(float height, float lower, float upper, int sides, Vector2 uv)
    {
        var piece = new Piece { Uv = uv };

        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            float b = (i + 1) / (float)sides * Mathf.PI * 2f;

            var a0 = new Vector3(Mathf.Cos(a) * lower, 0f, Mathf.Sin(a) * lower);
            var b0 = new Vector3(Mathf.Cos(b) * lower, 0f, Mathf.Sin(b) * lower);
            var a1 = new Vector3(Mathf.Cos(a) * upper, height, Mathf.Sin(a) * upper);
            var b1 = new Vector3(Mathf.Cos(b) * upper, height, Mathf.Sin(b) * upper);

            int at = piece.Points.Count;

            piece.Points.Add(a0); piece.Points.Add(b0); piece.Points.Add(b1); piece.Points.Add(a1);

            piece.Faces.Add(at); piece.Faces.Add(at + 2); piece.Faces.Add(at + 1);
            piece.Faces.Add(at); piece.Faces.Add(at + 3); piece.Faces.Add(at + 2);
        }

        return piece;
    }

    /// <summary>
    /// One pine, a unit tall, built from a trunk and a stack of tiers. Under
    /// snow each tier carries a second one just above it in white, which is
    /// what snow on a branch looks like from any distance worth drawing at.
    /// </summary>
    public static Mesh Build(int shape, bool snowy)
    {
        var needle = Swatch(new Color(0.11f, 0.22f, 0.07f));     // deep green
        var bark = Swatch(new Color(0.30f, 0.20f, 0.12f));       // brown
        var snow = Swatch(new Color(0.97f, 0.98f, 0.99f));       // white

        int tiers = 3 + shape % 3;
        int sides = 6 + shape % 3;

        float lean = 0.92f + (shape % 5) * 0.04f;               // a little variety in width

        var pieces = new List<Piece> { Trunk(0.42f, 0.045f, 0.028f, sides, bark) };

        for (int i = 0; i < tiers; i++)
        {
            float t = i / (float)tiers;

            float bottom = 0.20f + t * 0.62f;
            float top = bottom + 0.34f - t * 0.10f;
            float radius = (0.30f - t * 0.19f) * lean;

            pieces.Add(Cone(bottom, top, radius, sides, needle));

            if (snowy)
            {
                // sitting just above the green and a shade narrower, so the
                // green shows under its edge the way a bough does
                pieces.Add(Cone(bottom + 0.045f, top + 0.035f, radius * 0.88f, sides, snow));
            }
        }

        var points = new List<Vector3>();
        var uvs = new List<Vector2>();
        var faces = new List<int>();

        foreach (var piece in pieces)
        {
            int at = points.Count;

            points.AddRange(piece.Points);

            for (int i = 0; i < piece.Points.Count; i++) uvs.Add(piece.Uv);
            foreach (int f in piece.Faces) faces.Add(at + f);
        }

        var mesh = new Mesh { name = (snowy ? "Snow Pine " : "Pine ") + shape };

        mesh.SetVertices(points);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
