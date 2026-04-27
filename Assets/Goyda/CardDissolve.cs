using UnityEngine;

public class CardDissolve : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem fire;
    public void Dissolve()
    {
        fire.Play();
    }
    public void Del()
    {
        Destroy(gameObject);
    }
}
