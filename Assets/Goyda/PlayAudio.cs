using UnityEngine;

public class PlayAudio : MonoBehaviour
{
    private AudioSource audioSource;
    void Start()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        PlayClip();
    }

    private void PlayClip()
    {
        audioSource.pitch = Random.Range(0.4f, 3f);
        audioSource.Play();
        Invoke(nameof(PlayClip), Random.Range(20,50));
    }
}
