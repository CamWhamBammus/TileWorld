using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Streams the world around the player: creates chunks as they come into range,
/// draws them with GPU instancing, and drops the ones left far behind.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private TileLibrary tileLibrary;

    [Header("Build Safety")]
    [SerializeField] private Material overrideDrawMaterial;

    [Header("World")]
    [Tooltip("Chunks drawn in every direction around the player. 4 = a 9x9 block, about 270 units to the horizon.")]
    [SerializeField, Range(1, 8)] private int viewRadius = 4;

    [Tooltip("Chunks are discarded once they are this far outside the view radius.")]
    [SerializeField, Range(1, 8)] private int keepRadiusPadding = 2;

    [Tooltip("Same seed, same world. Leave at 0 for a different world each run.")]
    [SerializeField] private int worldSeed = 0;

    [Header("Terrain")]
    [Tooltip("Raises tiles onto terraces and builds a walkable collision surface under them.")]
    [SerializeField] private bool terrainCollision = true;

    [Tooltip("Chunks around the player that get a physics collider. Terrain only needs to be solid where you can reach it.")]
    [SerializeField, Range(1, 4)] private int collisionRadius = 1;

    [Header("Flat Ground Fallback")]
    [Tooltip("Used only when terrain collision is off: keeps flat floor under the player past the scene's ground plane.")]
    [SerializeField] private bool maintainGroundCollider = true;

    [Tooltip("Height of the walkable surface. Must match the scene's ground collider, or you get a step where they meet.")]
    [SerializeField] private float groundSurfaceY = 1.05f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private bool drawDebugGizmos = true;

    private Vector2Int playerChunk;
    private bool hasPlayerChunk;

    private readonly List<Chunk> visibleChunks = new List<Chunk>();

    // Every visible chunk's tiles, merged into one array per tile type. Drawing
    // per chunk meant chunks x tile types calls per frame — at view radius 4
    // that is around two thousand. Merged, it is one call per tile type
    // regardless of how far you can see.
    private readonly Dictionary<int, List<Matrix4x4>> batchScratch = new Dictionary<int, List<Matrix4x4>>();

    // Buffers are reused across rebuilds and only grow — rebuilding happens on
    // every chunk border crossing, and reallocating a megabyte of matrices each
    // time would hand the GC a steady drip of garbage.
    private readonly Dictionary<int, Matrix4x4[]> batches = new Dictionary<int, Matrix4x4[]>();
    private readonly Dictionary<int, int> batchCounts = new Dictionary<int, int>();
    private Bounds visibleBounds;
    private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private readonly List<Vector2Int> evictionScratch = new List<Vector2Int>();

    // RenderParams per material, built once — creating them per draw would
    // allocate every frame for no benefit.
    private readonly Dictionary<Material, RenderParams> renderParamsCache =
        new Dictionary<Material, RenderParams>();

    private const float GroundThickness = 0.1f;

    private Transform groundCollider;

    private readonly Dictionary<Vector2Int, GameObject> chunkColliders = new Dictionary<Vector2Int, GameObject>();
    private readonly List<Vector2Int> colliderScratch = new List<Vector2Int>();
    private Transform terrainRoot;

    private bool hasLoggedMissingReferences;
    private bool hasLoggedFirstDrawSummary;

    private void Start()
    {
        if (!ValidateReferences())
        {
            return;
        }

        if (worldSeed == 0)
        {
            worldSeed = Random.Range(1, int.MaxValue);
        }

        if (terrainCollision)
        {
            terrainRoot = new GameObject("Terrain Collision (runtime)").transform;
            terrainRoot.SetParent(transform, worldPositionStays: true);
        }
        else if (maintainGroundCollider)
        {
            CreateGroundCollider();
        }

        RefreshVisibleChunks(force: true);

        if (terrainCollision)
        {
            PlacePlayerOnSurface();
        }

        if (debugMode)
        {
            Debug.Log("[ChunkManager] World seed: " + worldSeed + " | View radius: " + viewRadius);
        }
    }

    private void Update()
    {
        if (!ValidateReferences())
        {
            return;
        }

        RefreshVisibleChunks(force: false);
        DrawChunks();
    }

    /// <summary>
    /// Rebuilds the visible set only when the player actually crosses a chunk
    /// border. The old version rebuilt it — and logged — every single frame.
    /// </summary>
    private void RefreshVisibleChunks(bool force)
    {
        Vector2Int current = WorldGrid.WorldToChunk(playerTransform.position);

        if (!force && hasPlayerChunk && current == playerChunk)
        {
            return;
        }

        playerChunk = current;
        hasPlayerChunk = true;

        visibleChunks.Clear();

        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dz = -viewRadius; dz <= viewRadius; dz++)
        {
            visibleChunks.Add(GetOrCreateChunk(new Vector2Int(playerChunk.x + dx, playerChunk.y + dz)));
        }

        RebuildBatches();
        EvictDistantChunks();

        if (terrainCollision)
        {
            RefreshChunkColliders();
        }
        else
        {
            UpdateGroundCollider();
        }

        if (debugMode && logEveryFrame)
        {
            Debug.Log(
                "[ChunkManager] Player chunk: " + playerChunk +
                " | Visible: " + visibleChunks.Count +
                " | Resident: " + chunks.Count
            );
        }
    }

    /// <summary>
    /// Merges the visible chunks into one instance array per tile type. Only
    /// runs when the player crosses a chunk border, so the per-frame draw does
    /// no work beyond issuing the calls.
    /// </summary>
    private void RebuildBatches()
    {
        foreach (var pair in batchScratch)
        {
            pair.Value.Clear();
        }

        for (int c = 0; c < visibleChunks.Count; c++)
        {
            foreach (var pair in visibleChunks[c].idToTransforms)
            {
                if (!batchScratch.TryGetValue(pair.Key, out var list))
                {
                    list = new List<Matrix4x4>(pair.Value.Length * 16);
                    batchScratch[pair.Key] = list;
                }

                list.AddRange(pair.Value);
            }
        }

        batchCounts.Clear();

        foreach (var pair in batchScratch)
        {
            int count = pair.Value.Count;

            if (count == 0)
            {
                continue;
            }

            if (!batches.TryGetValue(pair.Key, out var buffer) || buffer.Length < count)
            {
                buffer = new Matrix4x4[Mathf.NextPowerOfTwo(count)];
                batches[pair.Key] = buffer;
            }

            pair.Value.CopyTo(buffer);
            batchCounts[pair.Key] = count;
        }

        float span = (viewRadius * 2 + 1) * WorldGrid.ChunkWorldSize;

        visibleBounds = new Bounds(
            WorldGrid.ChunkCenter(playerChunk) + Vector3.up * (WorldHeight.MaxRelief * 0.5f),
            new Vector3(span, WorldHeight.MaxRelief + 24f, span)
        );
    }

    private Chunk GetOrCreateChunk(Vector2Int index)
    {
        if (!chunks.TryGetValue(index, out var chunk))
        {
            chunk = new Chunk(index, worldSeed);
            chunks.Add(index, chunk);
        }

        return chunk;
    }

    /// <summary>
    /// Chunks are cheap to regenerate and identical every time (the seed makes
    /// generation deterministic), so holding every chunk ever visited just grows
    /// memory forever. Anything well outside view goes.
    /// </summary>
    private void EvictDistantChunks()
    {
        int keepRadius = viewRadius + keepRadiusPadding;

        evictionScratch.Clear();

        foreach (var pair in chunks)
        {
            Vector2Int offset = pair.Key - playerChunk;

            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > keepRadius)
            {
                evictionScratch.Add(pair.Key);
            }
        }

        foreach (var index in evictionScratch)
        {
            chunks.Remove(index);
        }

        if (debugMode && evictionScratch.Count > 0 && logEveryFrame)
        {
            Debug.Log("[ChunkManager] Evicted " + evictionScratch.Count + " distant chunks.");
        }
    }

    private void DrawChunks()
    {
        int drawCalls = 0;
        int totalInstances = 0;

        foreach (var pair in batchCounts)
        {
            if (!tileLibrary.TryGet(pair.Key, out var def) || def == null)
            {
                continue;
            }

            Mesh mesh = def.MeshGetter();
            Material material = overrideDrawMaterial != null ? overrideDrawMaterial : def.MaterialGetter();

            if (mesh == null || material == null)
            {
                continue;
            }

            Matrix4x4[] transforms = batches[pair.Key];
            int instanceCount = pair.Value;

            RenderParams rp = GetRenderParams(material);
            rp.worldBounds = visibleBounds;

            // Instanced draws cap out at 1023 per call; startInstance walks the
            // array in place, so no per-frame copies are made.
            const int Max = 1023;

            for (int i = 0; i < instanceCount; i += Max)
            {
                int count = Mathf.Min(Max, instanceCount - i);

                Graphics.RenderMeshInstanced(rp, mesh, 0, transforms, count, i);

                drawCalls++;
                totalInstances += count;
            }
        }

        if (debugMode && (logEveryFrame || !hasLoggedFirstDrawSummary))
        {
            Debug.Log(
                "[ChunkManager] Draw summary | Draw calls: " + drawCalls +
                " | Instances: " + totalInstances +
                " | Visible chunks: " + visibleChunks.Count +
                " | Resident chunks: " + chunks.Count
            );

            hasLoggedFirstDrawSummary = true;
        }
    }

    private RenderParams GetRenderParams(Material material)
    {
        if (!renderParamsCache.TryGetValue(material, out var rp))
        {
            material.enableInstancing = true;

            rp = new RenderParams(material)
            {
                layer = gameObject.layer,
                receiveShadows = true,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On
            };

            renderParamsCache[material] = rp;
        }

        return rp;
    }

    /// <summary>
    /// The scene ships with a fixed 500x500 ground box, so an "infinite" world
    /// ran out of floor about eight chunks from spawn and dropped the player
    /// through it. This keeps a collider centred under them instead.
    /// </summary>
    private void CreateGroundCollider()
    {
        var go = new GameObject("Infinite Ground (runtime)");
        go.transform.SetParent(transform, worldPositionStays: true);

        var box = go.AddComponent<BoxCollider>();
        float span = (viewRadius * 2 + 1 + keepRadiusPadding) * WorldGrid.ChunkWorldSize;
        box.size = new Vector3(span, GroundThickness, span);

        groundCollider = go.transform;
    }

    private void UpdateGroundCollider()
    {
        if (groundCollider == null)
        {
            return;
        }

        // Positioned by its top face, not its centre, so this surface lines up
        // exactly with the scene's ground box instead of leaving a lip to trip on.
        Vector3 centre = WorldGrid.ChunkCenter(playerChunk);
        groundCollider.position = new Vector3(centre.x, groundSurfaceY - GroundThickness * 0.5f, centre.z);
    }

    /// <summary>
    /// The scene spawns the player at a fixed height that assumed flat ground.
    /// With terrain, the hill under spawn can be higher than that — which would
    /// start the player inside the collision mesh. Lift them clear.
    /// </summary>
    private void PlacePlayerOnSurface()
    {
        Vector3 p = playerTransform.position;

        int tileX = Mathf.RoundToInt(p.x / WorldGrid.TileSize);
        int tileZ = Mathf.RoundToInt(p.z / WorldGrid.TileSize);

        float surface = WorldHeight.SurfaceY(tileX, tileZ, worldSeed);

        if (p.y >= surface + 0.1f)
        {
            return;
        }

        // A CharacterController overrides direct transform writes, so it has to
        // be off while the position changes.
        var controller = playerTransform.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;

        if (wasEnabled)
        {
            controller.enabled = false;
        }

        playerTransform.position = new Vector3(p.x, surface + 0.5f, p.z);

        if (wasEnabled)
        {
            controller.enabled = true;
        }

        if (debugMode)
        {
            Debug.Log("[ChunkManager] Placed player on the surface at y=" + (surface + 0.5f));
        }
    }

    /// <summary>
    /// Colliders are the expensive part of terrain, so only the chunks the
    /// player can physically reach get one; the rest are drawn but not solid.
    /// </summary>
    private void RefreshChunkColliders()
    {
        for (int dx = -collisionRadius; dx <= collisionRadius; dx++)
        for (int dz = -collisionRadius; dz <= collisionRadius; dz++)
        {
            var index = new Vector2Int(playerChunk.x + dx, playerChunk.y + dz);

            if (chunkColliders.ContainsKey(index))
            {
                continue;
            }

            var go = new GameObject("ChunkCollision " + index);
            go.transform.SetParent(terrainRoot, worldPositionStays: true);
            go.transform.position = new Vector3(
                index.x * WorldGrid.ChunkWorldSize,
                0f,
                index.y * WorldGrid.ChunkWorldSize
            );

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = TerrainCollision.BuildMesh(index, worldSeed);

            chunkColliders.Add(index, go);
        }

        colliderScratch.Clear();

        foreach (var pair in chunkColliders)
        {
            Vector2Int offset = pair.Key - playerChunk;

            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > collisionRadius)
            {
                colliderScratch.Add(pair.Key);
            }
        }

        foreach (var index in colliderScratch)
        {
            var go = chunkColliders[index];
            chunkColliders.Remove(index);

            var mc = go.GetComponent<MeshCollider>();

            if (mc != null && mc.sharedMesh != null)
            {
                Destroy(mc.sharedMesh);
            }

            Destroy(go);
        }
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (playerTransform == null)
        {
            valid = false;

            if (!hasLoggedMissingReferences)
            {
                Debug.LogError("[ChunkManager] Missing Player Transform. Drag PlayerArmature into the Player Transform slot on WorldCreator.");
            }
        }

        if (tileLibrary == null)
        {
            valid = false;

            if (!hasLoggedMissingReferences)
            {
                Debug.LogError("[ChunkManager] Missing Tile Library. Drag your TileLibrary asset into the Tile Library slot on WorldCreator.");
            }
        }

        if (!valid)
        {
            hasLoggedMissingReferences = true;
        }

        return valid;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos || playerTransform == null)
        {
            return;
        }

        Gizmos.DrawWireCube(
            WorldGrid.ChunkCenter(playerChunk),
            new Vector3(WorldGrid.ChunkWorldSize, 0.1f, WorldGrid.ChunkWorldSize)
        );

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        float span = (viewRadius * 2 + 1) * WorldGrid.ChunkWorldSize;
        Gizmos.DrawWireCube(WorldGrid.ChunkCenter(playerChunk), new Vector3(span, 0.1f, span));
    }
}
