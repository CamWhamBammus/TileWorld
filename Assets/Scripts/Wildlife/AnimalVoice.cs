using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The animals, synthesised the same way the wind and the birds are. Nothing
/// here is a recording: a call is a tone with harmonics over it, bent by a
/// vibrato and roughened with noise, which at the distance you hear an animal
/// across a valley is close enough.
///
/// Each kind gets a few versions of its call so a herd does not sound like one
/// animal played twice, and a sharper version of it for the moment it decides
/// you are too close.
/// </summary>
public static class AnimalVoice
{
    private const int Rate = 44100;

    private static readonly Dictionary<int, AudioClip[]> calls = new Dictionary<int, AudioClip[]>();

    /// <summary>
    /// Whichever of them spoke last, so a group startled together sounds like
    /// several animals rather than one noise.
    /// </summary>
    private static float lastSpoke;

    public static bool Ready => Time.time - lastSpoke > 0.45f;

    public static AudioClip Call(FaunaKind kind, bool alarmed)
    {
        int key = (int)kind * 2 + (alarmed ? 1 : 0);

        if (!calls.TryGetValue(key, out var set) || set == null || set.Length == 0 || set[0] == null)
        {
            set = Build(kind, alarmed);
            calls[key] = set;
        }

        lastSpoke = Time.time;

        return set[Random.Range(0, set.Length)];
    }

    private static AudioClip[] Build(FaunaKind kind, bool alarmed)
    {
        var set = new AudioClip[3];

        for (int i = 0; i < set.Length; i++) set[i] = One(kind, alarmed, i);

        return set;
    }

    private static AudioClip One(FaunaKind kind, bool alarmed, int variant)
    {
        var rng = new System.Random((int)kind * 977 + (alarmed ? 31 : 0) + variant);

        float length, baseHz, glide, vibratoHz, vibratoDepth, rasp, noise, thump;

        switch (kind)
        {
            case FaunaKind.Deer:
                // a short chesty grunt, dropping as it ends
                length = 0.40f; baseHz = 152f; glide = -0.22f;
                vibratoHz = 7f; vibratoDepth = 9f; rasp = 0.30f; noise = 0.14f; thump = 0f;
                break;

            case FaunaKind.Rabbit:
                // rabbits are near enough silent, so this is the foot drumming
                length = 0.16f; baseHz = 74f; glide = -0.45f;
                vibratoHz = 0f; vibratoDepth = 0f; rasp = 0f; noise = 0.55f; thump = 1f;
                break;

            case FaunaKind.Fox:
                // the bark: high, thin and rough, and it carries a long way
                length = 0.34f; baseHz = 590f; glide = 0.30f;
                vibratoHz = 22f; vibratoDepth = 60f; rasp = 0.45f; noise = 0.18f; thump = 0f;
                break;

            default:
                // the bleat, which is mostly its wobble
                length = 0.62f; baseHz = 366f; glide = -0.14f;
                vibratoHz = 15f; vibratoDepth = 52f; rasp = 0.34f; noise = 0.10f; thump = 0f;
                break;
        }

        if (alarmed)
        {
            // sharper, higher and shorter when it has seen you
            length *= 0.72f;
            baseHz *= 1.22f;
            rasp = Mathf.Min(1f, rasp + 0.18f);
            noise += 0.06f;
        }

        // a little apart from one another, so three of them are three animals
        baseHz *= 0.92f + (float)rng.NextDouble() * 0.16f;
        length *= 0.9f + (float)rng.NextDouble() * 0.2f;

        int samples = Mathf.RoundToInt(Rate * length);
        var data = new float[samples];

        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float seconds = i / (float)Rate;

            float frequency = baseHz * (1f + glide * t)
                            + Mathf.Sin(seconds * vibratoHz * Mathf.PI * 2f) * vibratoDepth;

            phase += 2f * Mathf.PI * Mathf.Max(20f, frequency) / Rate;

            // harmonics, or it is a test tone rather than an animal
            float value = Mathf.Sin(phase)
                        + 0.42f * Mathf.Sin(phase * 2f)
                        + 0.22f * Mathf.Sin(phase * 3f);

            if (rasp > 0f)
            {
                value *= 1f - rasp + rasp * Mathf.Abs(Mathf.Sin(seconds * 78f * Mathf.PI));
            }

            value += noise * (float)(rng.NextDouble() * 2.0 - 1.0);

            // Struck sounds die away; called ones open and close.
            float envelope = thump > 0f
                ? Mathf.Exp(-t * 14f)
                : Mathf.Min(1f, t / 0.12f) * Mathf.Min(1f, (1f - t) / 0.35f);

            data[i] = Mathf.Clamp(value * envelope * 0.30f, -1f, 1f);
        }

        var clip = AudioClip.Create(kind + (alarmed ? "Alarm" : "Call") + variant, samples, 1, Rate, false);
        clip.SetData(data, 0);

        return clip;
    }
}
