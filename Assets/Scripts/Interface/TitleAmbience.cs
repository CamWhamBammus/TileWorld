using UnityEngine;

/// <summary>Wind, and a bird now and then, under the title: the same sounds as the world's, made the same way.</summary>
public class TitleAmbience : MonoBehaviour
{
    private AudioSource wind, birds;
    private AudioClip[] calls;
    private float next;

    private void Start()
    {
        wind = gameObject.AddComponent<AudioSource>();
        wind.clip = WindAmbience.BuildWind(8);
        wind.loop = true;
        wind.spatialBlend = 0f;
        wind.volume = 0f;
        wind.Play();

        birds = gameObject.AddComponent<AudioSource>();
        birds.spatialBlend = 0f;
        calls = new AudioClip[5];
        for (int i = 0; i < calls.Length; i++) calls[i] = BirdSong.BuildCall(i);
        next = Time.time + Random.Range(2f, 5f);
    }

    private void Update()
    {
        wind.volume = Mathf.MoveTowards(wind.volume, 0.16f, Time.deltaTime * 0.08f);
        if (Time.time < next) return;
        next = Time.time + Random.Range(4f, 11f);
        birds.pitch = Random.Range(0.85f, 1.2f);
        birds.PlayOneShot(calls[Random.Range(0, calls.Length)], Random.Range(0.25f, 0.45f));
    }
}
