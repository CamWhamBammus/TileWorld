using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The small things between the ruins: a tree down, a dead fire, a pack, a
/// snare, a cairn by the way, a cart with a wheel off. About a chunk in
/// three has one, on a level dry tile well inside the chunk, and never in a
/// chunk that has a ruin. They are placed from the seed like the ruins and
/// built by the same builder, and the book has a page for each sort, but
/// they are not charted from and do not go on the map.
/// </summary>
public static class Finds
{
    private static readonly Dictionary<long, Landmarks.Placement> placements = new Dictionary<long, Landmarks.Placement>();
    private static int placementsFor;

    private static readonly LandmarkKind[] None = new LandmarkKind[0];

    /// <summary>Which finds turn up in which country.</summary>
    private static LandmarkKind[] In(Regions.Character country) => country switch
    {
        Regions.Character.Forest => new[] { LandmarkKind.FallenTree, LandmarkKind.Snare, LandmarkKind.DeadFire, LandmarkKind.FallenTree },
        Regions.Character.Snow => new[] { LandmarkKind.FallenTree, LandmarkKind.Snare, LandmarkKind.DeadFire },
        Regions.Character.Fungal => new[] { LandmarkKind.FallenTree, LandmarkKind.DeadFire },
        Regions.Character.Dead => new[] { LandmarkKind.FallenTree, LandmarkKind.DeadFire, LandmarkKind.FallenTree },
        Regions.Character.Lowland => new[] { LandmarkKind.DeadFire, LandmarkKind.BrokenCart, LandmarkKind.DroppedPack, LandmarkKind.Waymark },
        Regions.Character.Hills => new[] { LandmarkKind.Waymark, LandmarkKind.DeadFire, LandmarkKind.DroppedPack },
        Regions.Character.Peaks => new[] { LandmarkKind.Waymark, LandmarkKind.DroppedPack },
        Regions.Character.Stone => new[] { LandmarkKind.Waymark, LandmarkKind.DroppedPack },
        Regions.Character.Desert => new[] { LandmarkKind.BrokenCart, LandmarkKind.DroppedPack, LandmarkKind.Waymark },
        Regions.Character.Reed => new[] { LandmarkKind.DeadFire, LandmarkKind.DroppedPack },

        // These four had no entry at all, so four countries -- two of them the newest and best
        // looking in the game -- had no trace of anybody ever having passed through them.
        Regions.Character.Jungle => new[] { LandmarkKind.FallenTree, LandmarkKind.DeadFire,
                                            LandmarkKind.DroppedPack, LandmarkKind.FallenTree },
        Regions.Character.Savanna => new[] { LandmarkKind.DeadFire, LandmarkKind.BrokenCart,
                                             LandmarkKind.Waymark, LandmarkKind.DroppedPack },
        Regions.Character.Water => new[] { LandmarkKind.DeadFire, LandmarkKind.DroppedPack },
        Regions.Character.Reef => new[] { LandmarkKind.DeadFire, LandmarkKind.DroppedPack },
        _ => None
    };

    private const int Margin = 3;       // tiles in from the chunk's edge
    private const int OneIn = 3;        // chunks that could have one, that do

    public static Landmarks.Placement In(Vector2Int chunk, int worldSeed)
    {
        // a world made without ruins has none of these either
        if (WorldLibrary.Current != null && !WorldLibrary.Current.ruins) return new Landmarks.Placement { Exists = false, Chunk = chunk };

        if (placementsFor != worldSeed) { placements.Clear(); placementsFor = worldSeed; }

        long key = ((long)chunk.x << 32) ^ (uint)chunk.y;
        if (placements.TryGetValue(key, out var known)) return known;

        var worked = Work(chunk, worldSeed);
        placements[key] = worked;
        return worked;
    }

    private static Landmarks.Placement Work(Vector2Int chunk, int worldSeed)
    {
        var result = new Landmarks.Placement { Exists = false, Chunk = chunk };

        if (Landmarks.In(chunk, worldSeed).Exists) return result;

        var kinds = In(Regions.CharacterAt(chunk, worldSeed));
        if (kinds.Length == 0) return result;
        if (Hash(chunk.x, chunk.y, worldSeed ^ 0x3D17) % (OneIn * 100) >= 100) return result;

        var kind = kinds[Hash(chunk.x, chunk.y, worldSeed ^ 0x6B2) % kinds.Length];

        int span = WorldGrid.TilesPerChunk - Margin * 2;
        int start = Hash(chunk.x, chunk.y, worldSeed ^ 0x1F5) % (span * span);

        // a handful of spots tried in turn; the first level, dry one that no
        // ruin's ground reaches is it
        for (int n = 0; n < 8; n++)
        {
            int i = (start + n * 37) % (span * span);
            int tileX = chunk.x * WorldGrid.TilesPerChunk + Margin + i % span;
            int tileZ = chunk.y * WorldGrid.TilesPerChunk + Margin + i / span;

            if (!Landmarks.Level(tileX, tileZ, 1, 0.51f, worldSeed)) continue;
            if (Landmarks.Wet(tileX, tileZ, 2, worldSeed)) continue;

            bool taken = false;
            for (int dx = -2; dx <= 2 && !taken; dx += 2)
            for (int dz = -2; dz <= 2 && !taken; dz += 2)
                if (Landmarks.StructureOccupies(tileX + dx, tileZ + dz, worldSeed)) taken = true;
            if (taken) continue;

            result.Exists = true;
            result.Kind = kind;
            result.Yaw = Hash(chunk.x, chunk.y, worldSeed ^ 0x4C9) % 360;
            result.TileX = tileX;
            result.TileZ = tileZ;
            result.Position = new Vector3(
                tileX * WorldGrid.TileSize,
                WorldHeight.SurfaceY(tileX, tileZ, worldSeed),
                tileZ * WorldGrid.TileSize);
            return result;
        }

        return result;
    }

    /// <summary>Whether a find takes this tile, or one beside it.</summary>
    public static bool Occupies(int tileX, int tileZ, int worldSeed)
    {
        var chunk = new Vector2Int(
            Mathf.FloorToInt(tileX / (float)WorldGrid.TilesPerChunk),
            Mathf.FloorToInt(tileZ / (float)WorldGrid.TilesPerChunk));

        var at = In(chunk, worldSeed);
        return at.Exists && Mathf.Abs(tileX - at.TileX) <= 1 && Mathf.Abs(tileZ - at.TileZ) <= 1;
    }

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (int)(h & 0x7FFFFFFF);
    }
}
