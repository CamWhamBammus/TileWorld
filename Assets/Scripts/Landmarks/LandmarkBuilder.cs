using UnityEngine;

/// <summary>
/// Puts a structure together out of the tile pack's own pieces, on the tile
/// grid, the way the pack was drawn to be used: ground tiles stacked for a
/// platform, the pack's tower on top, its stair up the side, its fence round
/// the edge. Everything is placed from the seed, so a structure rebuilt after
/// you walk away is the one you left.
///
/// Distances here are in world units; a tile is two of them. Heights are
/// from the structure's root, which sits on the walking surface of its tile.
/// </summary>
public static class LandmarkBuilder
{
    private const float Tile = WorldGrid.TileSize;

    /// <summary>
    /// A ground tile's block runs from a unit below its centre to 0.95 above,
    /// with its centre 1.05 below the walking surface. So the ground's own
    /// block top is 0.10 under the root; a tile stacked on the ground has its
    /// centre 0.95 above the root and its top 1.90 above; the next, 2.0 more.
    /// </summary>
    private const float Ground = -0.10f;
    private const float Deck1 = 1.90f, Deck2 = 3.90f, Deck3 = 5.90f;

    /// <summary>Where a tile stacked n high has its centre.</summary>
    private static float Stack(int n) => 0.95f + (n - 1) * Tile;

    // where things stand: the height of a piece's pivot above the floor it rests on
    private const float LampUp = 1.0f, BustUp = 0.37f, BoxUp = 0.30f, ChestUp = 0.17f, SignUp = 1.01f,
                        TimberUp = 1.07f, DoorUp = 1.01f, FenceUp = 0.19f, StairUp = -0.94f, BridgeUp = -0.99f;

    // tile ids by kind of ground, five variants each
    private const int GrassTiles = 15, MudTiles = 20, SandTiles = 25, StoneTiles = 30;

    private struct Job
    {
        public Transform Root;
        public Structures Shelf;
        public ChunkManager World;
        public Flora Flora;
        public System.Random Rng;
        public Landmarks.Placement At;
        public int Seed;
    }

    public static GameObject Build(Landmarks.Placement placement, Transform parent)
    {
        var root = new GameObject(Landmarks.NameOf(placement.Kind) + " " + placement.Chunk);
        root.transform.SetParent(parent, false);
        root.transform.position = placement.Position;
        root.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);

        LandmarkTag.Attach(root, placement.Kind, placement.Chunk);

        var world = Object.FindFirstObjectByType<ChunkManager>();
        var shelf = Structures.Get();

        if (shelf == null || world == null)
        {
            Debug.LogError("[LandmarkBuilder] no structure pieces or no world to build in.");
            return root;
        }

        var b = new Job
        {
            Root = root.transform,
            Shelf = shelf,
            World = world,
            Flora = Resources.Load<Flora>("Flora"),
            Rng = new System.Random(placement.Chunk.x * 73856093 ^ placement.Chunk.y * 19349663 ^ ((int)placement.Kind * 977)),
            At = placement,
            Seed = world.WorldSeed
        };

        switch (placement.Kind)
        {
            case LandmarkKind.Shipwreck: Wreck(b); break;
            case LandmarkKind.SandGate: Gate(b); break;
            case LandmarkKind.TrappersCabin: Cabin(b); break;
            case LandmarkKind.FishingJetty: Jetty(b); break;
            case LandmarkKind.SteppedAltar: Altar(b); break;
            case LandmarkKind.ToadstoolRing: Ring(b); break;
            case LandmarkKind.CharcoalCamp: Camp(b); break;
            case LandmarkKind.HilltopBeacon: Beacon(b); break;
            case LandmarkKind.SummitCairn: Cairn(b); break;
            case LandmarkKind.WaysideShrine: Shrine(b); break;
            case LandmarkKind.StandingStones: Stones(b); break;
            case LandmarkKind.Lighthouse: Lighthouse(b); break;
            case LandmarkKind.HuntersHide: Hide(b); break;
            case LandmarkKind.BuriedTower: Buried(b); break;
            default:
            case LandmarkKind.ForestersWatch: Watch(b); break;
        }

        return root;
    }

    // ------------------------------------------------------- Forester's Watch

    /// <summary>
    /// A three-by-three platform of grass tiles one tile up, the tower on its
    /// middle, the stair down its +x side, a fence round the rest of the rim,
    /// a lamp at the stair head and two stone busts at its foot.
    /// </summary>
    private static void Watch(Job b)
    {
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            GroundTile(b, GrassTiles, new Vector3(x * Tile, Stack(1), z * Tile));

        Piece(b, b.Shelf.Tower, new Vector3(0f, Deck1, 0f), 0f, new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f));

        float edge = 1.5f * Tile;
        Stair(b, new Vector3(edge + 2.0f, Deck1 + StairUp, 0f), 90f);

        Rail(b, edge, Deck1, 1);

        Piece(b, b.Shelf.Lamp, new Vector3(edge - 0.42f, Deck1 + LampUp, 1.55f), 90f);
        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(edge + 4.5f, Ground + BustUp, 1.3f), 0f, new Vector3(0.6f, 1f, 0.6f), new Vector3(0f, 0.13f, 0f));
        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(edge + 4.5f, Ground + BustUp, -1.3f), 0f, new Vector3(0.6f, 1f, 0.6f), new Vector3(0f, 0.13f, 0f));

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(-edge + 0.6f, Deck1 + BoxUp, -edge + 0.6f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(-edge + 1.25f, Deck1 + BoxUp, -edge + 0.7f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Chest, new Vector3(-edge + 0.7f, Deck1 + ChestUp, edge - 0.7f), 180f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));

        Piece(b, b.Shelf.Signboard, new Vector3(edge + 3.2f, Ground + SignUp, -2.4f), 0f);
        Piece(b, b.Shelf.Timber, new Vector3(-edge - 0.45f, Ground + TimberUp, 0.4f), 90f, new Vector3(0.55f, 0.8f, 2.0f), new Vector3(0f, -0.65f, 0f));
    }

    // -------------------------------------------------------------- Shipwreck

    /// <summary>
    /// A hull in the shallows, its bow toward the shore and out of the water,
    /// its stern under. The deck is planks, tilted the way it settled; timber
    /// ribs stand up out of the water where the sides have gone; the mast is
    /// two logs, snapped. What was in it is up on the beach.
    /// </summary>
    private static void Wreck(Job b)
    {
        float water = WaterSurface.Level - b.At.Position.y;

        // the hull, as one tilted frame everything on deck hangs from
        var hull = new GameObject("Hull").transform;
        hull.SetParent(b.Root, false);
        hull.localPosition = new Vector3(0f, water - 0.35f, 0f);
        hull.localRotation = Quaternion.Euler(9f, 0f, -16f);

        // deck: two plank slabs end to end, a walkable box over the pair
        Slab(b, hull, new Vector3(-1.5f, 0f, 0f), 90f);
        Slab(b, hull, new Vector3(1.5f, 0f, 0f), 90f);

        var walk = hull.gameObject.AddComponent<BoxCollider>();
        walk.center = new Vector3(0f, 0.83f, 0f);
        walk.size = new Vector3(6f, 0.3f, 2.08f);

        // a rail along one side of the bow, the other side gone
        for (float x = 0.5f; x <= 2.6f; x += 0.86f)
            Under(b, hull, Pick(b.Shelf.Fences, b), new Vector3(x, 0.98f + FenceUp, 1.0f), 0f);

        // what is left of the cabin: a door standing at the stern
        Under(b, hull, b.Shelf.Doors, new Vector3(-2.2f, 0.98f + DoorUp, 0.2f), 0f);

        // the mast, in two pieces, the top one snapped over
        Under(b, hull, Pick(b.Shelf.Poles, b), new Vector3(0.4f, 0.98f + 1.0f, 0f), 0f, new Vector3(0f, 0f, 6f));
        Under(b, hull, Pick(b.Shelf.Poles, b), new Vector3(0.55f, 0.98f + 2.4f, 0.05f), 0f, new Vector3(0f, 0f, -38f));

        // cargo still lashed on deck
        Under(b, hull, Pick(b.Shelf.Boxes, b), new Vector3(1.6f, 0.98f + BoxUp, -0.55f), Turn(b));
        Under(b, hull, b.Shelf.Chest, new Vector3(-0.6f, 0.98f + ChestUp, 0.5f), 90f);

        // ribs standing out of the water along both sides
        for (int i = 0; i < 5; i++)
        {
            float x = -2.8f + i * 1.3f;
            Piece(b, Pick(b.Shelf.Poles, b), new Vector3(x, Ground + 1.0f, 1.35f), 0f, Vector3.zero, Vector3.zero, new Vector3(-32f, 0f, 8f - i * 6f));
            Piece(b, Pick(b.Shelf.Poles, b), new Vector3(x + 0.4f, Ground + 1.0f, -1.35f), 0f, Vector3.zero, Vector3.zero, new Vector3(32f, 0f, 4f + i * 5f));
        }

        // and what washed up, on the first dry ground ahead
        int shore = 1;
        while (shore < 5 && WaterSurface.IsUnderwater(b.At.TileX + Dir(b).x * shore, b.At.TileZ + Dir(b).y * shore, b.Seed)) shore++;

        float sx = shore * Tile;
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(sx + 0.3f, GroundAt(b, sx + 0.3f, -1.4f) + BoxUp, -1.4f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(sx + 1.4f, GroundAt(b, sx + 1.4f, -0.9f) + BoxUp, -0.9f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Chest, new Vector3(sx + 0.8f, GroundAt(b, sx + 0.8f, 1.5f) + ChestUp, 1.5f), 200f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, b.Shelf.Timber, new Vector3(sx + 2.2f, GroundAt(b, sx + 2.2f, 2.4f) + TimberUp, 2.4f), 70f);
        Piece(b, b.Shelf.Signboard, new Vector3(sx + 1.6f, GroundAt(b, sx + 1.6f, -2.6f) + SignUp, -2.6f), 180f);
    }

    // -------------------------------------------------------------- Sand Gate

    /// <summary>
    /// Two towers on sand plinths a tile apart, the pack's bridge laid across
    /// their tops for a walk between them, and a gap below to pass through.
    /// Standing stones flank both approaches; a gate leans, off its hinges.
    /// </summary>
    private static void Gate(Job b)
    {
        foreach (float x in new[] { -Tile, Tile })
        {
            GroundTile(b, SandTiles, new Vector3(x, Stack(1), 0f));
            Piece(b, b.Shelf.Tower, new Vector3(x, Deck1, 0f), 0f, new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f));
        }

        // the walk between the tops, embedded a little way into each
        Piece(b, b.Shelf.Bridge, new Vector3(0f, Deck1 + 3.5f - 0.28f, 0f), 90f, new Vector3(4.4f, 0.7f, 2f), new Vector3(0f, 0.63f, 0f));

        // the gate, leaning against the inside of one tower
        Piece(b, b.Shelf.Doors, new Vector3(0.95f, Ground + DoorUp - 0.25f, 0.5f), 90f, Vector3.zero, Vector3.zero, new Vector3(0f, 90f, -22f));

        foreach (float z in new[] { -3.4f, 3.4f })
        foreach (float x in new[] { -Tile, Tile })
            Standing(b, new Vector3(x, Ground, z), 1.3f);

        Piece(b, b.Shelf.Lamp, new Vector3(-3.4f, Ground + LampUp, 1.3f), 0f);
        Piece(b, b.Shelf.Lamp, new Vector3(3.4f, Ground + LampUp, -1.3f), 180f);

        // stubs of wall running off from each tower
        for (int i = 0; i < 3; i++)
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(-3.5f - i * 0.86f, Ground + FenceUp, 0f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(3.5f + i * 0.86f, Ground + FenceUp, 0f), 0f);
        }

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(3.4f, Ground + BoxUp, 1.6f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Signboard, new Vector3(1.3f, Ground + SignUp, 4.6f), 90f);
    }

    // --------------------------------------------------------- Trapper's Cabin

    /// <summary>
    /// A hut of pale stone tiles, three across and two courses high with the
    /// middle left open, one tile left out of the front for the doorway --
    /// which is two wide and two high, and the pack's door is a little less.
    /// Planks pitched over the top, the door standing open, a lamp by it,
    /// and a fenced yard with the woodpile.
    /// </summary>
    private static void Cabin(Job b)
    {
        for (int course = 1; course <= 2; course++)
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
        {
            if (x == 0 && z == 0) continue;                 // the room
            if (course == 1 && x == 1 && z == 0) continue;  // the doorway

            // Pale stone, not the barrens' boulders: those taper underneath,
            // and a wall of them was a wall of rocks hanging off each other.
            GroundTile(b, SandTiles, new Vector3(x * Tile, Stack(course), z * Tile));
        }

        // The roof: two pitches meeting at a ridge along z, each three
        // slabs side by side hung under a frame that carries the tilt.
        foreach (int side in new[] { -1, 1 })
        {
            var pitch = new GameObject("Pitch").transform;
            pitch.SetParent(b.Root, false);
            pitch.localPosition = new Vector3(side * 1.4f, Deck2 + 0.75f, 0f);
            pitch.localRotation = Quaternion.Euler(0f, 0f, -side * 30f);

            for (int k = -1; k <= 1; k++)
                Slab(b, pitch, new Vector3(0f, -0.98f, k * 2.08f), 90f);
        }

        // the door, standing open beside the doorway
        Piece(b, b.Shelf.Doors, new Vector3(2.15f, Ground + DoorUp, -1.15f), 55f);

        Piece(b, b.Shelf.Lamp, new Vector3(3.3f, Ground + LampUp, 2.2f), 180f);
        Piece(b, b.Shelf.Signboard, new Vector3(3.6f, Ground + SignUp, -2.5f), 0f);
        Piece(b, b.Shelf.Chest, new Vector3(-0.3f, Ground + ChestUp, 0.6f), 270f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));

        // the yard, ahead of the house, with the woodpile
        for (float z = -2.6f; z <= 2.6f; z += 0.86f)
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(7.6f, Ground + FenceUp, z), 90f);
        for (float x = 4.4f; x <= 7.2f; x += 0.86f)
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, Ground + FenceUp, -2.9f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, Ground + FenceUp, 2.9f), 0f);
        }
        Piece(b, Pick(b.Shelf.Fences, b), new Vector3(4.0f, Ground + FenceUp, -2.4f), 90f);

        Piece(b, b.Shelf.Timber, new Vector3(6.3f, Ground + TimberUp, -1.7f), 0f);
        Piece(b, b.Shelf.Timber, new Vector3(6.3f, Ground + TimberUp, -1.1f), 0f);
        Piece(b, b.Shelf.Timber, new Vector3(6.3f, Ground + TimberUp + 0.7f, -1.4f), 0f);

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(5.2f, Ground + BoxUp, 2.0f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(5.8f, Ground + BoxUp, 2.2f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
    }

    // ---------------------------------------------------------- Fishing Jetty

    /// <summary>
    /// Two of the pack's bridges end to end from the shore out over the water,
    /// on timber posts, railed both sides, a lamp at the far end and the
    /// fisherman's things at the near one.
    /// </summary>
    private static void Jetty(Job b)
    {
        float water = WaterSurface.Level - b.At.Position.y;
        // A step up from the shore no taller than the character can take:
        // the shore is at the water line, so this is the whole of the step.
        float deck = Mathf.Max(water + 0.35f, -0.25f);

        foreach (float x in new[] { 2.2f, 6.5f })
            Piece(b, b.Shelf.Bridge, new Vector3(x, deck + BridgeUp, 0f), 90f, new Vector3(4.4f, 0.7f, 2f), new Vector3(0f, 0.63f, 0f));

        foreach (float x in new[] { 1.0f, 4.4f, 7.8f })
        foreach (float z in new[] { -0.8f, 0.8f })
            Piece(b, Pick(b.Shelf.Poles, b), new Vector3(x, deck - 0.6f, z), 0f);

        for (float x = 0.9f; x <= 8.2f; x += 0.86f)
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, deck + FenceUp, 0.9f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, deck + FenceUp, -0.9f), 0f);
        }

        Piece(b, b.Shelf.Lamp, new Vector3(8.3f, deck + LampUp, 0.75f), 180f);

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(0.2f, Ground + BoxUp, 1.7f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Chest, new Vector3(-0.8f, Ground + ChestUp, -1.5f), 0f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, b.Shelf.Signboard, new Vector3(-1.4f, Ground + SignUp, 1.9f), 0f);
        Piece(b, b.Shelf.Timber, new Vector3(-1.8f, Ground + TimberUp, -2.5f), 20f);
    }

    // ---------------------------------------------------------- Stepped Altar

    /// <summary>
    /// Three tiers of pale stone, five across, then three, then one, an idol
    /// on the top: a standing stone. A stair up to the first tier and a
    /// second along the ledge up to the next; the top is not for climbing.
    /// </summary>
    private static void Altar(Job b)
    {
        // Pale stone, against the grey of the barrens: tiers of the barrens'
        // own tiles were lost against the ground they stood on, and those
        // tiles taper underneath, so a tier of them looked to float.
        for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++)
            GroundTile(b, SandTiles, new Vector3(x * Tile, Stack(1), z * Tile));

        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            GroundTile(b, SandTiles, new Vector3(x * Tile, Stack(2), z * Tile));

        GroundTile(b, SandTiles, new Vector3(0f, Stack(3), 0f));

        Standing(b, new Vector3(0f, Deck3, 0f), 2.6f);

        // up to the ledge, then along it to the second tier
        Stair(b, new Vector3(5f + 2.0f, Deck1 + StairUp, 0f), 90f);
        Stair(b, new Vector3(4f, Deck2 + StairUp, 2.5f), 0f);

        foreach (float x in new[] { -4.5f, 4.5f })
        foreach (float z in new[] { -4.5f, 4.5f })
            Piece(b, b.Shelf.Lamp, new Vector3(x, Deck1 + LampUp, z), x < 0f ? 0f : 180f);

        Standing(b, new Vector3(9.6f, Ground, 1.4f), 1.5f);
        Standing(b, new Vector3(9.6f, Ground, -1.4f), 1.5f);
        Piece(b, b.Shelf.Signboard, new Vector3(8.4f, Ground + SignUp, -2.6f), 0f);
    }

    // --------------------------------------------------------- Toadstool Ring

    /// <summary>
    /// A ring of the pack's mushrooms grown to twice your height round one
    /// raised tile of black earth with a standing stone on it, lamps between, and a
    /// broken fence where someone once tried to keep it in.
    /// </summary>
    private static void Ring(Job b)
    {
        GroundTile(b, MudTiles, new Vector3(0f, Stack(1), 0f));

        Standing(b, new Vector3(0f, Deck1, 0f), 1.9f);

        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Mushroom(b, new Vector3(Mathf.Cos(a) * 5.2f, 0f, Mathf.Sin(a) * 5.2f), 2.4f + (float)b.Rng.NextDouble() * 1.0f);

            float inner = a + Mathf.PI / 8f;
            Mushroom(b, new Vector3(Mathf.Cos(inner) * 3.1f, 0f, Mathf.Sin(inner) * 3.1f), 0.9f + (float)b.Rng.NextDouble() * 0.6f);
        }

        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + 0.4f;
            Piece(b, b.Shelf.Lamp, new Vector3(Mathf.Cos(a) * 2.3f, Ground + LampUp, Mathf.Sin(a) * 2.3f), -a * Mathf.Rad2Deg + 180f);
        }

        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.PI + (i - 2) * 0.22f;
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(Mathf.Cos(a) * 6.6f, Ground + FenceUp, Mathf.Sin(a) * 6.6f), -a * Mathf.Rad2Deg + 90f);
        }

        Piece(b, b.Shelf.Signboard, new Vector3(6.2f, Ground + SignUp, 0.9f), 0f);
        Piece(b, b.Shelf.Chest, new Vector3(-1.9f, Ground + ChestUp, 1.7f), 120f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
    }

    // ---------------------------------------------------------- Charcoal Camp

    /// <summary>
    /// A kiln of black earth, one tile up, with poles round it and a lamp on
    /// it; a fenced yard ahead with the wood cut and stacked; the burner's
    /// things left about.
    /// </summary>
    private static void Camp(Job b)
    {
        GroundTile(b, MudTiles, new Vector3(0f, Stack(1), 0f));
        Piece(b, b.Shelf.Lamp, new Vector3(0f, Deck1 + LampUp, 0f), Turn(b));

        foreach (float x in new[] { -1.35f, 1.35f })
        foreach (float z in new[] { -1.35f, 1.35f })
            Piece(b, Pick(b.Shelf.Poles, b), new Vector3(x, Ground + 1.0f, z), 0f, Vector3.zero, Vector3.zero, new Vector3((z < 0f ? -1f : 1f) * 8f, 0f, (x < 0f ? 1f : -1f) * 8f));

        Piece(b, b.Shelf.Doors, new Vector3(1.25f, Ground + DoorUp - 0.25f, 0.9f), 90f, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, -24f));

        for (float z = -2.6f; z <= 2.6f; z += 0.86f)
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(7.0f, Ground + FenceUp, z), 90f);
        for (float x = 3.2f; x <= 6.6f; x += 0.86f)
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, Ground + FenceUp, -2.9f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(x, Ground + FenceUp, 2.9f), 0f);
        }

        foreach (float z in new[] { -1.8f, -0.6f, 0.8f })
        {
            Piece(b, b.Shelf.Timber, new Vector3(5.2f, Ground + TimberUp, z), 0f);
            Piece(b, b.Shelf.Timber, new Vector3(5.2f, Ground + TimberUp + 0.7f, z + 0.2f), 0f);
        }
        Piece(b, b.Shelf.Timber, new Vector3(3.9f, Ground + TimberUp, 2.0f), 30f);

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(-2.2f, Ground + BoxUp, -1.4f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Chest, new Vector3(-2.4f, Ground + ChestUp, 1.2f), 90f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, b.Shelf.Signboard, new Vector3(2.6f, Ground + SignUp, -3.4f), 0f);
    }

    // -------------------------------------------------------- Hilltop Beacon

    /// <summary>
    /// The tower on a plinth two tiles high, a lamp burning on its top, a ring
    /// of fence round the foot and lamps at the quarters: a light to steer by
    /// from a long way off.
    /// </summary>
    private static void Beacon(Job b)
    {
        GroundTile(b, GrassTiles, new Vector3(0f, Stack(1), 0f));
        GroundTile(b, GrassTiles, new Vector3(0f, Stack(2), 0f));
        Piece(b, b.Shelf.Tower, new Vector3(0f, Deck2, 0f), 0f, new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f));
        Piece(b, b.Shelf.Lamp, new Vector3(0f, Deck2 + 3.5f + LampUp, 0f), Turn(b));

        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(Mathf.Cos(a) * 3.6f, Ground + FenceUp, Mathf.Sin(a) * 3.6f), -a * Mathf.Rad2Deg + 90f);
        }

        foreach (float x in new[] { -2.2f, 2.2f })
        foreach (float z in new[] { -2.2f, 2.2f })
            Piece(b, b.Shelf.Lamp, new Vector3(x, Ground + LampUp, z), x < 0f ? 0f : 180f);

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(1.5f, Ground + BoxUp, 0.4f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Chest, new Vector3(-1.5f, Ground + ChestUp, -0.6f), 90f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, b.Shelf.Signboard, new Vector3(4.6f, Ground + SignUp, -1.2f), 0f);
    }

    // ----------------------------------------------------------- Summit Cairn

    /// <summary>
    /// A heap of the pack's boulders, one on end at the top, with a pole
    /// beside it for whoever comes up to see from below.
    /// </summary>
    private static void Cairn(Job b)
    {
        Standing(b, new Vector3(0f, Ground + 0.5f, 0f), 1.5f);

        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + 0.3f;
            Lying(b, new Vector3(Mathf.Cos(a) * 0.95f, Ground, Mathf.Sin(a) * 0.95f), 0.8f + (float)b.Rng.NextDouble() * 0.3f);
        }
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            Lying(b, new Vector3(Mathf.Cos(a) * 1.7f, Ground, Mathf.Sin(a) * 1.7f), 0.45f + (float)b.Rng.NextDouble() * 0.25f);
        }

        Piece(b, Pick(b.Shelf.Poles, b), new Vector3(0.55f, Ground + 1.0f + 0.5f, -0.4f), 0f, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, -7f));
        Piece(b, b.Shelf.Signboard, new Vector3(2.4f, Ground + SignUp, -1.5f), 20f);
    }

    // --------------------------------------------------------- Wayside Shrine

    /// <summary>
    /// A stone on end on one raised tile, a lamp kept by it, a fence on three
    /// sides and bushes at the back; the sort of thing left where paths met.
    /// </summary>
    private static void Shrine(Job b)
    {
        GroundTile(b, GrassTiles, new Vector3(0f, Stack(1), 0f));
        Standing(b, new Vector3(0f, Deck1, 0f), 1.5f);

        foreach (float along in new[] { -0.45f, 0.45f })
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, Ground + FenceUp, -1.7f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, Ground + FenceUp, 1.7f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(-1.7f, Ground + FenceUp, along), 90f);
        }

        Piece(b, b.Shelf.Lamp, new Vector3(2.0f, Ground + LampUp, 1.4f), 180f);
        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(-2.1f, Ground + BustUp, 1.3f), Turn(b));
        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(-2.1f, Ground + BustUp, -1.3f), Turn(b));
        Piece(b, b.Shelf.Chest, new Vector3(2.0f, Ground + ChestUp, -1.3f), 270f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, b.Shelf.Signboard, new Vector3(3.4f, Ground + SignUp, 0.4f), 0f);
    }

    // -------------------------------------------------------- Standing Stones

    /// <summary>
    /// Seven stones on end in a ring, one fallen, one lying flat in the
    /// middle. Nothing else: no lamp, no fence, nobody keeps it.
    /// </summary>
    private static void Stones(Job b)
    {
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f + 0.2f;
            var foot = new Vector3(Mathf.Cos(a) * 4.6f, Ground, Mathf.Sin(a) * 4.6f);

            if (i == 4) Lying(b, foot, 1.3f);
            else Standing(b, foot, 2.2f + (float)b.Rng.NextDouble() * 0.9f);
        }

        Lying(b, new Vector3(0f, Ground, 0f), 1.2f);
        Piece(b, b.Shelf.Signboard, new Vector3(6.4f, Ground + SignUp, 0.8f), 0f);
    }

    // ------------------------------------------------------------ Lighthouse

    /// <summary>
    /// At a beach's edge: a platform of pale stone three across and one up,
    /// a plinth on that, the tower on the plinth with a lamp burning on top,
    /// and the stair up from the land side. The platform's seaward row stands
    /// in the water.
    /// </summary>
    private static void Lighthouse(Job b)
    {
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            GroundTile(b, SandTiles, new Vector3(x * Tile, Stack(1), z * Tile));

        GroundTile(b, SandTiles, new Vector3(0f, Stack(2), 0f));
        Piece(b, b.Shelf.Tower, new Vector3(0f, Deck2, 0f), 0f, new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f));
        Piece(b, b.Shelf.Lamp, new Vector3(0f, Deck2 + 3.5f + LampUp, 0f), Turn(b));

        float edge = 1.5f * Tile;
        Stair(b, new Vector3(-(edge + 2.0f), Deck1 + StairUp, 0f), 270f);
        Rail(b, edge, Deck1, -1);

        Piece(b, b.Shelf.Lamp, new Vector3(-edge + 0.42f, Deck1 + LampUp, -1.55f), 270f);
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(edge - 0.6f, Deck1 + BoxUp, edge - 0.6f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);

        Piece(b, b.Shelf.Chest, new Vector3(-edge - 4.6f, GroundAt(b, -edge - 4.6f, 1.6f) + ChestUp, 1.6f), 90f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(-edge - 5.2f, GroundAt(b, -edge - 5.2f, -1.4f) + BoxUp, -1.4f), Turn(b), new Vector3(0.6f, 0.6f, 0.6f), Vector3.zero);
        Piece(b, b.Shelf.Signboard, new Vector3(-edge - 4.0f, GroundAt(b, -edge - 4.0f, -2.4f) + SignUp, -2.4f), 180f);
    }

    // ---------------------------------------------------------- Hunter's Hide

    /// <summary>
    /// One tile up, fenced on three sides, a stair up the fourth; somewhere
    /// to wait above the eyeline of whatever comes by.
    /// </summary>
    private static void Hide(Job b)
    {
        GroundTile(b, GrassTiles, new Vector3(0f, Stack(1), 0f));

        float edge = 0.5f * Tile;
        Stair(b, new Vector3(edge + 2.0f, Deck1 + StairUp, 0f), 90f);

        foreach (float along in new[] { -0.45f, 0.45f })
        {
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, Deck1 + FenceUp, edge - 0.12f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, Deck1 + FenceUp, -edge + 0.12f), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(-edge + 0.12f, Deck1 + FenceUp, along), 90f);
        }

        Piece(b, b.Shelf.Lamp, new Vector3(-0.55f, Deck1 + LampUp, 0.55f), 0f);
        Piece(b, b.Shelf.Chest, new Vector3(0.3f, Deck1 + ChestUp, -0.5f), 180f, new Vector3(0.8f, 0.7f, 0.6f), new Vector3(0f, 0.2f, 0f));

        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(edge + 4.6f, Ground + BustUp, 1.3f), Turn(b));
        Piece(b, Pick(b.Shelf.Bushes, b), new Vector3(edge + 4.6f, Ground + BustUp, -1.3f), Turn(b));
        Piece(b, b.Shelf.Signboard, new Vector3(edge + 3.6f, Ground + SignUp, -2.2f), 0f);
    }

    // ---------------------------------------------------------- Buried Tower

    /// <summary>
    /// The tower sunk to its shoulders in the sand and leaning, what was
    /// round it half buried with it. The sand is winning.
    /// </summary>
    private static void Buried(Job b)
    {
        Piece(b, b.Shelf.Tower, new Vector3(0f, Ground - 1.9f, 0f), Turn(b), new Vector3(2.1f, 3.5f, 2.1f), new Vector3(0f, 1.75f, 0f), new Vector3(0f, 0f, 14f));

        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(2.3f, Ground + BoxUp - 0.32f, 1.1f), Turn(b));
        Piece(b, Pick(b.Shelf.Boxes, b), new Vector3(-1.9f, Ground + BoxUp - 0.2f, -1.6f), Turn(b));
        Piece(b, b.Shelf.Chest, new Vector3(1.6f, Ground + ChestUp - 0.3f, -2.2f), 120f);

        // the gate, flat on the sand and half under it
        Piece(b, b.Shelf.Doors, new Vector3(-2.6f, Ground + 0.02f, 0.9f), Turn(b), Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 90f));

        for (int i = 0; i < 3; i++)
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(3.2f + i * 0.86f, Ground + FenceUp - 0.12f * i, 0.2f), 0f);

        Standing(b, new Vector3(2.9f, Ground - 0.3f, 2.6f), 1.3f);
        Piece(b, b.Shelf.Signboard, new Vector3(3.8f, Ground + SignUp - 0.2f, -2.4f), 0f, Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 12f));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// A ground tile, out of the same library the ground is drawn from, with
    /// a box round it so it can be stood on.
    /// </summary>
    private static void GroundTile(Job b, int firstId, Vector3 at)
    {
        int id = firstId + b.Rng.Next(5);

        if (!b.World.TryTile(id, out var def) || def == null) return;

        var go = new GameObject("Tile " + id);
        go.transform.SetParent(b.Root, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(0f, b.Rng.Next(4) * 90f, 0f);

        go.AddComponent<MeshFilter>().sharedMesh = def.MeshGetter();
        go.AddComponent<MeshRenderer>().sharedMaterial = def.MaterialGetter();

        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.025f, 0f);
        box.size = new Vector3(Tile, 1.95f, Tile);
    }

    /// <summary>
    /// A standing stone: one of the pack's boulders, grown to a height and set
    /// on end. The pack has no statues; a stone stood up where a statue would
    /// be says the same thing.
    /// </summary>
    private static void Standing(Job b, Vector3 foot, float tall)
    {
        if (b.Flora == null || b.Flora.Boulders == null || b.Flora.Boulders.Length == 0) return;

        var stone = b.Flora.Boulders[b.Rng.Next(b.Flora.Boulders.Length)];
        if (stone.Mesh == null || stone.Size < 0.0001f) return;

        // Stood on end, and drawn out along its height: a boulder is about as
        // wide as it is long, so on end at its own proportions it was still a
        // boulder, and a ring of them read as a rock fall rather than as
        // stones somebody set up. After the quarter turn about z below, the
        // mesh's own x is what points up.
        const float drawn = 2.3f;
        float size = tall / Mathf.Max(stone.Wide * drawn, 0.01f);

        var go = new GameObject("Standing stone");
        go.transform.SetParent(b.Root, false);
        go.transform.localPosition = new Vector3(foot.x, foot.y + tall * 0.5f, foot.z);
        go.transform.localRotation = Quaternion.Euler(0f, b.Rng.Next(360), 90f);
        go.transform.localScale = new Vector3(size * drawn, size * 0.85f, size * 0.85f);

        go.AddComponent<MeshFilter>().sharedMesh = stone.Mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = b.Flora.Paint;

        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(stone.Size * 0.6f, stone.Wide, stone.Size * 0.6f);
    }

    /// <summary>A boulder lying as it fell, its own way up, to a height.</summary>
    private static void Lying(Job b, Vector3 foot, float tall)
    {
        if (b.Flora == null || b.Flora.Boulders == null || b.Flora.Boulders.Length == 0) return;

        var stone = b.Flora.Boulders[b.Rng.Next(b.Flora.Boulders.Length)];
        if (stone.Mesh == null || stone.Size < 0.0001f) return;

        float size = tall / stone.Size;
        float lift = stone.Foot > 0.9f ? 0f : stone.Foot * size;

        var go = new GameObject("Boulder");
        go.transform.SetParent(b.Root, false);
        go.transform.localPosition = new Vector3(foot.x, foot.y + 0.1f + lift, foot.z);
        go.transform.localRotation = Quaternion.Euler(0f, b.Rng.Next(360), 0f);
        go.transform.localScale = Vector3.one * size;

        go.AddComponent<MeshFilter>().sharedMesh = stone.Mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = b.Flora.Paint;

        if (tall > 0.7f)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(stone.Wide * 0.8f, stone.Size, stone.Wide * 0.8f);
            box.center = new Vector3(0f, stone.Size * 0.5f - (stone.Foot > 0.9f ? 0f : stone.Foot), 0f);
        }
    }

    /// <summary>One of the pack's mushrooms, stood on the ground at a height.</summary>
    private static void Mushroom(Job b, Vector3 at, float tall)
    {
        if (b.Flora == null || b.Flora.Mushrooms == null || b.Flora.Mushrooms.Length == 0) return;

        var sprout = b.Flora.Mushrooms[b.Rng.Next(b.Flora.Mushrooms.Length)];
        if (sprout.Mesh == null || sprout.Size < 0.0001f) return;

        float size = tall / sprout.Size;
        float lift = sprout.Foot > 0.9f ? 0f : sprout.Foot * size;

        var go = new GameObject("Toadstool");
        go.transform.SetParent(b.Root, false);
        go.transform.localPosition = new Vector3(at.x, Ground + 0.1f + lift, at.z);
        go.transform.localRotation = Quaternion.Euler(0f, b.Rng.Next(360), 0f);
        go.transform.localScale = Vector3.one * size;

        go.AddComponent<MeshFilter>().sharedMesh = sprout.Mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = b.Flora.Paint;

        if (tall > 1.5f)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, sprout.Size, 0.5f);
            box.center = new Vector3(0f, sprout.Size * 0.5f - (sprout.Foot > 0.9f ? 0f : sprout.Foot), 0f);
        }
    }

    /// <summary>The stair, its treads the thing you climb, so its mesh is the collider.</summary>
    private static void Stair(Job b, Vector3 at, float yaw)
    {
        var go = Piece(b, b.Shelf.Stair, at, yaw);
        if (go == null) return;

        var mf = go.GetComponentInChildren<MeshFilter>();
        if (mf != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }

    /// <summary>The flat plank slab, under a parent that may be tilted.</summary>
    private static void Slab(Job b, Transform under, Vector3 at, float yaw, Vector3 tilt = default)
    {
        Under(b, under, b.Shelf.Slab, at, yaw, tilt);
    }

    /// <summary>
    /// A fence round a square deck of a given half width, with a gap for a
    /// stair on the +x side (gap 1), the -x side (-1), or none (0).
    /// </summary>
    private static void Rail(Job b, float edge, float deck, int gap)
    {
        float rim = edge - 0.12f;

        for (float along = -edge + 0.5f; along <= edge - 0.4f; along += 0.86f)
        {
            bool middle = Mathf.Abs(along) < 1.25f;

            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, deck + FenceUp, rim), 0f);
            Piece(b, Pick(b.Shelf.Fences, b), new Vector3(along, deck + FenceUp, -rim), 0f);
            if (!(middle && gap < 0)) Piece(b, Pick(b.Shelf.Fences, b), new Vector3(-rim, deck + FenceUp, along), 90f);
            if (!(middle && gap > 0)) Piece(b, Pick(b.Shelf.Fences, b), new Vector3(rim, deck + FenceUp, along), 90f);
        }
    }

    /// <summary>A solid, invisible wall.</summary>
    private static void Wall(Transform root, Vector3 centre, Vector3 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(root, false);
        go.transform.localPosition = centre;
        go.AddComponent<BoxCollider>().size = size;
    }

    /// <summary>
    /// One of the pack's pieces under the root. The local transform is set
    /// outright rather than kept from the prefab: the stone tiles' carried
    /// where they stood in the pack's scene, and every one landed a field
    /// away.
    /// </summary>
    private static GameObject Piece(Job b, GameObject prefab, Vector3 at, float yaw)
    {
        return Piece(b, prefab, at, yaw, Vector3.zero, Vector3.zero, Vector3.zero);
    }

    private static GameObject Piece(Job b, GameObject prefab, Vector3 at, float yaw, Vector3 boxSize, Vector3 boxCentre)
    {
        return Piece(b, prefab, at, yaw, boxSize, boxCentre, Vector3.zero);
    }

    private static GameObject Piece(Job b, GameObject prefab, Vector3 at, float yaw, Vector3 boxSize, Vector3 boxCentre, Vector3 tilt)
    {
        return Under(b, b.Root, prefab, at, yaw, tilt, boxSize, boxCentre);
    }

    private static GameObject Under(Job b, Transform parent, GameObject prefab, Vector3 at, float yaw, Vector3 tilt = default, Vector3 boxSize = default, Vector3 boxCentre = default)
    {
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, parent, false);
        go.name = prefab.name;
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(tilt.x, yaw + tilt.y, tilt.z);
        go.transform.localScale = Vector3.one;

        if (boxSize.sqrMagnitude > 0f)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = boxSize;
            box.center = boxCentre;
        }

        return go;
    }

    /// <summary>The ground's block top at a point in the structure's frame, relative to the root.</summary>
    private static float GroundAt(Job b, float localX, float localZ)
    {
        Vector3 world = b.Root.TransformPoint(new Vector3(localX, 0f, localZ));
        int tx = Mathf.RoundToInt(world.x / Tile), tz = Mathf.RoundToInt(world.z / Tile);
        return WorldHeight.SurfaceY(tx, tz, b.Seed) - b.At.Position.y + Ground;
    }

    /// <summary>Which way 'ahead' is, in tiles.</summary>
    private static Vector2Int Dir(Job b)
    {
        Vector3 d = b.Root.rotation * Vector3.right;
        return new Vector2Int(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.z));
    }

    private static GameObject Pick(GameObject[] from, Job b)
    {
        return from == null || from.Length == 0 ? null : from[b.Rng.Next(from.Length)];
    }

    private static float Turn(Job b) => b.Rng.Next(4) * 90f;
}
