using Unity.VisualScripting;
using UnityEngine;
using GrimoireOfTheVoid.Audio;

public class PlayAudioClip : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private float coolTime;
    private bool cooldown = false;


    public void PlayOnce()
    {
        if (cooldown) return;
        float sfxScale = AudioSettingsRuntime.SfxVolume01;
        audioSource.PlayOneShot(audioClip, sfxScale);
        cooldown = true;
        audioSource.pitch = Random.Range(0.7f, 2.5f);
        Invoke(nameof(Wait), 1f);
    }
    private void Wait()
    {
        cooldown = false;
        audioSource.pitch = 1;
    }
}

