using UnityEngine;

public class CardDissolve : MonoBehaviour
{
    [SerializeField] private AudioSource audiS;
    [SerializeField] private ParticleSystem fire;
    [SerializeField] private AudioClip[] sound;

    public void Take()
    {
        audiS.PlayOneShot(sound[0]);
    }

    public void Dissolve()
    {
        fire.Play();
        audiS.PlayOneShot(sound[1]);
    }
    public void Del()
    {
        Destroy(gameObject);
    }
}
