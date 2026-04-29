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
    private GameObject volumeM;
    [SerializeField]
    private GameObject volumeS;
    [SerializeField]
    private GameObject light;
    [SerializeField]
    private Transform spawnLocation;
    private int index;
    private string text = "";
    private bool isFart = false;
    private float _prevWalkSpeed;
    private bool _hasPrevWalkSpeed;
    private float _prevTimeRateMultiplier = 1f;
    private bool _hasPrevTimeRateMultiplier;

    private bool isOnCooldown = false;

    [Header("For Arcanas")]
    [SerializeField] private GameObject[] babe;
    [SerializeField] private GameObject tentacles;
    [SerializeField] private GameObject GayDirect;
    [SerializeField] private GameObject Var1;
    [SerializeField] private GameObject Var2;
    private bool isVar1 = false;

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
        light.SetActive(false);
        Invoke(nameof(ResetCooldown), 5f);
        Invoke(nameof(Apply), 3f);
    }
    private void ResetCooldown() { isOnCooldown = false; light.SetActive(true); }
    private int count = 16;
    private void Apply()
    {
        //string title = "";
        string desc = "";
        count++;
        switch (count)
        {
            case 0: 
                //title = "Шут";
                desc = "Что же произошло?";
                text = "0 Аркан: Шут"; break;
            case 1: 
                //title = "Маг";
                desc = "В гримуаре что-то изменилось";
                OccultAspectRegistry.EnsureDefaultFromResources();
                var recipeList = OccultAspectRegistry.CloneOrderedList();
                for (int i = recipeList.Count - 1; i >= 0; i--)
                {
                    var a = recipeList[i];
                    if (a == null || OccultAspectRegistry.IsRevealedForPage(a)) recipeList.RemoveAt(i);
                }
                if (recipeList.Count > 0) OccultAspectRegistry.RevealRecipeAndNotifyUI(recipeList[UnityEngine.Random.Range(0, recipeList.Count)], false);
                text = "I Аркан: Маг"; break;
            case 2: 
                //title = "Папесса";
                desc = "В гримуаре что-то изменилось";
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
                //title = "Императрица";
                desc = "Мне не послышалось?";
                babe[UnityEngine.Random.Range(0,babe.Length-1)].GetComponent<PlayAudio>().enabled = true;
                text = "III Аркан: Императрица"; break;
            case 4:
                //title = "Император";
                desc = "Кто-то умолк, но кто?";
                babe[UnityEngine.Random.Range(0, babe.Length - 1)].GetComponent<AudioSource>().mute = true;
                text = "IV Аркан: Император"; break;
            case 5: 
                //title = "Иерофант";
                desc = "Срочно к гримуару!";
                OccultAspectRegistry.RevealAllForSecondsAndNotifyUI(30f);
                text = "V Аркан: Иерофант"; break;
            case 6:
                //title = "Влюблённые";
                desc = "Ох чёрт!";
                tentacles.SetActive(true);
                Invoke(nameof(RemoveTentackles), 210f);
                text = "VI Аркан: Влюблённые"; break;
            case 7:
                //title = "Колесница";
                desc = "Какая лёгкость!";
                ApplySpeedMultiplier(2f, 100f);
                text = "VII Аркан: Колесница"; break;
            case 8: 
                //title = "Сила";
                desc = "The world!";
                ApplyTimeRateMultiplier(0.6f, 90f);
                text = "VIII Аркан: Сила"; break;
            case 9: 
                //title = "Отшельник";
                desc = "Появилось некое чувство безопасности";
                GayDirect.GetComponent<GameDirector>().GrantIgnoreWrongDeliveryOnce();
                text = "IX Аркан: Отшельник"; break;
            case 10: 
                //title = "Колесо фортуны";
                desc = "Фортуна на моей стороне";
                isFart = true;
                Invoke(nameof(UnFart), 400f);
                text = "X Аркан: Колесо фортуны"; break;
            case 11:
                //title = "Правосудие";
                desc = "Какая тяжесть в теле";
                ApplySpeedMultiplier(0.5f, 100f);
                text = "XI Аркан: Правосудие"; break;
            case 12:
                //title = "Повешенный";
                desc = "Надо поспешить!";
                GayDirect.GetComponent<GameDirector>().AddTime(-30);
                text = "XII Аркан: Повешенный"; break;
            case 13: 
                //title = "Смерть";
                //desc = "Мгновенная смерть";
                GayDirect.GetComponent<GameDirector>().SetTimeRemaining(0f);
                text = "XIII Аркан: Смерть"; break;
            case 14:
                //title = "Умеренность";
                desc = "Можно слегка расслабится";
                GayDirect.GetComponent<GameDirector>().AddTime(30);
                text = "XIV Аркан: Умеренность"; break;
            case 15: 
                //title = "Дьявол";
                desc = "Хочется схватить ценный компонент";
                CraftingInteractor.RequestCopyNextClickedAspect();
                GayDirect.GetComponent<GameDirector>().ForceNextGoalTierBumpOnce();
                text = "XV Аркан: Дьявол"; break;
            case 16:Var1.SetActive(isVar1);
                isVar1=!isVar1;
                Var2.SetActive(isVar1);
                //title = "Башня";
                desc = "Что-то здесь изменилось";
                text = "XVI Аркан: Башня"; break;
            case 17: 
                //title = "Звезда";
                desc = "Жить стало полегче";
                GayDirect.GetComponent<GameDirector>().ForceNextGoalTierLowerOnce();
                text = "XVII Аркан: Звезда"; break;
            case 18:
                volumeM.SetActive(true);
                Invoke(nameof(RemoveVolumeM), 90f);
                //title = "Луна";
                desc = "Что за?..";
                text = "XVIII Аркан: Луна"; break;
            case 19:
                volumeS.SetActive(true);
                Invoke(nameof(RemoveVolumeS), 110f);
                //title = "Солнце";
                desc = "Мои глаза!";
                text = "XIX Аркан: Солнце"; break;
            case 20: 
                //title = "Суд";
                desc = "Чувствую, в следующий раз меня простят";
                GayDirect.GetComponent<GameDirector>().GrantExtraLifeOnTimeoutOnce();
                text = "XX Аркан: Суд"; break;
            case 21: 
                //title = "Мир";
                desc = "Наконец-то!";
                GayDirect.GetComponent<GameDirector>().MarkWorldCardDrawn();
                text = "XXI Аркан: Мир"; break;
        }
        OnCardInfo?.Invoke(text, desc);
        
    }

    private void RemoveTentackles() { tentacles.SetActive(false); }
    private void RemoveVolumeM() { volumeM.SetActive(false); }
    private void RemoveVolumeS() { volumeS.SetActive(false); }
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

    private void ApplyTimeRateMultiplier(float mult, float seconds)
    {
        var director = GayDirect != null ? GayDirect.GetComponent<GameDirector>() : GameDirector.Instance;
        if (director == null) return;
        if (!_hasPrevTimeRateMultiplier)
        {
            _prevTimeRateMultiplier = director.TimeRateMultiplier;
            _hasPrevTimeRateMultiplier = true;
        }
        CancelInvoke(nameof(TimeRateBack));
        director.TimeRateMultiplier = _prevTimeRateMultiplier * mult;
        if (seconds > 0f) Invoke(nameof(TimeRateBack), seconds);
    }

    private void TimeRateBack()
    {
        var director = GayDirect != null ? GayDirect.GetComponent<GameDirector>() : GameDirector.Instance;
        if (director == null) return;
        if (_hasPrevTimeRateMultiplier) director.TimeRateMultiplier = _prevTimeRateMultiplier;
        _hasPrevTimeRateMultiplier = false;
    }
    private void UnFart() { isFart = false; }
}
