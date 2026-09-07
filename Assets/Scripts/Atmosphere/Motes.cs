using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What drifts in the air by country: leaves turning down through the
/// woods, seeds riding the wind over the low ground, dust low over the
/// sand. Small flat-shaded lumps drawn in a few instanced calls, kept up
/// around the camera and let go when they land or drift off. Rain knocks
/// most of them down.
/// </summary>
public class Motes : MonoBehaviour
{
    public enum Kind { None, Leaves, Seeds, Dust }

    private struct Mote
    {
        public Vector3 At, Vel;
        public float Size, Phase, Made, Lasts;
        public Kind Kind;
        public int Tint;
    }

    private static Motes instance;

    /// <summary>What is in the air here and how much of it, for the probes.</summary>
    public static Kind Current { get; private set; }
    public static int Alive => instance != null ? instance.motes.Count : 0;

    private readonly List<Mote> motes = new List<Mote>(256);
    private readonly List<Matrix4x4>[] batches = new List<Matrix4x4>[5];
    private Material[] paints;
    private Mesh leaf, lump;
    private ChunkManager world;
    private float owed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<Motes>() == null) new GameObject("Motes (runtime)").AddComponent<Motes>();
    }

    private void Awake()
    {
        instance = this;
        leaf = Leaf();
        lump = Lump();
        paints = new[]
        {
            Paint.Flat(new Color(0.46f, 0.52f, 0.22f)),   // leaf, green going over
            Paint.Flat(new Color(0.58f, 0.40f, 0.20f)),   // leaf, brown
            Paint.Flat(new Color(0.78f, 0.64f, 0.26f)),   // leaf, yellow
            Paint.Flat(new Color(0.94f, 0.92f, 0.84f)),   // seed
            Paint.Flat(new Color(0.80f, 0.72f, 0.55f))    // dust
        };
        for (int i = 0; i < batches.Length; i++) batches[i] = new List<Matrix4x4>(128);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private static Kind KindFor(Regions.Character c) => c switch
    {
        Regions.Character.Forest or Regions.Character.Fungal or Regions.Character.Dead => Kind.Leaves,
        Regions.Character.Lowland or Regions.Character.Hills or Regions.Character.Reed => Kind.Seeds,
        Regions.Character.Desert or Regions.Character.Stone => Kind.Dust,
        _ => Kind.None
    };

    private void LateUpdate()
    {
        var view = Camera.main;
        if (view == null) return;
        if (world == null) { world = FindFirstObjectByType<ChunkManager>(); if (world == null) return; }

        int seed = world.WorldSeed;
        Vector3 eye = view.transform.position;
        Current = KindFor(Regions.CharacterAtTile(Mathf.RoundToInt(eye.x / WorldGrid.TileSize), Mathf.RoundToInt(eye.z / WorldGrid.TileSize), seed));

        float dt = Time.deltaTime;
        float rain = Rain.Intensity;
        Vector3 wind = Rain.Wind.sqrMagnitude > 0.01f ? Rain.Wind : new Vector3(0.8f, 0f, 0.5f);
        int wanted = Current == Kind.None ? 0 : Mathf.RoundToInt((Current == Kind.Leaves ? 110 : Current == Kind.Seeds ? 90 : 70) * (1f - rain * 0.85f));

        // new ones come in at a steady rate up to the count, so a change of country is a drift and not a switch
        if (motes.Count < wanted) owed += dt * 40f; else owed = 0f;

        while (owed >= 1f && motes.Count < wanted)
        {
            owed -= 1f;
            var flat = Random.insideUnitCircle * 22f;
            Vector3 at = eye + new Vector3(flat.x, 0f, flat.y);
            float ground = WaterSurface.WalkingY(Mathf.RoundToInt(at.x / WorldGrid.TileSize), Mathf.RoundToInt(at.z / WorldGrid.TileSize), seed);
            var m = new Mote { Kind = Current, Phase = Random.value * 6.28f, Made = Time.time, Tint = 0 };

            switch (Current)
            {
                case Kind.Leaves:
                    m.At = new Vector3(at.x, ground + Random.Range(1.5f, 9f), at.z);
                    m.Vel = new Vector3(0f, -Random.Range(0.25f, 0.45f), 0f);
                    m.Size = Random.Range(0.12f, 0.2f); m.Lasts = Random.Range(8f, 16f); m.Tint = Random.Range(0, 3);
                    break;
                case Kind.Seeds:
                    m.At = new Vector3(at.x, ground + Random.Range(0.4f, 4f), at.z);
                    m.Vel = new Vector3(0f, Random.Range(-0.05f, 0.08f), 0f);
                    m.Size = Random.Range(0.035f, 0.055f); m.Lasts = Random.Range(6f, 12f); m.Tint = 3;
                    break;
                default:
                    m.At = new Vector3(at.x, ground + Random.Range(0.1f, 1.6f), at.z);
                    m.Vel = Vector3.zero;
                    m.Size = Random.Range(0.04f, 0.075f); m.Lasts = Random.Range(4f, 9f); m.Tint = 4;
                    break;
            }

            motes.Add(m);
        }

        foreach (var b in batches) b.Clear();
        float t = Time.time;

        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var m = motes[i];
            float age = (t - m.Made) / m.Lasts;

            // each kind rides the air its own way
            float windShare = m.Kind == Kind.Leaves ? 0.35f : m.Kind == Kind.Seeds ? 0.6f : 0.45f;
            Vector3 sway = new Vector3(Mathf.Sin(t * 1.3f + m.Phase), 0f, Mathf.Cos(t * 1.1f + m.Phase * 1.7f)) * (m.Kind == Kind.Leaves ? 0.5f : 0.25f);
            m.At += (m.Vel + wind * windShare + sway) * dt;

            float ground = WaterSurface.WalkingY(Mathf.RoundToInt(m.At.x / WorldGrid.TileSize), Mathf.RoundToInt(m.At.z / WorldGrid.TileSize), seed);
            bool gone = age >= 1f || m.At.y < ground + 0.02f || Vector2.Distance(new Vector2(m.At.x, m.At.z), new Vector2(eye.x, eye.z)) > 32f;
            if (gone) { motes.RemoveAt(i); continue; }

            motes[i] = m;

            // in and out over its life, so none pops
            float show = Mathf.Min(1f, Mathf.Min(age, 1f - age) * 6f);
            float size = m.Size * show;
            Quaternion turn = m.Kind == Kind.Leaves
                ? Quaternion.Euler(t * 70f + m.Phase * 50f, t * 40f + m.Phase * 90f, Mathf.Sin(t * 2f + m.Phase) * 40f)
                : Quaternion.Euler(m.Phase * 57f, t * 30f, 0f);
            var mesh = m.Kind == Kind.Leaves ? leaf : lump;
            batches[m.Tint].Add(Matrix4x4.TRS(m.At, turn, m.Kind == Kind.Leaves ? new Vector3(size, size * 0.6f, size * 0.15f) : Vector3.one * size));
        }

        for (int b = 0; b < batches.Length; b++)
        {
            if (batches[b].Count == 0 || paints[b] == null) continue;
            var rp = new RenderParams(paints[b]) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(rp, b < 3 ? leaf : lump, 0, batches[b], batches[b].Count, 0);
        }
    }

    /// <summary>A leaf: a lozenge with a slight fold, faced both ways.</summary>
    private static Mesh Leaf()
    {
        Vector3[] v = { new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0.5f, 0.15f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, -0.5f, 0.15f) };
        var verts = new List<Vector3>(); var tris = new List<int>();
        int[][] faces = { new[] { 0, 1, 2 }, new[] { 0, 2, 3 }, new[] { 2, 1, 0 }, new[] { 3, 2, 0 } };
        foreach (var f in faces) { int n = verts.Count; verts.Add(v[f[0]]); verts.Add(v[f[1]]); verts.Add(v[f[2]]); tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); }
        var m = new Mesh { name = "leaf" }; m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    private static Mesh Lump()
    {
        Vector3[] tips = { Vector3.right * 0.5f, Vector3.left * 0.5f, Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f };
        int[][] faces = { new[] { 2, 0, 4 }, new[] { 2, 4, 1 }, new[] { 2, 1, 5 }, new[] { 2, 5, 0 }, new[] { 3, 4, 0 }, new[] { 3, 1, 4 }, new[] { 3, 5, 1 }, new[] { 3, 0, 5 } };
        var verts = new List<Vector3>(); var tris = new List<int>();
        foreach (var f in faces) { int n = verts.Count; verts.Add(tips[f[0]]); verts.Add(tips[f[1]]); verts.Add(tips[f[2]]); tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); }
        var m = new Mesh { name = "mote" }; m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
}
