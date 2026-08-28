using UnityEngine;

/// <summary>
/// Builds the landmarks out of primitives. The tile art has no ruins in it, so
/// these are made from boxes — deliberately plain and stone-coloured, which
/// sits acceptably in a low-poly world and can be swapped for real models later
/// without touching placement or discovery.
/// </summary>
public static class LandmarkBuilder
{
    private static Material stone;
    private static Material mossy;

    public static GameObject Build(Landmarks.Placement placement, Transform parent)
    {
        EnsureMaterials();

        var root = new GameObject(Landmarks.NameOf(placement.Kind) + " " + placement.Chunk);
        root.transform.SetParent(parent, false);
        root.transform.position = placement.Position;
        root.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);

        switch (placement.Kind)
        {
            case LandmarkKind.Cairn: BuildCairn(root.transform); break;
            case LandmarkKind.StandingStones: BuildRing(root.transform); break;
            default: BuildObelisk(root.transform); break;
        }

        return root;
    }

    private static void EnsureMaterials()
    {
        if (stone != null)
        {
            return;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        stone = new Material(lit);
        stone.SetColor("_BaseColor", new Color(0.44f, 0.44f, 0.45f));
        stone.color = new Color(0.44f, 0.44f, 0.45f);

        mossy = new Material(lit);
        mossy.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.31f));
        mossy.color = new Color(0.36f, 0.42f, 0.31f);
    }

    private static Transform Block(Transform parent, Vector3 localPos, Vector3 scale, float yaw, bool moss = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = moss ? mossy : stone;

        return go.transform;
    }

    /// <summary>A tapering stack — the classic trail marker.</summary>
    private static void BuildCairn(Transform root)
    {
        int layers = 5;
        float y = 0f;

        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)(layers - 1);
            float width = Mathf.Lerp(1.5f, 0.45f, t);
            float height = Mathf.Lerp(0.42f, 0.28f, t);

            Block(root, new Vector3(Mathf.Sin(i * 2.1f) * 0.08f, y + height * 0.5f, Mathf.Cos(i * 1.7f) * 0.08f),
                  new Vector3(width, height, width), i * 37f, moss: i == 0);

            y += height * 0.92f;
        }
    }

    /// <summary>A ring, with a couple of stones fallen.</summary>
    private static void BuildRing(Transform root)
    {
        const int count = 7;
        const float radius = 3.4f;

        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);

            bool fallen = i == 2 || i == 5;
            float height = fallen ? 0.5f : Mathf.Lerp(2.1f, 3.0f, (i % 3) / 2f);

            var t = Block(root, pos + Vector3.up * height * 0.5f,
                          new Vector3(0.75f, height, 0.55f), -a * Mathf.Rad2Deg, moss: fallen);

            if (fallen)
            {
                t.localRotation = Quaternion.Euler(72f, -a * Mathf.Rad2Deg, 0f);
                t.localPosition = pos + Vector3.up * 0.3f;
            }
        }
    }

    /// <summary>One tall marker with a base, meant to be seen from far off.</summary>
    private static void BuildObelisk(Transform root)
    {
        Block(root, new Vector3(0f, 0.22f, 0f), new Vector3(2.4f, 0.44f, 2.4f), 0f, moss: true);
        Block(root, new Vector3(0f, 0.62f, 0f), new Vector3(1.5f, 0.4f, 1.5f), 18f);
        Block(root, new Vector3(0f, 3.0f, 0f), new Vector3(0.85f, 4.4f, 0.85f), 6f);
        Block(root, new Vector3(0f, 5.35f, 0f), new Vector3(0.5f, 0.5f, 0.5f), 45f);
    }
}
