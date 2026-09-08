using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The menu's small sounds: a dry tick as the pointer comes onto a button
/// and a softer tock as it is pressed, made from a few milliseconds of
/// shaped noise, the way everything else here is made. One source, shared.
/// </summary>
public class MenuSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private static AudioSource source;
    private static AudioClip tick, tock;

    /// <summary>How many have played, for the probes.</summary>
    public static int Played { get; private set; }

    public static void Attach(Button button) => button.gameObject.AddComponent<MenuSounds>();

    private static void Ensure()
    {
        if (source != null) return;
        var go = new GameObject("Menu sounds (runtime)");
        source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        tick = Build(0.014f, 3200f, 0.9f, 11);
        tock = Build(0.03f, 900f, 0.6f, 12);
    }

    public static void Tick() { Ensure(); source.pitch = Random.Range(0.96f, 1.04f); source.PlayOneShot(tick, 0.11f); Played++; }
    public static void Tock() { Ensure(); source.pitch = Random.Range(0.97f, 1.03f); source.PlayOneShot(tock, 0.16f); Played++; }

    public void OnPointerEnter(PointerEventData e) { var b = GetComponent<Button>(); if (b != null && b.interactable) Tick(); }
    public void OnPointerClick(PointerEventData e) { var b = GetComponent<Button>(); if (b != null && b.interactable) Tock(); }

    private void OnDestroy() { if (source != null && source.gameObject == gameObject) source = null; }

    /// <summary>A short burst of noise through a resonant band, falling away.</summary>
    private static AudioClip Build(float seconds, float hz, float q, int seed)
    {
        const int rate = 44100;
        int samples = Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        var rng = new System.Random(seed);
        float low = 0f, band = 0f, f = 2f * Mathf.Sin(Mathf.PI * hz / rate);
        for (int i = 0; i < samples; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += f * band; float high = white - low - q * band; band += f * high;
            float t = i / (float)samples;
            data[i] = band * Mathf.Exp(-t * 6f) * (1f - t) * 0.8f;
        }
        var clip = AudioClip.Create("Menu " + Mathf.RoundToInt(hz), samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
