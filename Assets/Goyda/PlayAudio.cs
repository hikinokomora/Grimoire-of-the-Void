using UnityEngine;

public class PlayAudio : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip[] audioClip;
    void Awake()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        PlayClip();
    }

    private void PlayClip()
    {
        audioSource.PlayOneShot(audioClip[Random.Range(0, audioClip.Length-1)]);
        Invoke(nameof(PlayClip), Random.Range(10,40));
    }
}
