using Unity.VisualScripting;
using UnityEngine;
using GrimoireOfTheVoid.Audio;

public class PlayAudioClip : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private float coolTime;
    [Header("Pitch")]
    [SerializeField] private bool randomizePitch = false;
    [SerializeField] [Min(0.01f)] private float minPitch = 0.9f;
    [SerializeField] [Min(0.01f)] private float maxPitch = 1.1f;
    private bool cooldown = false;

    private void Reset()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    public void PlayOnce()
    {
        if (cooldown) return;
        if (audioSource == null || audioClip == null)
        {
            return;
        }

        float sfxScale = AudioSettingsRuntime.SfxVolume01;
        audioSource.PlayOneShot(audioClip, sfxScale);
        cooldown = true;
        if (randomizePitch)
        {
            float a = Mathf.Min(minPitch, maxPitch);
            float b = Mathf.Max(minPitch, maxPitch);
            audioSource.pitch = Random.Range(a, b);
        }
        else
        {
            audioSource.pitch = 1f;
        }
        Invoke(nameof(Wait), coolTime > 0f ? coolTime : 1f);
    }
    private void Wait()
    {
        cooldown = false;
        audioSource.pitch = 1;
    }
}

