using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A soft pad under the title: four voices on a pentatonic scale, each
/// swelling in over some seconds, holding, and fading, so that slow chords
/// drift in and out over the wind and the surf. Made the way the rest of
/// the game's sound is, from arithmetic: a few harmonics, two of them a
/// little apart so they beat, and a slow tremble.
/// </summary>
public class TitlePad : MonoBehaviour
{
    private class Voice
    {
        public AudioSource Source;
        public float Level, Target, Next;
        public int Note = -1;
    }

    private static readonly float[] Hz = { 110f, 130.81f, 164.81f, 196f, 220f, 261.63f, 329.63f, 392f };

    private readonly List<Voice> voices = new List<Voice>();
    private AudioClip[] notes;

    /// <summary>How many voices are sounding now, for the probes.</summary>
    public int Sounding
    {
        get { int n = 0; foreach (var v in voices) if (v.Source.isPlaying && v.Level > 0.005f) n++; return n; }
    }

    private void Start()
    {
        notes = new AudioClip[Hz.Length];
        for (int i = 0; i < Hz.Length; i++) notes[i] = Build(Hz[i]);

        float[] starts = { 2f, 6f, 11f, 17f };
        for (int i = 0; i < 4; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.playOnAwake = false;
            voices.Add(new Voice { Source = source, Next = Time.time + starts[i] });
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var v in voices)
        {
            if (Time.time > v.Next)
            {
                if (v.Target > 0f)
                {
                    v.Target = 0f;
                    v.Next = Time.time + 6f + Random.Range(2f, 7f);
                }
                else
                {
                    int note;
                    do { note = Random.Range(0, notes.Length); } while (Taken(note));
                    v.Note = note;
                    v.Source.clip = notes[note];
                    v.Source.pitch = 1f;
                    v.Source.Play();
                    v.Target = Random.Range(0.045f, 0.085f) * (note < 2 ? 1.3f : 1f);
                    v.Next = Time.time + 4f + Random.Range(3f, 8f);
                }
            }

            float rate = v.Target > v.Level ? 0.09f / 4f : 0.09f / 6f;
            v.Level = Mathf.MoveTowards(v.Level, v.Target, rate * dt);
            v.Source.volume = v.Level;
            if (v.Level <= 0f && v.Target <= 0f && v.Source.isPlaying) v.Source.Stop();
        }
    }

    private bool Taken(int note)
    {
        foreach (var v in voices) if (v.Note == note && v.Target > 0f) return true;
        return false;
    }

    /// <summary>One sustained tone, a whole number of cycles long so it loops.</summary>
    private static AudioClip Build(float hz)
    {
        const int rate = 44100;
        int cycles = Mathf.RoundToInt(hz * 6f);
        int samples = Mathf.RoundToInt(cycles / hz * rate);
        var data = new float[samples];
        float[] amps = { 1f, 0.42f, 0.18f, 0.09f, 0.04f };

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float s = 0f;
            for (int h = 0; h < amps.Length; h++)
            {
                float f = hz * (h + 1);
                s += amps[h] * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * (f + 0.35f * (h + 1)) * t + 1.3f));
            }
            float tremble = 1f + 0.07f * Mathf.Sin(2f * Mathf.PI * 0.27f * t);
            float edge = Mathf.Min(1f, Mathf.Min(i, samples - 1 - i) / (0.02f * rate));
            data[i] = s * 0.28f * tremble * edge;
        }

        var clip = AudioClip.Create("Pad " + Mathf.RoundToInt(hz), samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
