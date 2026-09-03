using UnityEngine;

/// <summary>
/// Puts a structure together out of the tile pack's own pieces, on the tile
/// grid, the way the pack was drawn to be used: ground tiles stacked for a
/// platform, the pack's tower on top, its stair up the side, its fence round
/// the edge. Everything is placed from the seed, so a structure rebuilt after
/// you walk away is the one you left.
///
/// Distances here are in world units; a tile is two of them.
/// </summary>
public static class LandmarkBuilder
{
    private const float Tile = WorldGrid.TileSize;

    /// <summary>
    /// A ground tile's block runs from a unit below its centre to 0.95 above,
    /// with its centre 1.05 below the walking surface. So a tile stacked on
    /// the ground has its centre 0.95 above the ground's walking surface, and
    /// its own top 1.9 above it.
    /// </summary>
    private const float BlockTop = 0.95f;
    private const float BlockUnderSurface = 1.05f;
    private const float Stacked = Tile - BlockUnderSurface;        // 0.95
    private const float Deck = Stacked + BlockTop;                  // 1.90

    public static GameObject Build(Landmarks.Placement placement, Transform parent)
    {
        var root = new GameObject(Landmarks.NameOf(placement.Kind) + " " + placement.Chunk);
        root.transform.SetParent(parent, false);
        root.transform.position = placement.Position;
        root.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);

        LandmarkTag.Attach(root, placement.Kind, placement.Chunk);

        var rng = new System.Random(placement.Chunk.x * 73856093 ^ placement.Chunk.y * 19349663 ^ ((int)placement.Kind * 977));

        switch (placement.Kind)
        {
            default:
            case LandmarkKind.ForestersWatch: BuildWatch(root.transform, rng); break;
        }

        return root;
    }

    // ------------------------------------------------------- Forester's Watch

    /// <summary>
    /// A three-by-three platform of grass tiles one tile up, the tower on its
    /// middle, the stair down its +x side, a fence round the rest of the rim,
    /// a lamp at the stair head and two stone busts at its foot.
    /// </summary>
    private static void BuildWatch(Transform p, System.Random rng)
    {
        var shelf = Structures.Get();
        var world = Object.FindFirstObjectByType<ChunkManager>();

        if (shelf == null || world == null)
        {
            Debug.LogError("[LandmarkBuilder] no structure pieces or no world to build in.");
            return;
        }

        int half = Landmarks.PlatformHalf;

        // the ground's block top, which is where things at the foot stand:
        // the walking surface is 0.1 above the block, on the grass
        const float ground = -0.10f;

        for (int x = -half; x <= half; x++)
        for (int z = -half; z <= half; z++)
            GroundTile(p, world, rng, new Vector3(x * Tile, Stacked, z * Tile));

        // the tower, on the middle tile, its base at the deck
        Piece(p, shelf.Tower, new Vector3(0f, Deck, 0f), 0f, true, new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f));

        // The stair: two tiles long, one tile high, its high end at its -z.
        // Turned so that -z faces the platform, with the high end just at
        // the platform's edge and the treads coming down to the ground.
        float edge = (half + 0.5f) * Tile;
        Piece(p, shelf.Stair, new Vector3(edge + 2.0f, Deck - 0.94f, 0f), 90f, true, Vector3.zero, Vector3.zero);

        // the fence round the rim, in the pack's four pieces, leaving the
        // stair head open
        float rim = edge - 0.12f;
        float railY = Deck + 0.19f;

        for (float along = -edge + 0.5f; along <= edge - 0.4f; along += 0.86f)
        {
            bool byStair = Mathf.Abs(along) < 1.25f;

            Fence(p, shelf, rng, new Vector3(along, railY, rim), 0f);
            Fence(p, shelf, rng, new Vector3(along, railY, -rim), 0f);
            Fence(p, shelf, rng, new Vector3(-rim, railY, along), 90f);
            if (!byStair) Fence(p, shelf, rng, new Vector3(rim, railY, along), 90f);
        }

        // a lamp at the stair head, lit side toward the steps
        Piece(p, shelf.Lamp, new Vector3(rim - 0.3f, Deck + 1.0f, 1.55f), 90f, false, Vector3.zero, Vector3.zero);

        // two busts flanking the foot of the stair
        var bustA = Pick(shelf.Busts, rng);
        var bustB = Pick(shelf.Busts, rng);
        Piece(p, bustA, new Vector3(edge + 4.5f, ground + 0.37f, 1.3f), 0f, true, new Vector3(0.6f, 1f, 0.6f), new Vector3(0f, 0.13f, 0f));
        Piece(p, bustB, new Vector3(edge + 4.5f, ground + 0.37f, -1.3f), 0f, true, new Vector3(0.6f, 1f, 0.6f), new Vector3(0f, 0.13f, 0f));

        // stores on the deck, in the corner away from the stair
        Piece(p, Pick(shelf.Boxes, rng), new Vector3(-edge + 0.6f, Deck + 0.30f, -edge + 0.6f), Turn(rng), true, new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(p, Pick(shelf.Boxes, rng), new Vector3(-edge + 1.25f, Deck + 0.30f, -edge + 0.7f), Turn(rng), true, new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(p, shelf.Chest, new Vector3(-edge + 0.7f, Deck + 0.17f, edge - 0.7f), 180f, true, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));

        // and at the foot: a signboard by the stair, timber left against the base
        Piece(p, shelf.Signboard, new Vector3(edge + 3.2f, ground + 1.01f, -2.4f), 0f, false, Vector3.zero, Vector3.zero);
        Piece(p, shelf.Timber, new Vector3(-edge - 0.45f, ground + 1.07f, 0.4f), 90f, true, new Vector3(0.55f, 0.8f, 2.0f), new Vector3(0f, -0.65f, 0f));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// A ground tile, out of the same library the ground is drawn from, with
    /// a box round it so it can be stood on. Treeless: the tower stands here.
    /// </summary>
    private static void GroundTile(Transform p, ChunkManager world, System.Random rng, Vector3 at)
    {
        // the bare Big Grass tiles are ids 15 to 19
        int id = 15 + rng.Next(5);

        if (!world.TryTile(id, out var def) || def == null) return;

        var go = new GameObject("Tile " + id);
        go.transform.SetParent(p, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);

        go.AddComponent<MeshFilter>().sharedMesh = def.MeshGetter();
        go.AddComponent<MeshRenderer>().sharedMaterial = def.MaterialGetter();

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.025f, 0f);
        box.size = new Vector3(Tile, 1.95f, Tile);
    }

    private static void Fence(Transform p, Structures shelf, System.Random rng, Vector3 at, float yaw)
    {
        Piece(p, Pick(shelf.Fences, rng), at, yaw, false, Vector3.zero, Vector3.zero);
    }

    /// <summary>
    /// One of the pack's pieces. The pack's prefabs carry no scene offset in
    /// this folder, but the local transform is set outright all the same:
    /// the stone tiles' did, and every one of them landed a field away.
    /// </summary>
    private static GameObject Piece(Transform p, GameObject prefab, Vector3 at, float yaw, bool solid, Vector3 boxSize, Vector3 boxCentre)
    {
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, p, false);
        go.name = prefab.name;
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;

        if (!solid) return go;

        if (boxSize.sqrMagnitude > 0f)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = boxSize;
            box.center = boxCentre;
        }
        else
        {
            // the stair: its treads are what you climb, so the mesh itself
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null)
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        return go;
    }

    private static GameObject Pick(GameObject[] from, System.Random rng)
    {
        return from == null || from.Length == 0 ? null : from[rng.Next(from.Length)];
    }

    private static float Turn(System.Random rng) => rng.Next(4) * 90f;
}
