using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Birds crossing the sky by day: a flock in a loose V now and then, high
/// and far off, each bird a dark chevron beating its wings on its own
/// time, drawn in one instanced call and gone when it has crossed.
/// </summary>
public class Flocks : MonoBehaviour
{
    private class Flock
    {
        public Vector3 Lead, Heading;
        public float Speed;
        public float[] Phase, Along, Aside, Bob;
        public float Born;
    }

    /// <summary>For the probes: birds aloft, flocks, and a way to call one up.</summary>
    public static int Birds { get; private set; }
    public static int Count => instance != null ? instance.flocks.Count : 0;
    public static Vector3 Nearest => instance != null && instance.flocks.Count > 0 ? instance.flocks[instance.flocks.Count - 1].Lead : Vector3.zero;

    private static Flocks instance;
    private readonly List<Flock> flocks = new List<Flock>();
    private readonly List<Matrix4x4> batch = new List<Matrix4x4>(64);
    private Mesh bird;
    private Material paint;
    private float nextFlock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Flocks>() == null) new GameObject("Flocks (runtime)").AddComponent<Flocks>();
    }

    private void Awake()
    {
        instance = this;
        bird = Bird();
        paint = Paint.Flat(new Color(0.12f, 0.12f, 0.14f));
        nextFlock = Time.time + Random.Range(15f, 40f);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>A flock now, from the dev tools and the probes: from off to one side, heading across the view.</summary>
    public static void Summon()
    {
        if (instance == null) return;
        var view = Camera.main; if (view == null) return;
        instance.Launch(view.transform, true);
    }

    private void Launch(Transform view, bool across)
    {
        float bearing = across ? view.eulerAngles.y + Random.Range(-40f, 40f) : Random.Range(0f, 360f);
        Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float far = across ? 170f : Random.Range(160f, 320f);
        Vector3 start = view.position + dir * far + side * (across ? 160f : Random.Range(-220f, 220f)) + Vector3.up * Random.Range(60f, 130f);
        Vector3 heading = across ? -side : Quaternion.Euler(0f, Random.Range(50f, 130f) * (Random.value < 0.5f ? 1f : -1f), 0f) * dir;

        int n = Random.Range(7, 16);
        var f = new Flock { Lead = start, Heading = heading.normalized, Speed = Random.Range(10f, 13f), Phase = new float[n], Along = new float[n], Aside = new float[n], Bob = new float[n], Born = Time.time };
        for (int i = 0; i < n; i++)
        {
            int rank = (i + 1) / 2; float wing = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f);
            f.Along[i] = -rank * 4.5f + Random.Range(-0.8f, 0.8f);
            f.Aside[i] = wing * rank * 3.4f + Random.Range(-0.7f, 0.7f);
            f.Phase[i] = Random.value * 6.28f;
            f.Bob[i] = Random.Range(0.8f, 1.2f);
        }
        flocks.Add(f);
    }

    private void LateUpdate()
    {
        var view = Camera.main;
        if (view == null || TimeOfDay.Instance == null) return;

        float height = Mathf.Sin((TimeOfDay.Instance.Normalized * 360f - 90f) * Mathf.Deg2Rad);
        bool day = height > 0.05f && TimeOfDay.Instance.Overcast < 0.85f && Rain.Intensity < 0.5f;

        if (day && Time.time > nextFlock && flocks.Count < 3)
        {
            nextFlock = Time.time + Random.Range(45f, 110f);
            Launch(view.transform, false);
        }

        batch.Clear();
        float dt = Time.deltaTime, t = Time.time;

        for (int k = flocks.Count - 1; k >= 0; k--)
        {
            var f = flocks[k];
            f.Lead += f.Heading * (f.Speed * dt);
            if (Vector3.Distance(f.Lead, view.transform.position) > 600f && t - f.Born > 20f) { flocks.RemoveAt(k); continue; }

            Vector3 side = Vector3.Cross(Vector3.up, f.Heading);
            var turn = Quaternion.LookRotation(f.Heading, Vector3.up);
            for (int i = 0; i < f.Phase.Length; i++)
            {
                Vector3 at = f.Lead + f.Heading * f.Along[i] + side * f.Aside[i] + Vector3.up * (Mathf.Sin(t * 0.7f + f.Phase[i]) * 1.2f);
                // the beat: the wings swing from raised to lowered, a chevron mirrored through its plane
                float flap = Mathf.Sin(t * 8f * f.Bob[i] + f.Phase[i]);
                batch.Add(Matrix4x4.TRS(at, turn, new Vector3(3.2f, 1.6f * flap, 3.2f)));
            }
        }

        Birds = batch.Count;
        if (batch.Count == 0 || paint == null) return;
        var rp = new RenderParams(paint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMeshInstanced(rp, bird, 0, batch, batch.Count, 0);
    }

    /// <summary>A bird: a chevron with its wings raised, faced both ways, a unit across a wing.</summary>
    private static Mesh Bird()
    {
        Vector3 nose = new Vector3(0f, 0f, 0.35f), tail = new Vector3(0f, 0f, -0.3f), left = new Vector3(-1f, 0.5f, -0.15f), right = new Vector3(1f, 0.5f, -0.15f), body = new Vector3(0f, 0.05f, 0f);
        Vector3[][] faces = { new[] { tail, left, nose }, new[] { tail, nose, right }, new[] { nose, left, tail }, new[] { right, nose, tail }, new[] { body, nose, tail }, new[] { body, tail, nose } };
        var verts = new List<Vector3>(); var tris = new List<int>();
        foreach (var f in faces) { int v = verts.Count; verts.AddRange(f); tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); }
        var m = new Mesh { name = "bird" }; m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
}
