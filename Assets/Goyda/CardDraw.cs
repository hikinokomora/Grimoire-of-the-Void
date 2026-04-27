using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDraw : MonoBehaviour
{
    [SerializeField]
    private GameObject[] prefab;
    [SerializeField]
    private Transform spawnLocation;


    public void Gambling()
    {
        GameObject card = Instantiate(prefab[Random.Range(0, prefab.Length)], gameObject.transform.position, gameObject.transform.rotation);
        card.transform.SetParent(transform);
    }
}
