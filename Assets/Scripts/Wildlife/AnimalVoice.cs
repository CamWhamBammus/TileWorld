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

    private enum Sound { Call, Alarm, Walk, Run, Chew, Sip }

    public static AudioClip Call(FaunaKind kind, bool alarmed)
    {
        lastSpoke = Time.time;

        return Pick(kind, alarmed ? Sound.Alarm : Sound.Call);
    }

    /// <summary>A hoof or a pad coming down. Half the life of a moving animal.</summary>
    public static AudioClip Step(FaunaKind kind, bool running)
    {
        return Pick(kind, running ? Sound.Run : Sound.Walk);
    }

    /// <summary>Tearing at the grass, for an animal with its head down.</summary>
    public static AudioClip Chew(FaunaKind kind) => Pick(kind, Sound.Chew);

    public static AudioClip Drink(FaunaKind kind) => Pick(kind, Sound.Sip);

    private static AudioClip Pick(FaunaKind kind, Sound sound)
    {
        int key = (int)kind * 8 + (int)sound;

        if (!calls.TryGetValue(key, out var set) || set == null || set.Length == 0 || set[0] == null)
        {
            set = new AudioClip[3];

            for (int i = 0; i < set.Length; i++)
            {
                set[i] = sound == Sound.Call || sound == Sound.Alarm
                    ? One(kind, sound == Sound.Alarm, i)
                    : Struck(kind, sound, i);
            }

            calls[key] = set;
        }

        return set[Random.Range(0, set.Length)];
    }

    /// <summary>
    /// The sounds that are hit rather than called: a foot on the ground, a
    /// mouthful of grass, a muzzle in the water. All of them are a knock of
    /// noise under a fast decay, and what separates them is how much low end
    /// there is and how quickly they die away.
    /// </summary>
    private static AudioClip Struck(FaunaKind kind, Sound sound, int variant)
    {
        var rng = new System.Random((int)kind * 613 + (int)sound * 71 + variant);

        var traits = Fauna.Of(kind);

        // a heavier animal lands harder and lower
        float weight = Mathf.Clamp01(traits.Size / 1.6f);

        float length, lowHz, noise, decay, tone;

        switch (sound)
        {
            case Sound.Walk:
                length = 0.11f; lowHz = Mathf.Lerp(150f, 78f, weight);
                noise = 0.55f; decay = 26f; tone = 0.5f;
                break;

            case Sound.Run:
                length = 0.13f; lowHz = Mathf.Lerp(135f, 66f, weight);
                noise = 0.75f; decay = 20f; tone = 0.62f;
                break;

            case Sound.Chew:
                length = 0.16f; lowHz = 220f;
                noise = 0.95f; decay = 15f; tone = 0.12f;
                break;

            default:
                length = 0.14f; lowHz = 320f;
                noise = 0.7f; decay = 17f; tone = 0.2f;
                break;
        }

        length *= 0.88f + (float)rng.NextDouble() * 0.24f;
        lowHz *= 0.9f + (float)rng.NextDouble() * 0.2f;

        int samples = Mathf.RoundToInt(Rate * length);
        var data = new float[samples];

        float phase = 0f;
        float rolling = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;

            phase += 2f * Mathf.PI * (lowHz * (1f - 0.45f * t)) / Rate;

            float body = Mathf.Sin(phase) * tone;

            // noise, softened a little so it is a thud and not a hiss
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            rolling = Mathf.Lerp(rolling, white, sound == Sound.Chew ? 0.55f : 0.30f);

            float envelope = Mathf.Exp(-t * decay);

            data[i] = Mathf.Clamp((body + rolling * noise) * envelope * 0.5f, -1f, 1f);
        }

        var clip = AudioClip.Create(kind + sound.ToString() + variant, samples, 1, Rate, false);
        clip.SetData(data, 0);

        return clip;
    }

    private static AudioClip One(FaunaKind kind, bool alarmed, int variant)
    {
        var rng = new System.Random((int)kind * 977 + (alarmed ? 31 : 0) + variant);

        float length, baseHz, glide, vibratoHz, vibratoDepth, rasp, noise, thump;

        var voice = Fauna.All(kind).Call;

        length = voice.Length; baseHz = voice.Pitch; glide = voice.Glide;
        vibratoHz = voice.WobbleRate; vibratoDepth = voice.WobbleDepth;
        rasp = voice.Rasp; noise = voice.Noise; thump = voice.Thump;

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
