using Unity.VisualScripting;
using UnityEngine;

public class CardDraw : MonoBehaviour
{
    [SerializeField]
    private Material[] matPrefab;
    [SerializeField]
    private GameObject prefab;
    [SerializeField]
    private Transform spawnLocation;
    private int index;
    private string text = "";

    private bool isOnCooldown = false;

    [Header("For Arcanas")]
    [SerializeField] private GameObject[] babe;
    [SerializeField] private GameObject tentacles;

    public void Gambling()
    {
        if (isOnCooldown) return;
        isOnCooldown = true;

        index = Random.Range(0, 21);
        GameObject card = Instantiate(prefab, gameObject.transform.position, gameObject.transform.rotation);
        card.transform.SetParent(transform);
        Renderer rend = card.GetComponent<Renderer>();
        Material[] mats = rend.materials;
        mats[0] = matPrefab[index];
        rend.materials = mats;

        Invoke(nameof(ResetCooldown), 120f);
        Invoke(nameof(Apply), 5f);
    }
    private void ResetCooldown() => isOnCooldown = false;

    private void Apply()
    {
        switch (index)
        {
            case 0: 
                text = "0 Аркан: Шут"; break;
            case 1: 
                text = "I Аркан: Маг"; break;
            case 2: 
                text = "II Аркан: Папесса"; break;
            case 3:
                babe[Random.Range(0,babe.Length-1)].GetComponent<PlayAudio>().enabled = true;
                text = "III Аркан: Императрица"; break;
            case 4:
                babe[Random.Range(0, babe.Length - 1)].GetComponent<AudioSource>().mute = true;
                text = "IV Аркан: Император"; break;
            case 5: 
                text = "V Аркан: Иерофант"; break;
            case 6:
                tentacles.SetActive(true);
                Invoke(nameof(RemoveTentackles), 210f);
                text = "VI Аркан: Влюблённые"; break;
            case 7:
                GameObject.FindGameObjectWithTag("Player").GetComponent<BasicMovement>.walkSpeed = 7;
                Invoke(nameof(SpeedBack), 100f);
                text = "VII Аркан: Колесница"; break;
            case 8: 
                text = "VIII Аркан: Сила"; break;
            case 9: 
                text = "IX Аркан: Отшельник"; break;
            case 10: 
                text = "X Аркан: Колесо фортуны"; break;
            case 11:
                GameObject.FindGameObjectWithTag("Player").GetComponent<BasicMovement>.walkSpeed = 2;
                Invoke(nameof(SpeedBack), 100f);
                text = "XI Аркан: Правосудие"; break;
            case 12: 
                text = "XII Аркан: Повешенный"; break;
            case 13: 
                text = "XIII Аркан: Смерть"; break;
            case 14: 
                text = "XIV Аркан: Умеренность"; break;
            case 15: 
                text = "XV Аркан: Дьявол"; break;
            case 16: 
                text = "XVI Аркан: Башня"; break;
            case 17: 
                text = "XVII Аркан: Звезда"; break;
            case 18: 
                text = "XVIII Аркан: Луна"; break;
            case 19: 
                text = "XIX Аркан: Солнце"; break;
            case 20: 
                text = "XX Аркан: Суд"; break;
            case 21: 
                text = "XXI Аркан: Мир"; break;
        }
    }

    private void RemoveTentackles() { tentacles.SetActive(false); }
    private void SpeedBack() { GameObject.FindGameObjectWithTag("Player").GetComponent<BasicMovement>.walkSpeed = 4; }
}
