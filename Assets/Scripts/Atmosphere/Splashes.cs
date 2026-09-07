using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What water does when something goes into it. A foot in the shallows
/// throws a few drops and leaves a ring; a body going in throws more; a
/// swimmer's stroke throws a little and leaves a ring behind. The drops are
/// small flat-shaded lumps drawn in one instanced call and gone in under a
/// second; the rings are the tracks system's, on the water. The sound is
/// made here rather than recorded: a burst of noise with the top taken off
/// it as it dies, which is most of what a splash is.
/// </summary>
public class Splashes : MonoBehaviour
{
    private struct Drop
    {
        public Vector3 At, Going;
        public float Size, Made, Lasts;
    }

    private static Splashes instance;

    private readonly List<Drop> drops = new List<Drop>(512);
    private readonly List<Matrix4x4> batch = new List<Matrix4x4>(512);
    private Mesh lump;
    private Material paint;
    private AudioClip[] plish, plunge;
    private readonly AudioSource[] voices = new AudioSource[4];
    private int nextVoice;
    private float lastSound;

    private const int Most = 480;

    /// <summary>How many drops are in the air, for the probes.</summary>
    public static int Alive => instance != null ? instance.drops.Count : 0;
    /// <summary>How many splash sounds have played, for the probes.</summary>
    public static int Played { get; private set; }
    /// <summary>Who made the last few, for the probes: the caller says.</summary>
    public static readonly List<string> Recent = new List<string>();
    private static string by = "?";

    /// <summary>Names the caller for the next call, so a probe can tell a heron's step from the player's.</summary>
    public static void By(string who) { by = who; }

    private static Splashes Ensure()
    {
        if (instance == null && !TitleMenu.IsUp)
        {
            instance = new GameObject("Splashes (runtime)").AddComponent<Splashes>();
        }

        return instance;
    }

    private void Awake()
    {
        instance = this;
        lump = Lump();
        paint = Paint.Flat(new Color(0.93f, 0.97f, 1.0f));

        plish = new[] { Splash(0.13f, 2600f, 0.35f, 11), Splash(0.16f, 2200f, 0.3f, 12), Splash(0.11f, 3000f, 0.4f, 13) };
        plunge = new[] { Splash(0.42f, 1500f, 1.0f, 21), Splash(0.5f, 1300f, 1.0f, 22) };

        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("splash voice " + i);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<AudioSource>();
            v.spatialBlend = 1f;
            v.rolloffMode = AudioRolloffMode.Linear;
            v.minDistance = 3f;
            v.maxDistance = 38f;
            v.playOnAwake = false;
            voices[i] = v;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------ the calls

    /// <summary>A foot coming down in the shallows. Strength 0..1: a wade is low, a run through it high.</summary>
    public static void Step(Vector3 at, float strength)
    {
        var it = Ensure();
        if (it == null) return;

        strength = Mathf.Clamp01(strength);
        at.y = WaterSurface.Level;

        Tracks.Ring(at, Mathf.Lerp(0.55f, 1.3f, strength), Mathf.Lerp(1.1f, 1.7f, strength));
        it.Throw(at, 4 + Mathf.RoundToInt(strength * 12f), Mathf.Lerp(1.2f, 2.8f, strength), Mathf.Lerp(0.5f, 1.5f, strength), Mathf.Lerp(0.06f, 0.1f, strength));
        it.Sound(at, it.plish, Mathf.Lerp(0.18f, 0.5f, strength), Random.Range(0.9f, 1.15f), 0.08f);
    }

    /// <summary>A body going in: off a bank, or a run that reaches deep water.</summary>
    public static void Plunge(Vector3 at, float strength)
    {
        var it = Ensure();
        if (it == null) return;

        strength = Mathf.Clamp01(strength);
        at.y = WaterSurface.Level;

        Tracks.Ring(at, Mathf.Lerp(1.6f, 2.8f, strength), 3.2f);
        Tracks.Ring(at, Mathf.Lerp(0.9f, 1.5f, strength), 2.0f);
        it.Throw(at, 22 + Mathf.RoundToInt(strength * 30f), Mathf.Lerp(2.4f, 4.4f, strength), Mathf.Lerp(1.2f, 2.6f, strength), Mathf.Lerp(0.065f, 0.105f, strength));
        it.Sound(at, it.plunge, Mathf.Lerp(0.4f, 0.85f, strength), Random.Range(0.85f, 1.05f), 0f);
    }

    /// <summary>A swimmer's stroke: a ring and a few drops off the arms.</summary>
    public static void Stroke(Vector3 at, float strength)
    {
        var it = Ensure();
        if (it == null) return;

        strength = Mathf.Clamp01(strength);
        at.y = WaterSurface.Level;

        Tracks.Ring(at, Mathf.Lerp(1.2f, 1.8f, strength), 2.6f);
        it.Throw(at, 3 + Mathf.RoundToInt(strength * 6f), 1.3f, 1.0f, 0.07f);
        it.Sound(at, it.plish, Mathf.Lerp(0.1f, 0.25f, strength), Random.Range(0.7f, 0.9f), 0.25f);
    }

    /// <summary>The wake of something moving through water: a ring and nothing else.</summary>
    public static void Wake(Vector3 at, float size)
    {
        if (Ensure() == null) return;

        at.y = WaterSurface.Level;
        Tracks.Ring(at, size, 1.6f);
    }

    // ------------------------------------------------------------ the drops

    private void Throw(Vector3 at, int count, float up, float out_, float size)
    {
        for (int i = 0; i < count; i++)
        {
            if (drops.Count >= Most) drops.RemoveAt(0);

            float a = Random.value * Mathf.PI * 2f;
            float spread = Random.Range(0.3f, 1f) * out_;

            drops.Add(new Drop
            {
                At = at + new Vector3(Mathf.Cos(a) * Random.Range(0f, 0.12f), 0.02f, Mathf.Sin(a) * Random.Range(0f, 0.12f)),
                Going = new Vector3(Mathf.Cos(a) * spread, up * Random.Range(0.55f, 1.15f), Mathf.Sin(a) * spread),
                Size = size * Random.Range(0.6f, 1.4f),
                Made = Time.time,
                Lasts = 1.4f
            });
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float now = Time.time;
        float level = WaterSurface.Level;

        batch.Clear();

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            d.Going.y -= 9.8f * dt;
            d.At += d.Going * dt;

            // back into the water, or lost in the air after a while
            if ((d.At.y < level - 0.02f && d.Going.y < 0f) || now - d.Made > d.Lasts)
            {
                drops.RemoveAt(i);
                continue;
            }

            drops[i] = d;

            // stretched along the way it is going, the way a drop reads in the air
            var turn = d.Going.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.Going) : Quaternion.identity;
            batch.Add(Matrix4x4.TRS(d.At, turn, new Vector3(d.Size, d.Size, d.Size * 1.8f)));
        }

        if (batch.Count == 0 || paint == null) return;

        var rp = new RenderParams(paint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMeshInstanced(rp, lump, 0, batch, batch.Count, 0);
    }

    /// <summary>A small flat-shaded lump, a unit across, that reads as a drop from any side.</summary>
    private static Mesh Lump()
    {
        Vector3[] tips = { Vector3.right * 0.5f, Vector3.left * 0.5f, Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f };
        int[][] faces =
        {
            new[] { 2, 0, 4 }, new[] { 2, 4, 1 }, new[] { 2, 1, 5 }, new[] { 2, 5, 0 },
            new[] { 3, 4, 0 }, new[] { 3, 1, 4 }, new[] { 3, 5, 1 }, new[] { 3, 0, 5 }
        };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        foreach (var f in faces)
        {
            int n = verts.Count;
            verts.Add(tips[f[0]]); verts.Add(tips[f[1]]); verts.Add(tips[f[2]]);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
        }

        var m = new Mesh { name = "drop" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------ the sound

    private void Sound(Vector3 at, AudioClip[] clips, float volume, float pitch, float minGap)
    {
        if (clips == null || clips.Length == 0) return;
        if (Time.time - lastSound < minGap) return;

        lastSound = Time.time;

        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.transform.position = at;
        v.pitch = pitch;
        v.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        Played++;
        Recent.Add(by + " " + volume.ToString("F2"));
        if (Recent.Count > 12) Recent.RemoveAt(0);
        by = "?";
    }

    /// <summary>
    /// A splash: noise that starts bright and has the top taken off it as it
    /// dies away, with a low knock at the front for the bigger ones. The
    /// "body" is how much of that knock there is.
    /// </summary>
    private static AudioClip Splash(float seconds, float brightHz, float body, int seed)
    {
        const int rate = 22050;
        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        var rng = new System.Random(seed);

        float low = 0f, band = 0f;
        float knockPhase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;

            // the envelope: up in a few milliseconds, then away
            float env = Mathf.Min(1f, i / (rate * 0.004f)) * Mathf.Pow(1f - t, 1.6f);

            // the noise, through a low-pass whose cutoff falls as the splash dies
            float cutoff = Mathf.Lerp(brightHz, 350f, t * t);
            float k = Mathf.Clamp01(cutoff / rate * 6.28f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += k * (white - low);
            band += 0.5f * (low - band);
            float hiss = low - band * 0.6f;

            // the knock: a short low thud, for a body going in
            float knockHz = Mathf.Lerp(180f, 70f, t * 3f);
            knockPhase += knockHz / rate * 6.28f;
            float knock = Mathf.Sin(knockPhase) * Mathf.Exp(-t * 14f) * body;

            data[i] = Mathf.Clamp(hiss * 1.6f * env + knock * 0.5f, -1f, 1f);
        }

        var clip = AudioClip.Create("Splash" + seed, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
