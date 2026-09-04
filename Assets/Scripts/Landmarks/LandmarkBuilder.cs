using System.Collections.Generic;
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
    /// A watchtower of three storeys on a stone plinth: a log store at the
    /// bottom, a timber-framed room above it, and an open lookout under a
    /// plank roof at the top with a lantern hung in it and a banner over.
    /// A log lodge beside it, a well, and a yard round the lot.
    /// </summary>
    private static void Watch(Job b)
    {
        const float Y = 0.45f;  // the yard, a court of packed earth
        const float B = 1.5f;   // the plinth top
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.85f, Weathering = WeatherAt(b) };

        Foundation(b, k, new Vector3(-9.8f, 0f, -6.6f), new Vector3(8.4f, 0f, 7.8f), Y, 1.4f, 1, false);
        Foundation(b, k, new Vector3(-3.3f, 0f, -3.3f), new Vector3(3.3f, 0f, 3.3f), B, B - Y + 0.2f);

        // the store: log walls, a door toward the steps
        float r = 0.17f;
        var c0 = new Vector3(-2.4f, B, -2.4f); var c1 = new Vector3(2.4f, B, -2.4f);
        var c2 = new Vector3(2.4f, B, 2.4f); var c3 = new Vector3(-2.4f, B, 2.4f);
        k.LogWall(c0, c1, 2.9f, 0f, r);
        k.LogWall(c3, c2, 2.9f, 0f, r);
        k.LogWall(c0, c3, 2.9f, r, r);
        k.LogWall(c1, new Vector3(2.4f, B, -0.7f), 2.9f, r, r);
        k.LogWall(new Vector3(2.4f, B, 0.7f), c2, 2.9f, r, r);
        k.LogWall(new Vector3(2.4f, B, -0.7f), new Vector3(2.4f, B, 0.7f), 2.9f, 2.2f, r);
        k.Door(new Vector3(2.4f + r, B, 0f), Vector3.right, 1.0f, 2.0f);

        // the room: timber frame on a plank floor that oversails the store
        float f1 = B + 2.9f;
        k.Block(new Vector3(0f, f1 + 0.1f, 0f), new Vector3(5.6f, 0.2f, 5.6f), Kit.Swatch.DarkWood, 0f, true);
        foreach (var (p, q) in new[] { (new Vector3(-2.6f, 0f, -2.6f), new Vector3(2.6f, 0f, -2.6f)), (new Vector3(2.6f, 0f, -2.6f), new Vector3(2.6f, 0f, 2.6f)),
                                       (new Vector3(2.6f, 0f, 2.6f), new Vector3(-2.6f, 0f, 2.6f)), (new Vector3(-2.6f, 0f, 2.6f), new Vector3(-2.6f, 0f, -2.6f)) })
            k.FrameWall(p + Vector3.up * (f1 + 0.2f), q + Vector3.up * (f1 + 0.2f), 2.6f, 0.28f);
        k.Window(new Vector3(2.6f + 0.16f, f1 + 1.6f, 0f), Vector3.right, 0.9f, 1.0f);
        k.Window(new Vector3(-2.6f - 0.16f, f1 + 1.6f, 0f), Vector3.left, 0.9f, 1.0f);
        k.Window(new Vector3(0f, f1 + 1.6f, 2.6f + 0.16f), Vector3.forward, 0.9f, 1.0f);
        k.Window(new Vector3(0f, f1 + 1.6f, -2.6f - 0.16f), Vector3.back, 0.9f, 1.0f);

        // the lookout: an open deck, posts, a railing, a plank roof over
        float f2 = f1 + 0.2f + 2.6f;
        k.Block(new Vector3(0f, f2 + 0.1f, 0f), new Vector3(6.4f, 0.2f, 6.4f), Kit.Swatch.Plank, 0f, true);
        // one corner of the lookout has gone: its post lies on the deck,
        // the rails there are gone, and the roof over it came down with it
        foreach (float x in new[] { -2.9f, 2.9f }) foreach (float z in new[] { -2.9f, 2.9f })
        {
            if (x > 0f && z > 0f) { k.Log(new Vector3(2.6f, f2 + 0.3f, 2.4f), new Vector3(0.4f, f2 + 0.3f, 0.8f), 0.13f, Kit.Swatch.OldWood, 7); continue; }
            k.Post(new Vector3(x, f2 + 0.2f, z), 2.6f, 0.13f, Kit.Swatch.Wood);
        }
        k.Railing(new Vector3(-3.1f, f2 + 0.2f, -3.1f), new Vector3(3.1f, f2 + 0.2f, -3.1f), 1.0f);
        k.Railing(new Vector3(3.1f, f2 + 0.2f, -3.1f), new Vector3(3.1f, f2 + 0.2f, 0.4f), 1.0f);
        k.Railing(new Vector3(0.2f, f2 + 0.2f, 3.1f), new Vector3(-3.1f, f2 + 0.2f, 3.1f), 1.0f);
        k.Railing(new Vector3(-3.1f, f2 + 0.2f, 3.1f), new Vector3(-3.1f, f2 + 0.2f, -3.1f), 1.0f);
        var eave = new Vector3(0f, f2 + 2.8f, 0f);
        k.Roof(eave, 6.2f, 6.2f, 28f, Kit.Builder.RoofStyle.Plank, 0.5f);
        // the fallen quarter of it, on the deck and over the edge
        k.Block(new Vector3(2.2f, f2 + 0.9f, 2.0f), new Vector3(2.6f, 0.1f, 2.4f), Quaternion.Euler(-22f, 15f, 30f), Kit.Swatch.OldWood);
        k.Block(new Vector3(4.0f, Y + 0.5f, 3.6f), new Vector3(2.2f, 0.1f, 1.8f), Quaternion.Euler(12f, -30f, 8f), Kit.Swatch.OldWood);
        k.Debris(new Vector3(3.6f, Y, 3.0f), 1.6f, 5);
        k.Block(eave + Vector3.down * 0.35f, new Vector3(0.28f, 0.34f, 0.28f), Kit.Swatch.Pane);
        k.Block(eave + Vector3.down * 0.16f, new Vector3(0.34f, 0.05f, 0.34f), Kit.Swatch.Iron);
        k.Banner(eave + new Vector3(0f, 1.9f, 0f), 2.6f, 0f);

        // the lodge beside, a log hut with a plank roof and a chimney
        var l0 = new Vector3(-7.4f, B, -1.6f); var l1 = new Vector3(-4.2f, B, -1.6f);
        var l2 = new Vector3(-4.2f, B, 2.6f); var l3 = new Vector3(-7.4f, B, 2.6f);
        Foundation(b, k, new Vector3(-8.2f, 0f, -2.4f), new Vector3(-3.4f, 0f, 3.4f), B, B - Y + 0.2f, 0);
        k.LogWall(l0, l1, 2.4f, 0f, r); k.LogWall(l3, l2, 2.4f, 0f, r);
        k.LogWall(l0, l3, 2.4f, r, r);
        k.LogWall(l1, new Vector3(-4.2f, B, -0.1f), 2.4f, r, r);
        k.LogWall(new Vector3(-4.2f, B, 1.1f), l2, 2.4f, r, r);
        k.LogWall(new Vector3(-4.2f, B, -0.1f), new Vector3(-4.2f, B, 1.1f), 2.4f, 1.95f, r);
        k.Door(new Vector3(-4.2f + r, B, 0.5f), Vector3.right, 0.95f, 1.85f);
        k.Window(new Vector3(-5.8f, B + 1.4f, 2.6f + r + 0.02f), Vector3.forward, 0.8f, 0.8f);
        var lodgeEave = new Vector3(-5.8f, B + 2.4f, 0.5f);
        k.GableEnd(lodgeEave, 3.2f + 2f * r, 38f, -2.1f - r, Kit.Swatch.Plaster, 0.3f);
        k.GableEnd(lodgeEave, 3.2f + 2f * r, 38f, 2.1f + r, Kit.Swatch.Plaster, 0.3f);
        k.Roof(lodgeEave, 3.2f + 2f * r, 4.2f + 2f * r, 38f, Kit.Builder.RoofStyle.Plank, 0.5f);
        k.Chimney(new Vector3(-7.4f - r - 0.36f, B, 1.6f), 4.6f, 0.7f);

        // the yard
        k.Well(new Vector3(1.6f, Y, 6.2f));
        k.Woodpile(new Vector3(-6.0f, Y, 5.4f), 1.8f, 4, 0f);
        k.Barrel(new Vector3(4.6f, Y, -3.2f), 0.32f, 0.9f);
        k.Barrel(new Vector3(5.3f, Y, -2.7f), 0.28f, 0.8f);
        k.Crate(new Vector3(4.8f, Y, -4.2f), 0.7f);
        k.Lantern(new Vector3(6.4f, Y, 1.6f), 2.6f, 180f);
        k.Lantern(new Vector3(-2.2f, Y, -5.6f), 2.4f, 0f);
        k.HangingSign(new Vector3(7.2f, Y, -1.4f), 2.7f, 90f);
        k.Trough(new Vector3(-1.6f, Y, 6.4f), 1.8f, 0f);
        k.HayBale(new Vector3(4.0f, Y, 5.6f), 25f);
        k.Cartwheel(new Vector3(-8.6f, Y, -3.3f), 0.55f, 20f);
        k.Railing(new Vector3(-9.6f, Y, -6.4f), new Vector3(8.2f, Y, -6.4f), 1.0f);
        k.Railing(new Vector3(-9.6f, Y, 7.6f), new Vector3(8.2f, Y, 7.6f), 1.0f);
        k.Railing(new Vector3(-9.6f, Y, -6.4f), new Vector3(-9.6f, Y, 7.6f), 1.0f);
        k.Railing(new Vector3(8.2f, Y, 1.2f), new Vector3(8.2f, Y, 7.6f), 1.0f);
        k.Railing(new Vector3(8.2f, Y, -6.4f), new Vector3(8.2f, Y, -1.2f), 1.0f);
        k.Pavers(new Vector3(6.6f, Y, 0f), 5.2f, 2.0f, 0.7f);

        // what has come down, and what has come up
        k.Debris(new Vector3(4.4f, Y, 2.4f), 2.4f, 6);
        k.Debris(new Vector3(-6.0f, Y, -3.6f), 1.8f, 4);
        k.Rubble(new Vector3(3.9f, Y, -2.8f), 1.2f, 5);
        for (int i = 0; i < 10; i++) k.Tuft(new Vector3(b.Rng.Next(-90, 80) * 0.1f, Y, b.Rng.Next(-60, 70) * 0.1f), 0.5f);

        k.Finish("Watch", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // -------------------------------------------------------------- Shipwreck

    /// <summary>
    /// A ship broken on the beach, bow to the shore: a keel and ribs, plank
    /// sides in a curve, a deck with a stern cabin on it, the mast snapped
    /// over with the yard and a torn sail hanging, all heeled over the way
    /// she settled. What was in her is up on the sand.
    /// </summary>
    private static void Wreck(Job b)
    {
        float water = WaterSurface.Level - b.At.Position.y;
        var k = new Kit.Builder(b.Rng.Next());

        // the hull in her own frame: x runs bow to stern, +x the bow
        float length = 14f, beam = 4.6f, depth = 2.2f;
        int ribs = 11;

        k.Log(new Vector3(-length * 0.5f, 0f, 0f), new Vector3(length * 0.5f + 0.8f, 0.6f, 0f), 0.2f, Kit.Swatch.DarkWood, 7);
        for (int i = 0; i < ribs; i++)
        {
            float x = -length * 0.5f + 0.8f + i * (length - 1.6f) / (ribs - 1);
            float t = Mathf.Abs(x) / (length * 0.5f);
            float half = beam * 0.5f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - t * t * 0.85f));
            foreach (float side in new[] { -1f, 1f })
            {
                var foot = new Vector3(x, 0.1f, 0f);
                var top = new Vector3(x, depth, side * half);
                k.Log(foot, top, 0.11f, Kit.Swatch.DarkWood, 6);
            }
        }

        // the planking: strakes along the hull, each a run of blocks following the curve
        for (int strake = 0; strake < 5; strake++)
        {
            float y = 0.45f + strake * 0.42f;
            for (int i = 0; i < ribs - 1; i++)
            {
                float x0 = -length * 0.5f + 0.8f + i * (length - 1.6f) / (ribs - 1);
                float x1 = x0 + (length - 1.6f) / (ribs - 1);
                foreach (float side in new[] { -1f, 1f })
                {
                    float h0 = beam * 0.5f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(x0 / (length * 0.5f), 2f) * 0.85f)) * (y / depth * 0.35f + 0.65f);
                    float h1 = beam * 0.5f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(x1 / (length * 0.5f), 2f) * 0.85f)) * (y / depth * 0.35f + 0.65f);
                    var p0 = new Vector3(x0, y, side * h0); var p1 = new Vector3(x1, y, side * h1);
                    if (strake == 4 && i > 2 && i < 5 && side > 0f) continue;   // the hole in her side
                    var mid = (p0 + p1) * 0.5f;
                    var along = (p1 - p0).normalized;
                    var facing = Vector3.Cross(along, Vector3.up) * side;
                    k.Block(mid, new Vector3(Vector3.Distance(p0, p1) + 0.04f, 0.4f, 0.1f), Quaternion.LookRotation(facing, Vector3.up), strake % 2 == 0 ? Kit.Swatch.Wood : Kit.Swatch.Plank);
                }
            }
        }

        // the deck, the cabin at the stern, the mast and its yard and sail
        k.Block(new Vector3(0.4f, depth - 0.05f, 0f), new Vector3(length - 2.4f, 0.12f, beam - 0.9f), Kit.Swatch.Plank, 0f, true);
        k.Railing(new Vector3(-4.0f, depth, beam * 0.5f - 0.6f), new Vector3(4.5f, depth, beam * 0.5f - 0.6f), 0.9f);
        k.Railing(new Vector3(-4.0f, depth, -beam * 0.5f + 0.6f), new Vector3(1.0f, depth, -beam * 0.5f + 0.6f), 0.9f);
        k.PlankWall(new Vector3(-5.6f, depth, -1.5f), new Vector3(-5.6f, depth, 1.5f), 1.7f, 0.08f);
        k.PlankWall(new Vector3(-5.6f, depth, 1.5f), new Vector3(-3.2f, depth, 1.5f), 1.7f, 0.08f);
        k.PlankWall(new Vector3(-3.2f, depth, -1.5f), new Vector3(-5.6f, depth, -1.5f), 1.7f, 0.08f);
        k.Door(new Vector3(-3.2f, depth, 0f), Vector3.right, 0.9f, 1.6f);
        k.Block(new Vector3(-4.4f, depth + 1.75f, 0f), new Vector3(2.8f, 0.1f, 3.4f), Kit.Swatch.DarkWood);
        k.Log(new Vector3(1.2f, depth - 0.2f, 0f), new Vector3(1.6f, depth + 5.2f, 0.3f), 0.17f, Kit.Swatch.DarkWood, 8);
        k.Log(new Vector3(1.6f, depth + 5.2f, 0.3f), new Vector3(4.6f, depth + 3.2f, 2.6f), 0.13f, Kit.Swatch.DarkWood, 7);
        k.Log(new Vector3(2.0f, depth + 4.4f, -2.2f), new Vector3(1.4f, depth + 4.6f, 2.4f), 0.09f, Kit.Swatch.DarkWood, 6);
        k.Block(new Vector3(1.3f, depth + 3.2f, -0.6f), new Vector3(0.04f, 2.4f, 3.0f), Quaternion.Euler(0f, 0f, 8f), Kit.Swatch.Plaster);
        k.Block(new Vector3(1.4f, depth + 2.1f, -1.6f), new Vector3(0.04f, 1.2f, 1.2f), Quaternion.Euler(0f, 10f, -12f), Kit.Swatch.Plaster);
        k.Barrel(new Vector3(3.6f, depth + 0.02f, -1.2f), 0.3f, 0.85f);
        k.Crate(new Vector3(-1.6f, depth + 0.02f, 1.2f), 0.6f);

        // heeled over as she settled, and set so her keel is on the bed
        var hull = k.Finish("Hull", b.Root, new Vector3(0f, Ground + 0.1f, 0f), b.Flora.Paint);
        hull.transform.localRotation = Quaternion.Euler(14f, 0f, -6f);

        // what washed up, on the first dry ground ahead
        int shore = 1;
        while (shore < 5 && WaterSurface.IsUnderwater(b.At.TileX + Dir(b).x * shore, b.At.TileZ + Dir(b).y * shore, b.Seed)) shore++;
        float sx = shore * Tile + 4f;

        var beach = new Kit.Builder(b.Rng.Next());
        beach.Barrel(new Vector3(sx + 0.3f, GroundAt(b, sx + 0.3f, -2.4f) + 0.1f, -2.4f), 0.32f, 0.9f);
        beach.Barrel(new Vector3(sx + 1.2f, GroundAt(b, sx + 1.2f, -1.8f) + 0.1f, -1.8f), 0.28f, 0.8f);
        beach.Crate(new Vector3(sx + 0.6f, GroundAt(b, sx + 0.6f, 1.6f) + 0.1f, 1.6f), 0.7f);
        beach.Crate(new Vector3(sx + 1.6f, GroundAt(b, sx + 1.6f, 2.4f) + 0.1f, 2.4f), 0.55f);
        beach.Log(new Vector3(sx + 2.4f, GroundAt(b, sx + 2.4f, 3.4f) + 0.3f, 3.4f), new Vector3(sx + 4.6f, GroundAt(b, sx + 4.6f, 4.0f) + 0.3f, 4.0f), 0.2f, Kit.Swatch.DarkWood, 7);
        beach.Cartwheel(new Vector3(sx + 3.0f, GroundAt(b, sx + 3.0f, -3.2f) + 0.1f, -3.2f), 0.5f, 40f);
        beach.HangingSign(new Vector3(sx + 2.2f, GroundAt(b, sx + 2.2f, -0.2f) + 0.1f, -0.2f), 2.6f, 180f);
        beach.Lantern(new Vector3(sx + 3.6f, GroundAt(b, sx + 3.6f, 1.0f) + 0.1f, 1.0f), 2.2f, 180f);
        beach.Finish("Beach", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // -------------------------------------------------------------- Sand Gate

    /// <summary>
    /// A gatehouse across the way: two round towers with battlements, a
    /// curtain wall between them with a walk along its top, and the gateway
    /// through the middle with its doors swung open. Behind, a flagged court
    /// with a well, a guard's hut and what the last caravan left. The wall
    /// runs across z; the way through runs along x.
    /// </summary>
    private static void Gate(Job b)
    {
        const float B = 0.5f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-8.5f, 0f, -9.5f), new Vector3(3.0f, 0f, 9.5f), B, 1.3f, 1);

        // the towers
        float towerR = 2.2f, towerH = 9f;
        foreach (float z in new[] { -6.0f, 6.0f })
        {
            k.RoundTower(new Vector3(0f, B, z), towerR, towerH, true, Kit.Builder.RoofStyle.Slate, 0.5f);
            k.Door(new Vector3(-towerR - 0.06f, B, z), Vector3.left, 1.0f, 2.0f);
            k.Banner(new Vector3(0f, B + towerH + 0.3f, z), 2.8f, 90f);
        }

        // the curtain wall in two runs either side of the gateway, and the
        // lintel wall over it; a walk along the top behind the merlons
        float wallH = 5.2f, gate = 1.8f, lintel = 3.6f;
        k.StoneWall(new Vector3(0f, B, -6.0f + towerR - 0.3f), new Vector3(0f, B, -gate), wallH, 1.2f);
        k.StoneWall(new Vector3(0f, B, gate), new Vector3(0f, B, 6.0f - towerR + 0.3f), wallH, 1.2f);
        k.StoneWall(new Vector3(0f, B + lintel, -gate - 0.1f), new Vector3(0f, B + lintel, gate + 0.1f), wallH - lintel, 1.2f);
        k.Block(new Vector3(0f, B + lintel - 0.12f, 0f), new Vector3(1.4f, 0.24f, gate * 2f + 0.2f), Kit.Swatch.DarkWood);
        k.Block(new Vector3(0f, B + wallH + 0.1f, 0f), new Vector3(1.6f, 0.2f, 12f - 2f * towerR + 0.6f), Kit.Swatch.Plank, 0f, true);
        k.MerlonsAlong(new Vector3(0.7f, B + wallH + 0.2f, -6.0f + towerR - 0.3f), new Vector3(0.7f, B + wallH + 0.2f, 6.0f - towerR + 0.3f), 0.36f);
        k.Railing(new Vector3(-0.75f, B + wallH + 0.2f, -6.0f + towerR - 0.3f), new Vector3(-0.75f, B + wallH + 0.2f, 6.0f - towerR + 0.3f), 1.0f);

        // the doors, swung open into the court
        k.Door(new Vector3(-0.5f, B, -gate + 0.1f), Quaternion.Euler(0f, -55f, 0f) * Vector3.right, 1.7f, 3.3f);
        k.Door(new Vector3(-0.5f, B, gate - 0.1f), Quaternion.Euler(0f, 55f, 0f) * Vector3.right, 1.7f, 3.3f);

        // the court behind the wall
        k.Well(new Vector3(-5.2f, B + 0.1f, -4.6f));
        var g0 = new Vector3(-7.6f, B, 2.6f); var g1 = new Vector3(-4.2f, B, 2.6f);
        var g2 = new Vector3(-4.2f, B, 6.4f); var g3 = new Vector3(-7.6f, B, 6.4f);
        k.StoneWall(g0, g1, 2.5f, 0.4f); k.StoneWall(g1, g2, 2.5f, 0.4f); k.StoneWall(g2, g3, 2.5f, 0.4f); k.StoneWall(g3, g0, 2.5f, 0.4f);
        k.Door(new Vector3(-4.2f + 0.22f, B, 4.5f), Vector3.right, 1.0f, 1.9f);
        k.Window(new Vector3(-5.9f, B + 1.5f, 2.6f - 0.22f), Vector3.back, 0.8f, 0.8f);
        var hutEave = new Vector3(-5.9f, B + 2.5f, 4.5f);
        k.GableEnd(hutEave, 3.4f + 0.4f, 30f, -1.9f - 0.2f, Kit.Swatch.Plaster, 0.36f);
        k.GableEnd(hutEave, 3.4f + 0.4f, 30f, 1.9f + 0.2f, Kit.Swatch.Plaster, 0.36f);
        k.Roof(hutEave, 3.4f + 0.4f, 3.8f + 0.4f, 30f, Kit.Builder.RoofStyle.Plank, 0.45f);

        k.Lantern(new Vector3(-1.6f, B + 0.1f, -3.2f), 2.6f, 90f);
        k.Lantern(new Vector3(-1.6f, B + 0.1f, 3.2f), 2.6f, 270f);
        k.Lantern(new Vector3(1.8f, B + 0.1f, -3.4f), 2.6f, 270f);
        k.Lantern(new Vector3(1.8f, B + 0.1f, 3.4f), 2.6f, 90f);
        k.Barrel(new Vector3(-2.4f, B + 0.1f, -7.6f), 0.32f, 0.9f);
        k.Barrel(new Vector3(-3.1f, B + 0.1f, -8.0f), 0.28f, 0.8f);
        k.Crate(new Vector3(-2.6f, B + 0.1f, -6.6f), 0.7f);
        k.Crate(new Vector3(-2.6f, B + 0.8f, -6.6f), 0.55f);
        k.Trough(new Vector3(-6.4f, B + 0.1f, -0.6f), 2.0f, 90f);
        k.HayBale(new Vector3(-7.4f, B + 0.1f, -7.2f), 20f);
        k.HayBale(new Vector3(-7.3f, B + 0.1f, -8.1f), 75f);
        k.Cartwheel(new Vector3(-7.9f, B + 0.1f, 0.6f), 0.55f, 15f);
        k.HangingSign(new Vector3(4.0f, Ground, -3.0f), 2.7f, 180f);
        k.Table(new Vector3(-3.0f, B + 0.1f, 7.6f), 1.6f, 0.8f, 0.8f);
        k.Bench(new Vector3(-3.0f, B + 0.1f, 6.8f), 1.4f);

        k.Finish("Gate", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // --------------------------------------------------------- Trapper's Cabin

    /// <summary>
    /// A log cabin on a flagged stone foundation: walls of stacked logs that
    /// cross at the corners, a plank roof pitched over plaster gables with a
    /// stone chimney through it, a porch along the front under its own
    /// lean-to, windows either side of the door. Ahead of it a fenced yard
    /// with the woodpile, the trough, the hay and a lantern, and a flagged
    /// path from the door to the gate.
    /// </summary>
    private static void Cabin(Job b)
    {
        // The foundation stands clear of the snow, which lies 0.17 above the
        // ground: anything lower and the snow comes up through the floor.
        const float B = 0.30f;
        var k = new Kit.Builder(b.Rng.Next()) { Decay = 0.7f, Weathering = WeatherAt(b) };

        Foundation(b, k, new Vector3(-4.6f, 0f, -4.4f), new Vector3(4.6f, 0f, 4.4f), B);

        // the walls, in a rectangle longer in z so the ridge runs that way
        float wallHeight = 2.7f, r = 0.17f;
        var bl = new Vector3(-2.6f, B, -3.4f); var br = new Vector3(2.6f, B, -3.4f);
        var fl = new Vector3(-2.6f, B, 2.2f); var fr = new Vector3(2.6f, B, 2.2f);

        k.LogWall(bl, br, wallHeight, 0f, r);                 // back gable wall
        k.LogWall(fl, fr, wallHeight, 0f, r);                 // front gable wall
        k.LogWall(bl, fl, wallHeight, r, r);                  // left, half a log up
        // the right wall is the door wall: two runs and a header over the gap
        k.LogWall(br, new Vector3(2.6f, B, -0.75f), wallHeight, r, r);
        k.LogWall(new Vector3(2.6f, B, 0.75f), fr, wallHeight, r, r);
        k.LogWall(new Vector3(2.6f, B, -0.75f), new Vector3(2.6f, B, 0.75f), wallHeight, 2.05f, r);

        k.Door(new Vector3(2.6f + r, B, 0f), Vector3.right, 1.0f, 1.95f);
        k.Window(new Vector3(2.6f + r + 0.02f, B + 1.55f, -2.1f), Vector3.right, 0.8f, 0.8f);
        k.Window(new Vector3(2.6f + r + 0.02f, B + 1.55f, 1.6f), Vector3.right, 0.8f, 0.8f);
        k.Window(new Vector3(0.6f, B + 1.55f, -3.4f - r - 0.02f), Vector3.back, 0.8f, 0.8f);
        k.Window(new Vector3(-2.6f - r - 0.02f, B + 1.55f, -0.6f), Vector3.left, 0.8f, 0.8f);

        // the roof: ridge along z, eaves at the wall tops, plaster gables
        var eave = new Vector3(0f, B + wallHeight, -0.6f);
        k.GableEnd(eave, 5.2f + 2f * r, 35f, -3.4f - r, Kit.Swatch.Plaster, 0.34f);
        k.GableEnd(eave, 5.2f + 2f * r, 35f, 2.2f + r, Kit.Swatch.Plaster, 0.34f);
        k.Roof(eave, 5.2f + 2f * r, 5.6f + 2f * r, 35f, Kit.Builder.RoofStyle.Plank, 0.6f);

        k.Chimney(new Vector3(-1.7f, B, -3.4f - r - 0.36f), 5.6f, 0.8f);

        // the porch: posts and a lean-to off the door wall
        foreach (float z in new[] { -2.9f, -0.9f, 1.1f, 2.7f })
            k.Post(new Vector3(4.3f, B, z), 2.35f, 0.11f, Kit.Swatch.Wood);
        k.Block(new Vector3(3.55f, B + 2.62f, -0.1f), new Vector3(2.3f, 0.1f, 6.2f), Quaternion.Euler(0f, 0f, 16f), Kit.Swatch.Plank);
        for (float z = -2.9f; z <= 2.7f; z += 0.8f)
            k.Block(new Vector3(3.55f, B + 2.68f, z), new Vector3(2.2f, 0.05f, 0.1f), Quaternion.Euler(0f, 0f, 16f), Kit.Swatch.DarkWood);
        k.Log(new Vector3(4.3f, B + 2.3f, -3.0f), new Vector3(4.3f, B + 2.3f, 2.8f), 0.07f, Kit.Swatch.DarkWood, 6);
        k.Block(new Vector3(3.5f, B + 0.05f, -0.1f), new Vector3(1.9f, 0.1f, 6.0f), Kit.Swatch.Plank);
        k.Debris(new Vector3(6.4f, B, -1.2f), 2.0f, 5);
        k.Rubble(new Vector3(-3.0f, B, 4.0f), 1.0f, 4);
        k.Bench(new Vector3(3.6f, B + 0.1f, 1.9f), 1.3f);
        k.Barrel(new Vector3(3.5f, B + 0.1f, -2.6f), 0.3f, 0.85f);
        k.Crate(new Vector3(3.6f, B + 0.1f, -1.7f), 0.6f);

        // the yard, ahead, fenced with a gap at the path
        k.Railing(new Vector3(4.6f, B, -4.4f), new Vector3(9.6f, B, -4.4f), 1.0f);
        k.Railing(new Vector3(4.6f, B, 4.4f), new Vector3(9.6f, B, 4.4f), 1.0f);
        k.Railing(new Vector3(9.6f, B, -4.4f), new Vector3(9.6f, B, -0.9f), 1.0f);
        k.Railing(new Vector3(9.6f, B, 0.9f), new Vector3(9.6f, B, 4.4f), 1.0f);
        k.Pavers(new Vector3(7.1f, B, 0f), 5.4f, 1.3f, 0.6f);

        k.Woodpile(new Vector3(7.6f, B, -3.2f), 1.8f, 4, 0f);
        k.Woodpile(new Vector3(5.6f, B, -3.4f), 1.4f, 3, 0f);
        k.Trough(new Vector3(6.6f, B, 3.4f), 1.8f, 0f);
        k.HayBale(new Vector3(8.6f, B, 3.0f), 20f);
        k.HayBale(new Vector3(8.5f, B, 2.2f), 70f);
        k.HayBale(new Vector3(8.55f, B + 0.6f, 2.6f), 40f);
        k.Lantern(new Vector3(4.9f, B, 3.9f), 2.4f, 180f);
        k.Lantern(new Vector3(9.3f, B, -1.3f), 2.2f, 90f);
        k.HangingSign(new Vector3(10.2f, B, 1.4f), 2.6f, 180f);
        k.Cartwheel(new Vector3(-3.7f, B, 1.4f), 0.55f, 80f);
        k.Ladder(new Vector3(-3.35f, B, -1.8f), 2.9f, 270f, 14f);

        k.Finish("Cabin", b.Root, Vector3.zero, b.Flora.Paint);

        // a flagged way out to the gate and beyond it
        Piece(b, b.Shelf.Signboard, new Vector3(11.2f, Ground + SignUp, -1.8f), 0f);
    }

    // ---------------------------------------------------------- Fishing Jetty

    /// <summary>
    /// A long plank pier on posts out over the lake, railed both sides, with
    /// a square landing at the end under a lantern; on the shore a log hut
    /// with a thatched roof, and the fisherman's things. +x is the water.
    /// </summary>
    private static void Jetty(Job b)
    {
        float water = WaterSurface.Level - b.At.Position.y;
        float deck = Mathf.Max(water + 0.55f, 0.25f);
        var k = new Kit.Builder(b.Rng.Next());

        // the pier: a deck of planks from the shore out, posts to the bed
        float length = 15f, width = 2.4f;
        k.Block(new Vector3(length * 0.5f + 0.4f, deck - 0.08f, 0f), new Vector3(length, 0.16f, width), Kit.Swatch.Plank, 0f, true);
        for (float x = 0.6f; x < length + 0.4f; x += 0.42f)
            k.Block(new Vector3(x, deck + 0.005f, 0f), new Vector3(0.36f, 0.03f, width - 0.06f), (x * 7f) % 2f < 1f ? Kit.Swatch.Wood : Kit.Swatch.Plank);
        for (float x = 1.0f; x <= length; x += 2.4f)
        foreach (float z in new[] { -width * 0.5f + 0.1f, width * 0.5f - 0.1f })
            k.Log(new Vector3(x, deck - 3.0f, z), new Vector3(x, deck + 0.02f, z), 0.11f, Kit.Swatch.DarkWood, 7);
        k.Railing(new Vector3(0.6f, deck, width * 0.5f), new Vector3(length - 0.2f, deck, width * 0.5f), 1.0f);
        k.Railing(new Vector3(0.6f, deck, -width * 0.5f), new Vector3(length - 0.2f, deck, -width * 0.5f), 1.0f);

        // the landing at the end, wider, with a lantern and a bench
        var end = new Vector3(length + 0.4f + 2.0f, deck, 0f);
        k.Block(end + Vector3.down * 0.08f, new Vector3(4.4f, 0.16f, 5.6f), Kit.Swatch.Plank, 0f, true);
        for (float z = -2.6f; z < 2.8f; z += 0.42f)
            k.Block(end + new Vector3(0f, 0.005f, z), new Vector3(4.3f, 0.03f, 0.36f), (z * 5f) % 2f < 1f ? Kit.Swatch.Wood : Kit.Swatch.Plank);
        foreach (float x in new[] { -1.6f, 1.6f }) foreach (float z in new[] { -2.4f, 2.4f })
            k.Log(end + new Vector3(x, -3.0f, z), end + new Vector3(x, 0.02f, z), 0.11f, Kit.Swatch.DarkWood, 7);
        k.Railing(end + new Vector3(-2.2f, 0f, -2.8f), end + new Vector3(2.2f, 0f, -2.8f), 1.0f);
        k.Railing(end + new Vector3(2.2f, 0f, -2.8f), end + new Vector3(2.2f, 0f, 2.8f), 1.0f);
        k.Railing(end + new Vector3(2.2f, 0f, 2.8f), end + new Vector3(-2.2f, 0f, 2.8f), 1.0f);
        k.Railing(end + new Vector3(-2.2f, 0f, 2.8f), end + new Vector3(-2.2f, 0f, 1.4f), 1.0f);
        k.Railing(end + new Vector3(-2.2f, 0f, -1.4f), end + new Vector3(-2.2f, 0f, -2.8f), 1.0f);
        k.Lantern(end + new Vector3(1.8f, 0f, 2.4f), 2.6f, 200f);
        k.Bench(end + new Vector3(1.6f, 0f, -1.2f), 1.3f);
        k.Barrel(end + new Vector3(-1.5f, 0f, -2.2f), 0.28f, 0.8f);
        k.Crate(end + new Vector3(-1.4f, 0f, 2.1f), 0.55f);

        // steps up from the shore
        int flight = Mathf.Max(1, Mathf.CeilToInt((deck - Ground) / 0.19f));
        k.Steps(new Vector3(0.6f - flight * 0.36f, Ground, 0f), Vector3.right, flight, (deck - Ground) / flight, 0.36f, 2.4f);

        // the hut on the shore, logs under thatch
        const float B = 0.25f;
        float r = 0.16f;
        Foundation(b, k, new Vector3(-7.0f, 0f, -4.6f), new Vector3(-0.6f, 0f, 4.6f), B, 1.0f);
        var h0 = new Vector3(-6.2f, B, 0.4f); var h1 = new Vector3(-2.2f, B, 0.4f);
        var h2 = new Vector3(-2.2f, B, 3.8f); var h3 = new Vector3(-6.2f, B, 3.8f);
        k.LogWall(h0, h1, 2.3f, 0f, r); k.LogWall(h3, h2, 2.3f, 0f, r);
        k.LogWall(h0, h3, 2.3f, r, r);
        k.LogWall(h1, new Vector3(-2.2f, B, 1.5f), 2.3f, r, r);
        k.LogWall(new Vector3(-2.2f, B, 2.7f), h2, 2.3f, r, r);
        k.LogWall(new Vector3(-2.2f, B, 1.5f), new Vector3(-2.2f, B, 2.7f), 2.3f, 1.9f, r);
        k.Door(new Vector3(-2.2f + r, B, 2.1f), Vector3.right, 0.95f, 1.8f);
        k.Window(new Vector3(-4.2f, B + 1.35f, 0.4f - r - 0.02f), Vector3.back, 0.8f, 0.7f);
        var hutEave = new Vector3(-4.2f, B + 2.3f, 2.1f);
        k.GableEnd(hutEave, 4.0f + 2f * r, 42f, -1.7f - r, Kit.Swatch.Plaster, 0.3f);
        k.GableEnd(hutEave, 4.0f + 2f * r, 42f, 1.7f + r, Kit.Swatch.Plaster, 0.3f);
        k.Roof(hutEave, 4.0f + 2f * r, 3.4f + 2f * r, 42f, Kit.Builder.RoofStyle.Thatch, 0.6f);

        // and the shore
        k.Table(new Vector3(-4.6f, B + 0.1f, -2.2f), 1.6f, 0.8f, 0.8f);
        k.Bench(new Vector3(-4.6f, B + 0.1f, -3.0f), 1.4f);
        k.Barrel(new Vector3(-1.6f, B + 0.1f, -3.4f), 0.32f, 0.9f);
        k.Barrel(new Vector3(-1.0f, B + 0.1f, -2.8f), 0.28f, 0.8f);
        k.Crate(new Vector3(-2.4f, B + 0.1f, -3.8f), 0.6f);
        k.Woodpile(new Vector3(-6.2f, B + 0.1f, -3.6f), 1.4f, 3, 90f);
        k.Lantern(new Vector3(-1.2f, B + 0.1f, 4.0f), 2.4f, 90f);
        k.HangingSign(new Vector3(-7.6f, Ground, -1.0f), 2.6f, 90f);
        k.Cartwheel(new Vector3(-6.6f, B + 0.1f, 4.2f), 0.5f, 30f);

        k.Finish("Jetty", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------- Stepped Altar

    /// <summary>
    /// A stepped pyramid of coursed stone, three tiers, a flight of stairs up
    /// the front face from the ground to the top, where a standing stone
    /// three times your height stands between four fires. Lesser stones at
    /// the foot of the stair, and flags round about.
    /// </summary>
    private static void Altar(Job b)
    {
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-8.5f, 0f, -8.5f), new Vector3(8.5f, 0f, 8.5f), 0.3f, 1.2f, 0);

        float[] half = { 6.4f, 4.4f, 2.4f };
        float rise = 1.7f;
        float floor = 0.3f;

        for (int t = 0; t < 3; t++)
        {
            float h = half[t];
            float top = floor + rise;
            k.Block(new Vector3(0f, (floor + top) * 0.5f, 0f), new Vector3(h * 2f, rise, h * 2f), Kit.Swatch.Mortar, 0f, true);
            k.StoneWall(new Vector3(-h, floor, -h), new Vector3(h, floor, -h), rise, 0.4f);
            k.StoneWall(new Vector3(h, floor, -h), new Vector3(h, floor, h), rise, 0.4f);
            k.StoneWall(new Vector3(h, floor, h), new Vector3(-h, floor, h), rise, 0.4f);
            k.StoneWall(new Vector3(-h, floor, h), new Vector3(-h, floor, -h), rise, 0.4f);
            k.Pavers(new Vector3(0f, top, 0f), h * 2f - 0.3f, h * 2f - 0.3f, 0.85f);

            // the stair up this tier's front face, cut into the terrace below
            int steps = Mathf.CeilToInt(rise / 0.21f);
            k.Steps(new Vector3(h + steps * 0.34f, floor, 0f), Vector3.left, steps, rise / steps, 0.34f, 2.6f);
            k.Railing(new Vector3(h + steps * 0.34f, floor, -1.4f), new Vector3(h, top, -1.4f), 0.9f);
            k.Railing(new Vector3(h + steps * 0.34f, floor, 1.4f), new Vector3(h, top, 1.4f), 0.9f);

            floor = top;
        }

        // the top: the stone, and four fires
        Standing(b, new Vector3(0f, floor, 0f), 4.2f);
        foreach (float x in new[] { -1.7f, 1.7f }) foreach (float z in new[] { -1.7f, 1.7f })
        {
            k.Block(new Vector3(x, floor + 0.25f, z), new Vector3(0.8f, 0.5f, 0.8f), Kit.Swatch.DarkStone, 0.01f);
            k.Block(new Vector3(x, floor + 0.62f, z), new Vector3(0.5f, 0.3f, 0.5f), Kit.Swatch.Thatch, 0.06f);
            k.Block(new Vector3(x, floor + 0.85f, z), new Vector3(0.28f, 0.3f, 0.28f), Kit.Swatch.Cloth, 0.05f);
        }

        // lesser stones at the foot of the stair, and a lamp either side
        Standing(b, new Vector3(9.6f, 0.3f, 2.4f), 2.2f);
        Standing(b, new Vector3(9.6f, 0.3f, -2.4f), 2.2f);
        Standing(b, new Vector3(7.4f, 0.3f, 4.6f), 1.6f);
        Standing(b, new Vector3(7.4f, 0.3f, -4.6f), 1.6f);
        k.Lantern(new Vector3(8.4f, 0.3f, 3.6f), 2.4f, 270f);
        k.Lantern(new Vector3(8.4f, 0.3f, -3.6f), 2.4f, 90f);
        k.HangingSign(new Vector3(11.0f, Ground, -1.9f), 2.6f, 180f);

        k.Finish("Altar", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // --------------------------------------------------------- Toadstool Ring

    /// <summary>
    /// A shrine in the damp woods: a mound of black earth with a ring of
    /// standing stones on it and a stone table in the middle, a fire on it;
    /// round the mound a ring of the pack's toadstools grown taller than a
    /// house, and a fence that was put up once and gave up.
    /// </summary>
    private static void Ring(Job b)
    {
        const float M = 0.7f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-5.0f, 0f, -5.0f), new Vector3(5.0f, 0f, 5.0f), M, 1.4f, 1, false);

        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + 0.3f;
            Standing(b, new Vector3(Mathf.Cos(a) * 3.4f, M, Mathf.Sin(a) * 3.4f), 2.3f + (float)b.Rng.NextDouble() * 0.9f);
        }

        k.Block(new Vector3(0f, M + 0.45f, 0f), new Vector3(2.0f, 0.9f, 1.2f), Kit.Swatch.DarkStone, 0.015f, true);
        k.Block(new Vector3(0f, M + 0.95f, 0f), new Vector3(2.4f, 0.14f, 1.5f), Kit.Swatch.Stone, 0.01f);
        k.Block(new Vector3(0f, M + 1.2f, 0f), new Vector3(0.7f, 0.35f, 0.5f), Kit.Swatch.Thatch, 0.06f);
        k.Block(new Vector3(0f, M + 1.45f, 0f), new Vector3(0.4f, 0.3f, 0.3f), Kit.Swatch.Cloth, 0.05f);

        foreach (float x in new[] { -2.2f, 2.2f })
            k.Log(new Vector3(x, M + 0.2f, -1.6f), new Vector3(x, M + 0.2f, 1.6f), 0.2f, Kit.Swatch.Wood, 7);

        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            Mushroom(b, new Vector3(Mathf.Cos(a) * 8.0f, 0f, Mathf.Sin(a) * 8.0f), 3.6f + (float)b.Rng.NextDouble() * 1.6f);
            float inner = a + Mathf.PI / 9f;
            Mushroom(b, new Vector3(Mathf.Cos(inner) * 6.2f, 0f, Mathf.Sin(inner) * 6.2f), 1.2f + (float)b.Rng.NextDouble() * 0.9f);
        }

        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + 0.5f;
            k.Lantern(new Vector3(Mathf.Cos(a) * 4.4f, M, Mathf.Sin(a) * 4.4f), 2.4f, -a * Mathf.Rad2Deg + 180f);
        }

        // the fence that gave up: a run, a gap, a run, and one post left
        k.Railing(new Vector3(6.4f, Ground, -9.2f), new Vector3(6.4f, Ground, -4.6f), 1.0f);
        k.Railing(new Vector3(6.4f, Ground, 4.0f), new Vector3(6.4f, Ground, 9.0f), 1.0f);
        k.Post(new Vector3(6.4f, Ground, 0.8f), 1.0f, 0.06f);
        k.HangingSign(new Vector3(9.6f, Ground, 1.4f), 2.6f, 180f);

        k.Finish("Ring", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------- Charcoal Camp

    /// <summary>
    /// A burner's camp on a court of packed earth: two clamps of earth
    /// heaped over stacked wood, smoking at the top, the burner's log hut
    /// under thatch, and the wood for the next burn stacked everywhere.
    /// </summary>
    private static void Camp(Job b)
    {
        const float Y = 0.45f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-8.5f, 0f, -7.0f), new Vector3(8.5f, 0f, 7.0f), Y, 1.4f, 1, false);

        // the clamps: a ring of logs, earth heaped over, a dark mouth on top
        foreach (var at in new[] { new Vector3(2.6f, Y, -3.2f), new Vector3(3.4f, Y, 3.0f) })
        {
            float rad = 2.3f;
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var foot = at + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                k.Log(foot, foot + new Vector3(-Mathf.Cos(a) * 0.5f, 1.4f, -Mathf.Sin(a) * 0.5f), 0.12f, Kit.Swatch.DarkWood, 6);
            }
            k.Cone(at + Vector3.up * 0.3f, rad + 0.1f, 2.4f, Kit.Swatch.Earth, 14);
            k.Block(at + Vector3.up * 0.15f, new Vector3(rad * 2f, 0.3f, rad * 2f), Kit.Swatch.Earth, 0.04f, true);
            k.Block(at + Vector3.up * 2.55f, new Vector3(0.5f, 0.5f, 0.5f), Kit.Swatch.Iron, 0.03f);
            k.Block(at + Vector3.up * 2.95f, new Vector3(0.35f, 0.4f, 0.35f), Kit.Swatch.DarkStone, 0.05f);
        }

        // the hut
        const float r = 0.16f;
        var h0 = new Vector3(-7.4f, Y, -2.2f); var h1 = new Vector3(-3.6f, Y, -2.2f);
        var h2 = new Vector3(-3.6f, Y, 1.6f); var h3 = new Vector3(-7.4f, Y, 1.6f);
        k.LogWall(h0, h1, 2.3f, 0f, r); k.LogWall(h3, h2, 2.3f, 0f, r);
        k.LogWall(h0, h3, 2.3f, r, r);
        k.LogWall(h1, new Vector3(-3.6f, Y, -0.9f), 2.3f, r, r);
        k.LogWall(new Vector3(-3.6f, Y, 0.3f), h2, 2.3f, r, r);
        k.LogWall(new Vector3(-3.6f, Y, -0.9f), new Vector3(-3.6f, Y, 0.3f), 2.3f, 1.9f, r);
        k.Door(new Vector3(-3.6f + r, Y, -0.3f), Vector3.right, 0.95f, 1.8f);
        k.Window(new Vector3(-5.5f, Y + 1.35f, 1.6f + r + 0.02f), Vector3.forward, 0.8f, 0.7f);
        var eave = new Vector3(-5.5f, Y + 2.3f, -0.3f);
        k.GableEnd(eave, 3.8f + 2f * r, 42f, -1.9f - r, Kit.Swatch.Plaster, 0.3f);
        k.GableEnd(eave, 3.8f + 2f * r, 42f, 1.9f + r, Kit.Swatch.Plaster, 0.3f);
        k.Roof(eave, 3.8f + 2f * r, 3.8f + 2f * r, 42f, Kit.Builder.RoofStyle.Thatch, 0.6f);

        // the wood, everywhere
        k.Woodpile(new Vector3(-6.0f, Y, 5.0f), 2.2f, 5, 0f);
        k.Woodpile(new Vector3(-3.2f, Y, 5.2f), 1.8f, 4, 0f);
        k.Woodpile(new Vector3(-6.4f, Y, -5.2f), 2.0f, 4, 90f);
        k.Woodpile(new Vector3(6.8f, Y, 0.2f), 1.6f, 3, 90f);
        for (int i = 0; i < 6; i++)
            k.Log(new Vector3(-0.8f + i * 0.3f, Y + 0.16f, -5.6f + (i % 2) * 0.2f), new Vector3(-0.8f + i * 0.3f + 0.2f, Y + 0.16f, -5.6f + (i % 2) * 0.2f + 2.2f), 0.14f, Kit.Swatch.Wood, 7);

        k.Barrel(new Vector3(-1.4f, Y, 1.8f), 0.32f, 0.9f);
        k.Crate(new Vector3(-0.8f, Y, 2.6f), 0.65f);
        k.Cartwheel(new Vector3(-8.0f, Y, 3.4f), 0.55f, 70f);
        k.Lantern(new Vector3(-1.2f, Y, -1.4f), 2.4f, 0f);
        k.Lantern(new Vector3(6.6f, Y, -5.4f), 2.4f, 180f);
        k.Trough(new Vector3(0.6f, Y, 5.8f), 1.6f, 0f);
        k.Table(new Vector3(-1.4f, Y, 4.0f), 1.4f, 0.7f, 0.75f);
        k.Bench(new Vector3(-1.4f, Y, 3.2f), 1.2f);
        k.HangingSign(new Vector3(9.4f, Ground, -2.0f), 2.6f, 180f);

        k.Finish("Camp", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // -------------------------------------------------------- Hilltop Beacon

    /// <summary>
    /// A round stone tower twelve high on a flagged plinth with a great
    /// brazier burning on its top, a stone stair up a buttress to a landing
    /// and a door a storey up, the keeper's stone hut, the fuel for the fire
    /// stacked about, and banners: a light to steer by from a long way off.
    /// </summary>
    private static void Beacon(Job b)
    {
        const float B = 0.8f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-7.5f, 0f, -6.5f), new Vector3(5.5f, 0f, 6.5f), B, 1.8f, 1);

        float radius = 2.4f, height = 12f;
        var foot = new Vector3(-1.0f, B, 0f);
        k.RoundTower(foot, radius, height, true, Kit.Builder.RoofStyle.Slate, 0.5f);

        // the fire on top: an iron bowl on a stone pier, flame and embers
        var top = foot + Vector3.up * (height + 0.12f);
        k.Block(top + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1.4f), Kit.Swatch.DarkStone, 0.01f);
        k.Ring(top + Vector3.up * 1.1f, 0.9f, 0.6f, 0.08f, 10, Kit.Swatch.Iron);
        k.Block(top + Vector3.up * 1.3f, new Vector3(1.3f, 0.5f, 1.3f), Kit.Swatch.Thatch, 0.08f);
        k.Block(top + Vector3.up * 1.85f, new Vector3(0.8f, 0.9f, 0.8f), Kit.Swatch.Cloth, 0.08f);
        k.Block(top + Vector3.up * 2.5f, new Vector3(0.4f, 0.6f, 0.4f), Kit.Swatch.Cloth, 0.1f);

        // the stair up a buttress to the door, a storey up
        float landing = B + 2.6f;
        k.Block(new Vector3(foot.x + radius + 0.9f, (B + landing) * 0.5f, 0f), new Vector3(2.0f, landing - B, 2.4f), Kit.Swatch.Mortar, 0f, true);
        k.StoneWall(new Vector3(foot.x + radius - 0.1f, B, -1.2f), new Vector3(foot.x + radius + 1.9f, B, -1.2f), landing - B, 0.3f);
        k.StoneWall(new Vector3(foot.x + radius + 1.9f, B, -1.2f), new Vector3(foot.x + radius + 1.9f, B, 1.2f), landing - B, 0.3f);
        k.Pavers(new Vector3(foot.x + radius + 0.9f, landing, 0f), 2.0f, 2.4f, 0.6f);
        int flight = Mathf.CeilToInt((landing - B) / 0.2f);
        float stairFoot = foot.x + radius + 1.9f + flight * 0.34f;
        k.Steps(new Vector3(stairFoot, B, 0f), Vector3.left, flight, (landing - B) / flight, 0.34f, 2.0f);
        k.Railing(new Vector3(stairFoot, B, 1.1f), new Vector3(foot.x + radius + 1.9f, landing, 1.1f), 0.9f);
        k.Railing(new Vector3(foot.x + radius + 1.9f, landing, 1.2f), new Vector3(foot.x + radius + 1.9f, landing, -1.2f), 0.9f);
        k.Door(foot + new Vector3(radius - 0.06f, landing - B, 0f), Vector3.right, 1.0f, 2.0f);
        k.Door(foot + new Vector3(0f, 0f, -radius - 0.06f), Vector3.back, 1.0f, 2.0f);

        // the keeper's hut
        var s0 = new Vector3(-7.0f, B, 2.2f); var s1 = new Vector3(-3.6f, B, 2.2f);
        var s2 = new Vector3(-3.6f, B, 5.8f); var s3 = new Vector3(-7.0f, B, 5.8f);
        k.StoneWall(s0, s1, 2.5f, 0.4f); k.StoneWall(s1, s2, 2.5f, 0.4f); k.StoneWall(s2, s3, 2.5f, 0.4f); k.StoneWall(s3, s0, 2.5f, 0.4f);
        k.Door(new Vector3(-5.3f, B, 2.2f - 0.22f), Vector3.back, 1.0f, 1.9f);
        k.Window(new Vector3(-3.6f + 0.22f, B + 1.5f, 4.0f), Vector3.right, 0.8f, 0.8f);
        var hutEave = new Vector3(-5.3f, B + 2.5f, 4.0f);
        k.GableEnd(hutEave, 3.4f + 0.4f, 34f, -1.8f - 0.2f, Kit.Swatch.Stone, 0.4f);
        k.GableEnd(hutEave, 3.4f + 0.4f, 34f, 1.8f + 0.2f, Kit.Swatch.Stone, 0.4f);
        k.Roof(hutEave, 3.4f + 0.4f, 3.6f + 0.4f, 34f, Kit.Builder.RoofStyle.Slate, 0.45f);
        k.Chimney(new Vector3(-7.0f - 0.22f - 0.36f, B, 4.8f), 4.6f, 0.7f);

        // fuel and the rest
        k.Woodpile(new Vector3(-5.6f, B + 0.1f, -4.6f), 2.2f, 5, 0f);
        k.Woodpile(new Vector3(-2.6f, B + 0.1f, -4.9f), 1.8f, 4, 0f);
        k.Barrel(new Vector3(3.2f, B + 0.1f, -4.4f), 0.32f, 0.9f);
        k.Barrel(new Vector3(3.9f, B + 0.1f, -3.9f), 0.28f, 0.8f);
        k.Crate(new Vector3(3.4f, B + 0.1f, -5.3f), 0.65f);
        k.Lantern(new Vector3(4.6f, B + 0.1f, 5.4f), 2.6f, 180f);
        k.Lantern(new Vector3(-6.6f, B + 0.1f, -5.6f), 2.4f, 0f);
        k.Banner(new Vector3(4.6f, B + 0.1f, -5.6f), 3.4f, 180f);
        k.Banner(new Vector3(-6.8f, B + 0.1f, 0.4f), 3.4f, 0f);
        k.Bench(new Vector3(-1.0f, B + 0.1f, 5.2f), 1.4f);
        k.HangingSign(new Vector3(7.0f, Ground, -2.2f), 2.7f, 180f);
        k.Railing(new Vector3(-7.5f, B + 0.1f, -6.5f), new Vector3(5.5f, B + 0.1f, -6.5f), 0.9f);
        k.Railing(new Vector3(-7.5f, B + 0.1f, 6.5f), new Vector3(5.5f, B + 0.1f, 6.5f), 0.9f);

        k.Finish("Beacon", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ----------------------------------------------------------- Summit Cairn

    /// <summary>
    /// On a flagged base, a great heap of the pack's boulders with a standing
    /// stone on top and a banner over it; a windbreak of coursed stone on
    /// the weather side with a plank lean-to against it, a stone bench, and
    /// a lantern for whoever comes up after dark.
    /// </summary>
    private static void Cairn(Job b)
    {
        const float B = 0.4f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-5.0f, 0f, -5.0f), new Vector3(5.0f, 0f, 5.0f), B, 1.6f, 1);

        // the heap: rings of lying stones, each ring higher and tighter
        for (int ring = 0; ring < 4; ring++)
        {
            float rad = 2.6f - ring * 0.65f;
            int count = 10 - ring * 2;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + ring * 0.4f;
                Lying(b, new Vector3(Mathf.Cos(a) * rad, B + ring * 0.55f, Mathf.Sin(a) * rad), 0.9f + (float)b.Rng.NextDouble() * 0.5f - ring * 0.08f);
            }
        }
        k.Block(new Vector3(0f, B + 1.0f, 0f), new Vector3(3.0f, 2.0f, 3.0f), Kit.Swatch.Mortar, 0f, true);
        Standing(b, new Vector3(0f, B + 2.15f, 0f), 2.8f);
        k.Banner(new Vector3(0.9f, B + 2.0f, -0.6f), 4.6f, 0f);

        // the windbreak and the lean-to
        k.StoneWall(new Vector3(-4.6f, B, -4.4f), new Vector3(-4.6f, B, 4.4f), 2.0f, 0.5f);
        k.StoneWall(new Vector3(-4.6f, B, -4.4f), new Vector3(-1.4f, B, -4.4f), 2.0f, 0.5f);
        foreach (float z in new[] { -0.2f, 1.6f, 3.4f }) k.Post(new Vector3(-2.4f, B, z), 1.6f, 0.1f, Kit.Swatch.Wood);
        k.Block(new Vector3(-3.4f, B + 1.95f, 1.6f), new Vector3(2.5f, 0.1f, 4.2f), Quaternion.Euler(0f, 0f, -14f), Kit.Swatch.Wood);
        k.Log(new Vector3(-2.4f, B + 1.55f, -0.4f), new Vector3(-2.4f, B + 1.55f, 3.6f), 0.07f, Kit.Swatch.DarkWood, 6);
        k.Woodpile(new Vector3(-3.6f, B, 3.2f), 1.2f, 3, 90f);
        k.Barrel(new Vector3(-3.2f, B, 0.3f), 0.28f, 0.8f);
        k.Block(new Vector3(2.8f, B + 0.25f, 3.6f), new Vector3(1.8f, 0.5f, 0.6f), Kit.Swatch.Stone, 0.015f);
        k.Lantern(new Vector3(3.8f, B, -3.6f), 2.4f, 90f);
        k.HangingSign(new Vector3(6.2f, Ground, -1.4f), 2.6f, 180f);

        k.Finish("Cairn", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // --------------------------------------------------------- Wayside Shrine

    /// <summary>
    /// A roofed shrine by the way: a stone plinth under a slate roof on four
    /// posts, a standing stone in it with a fire before it, a low wall on
    /// three sides, a bench, a lantern kept lit, and a well beside for
    /// whoever stops.
    /// </summary>
    private static void Shrine(Job b)
    {
        const float B = 0.5f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-4.5f, 0f, -4.0f), new Vector3(3.5f, 0f, 4.0f), B, 1.3f, 1);

        // the shrine
        k.Block(new Vector3(-1.5f, B + 0.25f, 0f), new Vector3(3.6f, 0.5f, 3.6f), Kit.Swatch.DarkStone, 0.01f, true);
        k.Pavers(new Vector3(-1.5f, B + 0.5f, 0f), 3.4f, 3.4f, 0.55f);
        Standing(b, new Vector3(-2.0f, B + 0.5f, 0f), 2.4f);
        k.Block(new Vector3(-0.6f, B + 0.75f, 0f), new Vector3(0.6f, 0.5f, 0.6f), Kit.Swatch.Stone, 0.01f);
        k.Block(new Vector3(-0.6f, B + 1.1f, 0f), new Vector3(0.4f, 0.24f, 0.4f), Kit.Swatch.Cloth, 0.05f);
        foreach (float x in new[] { -3.2f, 0.2f }) foreach (float z in new[] { -1.7f, 1.7f })
            k.Post(new Vector3(x, B + 0.5f, z), 2.6f, 0.12f, Kit.Swatch.DarkWood);
        var eave = new Vector3(-1.5f, B + 3.1f, 0f);
        k.Roof(eave, 3.8f, 3.8f, 34f, Kit.Builder.RoofStyle.Slate, 0.5f);
        k.StoneWall(new Vector3(-3.4f, B, -2.0f), new Vector3(-3.4f, B, 2.0f), 1.1f, 0.4f);
        k.StoneWall(new Vector3(-3.4f, B, -2.0f), new Vector3(0.4f, B, -2.0f), 1.1f, 0.4f);
        k.StoneWall(new Vector3(-3.4f, B, 2.0f), new Vector3(0.4f, B, 2.0f), 1.1f, 0.4f);

        k.Well(new Vector3(1.8f, B + 0.1f, 2.4f));
        k.Bench(new Vector3(1.6f, B + 0.1f, -2.6f), 1.4f);
        k.Lantern(new Vector3(2.6f, B + 0.1f, -0.2f), 2.5f, 270f);
        k.Trough(new Vector3(-3.6f, B + 0.1f, -3.2f), 1.5f, 0f);
        k.HangingSign(new Vector3(4.6f, Ground, 1.8f), 2.6f, 180f);
        k.Pavers(new Vector3(5.6f, Ground + 0.02f, 0f), 3.0f, 1.4f, 0.6f);

        k.Finish("Shrine", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // -------------------------------------------------------- Standing Stones

    /// <summary>
    /// A great ring of stones on end on a raised court of earth, a trilithon
    /// at its entrance, a flat stone with a fire in the middle, and an
    /// avenue of lesser stones leading out. Nobody keeps it.
    /// </summary>
    private static void Stones(Job b)
    {
        const float M = 0.5f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-9.5f, 0f, -9.5f), new Vector3(9.5f, 0f, 9.5f), M, 1.3f, 1, false);

        for (int i = 1; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            var foot = new Vector3(Mathf.Cos(a) * 7.8f, M, Mathf.Sin(a) * 7.8f);
            if (i == 6) Lying(b, foot, 1.6f);
            else Standing(b, foot, 3.2f + (float)b.Rng.NextDouble() * 1.2f);
        }

        // the trilithon at the entrance, on +x
        Standing(b, new Vector3(7.8f, M, -1.5f), 4.2f);
        Standing(b, new Vector3(7.8f, M, 1.5f), 4.2f);
        k.Block(new Vector3(7.8f, M + 4.35f, 0f), new Vector3(1.1f, 0.7f, 4.2f), Kit.Swatch.DarkStone, 0.02f);

        k.Block(new Vector3(0f, M + 0.35f, 0f), new Vector3(2.8f, 0.7f, 1.8f), Kit.Swatch.Stone, 0.015f, true);
        k.Block(new Vector3(0f, M + 0.9f, 0f), new Vector3(0.8f, 0.4f, 0.7f), Kit.Swatch.Thatch, 0.06f);
        k.Block(new Vector3(0f, M + 1.2f, 0f), new Vector3(0.45f, 0.35f, 0.4f), Kit.Swatch.Cloth, 0.05f);

        for (int i = 0; i < 4; i++)
        {
            float x = 11.5f + i * 2.4f;
            Standing(b, new Vector3(x, Ground, -2.2f), 1.3f + (i % 2) * 0.3f);
            Standing(b, new Vector3(x, Ground, 2.2f), 1.3f + ((i + 1) % 2) * 0.3f);
        }
        k.Pavers(new Vector3(14.5f, Ground + 0.02f, 0f), 9.0f, 1.6f, 0.7f);
        k.HangingSign(new Vector3(20.4f, Ground, 2.6f), 2.6f, 180f);

        k.Finish("Stones", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ------------------------------------------------------------ Lighthouse

    /// <summary>
    /// A round stone tower on a foundation at the water's edge, its seaward
    /// half standing in the shallows; a gallery round the top and a glazed
    /// lantern room with a slate cone over it; a stone keeper's cottage on
    /// the land side with a slate roof and a chimney, and the keeper's
    /// things about the yard. +x is the water.
    /// </summary>
    private static void Lighthouse(Job b)
    {
        const float B = 0.9f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-8.5f, 0f, -4.5f), new Vector3(3.5f, 0f, 4.5f), B, 2.4f, -1);

        // the tower
        float towerHeight = 11f, radius = 2.0f;
        var foot = new Vector3(0.8f, B, 0f);
        k.RoundTower(foot, radius, towerHeight, false, Kit.Builder.RoofStyle.Slate, 0.5f);
        k.Door(foot + new Vector3(-radius - 0.06f, 0f, 0f), Vector3.left, 1.0f, 2.0f);

        // the gallery: a plank ring on brackets, railed
        var top = foot + Vector3.up * towerHeight;
        k.Block(top + Vector3.up * 0.08f, new Vector3(radius * 2f + 1.6f, 0.16f, radius * 2f + 1.6f), Kit.Swatch.DarkWood, 0f, true);
        for (int i = 0; i < 12; i++)
        {
            float a0 = i / 12f * Mathf.PI * 2f, a1 = (i + 1) / 12f * Mathf.PI * 2f;
            float gr = radius + 0.75f;
            k.Railing(top + new Vector3(Mathf.Cos(a0) * gr, 0.16f, Mathf.Sin(a0) * gr), top + new Vector3(Mathf.Cos(a1) * gr, 0.16f, Mathf.Sin(a1) * gr), 1.0f);
        }

        // the lantern room: glass between iron posts, the light inside, a cone over
        var lantern = top + Vector3.up * 0.16f;
        k.Ring(lantern + Vector3.up * 0.8f, radius - 0.5f, 1.5f, 0.05f, 12, Kit.Swatch.Pane);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            k.Post(lantern + new Vector3(Mathf.Cos(a) * (radius - 0.45f), 0f, Mathf.Sin(a) * (radius - 0.45f)), 1.6f, 0.05f, Kit.Swatch.Iron);
        }
        k.Block(lantern + Vector3.up * 0.85f, new Vector3(0.7f, 0.7f, 0.7f), Kit.Swatch.Thatch);
        k.Block(lantern + Vector3.up * 1.65f, new Vector3(radius * 2f - 0.6f, 0.12f, radius * 2f - 0.6f), Kit.Swatch.Iron);
        k.Cone(lantern + Vector3.up * 1.7f, radius - 0.1f, 1.6f, Kit.Swatch.Slate, 12);

        // the cottage, on the land side
        var s0 = new Vector3(-7.6f, B, -3.4f); var s1 = new Vector3(-3.4f, B, -3.4f);
        var s2 = new Vector3(-3.4f, B, 1.2f); var s3 = new Vector3(-7.6f, B, 1.2f);
        k.StoneWall(s0, s1, 2.6f); k.StoneWall(s1, s2, 2.6f); k.StoneWall(s2, s3, 2.6f); k.StoneWall(s3, s0, 2.6f);
        k.Door(new Vector3(-5.5f, B, 1.2f + 0.22f), Vector3.forward, 1.0f, 1.9f);
        k.Window(new Vector3(-3.4f + 0.22f, B + 1.5f, -1.1f), Vector3.right, 0.8f, 0.8f);
        k.Window(new Vector3(-7.6f - 0.22f, B + 1.5f, -1.1f), Vector3.left, 0.8f, 0.8f);
        var cotEave = new Vector3(-5.5f, B + 2.6f, -1.1f);
        k.GableEnd(cotEave, 4.2f + 0.44f, 38f, -2.3f - 0.22f, Kit.Swatch.Stone, 0.42f);
        k.GableEnd(cotEave, 4.2f + 0.44f, 38f, 2.3f + 0.22f, Kit.Swatch.Stone, 0.42f);
        k.Roof(cotEave, 4.2f + 0.44f, 4.6f + 0.44f, 38f, Kit.Builder.RoofStyle.Slate, 0.45f);
        k.Chimney(new Vector3(-7.6f - 0.22f - 0.36f, B, -2.6f), 4.9f, 0.7f);

        // the yard
        k.Barrel(new Vector3(-2.6f, B + 0.1f, 3.2f), 0.32f, 0.9f);
        k.Barrel(new Vector3(-1.9f, B + 0.1f, 3.6f), 0.28f, 0.8f);
        k.Crate(new Vector3(-3.4f, B + 0.1f, 3.5f), 0.65f);
        k.Woodpile(new Vector3(-6.4f, B + 0.1f, 3.2f), 1.6f, 3, 0f);
        k.Lantern(new Vector3(-2.0f, B + 0.1f, -3.8f), 2.4f, 90f);
        k.Bench(new Vector3(-4.8f, B + 0.1f, 2.6f), 1.3f);
        k.HangingSign(new Vector3(-8.9f, Ground, 0.6f), 2.6f, 270f);
        k.Cartwheel(new Vector3(-8.0f, B + 0.1f, -4.0f), 0.5f, 70f);

        k.Finish("Lighthouse", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------- Hunter's Hide

    /// <summary>
    /// A hide up on log stilts, plank-walled under a plank roof, a wooden
    /// stair up to it; below, a fire ring, a drying rack, the woodpile and
    /// the hunter's things, and a pen.
    /// </summary>
    private static void Hide(Job b)
    {
        const float Y = 0.4f, H = 3.6f;
        var k = new Kit.Builder(b.Rng.Next());

        Foundation(b, k, new Vector3(-5.0f, 0f, -5.5f), new Vector3(4.0f, 0f, 5.5f), Y, 1.3f, 1, false);

        // the stilts, the platform, the walls and the roof
        foreach (float x in new[] { -2.4f, 1.0f }) foreach (float z in new[] { -2.0f, 2.0f })
        {
            k.Log(new Vector3(x, Y, z), new Vector3(x, Y + H + 0.1f, z), 0.16f, Kit.Swatch.DarkWood, 7);
            k.Log(new Vector3(x, Y + 1.2f, z), new Vector3(x + (x < 0f ? 1f : -1f) * 1.1f, Y + H - 0.2f, z), 0.06f, Kit.Swatch.DarkWood, 5);
        }
        k.Block(new Vector3(-0.7f, Y + H, 0f), new Vector3(4.6f, 0.16f, 5.2f), Kit.Swatch.Plank, 0f, true);
        k.PlankWall(new Vector3(-3.0f, Y + H + 0.08f, -2.6f), new Vector3(1.6f, Y + H + 0.08f, -2.6f), 1.15f);
        k.PlankWall(new Vector3(1.6f, Y + H + 0.08f, 2.6f), new Vector3(-3.0f, Y + H + 0.08f, 2.6f), 1.15f);
        k.PlankWall(new Vector3(-3.0f, Y + H + 0.08f, 2.6f), new Vector3(-3.0f, Y + H + 0.08f, -2.6f), 1.15f);
        k.Railing(new Vector3(1.6f, Y + H + 0.08f, -2.6f), new Vector3(1.6f, Y + H + 0.08f, -0.7f), 1.0f);
        k.Railing(new Vector3(1.6f, Y + H + 0.08f, 0.7f), new Vector3(1.6f, Y + H + 0.08f, 2.6f), 1.0f);
        foreach (float x in new[] { -2.8f, 1.4f }) foreach (float z in new[] { -2.4f, 2.4f })
            k.Post(new Vector3(x, Y + H + 0.16f, z), 2.2f, 0.1f, Kit.Swatch.Wood);
        var eave = new Vector3(-0.7f, Y + H + 2.3f, 0f);
        k.Roof(eave, 5.0f, 5.6f, 26f, Kit.Builder.RoofStyle.Plank, 0.5f);
        k.Bench(new Vector3(-2.2f, Y + H + 0.16f, 0f), 1.3f);
        k.Crate(new Vector3(-2.5f, Y + H + 0.16f, 1.9f), 0.5f);

        // the stair, wooden, from the court to the platform's open side
        int flight = Mathf.CeilToInt(H / 0.2f);
        float stairFoot = 1.8f + flight * 0.32f;
        k.Steps(new Vector3(stairFoot, Y, 0f), Vector3.left, flight, H / flight, 0.32f, 1.4f, true);
        k.Railing(new Vector3(stairFoot, Y, 0.75f), new Vector3(1.8f, Y + H, 0.75f), 0.9f);
        k.Railing(new Vector3(stairFoot, Y, -0.75f), new Vector3(1.8f, Y + H, -0.75f), 0.9f);

        // below: the fire, the rack, the pile, the pen
        k.Ring(new Vector3(-0.7f, Y + 0.15f, 0f), 0.7f, 0.3f, 0.3f, 8, Kit.Swatch.DarkStone, true);
        k.Block(new Vector3(-0.7f, Y + 0.25f, 0f), new Vector3(0.7f, 0.35f, 0.7f), Kit.Swatch.Thatch, 0.06f);
        k.Block(new Vector3(-0.7f, Y + 0.5f, 0f), new Vector3(0.4f, 0.3f, 0.4f), Kit.Swatch.Cloth, 0.05f);
        k.Post(new Vector3(-4.2f, Y, -1.4f), 2.0f, 0.07f); k.Post(new Vector3(-4.2f, Y, 1.4f), 2.0f, 0.07f);
        k.Log(new Vector3(-4.2f, Y + 1.85f, -1.5f), new Vector3(-4.2f, Y + 1.85f, 1.5f), 0.04f, Kit.Swatch.Wood, 5);
        foreach (float z in new[] { -0.9f, -0.2f, 0.6f })
            k.Block(new Vector3(-4.2f, Y + 1.35f, z), new Vector3(0.06f, 0.9f, 0.4f), Kit.Swatch.Plaster);
        k.Woodpile(new Vector3(-3.8f, Y, 4.2f), 1.6f, 4, 0f);
        k.Barrel(new Vector3(3.0f, Y, -4.2f), 0.3f, 0.85f);
        k.Crate(new Vector3(2.2f, Y, -4.6f), 0.6f);
        k.Lantern(new Vector3(3.4f, Y, 4.6f), 2.4f, 180f);
        k.HayBale(new Vector3(-0.2f, Y, -4.2f), 30f);
        k.Railing(new Vector3(-5.0f, Y, -5.5f), new Vector3(4.0f, Y, -5.5f), 1.0f);
        k.Railing(new Vector3(-5.0f, Y, 5.5f), new Vector3(4.0f, Y, 5.5f), 1.0f);
        k.Railing(new Vector3(-5.0f, Y, -5.5f), new Vector3(-5.0f, Y, 5.5f), 1.0f);
        k.HangingSign(new Vector3(9.0f, Ground, -1.6f), 2.6f, 180f);

        k.Finish("Hide", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------- Buried Tower

    /// <summary>
    /// The ruin of a keep, mostly under the sand: a great round tower sunk
    /// to its shoulders and leaning, a second fallen and lying broken beside
    /// it, a run of curtain wall drifted over, and a stair that once led up
    /// to something. The sand is winning.
    /// </summary>
    private static void Buried(Job b)
    {
        var tower = new Kit.Builder(b.Rng.Next());
        tower.RoundTower(new Vector3(0f, -6.5f, 0f), 2.8f, 13f, true, Kit.Builder.RoofStyle.Slate, 0.5f);
        var stood = tower.Finish("Tower", b.Root, new Vector3(0f, 0f, 0f), b.Flora.Paint);
        stood.transform.localRotation = Quaternion.Euler(0f, b.Rng.Next(360), 11f);

        var fallen = new Kit.Builder(b.Rng.Next());
        fallen.RoundTower(Vector3.zero, 2.2f, 4.5f, false, Kit.Builder.RoofStyle.Slate, 0.5f);
        var lying = fallen.Finish("Fallen", b.Root, new Vector3(3.8f, -0.6f, 5.6f), b.Flora.Paint);
        lying.transform.localRotation = Quaternion.Euler(0f, 40f, 72f);

        var k = new Kit.Builder(b.Rng.Next());
        k.StoneWall(new Vector3(-8.5f, -1.4f, -3.2f), new Vector3(-3.2f, -1.4f, -3.2f), 2.6f, 1.0f);
        k.MerlonsAlong(new Vector3(-8.5f, 1.2f, -3.2f), new Vector3(-3.2f, 1.2f, -3.2f), 1.0f);
        k.StoneWall(new Vector3(3.2f, -1.8f, -3.2f), new Vector3(7.5f, -1.8f, -3.2f), 2.6f, 1.0f);
        k.Steps(new Vector3(-5.2f, -0.4f, 2.0f), Vector3.forward, 5, 0.2f, 0.34f, 1.6f);
        k.Barrel(new Vector3(4.4f, Ground - 0.35f, -0.4f), 0.32f, 0.9f);
        k.Crate(new Vector3(-3.4f, Ground - 0.3f, 2.4f), 0.7f);
        k.Crate(new Vector3(5.6f, Ground - 0.45f, 1.6f), 0.6f);
        k.Cartwheel(new Vector3(6.6f, Ground - 0.25f, -1.4f), 0.55f, 60f);
        k.Post(new Vector3(-6.4f, Ground - 0.1f, 4.4f), 3.2f, 0.09f, Kit.Swatch.DarkWood);
        k.Block(new Vector3(-6.4f, Ground + 2.5f, 4.8f), new Vector3(0.03f, 1.0f, 0.7f), Kit.Swatch.Cloth);
        k.HangingSign(new Vector3(8.6f, Ground - 0.15f, -1.6f), 2.5f, 180f);
        Standing(b, new Vector3(7.4f, Ground - 0.4f, 3.6f), 1.6f);
        k.Finish("Ruin", b.Root, Vector3.zero, b.Flora.Paint);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// What the country does to a place left to it, from the ground the place
    /// actually stands on: snow if the site is snowy, whatever the kind, since
    /// a forest can climb above the snowline; sand in the desert; char in the
    /// dead woods; nothing but moss on bare rock; vines everywhere green.
    /// </summary>
    private static Kit.Builder.Weather WeatherAt(Job b)
    {
        if (SnowCover.IsSnowy(b.At.TileX, b.At.TileZ, b.Seed)) return Kit.Builder.Weather.Snow;

        switch (Regions.CharacterAtTile(b.At.TileX, b.At.TileZ, b.Seed, false))
        {
            case Regions.Character.Desert: return Kit.Builder.Weather.Sand;
            case Regions.Character.Dead: return Kit.Builder.Weather.Char;
            case Regions.Character.Stone:
            case Regions.Character.Peaks: return Kit.Builder.Weather.None;
            default: return Kit.Builder.Weather.Vines;
        }
    }

    /// <summary>
    /// A stone foundation from one corner to the other, its top at a height
    /// above the root, flagged or packed earth, with a skirt of coursed stone
    /// down its sides to well below the ground: the ground under a big
    /// footprint steps by a terrace or two, and the skirt swallows the
    /// steps. A raised court also buries the ground tiles' own rocks and
    /// logs, which no rule about planting can keep out of a yard. A flight
    /// of steps leads up onto it from the side asked for.
    /// </summary>
    private static void Foundation(Job b, Kit.Builder k, Vector3 min, Vector3 max, float top, float skirt = 1.0f, int stepsSide = 1, bool flagged = true)
    {
        var centre = (min + max) * 0.5f;
        var size = max - min;

        k.Block(new Vector3(centre.x, top - 0.55f, centre.z), new Vector3(size.x, 1.1f, size.z), flagged ? Kit.Swatch.Mortar : Kit.Swatch.Earth, 0f, true);
        if (flagged) k.Pavers(new Vector3(centre.x, top, centre.z), size.x, size.z, 0.9f);

        float foot = top - skirt;
        k.StoneWall(new Vector3(min.x, foot, min.z), new Vector3(max.x, foot, min.z), skirt, 0.3f);
        k.StoneWall(new Vector3(max.x, foot, min.z), new Vector3(max.x, foot, max.z), skirt, 0.3f);
        k.StoneWall(new Vector3(max.x, foot, max.z), new Vector3(min.x, foot, max.z), skirt, 0.3f);
        k.StoneWall(new Vector3(min.x, foot, max.z), new Vector3(min.x, foot, min.z), skirt, 0.3f);

        if (stepsSide == 0) return;

        // Steps climb from their foot in the direction given, so the foot is
        // out on the ground and the flight climbs back toward the slab. Given
        // the slab's edge as the foot, it climbed away from it.
        int flight = Mathf.Max(1, Mathf.CeilToInt((top + 0.1f) / 0.19f));
        var toward = stepsSide > 0 ? Vector3.left : Vector3.right;
        var stairFoot = new Vector3(stepsSide > 0 ? max.x + flight * 0.36f : min.x - flight * 0.36f, Ground, centre.z);
        k.Steps(stairFoot, toward, flight, (top - Ground) / flight, 0.36f, 1.8f);
    }

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
    /// <summary>
    /// The boulders that are actually stone-coloured. The pack's stones are
    /// not all grey: mixed in with them are bright gems, and one of those
    /// stood on end and drawn out tall is a white pole eight high -- which
    /// is what was standing in the middle of the stone circle, and on a
    /// good many hills.
    /// </summary>
    private static Flora.Sprout[] GreyStones(Flora flora)
    {
        if (greyStones != null && greyStonesFrom == flora) return greyStones;

        var grey = new List<Flora.Sprout>();
        foreach (var stone in flora.Boulders)
        {
            float most = Mathf.Max(stone.Colour.r, Mathf.Max(stone.Colour.g, stone.Colour.b));
            float least = Mathf.Min(stone.Colour.r, Mathf.Min(stone.Colour.g, stone.Colour.b));
            if (most - least < 0.12f && most < 0.8f) grey.Add(stone);
        }

        greyStonesFrom = flora;
        greyStones = grey.Count > 0 ? grey.ToArray() : flora.Boulders;
        return greyStones;
    }

    private static Flora.Sprout[] greyStones;
    private static Flora greyStonesFrom;

    private static void Standing(Job b, Vector3 foot, float tall)
    {
        if (b.Flora == null || b.Flora.Boulders == null || b.Flora.Boulders.Length == 0) return;

        var stones = GreyStones(b.Flora);
        var stone = stones[b.Rng.Next(stones.Length)];
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

        var stones = GreyStones(b.Flora);
        var stone = stones[b.Rng.Next(stones.Length)];
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
