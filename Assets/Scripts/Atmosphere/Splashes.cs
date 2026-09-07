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
        public bool Breath;         // rises and swells rather than falls
        public float Floor;         // gone below this, for a drip on land
    }

    /// <summary>Counts for the probes.</summary>
    public static int Puffs { get; private set; }
    public static int Drips { get; private set; }
    public static int Creaks { get; private set; }

    /// <summary>A breath in the cold: a few pale lumps that drift forward, swell and go.</summary>
    public static void Puff(Vector3 at, Vector3 dir)
    {
        var it = Ensure();
        if (it == null) return;

        int count = 5 + Random.Range(0, 4);
        for (int i = 0; i < count; i++)
        {
            if (it.drops.Count >= Most) it.drops.RemoveAt(0);
            it.drops.Add(new Drop
            {
                At = at + Random.insideUnitSphere * 0.05f,
                Going = dir * Random.Range(0.35f, 0.7f) + Random.insideUnitSphere * 0.18f + Vector3.up * 0.08f,
                Size = Random.Range(0.035f, 0.06f),
                Made = Time.time,
                Lasts = Random.Range(0.9f, 1.3f),
                Breath = true,
                Floor = float.MinValue
            });
        }

        Puffs++;
    }

    /// <summary>A drip off something wet: one drop, falling to the ground under it.</summary>
    public static void Drip(Vector3 at, float floor)
    {
        var it = Ensure();
        if (it == null) return;

        if (it.drops.Count >= Most) it.drops.RemoveAt(0);
        it.drops.Add(new Drop
        {
            At = at,
            Going = Random.insideUnitSphere * 0.15f,
            Size = Random.Range(0.04f, 0.065f),
            Made = Time.time,
            Lasts = 1.5f,
            Floor = floor
        });

        Drips++;
    }

    /// <summary>The ice creaking under a step.</summary>
    public static void Creak(Vector3 at)
    {
        var it = Ensure();
        if (it == null) return;
        it.Sound(at, it.creaks, Random.Range(0.25f, 0.45f), Random.Range(0.85f, 1.2f), 1.2f);
        Creaks++;
    }

    private static Splashes instance;

    private readonly List<Drop> drops = new List<Drop>(512);
    private readonly List<Matrix4x4> batch = new List<Matrix4x4>(512);
    private Mesh lump;
    private Material paint;
    private AudioClip[] plish, plunge, strokes, creaks;

    /// <summary>The clips by name, for a probe to write out and listen to.</summary>
    public static IEnumerable<KeyValuePair<string, AudioClip>> Clips()
    {
        if (instance == null) yield break;
        for (int i = 0; i < instance.plish.Length; i++) yield return new KeyValuePair<string, AudioClip>("step" + i, instance.plish[i]);
        for (int i = 0; i < instance.plunge.Length; i++) yield return new KeyValuePair<string, AudioClip>("plunge" + i, instance.plunge[i]);
        for (int i = 0; i < instance.strokes.Length; i++) yield return new KeyValuePair<string, AudioClip>("stroke" + i, instance.strokes[i]);
    }
    private readonly AudioSource[] voices = new AudioSource[4];
    private int nextVoice;
    private float lastSound;
    private AudioSource patter;     // rain on the water, near
    private float patterLevel;

    /// <summary>How loud the rain on the water is, 0 to 1, for the probes.</summary>
    public static float Patter => instance != null ? instance.patterLevel : 0f;

    private const int Most = 480;

    private struct Ripple { public Vector3 At; public float Made, Lasts, Size; }
    private readonly List<Ripple> ripples = new List<Ripple>(1024);
    private readonly List<Matrix4x4> rippleBatch = new List<Matrix4x4>(1024);
    private Mesh thinRing;
    private Material ringPaint;
    private const int MostRipples = 2400;

    /// <summary>How many rain rings are on the water, for the probes.</summary>
    public static int RainAlive => instance != null ? instance.ripples.Count : 0;

    /// <summary>A raindrop landing on the water: a small ring, quickly gone. Only where the water is.</summary>
    public static void Raindrop(Vector3 at, int worldSeed, float scale = 1f)
    {
        var it = Ensure();
        if (it == null) return;

        if (!WaterSurface.IsOpenWater(Mathf.RoundToInt(at.x / WorldGrid.TileSize), Mathf.RoundToInt(at.z / WorldGrid.TileSize), worldSeed)) return;

        if (it.ripples.Count >= MostRipples) it.ripples.RemoveAt(0);
        it.ripples.Add(new Ripple { At = new Vector3(at.x, WaterSurface.Level + 0.025f, at.z), Made = Time.time, Lasts = Random.Range(0.6f, 0.95f), Size = Random.Range(0.5f, 0.95f) * scale });
    }

    /// <summary>How the rain's rings are spread from a point, for the probes: within 15 m, 15 to 40, and beyond.</summary>
    public static void RainSpread(Vector3 from, out int near, out int mid, out int far)
    {
        near = mid = far = 0;
        if (instance == null) return;

        foreach (var r in instance.ripples)
        {
            float d = Vector2.Distance(new Vector2(r.At.x, r.At.z), new Vector2(from.x, from.z));
            if (d < 15f) near++; else if (d < 40f) mid++; else far++;
        }
    }

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
        thinRing = Tracks.Annulus(24, 0.5f, 0.455f);   // the tracks' ring, wound to face up
        ringPaint = Paint.Flat(new Color(0.70f, 0.81f, 0.90f));

        plish = new[] { Splash(Kind.Step, 11), Splash(Kind.Step, 12), Splash(Kind.Step, 13), Splash(Kind.Step, 14) };
        plunge = new[] { Splash(Kind.Plunge, 21), Splash(Kind.Plunge, 22) };
        strokes = new[] { Splash(Kind.Stroke, 31), Splash(Kind.Stroke, 32) };
        creaks = new[] { CreakClip(51), CreakClip(52), CreakClip(53) };

        patter = gameObject.AddComponent<AudioSource>();
        patter.clip = RainOnWater(4f, 41);
        patter.loop = true;
        patter.spatialBlend = 0f;
        patter.volume = 0f;
        patter.playOnAwake = false;

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
        it.Sound(at, it.strokes, Mathf.Lerp(0.12f, 0.28f, strength), Random.Range(0.9f, 1.1f), 0.25f);
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
                Lasts = 1.4f,
                Floor = float.MinValue
            });
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float now = Time.time;
        float level = WaterSurface.Level;

        // the patter: rain on the water, as loud as there is rain and water near
        float wanted = 0.5f * Rain.Intensity * Mathf.Clamp01(ripples.Count / 250f);
        patterLevel = Mathf.Lerp(patterLevel, wanted, 1f - Mathf.Exp(-2f * dt));

        if (patter != null)
        {
            patter.volume = patterLevel;
            if (patterLevel > 0.01f && !patter.isPlaying) patter.Play();
            else if (patterLevel <= 0.01f && patter.isPlaying) patter.Pause();
        }

        batch.Clear();

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            float age = (now - d.Made) / d.Lasts;

            if (d.Breath)
            {
                // slows, drifts up a little, swells, and is gone
                d.Going *= Mathf.Exp(-1.8f * dt);
                d.Going.y += 0.12f * dt;
                d.At += d.Going * dt;

                if (age >= 1f) { drops.RemoveAt(i); continue; }

                drops[i] = d;
                float swell = d.Size * Mathf.Lerp(1f, 3.2f, age) * (age > 0.75f ? Mathf.InverseLerp(1f, 0.75f, age) : 1f);
                batch.Add(Matrix4x4.TRS(d.At, Quaternion.Euler(age * 90f, age * 130f, 0f), new Vector3(swell, swell, swell)));
                continue;
            }

            d.Going.y -= 9.8f * dt;
            d.At += d.Going * dt;

            // back into the water, onto the ground, or lost in the air after a while
            if ((d.At.y < level - 0.02f && d.Going.y < 0f) || d.At.y < d.Floor || now - d.Made > d.Lasts)
            {
                drops.RemoveAt(i);
                continue;
            }

            drops[i] = d;

            // stretched along the way it is going, the way a drop reads in the air
            var turn = d.Going.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.Going) : Quaternion.identity;
            batch.Add(Matrix4x4.TRS(d.At, turn, new Vector3(d.Size, d.Size, d.Size * 1.8f)));
        }

        if (batch.Count > 0 && paint != null)
        {
            var rp = new RenderParams(paint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(rp, lump, 0, batch, batch.Count, 0);
        }

        // the rain's rings: spreading, and gone
        rippleBatch.Clear();

        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            var r = ripples[i];
            float age = (now - r.Made) / r.Lasts;

            if (age >= 1f) { ripples.RemoveAt(i); continue; }

            float size = r.Size * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(age));
            rippleBatch.Add(Matrix4x4.TRS(r.At, Quaternion.identity, new Vector3(size, 1f, size)));
        }

        if (rippleBatch.Count > 0 && ringPaint != null)
        {
            var rp = new RenderParams(ringPaint) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            for (int from = 0; from < rippleBatch.Count; from += 1000)
                Graphics.RenderMeshInstanced(rp, thinRing, 0, rippleBatch, Mathf.Min(1000, rippleBatch.Count - from), from);
        }
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

    /// <summary>
    /// Ice creaking: a low tone that bends about as it goes, with a grain of
    /// noise on it, the way a sheet complains under a step.
    /// </summary>
    private static AudioClip CreakClip(int seed)
    {
        const int rate = 22050;
        var rng = new System.Random(seed);
        float seconds = 0.3f + (float)rng.NextDouble() * 0.25f;
        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        float phase = 0f, wobblePhase = (float)rng.NextDouble() * 6.28f;
        float f0 = 70f + (float)rng.NextDouble() * 90f;
        float wobbleHz = 5f + (float)rng.NextDouble() * 6f;
        float low = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float env = Mathf.Min(1f, i / (rate * 0.02f)) * Mathf.Sin(t * Mathf.PI);
            wobblePhase += wobbleHz / rate * 6.2831853f;
            float freq = f0 * (1f + 0.35f * Mathf.Sin(wobblePhase) + 0.6f * t);
            phase += freq / rate * 6.2831853f;
            float tone = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2.01f) * 0.25f + Mathf.Sin(phase * 3.02f) * 0.1f;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += 0.12f * (white - low);
            data[i] = (tone + low * 0.5f) * env;
        }

        float peak = 0.0001f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < samples; i++) data[i] = data[i] / peak * 0.85f;

        var clip = AudioClip.Create("Creak" + seed, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Rain on water, to loop: a thick scatter of the smallest bubbles, high
    /// and quick, over a faint high hiss that gurgles. Nothing in it repeats
    /// on any beat, so the loop is not heard as one.
    /// </summary>
    private static AudioClip RainOnWater(float seconds, int seed)
    {
        const int rate = 22050;
        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        var rng = new System.Random(seed);
        float U(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        int count = Mathf.RoundToInt(seconds * 260f);
        var start = new float[count]; var f0 = new float[count]; var tau = new float[count]; var amp = new float[count]; var phase = new float[count];

        for (int b = 0; b < count; b++)
        {
            start[b] = U(0f, seconds);
            f0[b] = Mathf.Exp(U(Mathf.Log(1100f), Mathf.Log(4200f)));
            tau[b] = U(0.003f, 0.010f);
            amp[b] = Mathf.Pow(1500f / f0[b], 0.3f) * U(0.25f, 1f);
        }

        // the bubbles are sorted by start so only a window of them is summed
        System.Array.Sort(start, f0, 0, count);
        int first = 0;
        float low = 0f, band = 0f, gurgle = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            while (first < count && t - start[first] > 0.08f) first++;

            float bubbles = 0f;
            for (int b = first; b < count && start[b] <= t; b++)
            {
                float dt = t - start[b];
                float freq = f0[b] * (1f + 0.3f * dt / tau[b]);
                phase[b] += freq / rate * 6.2831853f;
                bubbles += Mathf.Sin(phase[b]) * amp[b] * Mathf.Min(1f, dt / 0.0008f) * Mathf.Exp(-dt / tau[b]);
            }

            gurgle += 0.004f * (white * 35f - gurgle);
            float f = 2f * Mathf.Sin(3.1415926f * 3600f / rate);
            low += f * band;
            float high = white - low - 1.2f * band;
            band += f * high;
            float hiss = band * (0.6f + 0.4f * Mathf.Clamp(gurgle, -1f, 1f)) * 0.22f;

            data[i] = bubbles * 0.3f + hiss;
        }

        float peak = 0.0001f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        int fade = Mathf.RoundToInt(rate * 0.04f);
        for (int i = 0; i < samples; i++)
        {
            float edge = Mathf.Min(1f, Mathf.Min(i, samples - 1 - i) / (float)fade);
            data[i] = data[i] / peak * 0.8f * edge;
        }

        var clip = AudioClip.Create("RainOnWater", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

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

    private enum Kind { Step, Plunge, Stroke }

    /// <summary>
    /// A splash, made rather than recorded. Water's sound is mostly bubbles:
    /// each one a short sine that rises a little in pitch as it shrinks and
    /// dies in a few tens of milliseconds, the big ones low and loud, the
    /// small ones high and quick. A splash is a few dozen of them, most in
    /// the first moments, over a spray of band-limited noise that gurgles
    /// rather than hisses, and a slower slosh behind it as the water settles.
    /// A body going in adds a low knock at the front and bigger bubbles as
    /// the water closes over it.
    /// </summary>
    private static AudioClip Splash(Kind kind, int seed)
    {
        const int rate = 44100;
        var rng = new System.Random(seed);
        float U(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float LogU(float a, float b) => Mathf.Exp(U(Mathf.Log(a), Mathf.Log(b)));

        float seconds; int bubbleCount; float fLow, fHigh, spreadOver, tauLow, tauHigh;
        float sprayFrom, sprayTo, sprayDecay, sprayLevel;
        float sloshFc, sloshDelay, sloshAttack, sloshDecay, sloshLevel;
        float knock, closing;

        switch (kind)
        {
            case Kind.Plunge:
                seconds = 0.9f; bubbleCount = 42; fLow = 160f; fHigh = 1800f; spreadOver = 0.26f; tauLow = 0.02f; tauHigh = 0.07f;
                sprayFrom = 2600f; sprayTo = 900f; sprayDecay = 0.2f; sprayLevel = 0.5f;
                sloshFc = 520f; sloshDelay = 0.06f; sloshAttack = 0.05f; sloshDecay = 0.3f; sloshLevel = 0.36f;
                knock = 0.9f; closing = 1f;
                break;
            case Kind.Stroke:
                seconds = 0.45f; bubbleCount = 9; fLow = 280f; fHigh = 1400f; spreadOver = 0.22f; tauLow = 0.02f; tauHigh = 0.05f;
                sprayFrom = 1800f; sprayTo = 900f; sprayDecay = 0.12f; sprayLevel = 0.2f;
                sloshFc = 600f; sloshDelay = 0.05f; sloshAttack = 0.06f; sloshDecay = 0.18f; sloshLevel = 0.3f;
                knock = 0f; closing = 0f;
                break;
            default:
                seconds = 0.36f; bubbleCount = 14; fLow = 340f; fHigh = 2000f; spreadOver = 0.16f; tauLow = 0.012f; tauHigh = 0.035f;
                sprayFrom = 2100f; sprayTo = 850f; sprayDecay = 0.1f; sprayLevel = 0.3f;
                sloshFc = 650f; sloshDelay = 0.04f; sloshAttack = 0.04f; sloshDecay = 0.15f; sloshLevel = 0.4f;
                knock = 0f; closing = 0f;
                break;
        }

        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];

        // the bubbles: when each starts, its pitch, how long it lasts, how loud
        int count = bubbleCount + (closing > 0f ? 10 : 0);
        var start = new float[count]; var f0 = new float[count]; var tau = new float[count]; var amp = new float[count];

        for (int b = 0; b < count; b++)
        {
            bool late = b >= bubbleCount;   // the water closing over a body: bigger, a moment after
            float t = late ? U(0.22f, 0.45f) : Mathf.Pow((float)rng.NextDouble(), 1.7f) * spreadOver;
            float f = late ? LogU(fLow, fLow * 3f) : LogU(fLow, fHigh);
            start[b] = t;
            f0[b] = f;
            tau[b] = U(tauLow, tauHigh) * Mathf.Clamp(Mathf.Sqrt(600f / f), 0.6f, 1.8f);   // big bubbles ring longer
            amp[b] = Mathf.Pow(600f / f, 0.35f) * U(0.35f, 1f) * (late ? 0.7f : 1f);
        }

        var phase = new float[count];

        // the spray: white noise through a resonant band-pass that falls in pitch as it dies
        float low = 0f, band = 0f;
        float gurgle = 0f; float gurgle2 = 0f;
        float sLow = 0f, sBand = 0f;
        float knockPhase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            // the gurgle: slow noise, so nothing here is a steady tone or a steady hiss
            gurgle += 0.0035f * (white * 40f - gurgle);
            gurgle2 += 0.0012f * (white * 60f - gurgle2);
            float gurgling = Mathf.Clamp(0.55f + 0.45f * Mathf.Clamp(gurgle, -1f, 1f), 0.1f, 1f);

            // the bubbles
            float bubbles = 0f;
            for (int b = 0; b < count; b++)
            {
                float dt = t - start[b];
                if (dt < 0f || dt > tau[b] * 6f) continue;
                float freq = f0[b] * (1f + 0.35f * dt / tau[b]);   // rising as it shrinks
                phase[b] += freq / rate * 6.2831853f;
                float rise = Mathf.Min(1f, dt / 0.0012f);
                bubbles += Mathf.Sin(phase[b]) * amp[b] * rise * Mathf.Exp(-dt / tau[b]);
            }

            // the spray
            float sprayEnv = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t / sprayDecay);
            float fc = Mathf.Lerp(sprayFrom, sprayTo, Mathf.Clamp01(t / (sprayDecay * 2.5f)));
            float f = 2f * Mathf.Sin(3.1415926f * fc / rate);
            low += f * band;
            float high = white - low - 0.8f * band;
            band += f * high;
            float spray = band * sprayEnv * gurgling * sprayLevel * 2.2f;

            // the slosh: lower, later, slower, and gurgling on its own clock
            float st = t - sloshDelay;
            float sloshEnv = st < 0f ? 0f : Mathf.Min(1f, st / sloshAttack) * Mathf.Exp(-st / sloshDecay);
            float sf = 2f * Mathf.Sin(3.1415926f * sloshFc / rate);
            sLow += sf * sBand;
            float sHigh = white - sLow - 1.1f * sBand;
            sBand += sf * sHigh;
            float slosh = sBand * sloshEnv * Mathf.Clamp(0.5f + 0.5f * Mathf.Clamp(gurgle2, -1f, 1f), 0.05f, 1f) * sloshLevel * 2.6f;

            // the knock, for a body going in
            float thud = 0f;
            if (knock > 0f)
            {
                knockPhase += Mathf.Lerp(120f, 55f, Mathf.Clamp01(t / 0.12f)) / rate * 6.2831853f;
                thud = Mathf.Sin(knockPhase) * Mathf.Exp(-t / 0.09f) * knock;
            }

            data[i] = bubbles * 0.26f + spray * 1.7f + slosh * 1.5f + thud;
        }

        // brought to a common level, and the last few milliseconds eased out
        float peak = 0.0001f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        int tail = Mathf.RoundToInt(rate * 0.012f);
        for (int i = 0; i < samples; i++)
        {
            float ease = i > samples - tail ? (samples - i) / (float)tail : 1f;
            data[i] = Mathf.Clamp(data[i] / peak * 0.9f * ease, -1f, 1f);
        }

        var clip = AudioClip.Create("Splash" + kind + seed, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
