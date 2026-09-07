using UnityEngine;

/// <summary>Shapes for the glow shader: a disc that is bright in the middle and gone at the rim.</summary>
public static class Glows
{
    private static Mesh disc;

    /// <summary>The disc's mesh on its own, for instanced draws.</summary>
    public static Mesh DiscMesh()
    {
        Disc("glow disc (mesh)", null);
        return disc;
    }

    public static Transform Disc(string name, Material paint)
    {
        if (disc == null)
        {
            const int sides = 24;
            var verts = new Vector3[sides + 1]; var cols = new Color[sides + 1]; var uvs = new Vector2[sides + 1]; var tris = new int[sides * 3];
            verts[0] = Vector3.zero; cols[0] = Color.white; uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                cols[i + 1] = new Color(1f, 1f, 1f, 0f);
                uvs[i + 1] = new Vector2(0.5f, 0.5f);
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % sides + 1;
            }
            disc = new Mesh { name = "glow disc", vertices = verts, colors = cols, uv = uvs, triangles = tris };
            disc.RecalculateBounds();
        }

        if (paint == null) return null;

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = disc;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = paint;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }
}
