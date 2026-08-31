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

    [Tooltip("New chunks built per frame once the world is running. Crossing a border needs a whole ring of them, which is enough work to show as a stutter if it all happens at once.")]
    [SerializeField, Range(1, 32)] private int chunksPerFrame = 4;

    [Tooltip("Same seed, same world. Leave at 0 for a different world each run.")]
    [SerializeField] private int worldSeed = 0;

    [Header("Performance")]
    [Tooltip("Skip chunks outside the camera. These tiles average ~3,900 vertices each, so this is the difference between drawing 70 million and 20 million per frame.")]
    [SerializeField] private bool frustumCulling = true;

    [Tooltip("Tiles casting shadows means running all that geometry a second time for the shadow map. Off by default; tiles still receive shadows.")]
    [SerializeField] private bool tilesCastShadows = false;

    [Header("Terrain")]
    [Tooltip("Raises tiles onto terraces and builds a walkable collision surface under them.")]
    [SerializeField] private bool terrainCollision = true;

    [Tooltip("Chunks around the player that get a physics collider. Terrain only needs to be solid where you can reach it.")]
    [SerializeField, Range(1, 4)] private int collisionRadius = 1;

    [Header("Water")]
    [Tooltip("Standing water in hollows below the water line.")]
    [SerializeField] private bool water = true;

    [Header("Flat Ground Fallback")]
    [Tooltip("Used only when terrain collision is off: keeps flat floor under the player past the scene's ground plane.")]
    [SerializeField] private bool maintainGroundCollider = true;

    [Tooltip("Height of the walkable surface. Must match the scene's ground collider, or you get a step where they meet.")]
    [SerializeField] private float groundSurfaceY = 1.05f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private bool drawDebugGizmos = true;

    /// <summary>The player the world is streaming around. Read by the map.</summary>
    public Transform PlayerTransform => playerTransform;

    /// <summary>The seed this world was generated from. Read by the map.</summary>
    public int WorldSeed => worldSeed;

    /// <summary>Counters for the overlay, so tuning draw distance is not guesswork.</summary>
    public struct DrawStats
    {
        public int resident, visible, drawn, pending, drawCalls, instances;
    }

    public DrawStats Stats => stats;

    private DrawStats stats;

    /// <summary>How far chunks are drawn. Landmarks have to reach at least this far.</summary>
    public int ViewRadius => viewRadius;

    /// <summary>Changes the draw distance while running, rebuilding what is visible.</summary>
    public void SetViewRadius(int radius)
    {
        int clamped = Mathf.Clamp(radius, 1, 8);

        if (clamped == viewRadius) return;

        viewRadius = clamped;
        RefreshVisibleChunks(force: true);
    }

    private Vector2Int playerChunk;
    private bool hasPlayerChunk;

    private readonly List<Chunk> visibleChunks = new List<Chunk>();

    // Every visible chunk's tiles, merged into one array per tile type. Drawing
    // per chunk meant chunks x tile types calls per frame — at view radius 4
    // that is around two thousand. Merged, it is one call per tile type
    // regardless of how far you can see.
    // One buffer per tile type, sized for the worst case (every visible chunk)
    // and reused. Each frame the on-screen chunks are copied in, so the draw
    // keeps one call per tile type while still culling what is behind you.
    private readonly Dictionary<int, Matrix4x4[]> batches = new Dictionary<int, Matrix4x4[]>();
    private readonly Dictionary<int, int> batchCounts = new Dictionary<int, int>();
    private readonly List<int> batchTypes = new List<int>();

    private readonly Plane[] frustum = new Plane[6];
    private Camera view;

    // Mesh and material per tile type, looked up once. These were being
    // resolved every frame for every type, and MaterialGetter writes to the
    // material each time it is called.
    private readonly Dictionary<int, (Mesh mesh, Material material)> resolved =
        new Dictionary<int, (Mesh, Material)>();
    private Bounds visibleBounds;
    private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private readonly List<Vector2Int> evictionScratch = new List<Vector2Int>();

    // Chunks wanted but not built yet, nearest first.
    private readonly List<Vector2Int> pending = new List<Vector2Int>();
    private bool batchesDirty;

    // RenderParams per material, built once — creating them per draw would
    // allocate every frame for no benefit.
    private readonly Dictionary<Material, RenderParams> renderParamsCache =
        new Dictionary<Material, RenderParams>();

    private const float GroundThickness = 0.1f;

    private Transform groundCollider;

    private readonly Dictionary<Vector2Int, GameObject> chunkColliders = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, GameObject> waterPatches = new Dictionary<Vector2Int, GameObject>();
    private readonly List<Vector2Int> waterScratch = new List<Vector2Int>();
    private Transform waterRoot;
    private Material waterMaterial;
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

        if (SaveGame.HasSave)
        {
            worldSeed = SaveGame.Data.seed;      // return to the world you left
        }
        else if (worldSeed == 0)
        {
            worldSeed = Random.Range(1, int.MaxValue);
        }

        if (terrainCollision)
        {
            terrainRoot = new GameObject("Terrain Collision (runtime)").transform;
            terrainRoot.SetParent(transform, worldPositionStays: true);
        }

        if (water)
        {
            waterRoot = new GameObject("Water (runtime)").transform;
            waterRoot.SetParent(transform, worldPositionStays: true);
            waterMaterial = WaterSurface.CreateMaterial();
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
        BuildPending();

        if (batchesDirty)
        {
            RebuildBatches();
            batchesDirty = false;
        }

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

        ExplorationLog.Visit(playerChunk);

        visibleChunks.Clear();
        pending.Clear();

        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dz = -viewRadius; dz <= viewRadius; dz++)
        {
            var index = new Vector2Int(playerChunk.x + dx, playerChunk.y + dz);

            if (chunks.TryGetValue(index, out var chunk))
            {
                visibleChunks.Add(chunk);
            }
            else
            {
                pending.Add(index);
            }
        }

        // nearest first, so the ground fills in outward from the player
        pending.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.x - playerChunk.x) + Mathf.Abs(a.y - playerChunk.y);
            int db = Mathf.Abs(b.x - playerChunk.x) + Mathf.Abs(b.y - playerChunk.y);
            return da.CompareTo(db);
        });

        // The first fill has nothing on screen to protect, so it happens at once.
        if (force)
        {
            BuildPending(pending.Count);
        }

        batchesDirty = true;
        EvictDistantChunks();

        if (terrainCollision)
        {
            RefreshChunkColliders();
        }
        else
        {
            UpdateGroundCollider();
        }

        if (water)
        {
            RefreshWater();
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
    /// Sizes one buffer per tile type to hold every visible chunk at once, so
    /// the per-frame fill never allocates. Runs only when the visible set
    /// changes, which is when the player crosses a chunk border.
    /// </summary>
    private void RebuildBatches()
    {
        batchTypes.Clear();

        foreach (var pair in batchCounts)
        {
            batchTypes.Add(pair.Key);
        }

        // count worst-case instances per type across the visible set
        var needed = new Dictionary<int, int>();

        for (int c = 0; c < visibleChunks.Count; c++)
        {
            foreach (var pair in visibleChunks[c].idToTransforms)
            {
                needed.TryGetValue(pair.Key, out int have);
                needed[pair.Key] = have + pair.Value.Length;
            }
        }

        batchTypes.Clear();

        foreach (var pair in needed)
        {
            if (!batches.TryGetValue(pair.Key, out var buffer) || buffer.Length < pair.Value)
            {
                batches[pair.Key] = new Matrix4x4[Mathf.NextPowerOfTwo(pair.Value)];
            }

            batchTypes.Add(pair.Key);
        }

        float span = (viewRadius * 2 + 1) * WorldGrid.ChunkWorldSize;

        visibleBounds = new Bounds(
            WorldGrid.ChunkCenter(playerChunk) + Vector3.up * (WorldHeight.MaxRelief * 0.5f),
            new Vector3(span, WorldHeight.MaxRelief + 24f, span)
        );
    }

    /// <summary>Builds a few of the waiting chunks, so a border crossing is spread over frames.</summary>
    private void BuildPending(int budget = -1)
    {
        if (pending.Count == 0) return;

        int allowed = budget < 0 ? chunksPerFrame : budget;
        int made = 0;

        while (pending.Count > 0 && made < allowed)
        {
            var index = pending[0];
            pending.RemoveAt(0);

            visibleChunks.Add(GetOrCreateChunk(index));
            made++;
        }

        if (made > 0) batchesDirty = true;
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
        if (view == null)
        {
            view = Camera.main;
        }

        bool cull = frustumCulling && view != null;

        if (cull)
        {
            GeometryUtility.CalculateFrustumPlanes(view, frustum);
        }

        batchCounts.Clear();

        int culled = 0;

        for (int c = 0; c < visibleChunks.Count; c++)
        {
            Chunk chunk = visibleChunks[c];

            if (cull && !GeometryUtility.TestPlanesAABB(frustum, chunk.Bounds))
            {
                culled++;
                continue;
            }

            foreach (var pair in chunk.idToTransforms)
            {
                if (!batches.TryGetValue(pair.Key, out var buffer))
                {
                    continue;
                }

                batchCounts.TryGetValue(pair.Key, out int count);

                if (count + pair.Value.Length > buffer.Length)
                {
                    continue;
                }

                // straight memory copy — no per-frame allocation
                System.Array.Copy(pair.Value, 0, buffer, count, pair.Value.Length);
                batchCounts[pair.Key] = count + pair.Value.Length;
            }
        }

        int drawCalls = 0;
        int totalInstances = 0;

        foreach (var pair in batchCounts)
        {
            if (pair.Value == 0) continue;

            if (!resolved.TryGetValue(pair.Key, out var art))
            {
                art = Resolve(pair.Key);
                resolved[pair.Key] = art;
            }

            Mesh mesh = art.mesh;
            Material material = art.material;

            if (mesh == null || material == null)
            {
                continue;
            }

            Matrix4x4[] transforms = batches[pair.Key];
            int instanceCount = pair.Value;

            RenderParams rp = GetRenderParams(material);
            rp.worldBounds = visibleBounds;

            const int Max = 1023;

            for (int i = 0; i < instanceCount; i += Max)
            {
                int count = Mathf.Min(Max, instanceCount - i);

                Graphics.RenderMeshInstanced(rp, mesh, 0, transforms, count, i);

                drawCalls++;
                totalInstances += count;
            }
        }

        stats.resident = chunks.Count;
        stats.visible = visibleChunks.Count;
        stats.drawn = visibleChunks.Count - culled;
        stats.pending = pending.Count;
        stats.drawCalls = drawCalls;
        stats.instances = totalInstances;

        if (debugMode && (logEveryFrame || !hasLoggedFirstDrawSummary))
        {
            Debug.Log(
                "[ChunkManager] Draw summary | Draw calls: " + drawCalls +
                " | Instances: " + totalInstances +
                " | Chunks drawn: " + (visibleChunks.Count - culled) + "/" + visibleChunks.Count +
                " | Shadows: " + (tilesCastShadows ? "on" : "off")
            );

            hasLoggedFirstDrawSummary = true;
        }
    }

    /// <summary>Finds the mesh and material for a tile type, once.</summary>
    private (Mesh, Material) Resolve(int id)
    {
        if (!tileLibrary.TryGet(id, out var def) || def == null)
        {
            return (null, null);
        }

        Mesh mesh = def.MeshGetter();
        Material material = overrideDrawMaterial != null ? overrideDrawMaterial : def.MaterialGetter();

        return (mesh, material);
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

                // Tens of thousands of instances, so per-instance probe work is
                // not free. The tiles are lit flatly enough not to miss it.
                lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
                reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off,
                shadowCastingMode = tilesCastShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off
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

    /// <summary>Water is built for the chunks in view and dropped with them.</summary>
    private void RefreshWater()
    {
        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dz = -viewRadius; dz <= viewRadius; dz++)
        {
            var index = new Vector2Int(playerChunk.x + dx, playerChunk.y + dz);

            if (waterPatches.ContainsKey(index)) continue;

            var mesh = WaterSurface.BuildMesh(index, worldSeed);

            // A dry chunk still gets an entry, so it is not rebuilt every time.
            var go = new GameObject("Water " + index);
            go.transform.SetParent(waterRoot, worldPositionStays: true);
            go.transform.position = new Vector3(index.x * WorldGrid.ChunkWorldSize, 0f, index.y * WorldGrid.ChunkWorldSize);

            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            }

            waterPatches.Add(index, go);
        }

        waterScratch.Clear();

        foreach (var pair in waterPatches)
        {
            Vector2Int offset = pair.Key - playerChunk;

            if (Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) > viewRadius + keepRadiusPadding)
            {
                waterScratch.Add(pair.Key);
            }
        }

        foreach (var index in waterScratch)
        {
            var go = waterPatches[index];
            waterPatches.Remove(index);

            var filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);

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
