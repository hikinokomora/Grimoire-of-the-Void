using GrimoireOfTheVoid.Game;
using GrimoireOfTheVoid.Crafting;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class CardDraw : MonoBehaviour, IInteractable
{
    public static event Action<string, string> OnCardInfo;

    [SerializeField]
    private Material[] matPrefab;
    [SerializeField]
    private GameObject prefab;
    [SerializeField]
    private Transform spawnLocation;
    private int index;
    private string text = "";
    private bool isFart = false;
    private float _prevWalkSpeed;
    private bool _hasPrevWalkSpeed;

    private bool isOnCooldown = false;

    [Header("For Arcanas")]
    [SerializeField] private GameObject[] babe;
    [SerializeField] private GameObject tentacles;
    [SerializeField] private GameObject GayDirect;

    public void Interact()
    {
        if (isOnCooldown) return;
        isOnCooldown = true;

        index = UnityEngine.Random.Range(0, 21);
        if (isFart && index is 3 or 6 or 11 or 12 or 13 or 15 or 16 or 18 or 19) { index = 0; }
        GameObject card = Instantiate(prefab, gameObject.transform.position, gameObject.transform.rotation);
        card.transform.SetParent(transform);
        Renderer rend = card.GetComponent<Renderer>();
        Material[] mats = rend.materials;
        mats[0] = matPrefab[index];
        rend.materials = mats;

        Invoke(nameof(ResetCooldown), 5f);
        Invoke(nameof(Apply), 5f);
    }
    private void ResetCooldown() => isOnCooldown = false;

    private void Apply()
    {
        string title = "";
        string desc = "";
        switch (index)
        {
            case 0: 
                title = "Шут";
                desc = "Хер тебе, а не бафф";
                text = "0 Аркан: Шут"; break;
            case 1: 
                title = "Маг";
                text = "I Аркан: Маг"; break;
            case 2: 
                title = "Папесса";
                desc = "Раскрывает иконку рандомного рецепта";
                OccultAspectRegistry.EnsureDefaultFromResources();
                var list = OccultAspectRegistry.CloneOrderedList();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var a = list[i];
                    if (a == null || a.aspectIcon == null || OccultAspectRegistry.IsImageRevealedForPage(a)) list.RemoveAt(i);
                }
                if (list.Count > 0) OccultAspectRegistry.RevealImageAndNotifyUI(list[UnityEngine.Random.Range(0, list.Count)], false);
                text = "II Аркан: Папесса"; break;
            case 3:
                title = "Императрица";
                desc = "Один из эмбрионов начинает говорить";
                babe[UnityEngine.Random.Range(0,babe.Length-1)].GetComponent<PlayAudio>().enabled = true;
                text = "III Аркан: Императрица"; break;
            case 4:
                title = "Император";
                desc = "Один из эмбрионов умирает";
                babe[UnityEngine.Random.Range(0, babe.Length - 1)].GetComponent<AudioSource>().mute = true;
                text = "IV Аркан: Император"; break;
            case 5: 
                title = "Иерофант";
                desc = "Пару секунд все крафты известны";
                OccultAspectRegistry.RevealAllForSecondsAndNotifyUI(30f);
                text = "V Аркан: Иерофант"; break;
            case 6:
                title = "Влюблённые";
                desc = "Появляются тентакли (блок прохода)";
                tentacles.SetActive(true);
                Invoke(nameof(RemoveTentackles), 210f);
                text = "VI Аркан: Влюблённые"; break;
            case 7:
                title = "Колесница";
                desc = "Скорость передвижения х2";
                ApplySpeedMultiplier(2f, 100f);
                text = "VII Аркан: Колесница"; break;
            case 8: 
                title = "Сила";
                text = "VIII Аркан: Сила"; break;
            case 9: 
                title = "Отшельник";
                desc = "1 неправильный крафт без наказания";
                GayDirect.GetComponent<GameDirector>().GrantIgnoreWrongDeliveryOnce();
                text = "IX Аркан: Отшельник"; break;
            case 10: 
                title = "Колесо фортуны";
                desc = "Дебафф не выпадает(шут) ближайшие 5 траев";
                isFart = true;
                Invoke(nameof(UnFart), 400f);
                text = "X Аркан: Колесо фортуны"; break;
            case 11:
                title = "Правосудие";
                desc = "Скорость передвижения х0.5";
                ApplySpeedMultiplier(0.5f, 100f);
                text = "XI Аркан: Правосудие"; break;
            case 12:
                title = "Повешенный";
                desc = "Время теряется";
                GayDirect.GetComponent<GameDirector>().AddTime(-30);
                text = "XII Аркан: Повешенный"; break;
            case 13: 
                title = "Смерть";
                desc = "Мгновенная смерть";
                GayDirect.GetComponent<GameDirector>().SetTimeRemaining(0f);
                text = "XIII Аркан: Смерть"; break;
            case 14:
                title = "Умеренность";
                desc = "Время даётся";
                GayDirect.GetComponent<GameDirector>().AddTime(30);
                text = "XIV Аркан: Умеренность"; break;
            case 15: 
                title = "Дьявол";
                desc = "Копия имеющегося компонента, но следующий запрос тир +1";
                CraftingInteractor.RequestCopyNextClickedAspect();
                GayDirect.GetComponent<GameDirector>().ForceNextGoalTierBumpOnce();
                text = "XV Аркан: Дьявол"; break;
            case 16: 
                title = "Башня";
                desc = "Переставляем оборудование";
                text = "XVI Аркан: Башня"; break;
            case 17: 
                title = "Звезда";
                desc = "Тир запроса -1";
                text = "XVII Аркан: Звезда"; break;
            case 18: 
                title = "Луна";
                desc = "Следующих запросов 2, только один настоящий";
                text = "XVIII Аркан: Луна"; break;
            case 19: 
                title = "Солнце";
                desc = "FlashBang и всё сгнило";
                text = "XIX Аркан: Солнце"; break;
            case 20: 
                title = "Суд";
                desc = "Доп жизнь";
                GayDirect.GetComponent<GameDirector>().GrantExtraLifeOnTimeoutOnce();
                text = "XX Аркан: Суд"; break;
            case 21: 
                title = "Мир";
                desc = "Даёт возможность закончить игру (висит над столом компонентов)";
                GayDirect.GetComponent<GameDirector>().MarkWorldCardDrawn();
                text = "XXI Аркан: Мир"; break;
        }
        OnCardInfo?.Invoke(title, desc);
    }

    private void RemoveTentackles() { tentacles.SetActive(false); }
    private void ApplySpeedMultiplier(float mult, float seconds)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var m = player.GetComponent<BasicMovement>();
        if (m == null) return;
        if (!_hasPrevWalkSpeed)
        {
            _prevWalkSpeed = m.walkSpeed;
            _hasPrevWalkSpeed = true;
        }
        CancelInvoke(nameof(SpeedBack));
        m.walkSpeed = _prevWalkSpeed * mult;
        if (seconds > 0f) Invoke(nameof(SpeedBack), seconds);
    }

    private void SpeedBack()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var m = player.GetComponent<BasicMovement>();
        if (m == null) return;
        if (_hasPrevWalkSpeed) m.walkSpeed = _prevWalkSpeed;
        _hasPrevWalkSpeed = false;
    }
    private void UnFart() { isFart = false; }
}
