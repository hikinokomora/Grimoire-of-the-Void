using UnityEngine;
using GrimoireOfTheVoid.Audio;

public class PlayAudio : MonoBehaviour
{
    private AudioSource audioSource;
    private float baseVolume = 1f;
    void Start()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            baseVolume = audioSource.volume;
            audioSource.volume = baseVolume * AudioSettingsRuntime.SfxVolume01;
        }
        PlayClip();
    }

    private void PlayClip()
    {
        if (audioSource != null)
        {
            audioSource.volume = baseVolume * AudioSettingsRuntime.SfxVolume01;
        }
        audioSource.pitch = Random.Range(0.4f, 3f);
        audioSource.Play();
        Invoke(nameof(PlayClip), Random.Range(20,50));
    }
}
