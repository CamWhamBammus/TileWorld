using UnityEngine;

public class QuestMenuToggle : MonoBehaviour
{
    [Header("Quest Menu")]
    [SerializeField] private GameObject questMenu;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Q;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip menuSound;

    [Header("Open Sound Timing")]
    [SerializeField] private float openStartTime = 0f;
    [SerializeField] private float openDuration = 0.3f;

    [Header("Close Sound Timing")]
    [SerializeField] private float closeStartTime = 0.3f;
    [SerializeField] private float closeDuration = 0.3f;

    private float stopSoundTime;
    private bool isPlayingTimedSound;

    private void Start()
    {
        if (questMenu == null)
        {
            Debug.LogError("[QuestMenuToggle] Missing questMenu reference.");
            enabled = false;
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogError("[QuestMenuToggle] Missing AudioSource. Add an AudioSource to this object.");
            enabled = false;
            return;
        }

        questMenu.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            bool shouldOpen = !questMenu.activeSelf;
            questMenu.SetActive(shouldOpen);

            if (shouldOpen)
            {
                PlaySoundSection(openStartTime, openDuration);
            }
            else
            {
                PlaySoundSection(closeStartTime, closeDuration);
            }
        }

        if (isPlayingTimedSound && Time.time >= stopSoundTime)
        {
            audioSource.Stop();
            isPlayingTimedSound = false;
        }
    }

    private void PlaySoundSection(float startTime, float duration)
    {
        if (menuSound == null)
        {
            Debug.LogWarning("[QuestMenuToggle] Missing menuSound clip.");
            return;
        }

        audioSource.Stop();
        audioSource.clip = menuSound;
        audioSource.time = startTime;
        audioSource.Play();

        stopSoundTime = Time.time + duration;
        isPlayingTimedSound = true;
    }
}