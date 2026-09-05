using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the animals leave behind: prints in snow and sand, the ground a boar
/// has turned over, a feather where a bird went up, and the line worn where
/// things keep passing. None of it is kept between sessions -- a sign is a
/// thing of the last little while -- and all of it is drawn as a few hundred
/// flat pieces in one call per sort, so it costs nothing to have about.
///
/// The point of it is the surveyor's: you find the animal by what it left.
/// Prints lead somewhere; fresh rooting means a boar is near; a feather says
/// what stood here.
/// </summary>
public class Tracks : MonoBehaviour
{
    public enum Sort { SnowPrint, SandPrint, Rooting, PaleFeather, DarkFeather, Trail, Ring }

    private struct Mark
    {
        public Sort Sort;
        public Vector3 At;
        public float Yaw;
        public float Size;
        public float Made;
        public float Lasts;
        public FaunaKind Kind;
    }

    private static Tracks instance;

    private readonly List<Mark> marks = new List<Mark>(1024);
    private readonly Dictionary<Sort, List<Matrix4x4>> batches = new Dictionary<Sort, List<Matrix4x4>>();
    private readonly Dictionary<Sort, Material> paints = new Dictionary<Sort, Material>();
    private Mesh oval, feather, ring;

    // trails: how often each tile has been crossed lately
    private readonly Dictionary<long, float> worn = new Dictionary<long, float>();
    private readonly List<long> fading = new List<long>();

    private const int Most = 900;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Tracks>() != null) return;
        new GameObject("Tracks (runtime)").AddComponent<Tracks>();
    }

    private void Awake()
    {
        instance = this;
        oval = Oval(8, 1f, 0.7f);
        feather = Oval(6, 1f, 0.28f);
        ring = Annulus(16, 0.5f, 0.42f);

        paints[Sort.SnowPrint] = Paint.Flat(new Color(0.58f, 0.64f, 0.76f));
        paints[Sort.SandPrint] = Paint.Flat(new Color(0.60f, 0.48f, 0.30f));
        paints[Sort.Rooting] = Paint.Flat(new Color(0.28f, 0.22f, 0.15f));
        paints[Sort.PaleFeather] = Paint.Flat(new Color(0.90f, 0.90f, 0.87f));
        paints[Sort.DarkFeather] = Paint.Flat(new Color(0.09f, 0.09f, 0.11f));
        paints[Sort.Trail] = Paint.Flat(new Color(0.38f, 0.32f, 0.20f));
        paints[Sort.Ring] = Paint.Flat(new Color(0.86f, 0.92f, 0.97f));

        foreach (Sort s in System.Enum.GetValues(typeof(Sort))) batches[s] = new List<Matrix4x4>(256);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    // ------------------------------------------------------------ leaving

    /// <summary>A foot set down on ground that takes a print.</summary>
    public static void Print(Vector3 at, float yaw, float size, FaunaKind kind, int seed)
    {
        if (instance == null) return;
        int tx = Mathf.RoundToInt(at.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(at.z / WorldGrid.TileSize);
        if (WaterSurface.IsUnderwater(tx, tz, seed)) return;

        Sort sort;
        if (SnowCover.IsSnowy(tx, tz, seed)) sort = Sort.SnowPrint;
        else
        {
            var here = Regions.CharacterAtTile(tx, tz, seed);
            if (here == Regions.Character.Desert || here == Regions.Character.Water) sort = Sort.SandPrint;
            else return;                                     // grass keeps no print
        }

        // on the ground of its own tile, not where the foot was: a foot at a
        // terrace edge can be over the step, and a print there would hang
        at.y = WorldHeight.SurfaceY(tx, tz, seed) + Animal.FootingAt(tx, tz, seed) + 0.012f;
        instance.Leave(new Mark { Sort = sort, At = at, Yaw = yaw, Size = size, Made = Time.time,
                                  Lasts = sort == Sort.SnowPrint ? 240f : 150f, Kind = kind });
    }

    /// <summary>Ground turned over by a snout.</summary>
    public static void Root(Vector3 at, float size)
    {
        if (instance == null) return;
        instance.Leave(new Mark { Sort = Sort.Rooting, At = at + Vector3.up * 0.015f, Yaw = Random.Range(0f, 360f), Size = size, Made = Time.time, Lasts = 900f });
    }

    /// <summary>A feather, where a bird stood up and went.</summary>
    public static void Feather(Vector3 at, bool pale)
    {
        if (instance == null) return;
        instance.Leave(new Mark { Sort = pale ? Sort.PaleFeather : Sort.DarkFeather, At = at + Vector3.up * 0.014f, Yaw = Random.Range(0f, 360f), Size = 0.34f, Made = Time.time, Lasts = 1200f });
    }

    /// <summary>A ring on the water where a fish rose, spreading and fading.</summary>
    public static void Ring(Vector3 at)
    {
        if (instance == null) return;
        instance.Leave(new Mark { Sort = Sort.Ring, At = at + Vector3.up * 0.03f, Yaw = 0f, Size = 2.4f, Made = Time.time, Lasts = 4.5f });
    }

    /// <summary>An animal crossing a tile: enough crossings and the tile wears to a trail.</summary>
    public static void Cross(int tileX, int tileZ)
    {
        if (instance == null) return;
        long key = ((long)tileX << 32) ^ (uint)tileZ;
        instance.worn.TryGetValue(key, out float count);
        instance.worn[key] = Mathf.Min(count + 1f, 30f);
    }

    /// <summary>The nearest sign of a sort within reach of a point, or nothing.</summary>
    public static bool Near(Vector3 at, float within, out Sort sort, out FaunaKind kind)
    {
        sort = Sort.Trail; kind = FaunaKind.Deer;
        if (instance == null) return false;
        float best = within;
        foreach (var m in instance.marks)
        {
            float d = Vector3.Distance(m.At, at);
            if (d < best) { best = d; sort = m.Sort; kind = m.Kind; }
        }
        return best < within;
    }

    private void Leave(Mark mark)
    {
        if (marks.Count >= Most) marks.RemoveAt(0);
        marks.Add(mark);
    }

    // ------------------------------------------------------------ drawing

    private void Update()
    {
        float now = Time.time;

        // the old ones go; the last fifth of a mark's life it shrinks away
        for (int i = marks.Count - 1; i >= 0; i--)
            if (now - marks[i].Made > marks[i].Lasts) marks.RemoveAt(i);

        foreach (var pair in batches) pair.Value.Clear();

        foreach (var m in marks)
        {
            float age = (now - m.Made) / m.Lasts;
            float shrink = age > 0.8f ? Mathf.InverseLerp(1f, 0.8f, age) : 1f;
            float s = m.Size * shrink;
            if (m.Sort == Sort.Ring) s = m.Size * Mathf.Lerp(0.15f, 1f, Mathf.Sqrt(age));   // a ring spreads, and thins as it goes
            batches[m.Sort].Add(Matrix4x4.TRS(m.At, Quaternion.Euler(0f, m.Yaw, 0f), new Vector3(s, 1f, s)));
        }

        // the trails: worn tiles, fading slowly when nothing crosses them
        fading.Clear();
        foreach (var pair in worn) if (pair.Value < 0.05f) fading.Add(pair.Key);
        foreach (var key in fading) worn.Remove(key);
        var keys = new List<long>(worn.Keys);
        foreach (var key in keys)
        {
            float count = worn[key] - Time.deltaTime * 0.02f;
            worn[key] = count;
            if (count < 6f) continue;
            int tx = (int)(key >> 32), tz = (int)(uint)(key & 0xffffffff);
            var world = FindFirstObjectByType<ChunkManager>();
            int seed = world != null ? world.WorldSeed : 0;
            float y = WorldHeight.SurfaceY(tx, tz, seed) + Animal.FootingAt(tx, tz, seed) + 0.01f;
            float size = Mathf.Lerp(0.6f, 1.5f, Mathf.InverseLerp(6f, 30f, count));
            batches[Sort.Trail].Add(Matrix4x4.TRS(new Vector3(tx * WorldGrid.TileSize, y, tz * WorldGrid.TileSize), Quaternion.Euler(0f, (tx * 37 + tz * 17) % 180, 0f), new Vector3(size, 1f, size)));
        }

        foreach (var pair in batches)
        {
            if (pair.Value.Count == 0 || paints[pair.Key] == null) continue;
            var rp = new RenderParams(paints[pair.Key]) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true };
            var mesh = pair.Key == Sort.PaleFeather || pair.Key == Sort.DarkFeather ? feather : pair.Key == Sort.Ring ? ring : oval;
            for (int from = 0; from < pair.Value.Count; from += 1000)
                Graphics.RenderMeshInstanced(rp, mesh, 0, pair.Value, Mathf.Min(1000, pair.Value.Count - from), from);
        }
    }

    /// <summary>A flat ring, lying in the ground plane, a unit across.</summary>
    private static Mesh Annulus(int sides, float outer, float inner)
    {
        var verts = new Vector3[sides * 2];
        var norms = new Vector3[sides * 2];
        var tris = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            verts[i * 2] = new Vector3(Mathf.Cos(a) * inner, 0f, Mathf.Sin(a) * inner);
            verts[i * 2 + 1] = new Vector3(Mathf.Cos(a) * outer, 0f, Mathf.Sin(a) * outer);
            norms[i * 2] = norms[i * 2 + 1] = Vector3.up;
            int j = (i + 1) % sides;
            tris[i * 6] = i * 2; tris[i * 6 + 1] = j * 2 + 1; tris[i * 6 + 2] = i * 2 + 1;
            tris[i * 6 + 3] = i * 2; tris[i * 6 + 4] = j * 2; tris[i * 6 + 5] = j * 2 + 1;
        }
        var m = new Mesh { name = "ring" };
        m.vertices = verts; m.normals = norms; m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    /// <summary>A flat oval, lying in the ground plane, a unit long.</summary>
    private static Mesh Oval(int sides, float length, float width)
    {
        var verts = new Vector3[sides + 1];
        var norms = new Vector3[sides + 1];
        var tris = new int[sides * 3];
        verts[0] = Vector3.zero; norms[0] = Vector3.up;
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * width * 0.5f, 0f, Mathf.Sin(a) * length * 0.5f);
            norms[i + 1] = Vector3.up;
            tris[i * 3] = 0; tris[i * 3 + 1] = (i + 1) % sides + 1; tris[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { name = "oval" };
        m.vertices = verts; m.normals = norms; m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }
}
