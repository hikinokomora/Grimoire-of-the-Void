using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GrimoireOfTheVoid.Crafting;

public class PhysicalRecipeBook : MonoBehaviour
{
    [Header("Book Settings")]
    public Transform spinePivot;
    public GameObject pagePrefab;
    public float turnSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource pageTurnAudioSource;
    [SerializeField] private AudioClip pageTurnClip;
    [SerializeField] [Range(0f, 1f)] private float pageTurnVolume = 1f;
    [SerializeField] private bool randomizePageTurnPitch = false;
    [SerializeField] [Min(0.01f)] private float minPageTurnPitch = 0.9f;
    [SerializeField] [Min(0.01f)] private float maxPageTurnPitch = 1.1f;

    [Header("Data (кэш, синхронизируется с OccultAspectRegistry)")]
    public List<OccultAspect> allAspects = new List<OccultAspect>();
    private List<OccultAspect> unlockedAspects = new List<OccultAspect>();

    private List<GameObject> spawnedPages = new List<GameObject>();
    private int currentPageIndex = 0; // 0 means page 0 is front. Page index increments by 1 per leaf.
    private bool isTurning = false;

    private void Reset()
    {
        if (pageTurnAudioSource == null)
        {
            pageTurnAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        OccultAspectRegistry.EnsureDefaultFromResources();
        if (OccultAspectRegistry.Count == 0)
        {
            Debug.LogError("[PhysicalRecipeBook] Каталог аспектов пуст. Поместите OccultAspect в Resources/" + OccultAspectRegistry.ResourcesCatalogSubfolder + " и/или заполните каталог в AspectManager.");
            return;
        }
        allAspects = OccultAspectRegistry.CloneOrderedList();
        UpdateUnlockedAspects();
        GeneratePages();
        CreateClickZones();
    }

    private void PlayPageTurnSound()
    {
        if (pageTurnAudioSource == null || pageTurnClip == null)
        {
            return;
        }

        if (randomizePageTurnPitch)
        {
            float a = Mathf.Min(minPageTurnPitch, maxPageTurnPitch);
            float b = Mathf.Max(minPageTurnPitch, maxPageTurnPitch);
            pageTurnAudioSource.pitch = Random.Range(a, b);
        }
        else
        {
            pageTurnAudioSource.pitch = 1f;
        }

        pageTurnAudioSource.PlayOneShot(pageTurnClip, Mathf.Clamp01(pageTurnVolume));
    }

    private void Update()
    {
        // Поддержка перелистывания с клавиатуры (A / D или Стрелочки) — только в режиме у стола
        if (UnityEngine.InputSystem.Keyboard.current != null && CraftingViewController.IsInCraftingView)
        {
            if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame || 
                UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                NextPage();
            }
            if (UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame || 
                UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                PrevPage();
            }
        }
    }

    private void LateUpdate()
    {
        // Прячем шаблон-болванку ПОСЛЕ первого кадра, чтобы Unity точно успела распарсить Instantiate
        if (pagePrefab != null && pagePrefab.scene.IsValid() && pagePrefab.activeInHierarchy)
        {
            pagePrefab.SetActive(false);
        }
    }

    /// <summary>После крафта / ad-hoc добавления в реестр: перерисовка или полная пересборка стопки.</summary>
    public void SyncFromRegistry(OccultAspect focus = null)
    {
        if (!OccultAspectRegistry.IsInitialized) return;
        int prev = allAspects != null ? allAspects.Count : 0;
        allAspects = OccultAspectRegistry.CloneOrderedList();
        UpdateUnlockedAspects();
        if (allAspects.Count != prev || spawnedPages.Count < 2)
        {
            GeneratePages();
            CreateClickZones();
        }
        else
        {
            RebindPageContents();
        }
        if (focus != null) GoToPageForAspect(focus);
    }

    private void GoToPageForAspect(OccultAspect newAspect)
    {
        if (newAspect == null) return;
        int aspectIndex = IndexOfAspectForNavigation(newAspect);
        if (aspectIndex == -1) return;
        int targetLeafIndex = aspectIndex / 2;
        int targetPageIndex = targetLeafIndex + 1;
        if (currentPageIndex != targetPageIndex) GoToPage(targetPageIndex);
    }

    private void RebindPageContents()
    {
        int totalLeaves = Mathf.CeilToInt((float)unlockedAspects.Count / 2f);
        if (totalLeaves == 0) totalLeaves = 1;
        for (int i = 0; i < totalLeaves; i++)
        {
            int pageObjIndex = i + 1;
            if (pageObjIndex < spawnedPages.Count - 1)
            {
                PhysicalPage pp = spawnedPages[pageObjIndex].GetComponent<PhysicalPage>();
                if (pp == null) continue;
                int frontIndex = i * 2;
                if (frontIndex < unlockedAspects.Count) pp.SetupFront(unlockedAspects[frontIndex]);
                else pp.SetupFrontEmpty();
                int backIndex = i * 2 + 1;
                if (backIndex < unlockedAspects.Count) pp.SetupBack(unlockedAspects[backIndex]);
                else pp.SetupBackEmpty();
            }
        }
    }

    public void UpdateUnlockedAspects()
    {
        unlockedAspects.Clear();
        foreach (var aspect in allAspects)
        {
            // Теперь показываем страницы для ВСЕХ рецептов с самого начала игры, 
            // скрывать будем только сам текстовый состав внутри PhysicalPage.cs
            if (aspect != null)
            {
                unlockedAspects.Add(aspect);
            }
        }
    }

    public void RefreshBook(OccultAspect newAspect = null) => SyncFromRegistry(newAspect);

    private int IndexOfAspectForNavigation(OccultAspect a)
    {
        if (a == null) return -1;
        a = OccultAspectRegistry.GetCanonical(a) ?? a;
        for (int k = 0; k < unlockedAspects.Count; k++)
        {
            if (unlockedAspects[k] == a) return k;
        }
        if (string.IsNullOrEmpty(a.ID)) return -1;
        for (int k = 0; k < unlockedAspects.Count; k++)
        {
            if (unlockedAspects[k] != null && unlockedAspects[k].ID == a.ID) return k;
        }
        return -1;
    }

    public void GoToPage(int targetIndex)
    {
        StopAllCoroutines(); // Останавливаем текущие анимации перелистывания
        isTurning = false;
        StartCoroutine(TurnToPageCoroutine(targetIndex));
    }

    private IEnumerator TurnToPageCoroutine(int targetIndex)
    {
        isTurning = true;
        
        while (currentPageIndex != targetIndex)
        {
            if (currentPageIndex < targetIndex)
            {
                StartCoroutine(TurnPageCoroutine(spawnedPages[currentPageIndex], true, currentPageIndex));
                currentPageIndex++;
            }
            else
            {
                currentPageIndex--;
                StartCoroutine(TurnPageCoroutine(spawnedPages[currentPageIndex], false, currentPageIndex));
            }
            // Ждем небольшую паузу между перелистываниями, чтобы это выглядело как быстрое листание
            yield return new WaitForSeconds(0.1f);
        }
        
        isTurning = false;
    }

    private void CreateClickZones()
    {
        // Создаем две невидимые физические зоны прямо поверх половинок книги, 
        // чтобы игрок мог кликать прямо по страницам!
        CreateZone("RightPageClickZone", new Vector3(0.55f, 0.02f, 0), true);
        CreateZone("LeftPageClickZone", new Vector3(-0.55f, 0.02f, 0), false);
    }

    private void CreateZone(string zoneName, Vector3 pos, bool isNext)
    {
        Transform existing = spinePivot.Find(zoneName);
        if (existing != null) Destroy(existing.gameObject);

        GameObject zone = new GameObject(zoneName);
        zone.transform.SetParent(spinePivot, false);
        zone.transform.localPosition = pos;
        
        // Размеры примерно совпадают с размером лежащей страницы (Quad)
        BoxCollider col = zone.AddComponent<BoxCollider>();
        col.size = new Vector3(1.1f, 0.2f, 1.5f); 
        
        // Вешаем ваш скрипт кнопки - теперь клик "в левую часть книги" будет вызывать PrevPage, а в правую - NextPage
        PhysicalBookButton btn = zone.AddComponent<PhysicalBookButton>();
        btn.bookScript = this;
        btn.isNextPage = isNext;
    }

    private void GeneratePages()
    {
        // Удаляем старые
        foreach (var page in spawnedPages)
        {
            if (page != null) Destroy(page);
        }
        spawnedPages.Clear();

        if (pagePrefab == null)
        {
            Debug.LogError("[PhysicalRecipeBook] ОШИБКА: Не назначен Page Prefab! Листы не из чего создавать.");
            return;
        }

        // Включаем шаблон-источник на случай если он был скрыт
        pagePrefab.SetActive(true);

        // 0. СОЗДАЕМ ПЕРЕДНЮЮ ОБЛОЖКУ (Нулевая страница)
        GameObject frontCover = Instantiate(pagePrefab);
        SetupAsCover(frontCover, "FRONT_COVER");
        spawnedPages.Add(frontCover);

        // 1. СОЗДАЕМ ВНУТРЕННИЕ СТРАНИЦЫ
        int totalLeaves = Mathf.CeilToInt((float)unlockedAspects.Count / 2f);
        if (totalLeaves == 0) totalLeaves = 1;

        for (int i = 0; i < totalLeaves; i++)
        {
            // Клонируем
            GameObject newPage = Instantiate(pagePrefab);
            newPage.name = $"======> CLONED_PAGE_{i} <======";
            newPage.SetActive(true);
            newPage.hideFlags = HideFlags.None; // Принудительно показываем в Иерархии
            
            PhysicalPage pp = newPage.GetComponent<PhysicalPage>();
            if (pp != null)
            {
                // Настраиваем переднюю часть (Аспект: i*2)
                int frontIndex = i * 2;
                if (frontIndex < unlockedAspects.Count)
                    pp.SetupFront(unlockedAspects[frontIndex]);
                else
                    pp.SetupFrontEmpty();

                // Настраиваем заднюю часть (Аспект: i*2 + 1)
                int backIndex = i * 2 + 1;
                if (backIndex < unlockedAspects.Count)
                    pp.SetupBack(unlockedAspects[backIndex]);
                else
                    pp.SetupBackEmpty();
            }

            spawnedPages.Add(newPage);
        }

        // 2. СОЗДАЕМ ЗАДНЮЮ ОБЛОЖКУ
        GameObject backCover = Instantiate(pagePrefab);
        SetupAsCover(backCover, "BACK_COVER");
        spawnedPages.Add(backCover);

        // Расставляем правильные позиции и высоты для всей стопки книги (Обложка на самом верху, остальное под ней)
        for (int j = 0; j < spawnedPages.Count; j++)
        {
            spawnedPages[j].transform.SetParent(spinePivot, false);
            // Индекс j=0 это передняя обложка. Она имеет самую высокую Y позицию (0).
            // Остальные лежат всё глубже и глубже. Увеличиваем зазор до 0.005, чтобы избежать наслаивания Z-fighting!
            spawnedPages[j].transform.localPosition = new Vector3(0, -j * 0.005f, 0); 
            spawnedPages[j].transform.localRotation = Quaternion.Euler(0, 0, 0); // Все страницы на правой стороне
            spawnedPages[j].transform.localScale = Vector3.one; 
        }

        // Выключаем оригинальный префаб
        pagePrefab.SetActive(false);
        
        currentPageIndex = 0;
        Debug.Log($"[PhysicalRecipeBook] ГЕНЕРАЦИЯ ЗАВЕРШЕНА. Сгенерировано страниц: {totalLeaves}, плюс 2 обложки.");
    }

    private void SetupAsCover(GameObject coverObj, string coverName)
    {
        coverObj.name = $"======> {coverName} <======";
        coverObj.SetActive(true);
        coverObj.hideFlags = HideFlags.None;
        
        // Очищаем текст на обложках
        PhysicalPage pp = coverObj.GetComponent<PhysicalPage>();
        if (pp != null)
        {
            pp.SetupFrontEmpty();
            pp.SetupBackEmpty();
            if(pp.frontEmptyUI != null) pp.frontEmptyUI.SetActive(false);
            if(pp.backEmptyUI != null) pp.backEmptyUI.SetActive(false);
        }
        
        // Находим бумагу и красим в цвет обложки (тёмный)
        Transform front = coverObj.transform.Find("PaperQuad_Front");
        Transform back = coverObj.transform.Find("PaperQuad_Back");
        
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null) unlitShader = Shader.Find("UI/Default");
        Material coverMat = new Material(unlitShader);
        Color col = new Color(0.25f, 0.15f, 0.08f); // коричневый
        if (coverMat.HasProperty("_Color")) coverMat.SetColor("_Color", col);
        if (coverMat.HasProperty("_BaseColor")) coverMat.SetColor("_BaseColor", col);

        if (front != null)
        {
            front.GetComponent<MeshRenderer>().sharedMaterial = coverMat;
            front.localScale = new Vector3(1.05f, 1.45f, 1f); // Обложка чуть больше страниц
        }
        if (back != null)
        {
            back.GetComponent<MeshRenderer>().sharedMaterial = coverMat;
            back.localScale = new Vector3(1.05f, 1.45f, 1f);
        }
    }

    public void NextPage()
    {
        if (isTurning || currentPageIndex >= spawnedPages.Count) return;
        
        StartCoroutine(TurnPageCoroutine(spawnedPages[currentPageIndex], true, currentPageIndex));
        currentPageIndex++;
    }

    public void PrevPage()
    {
        if (isTurning || currentPageIndex <= 0) return;
        
        currentPageIndex--;
        StartCoroutine(TurnPageCoroutine(spawnedPages[currentPageIndex], false, currentPageIndex));
    }

    private IEnumerator TurnPageCoroutine(GameObject page, bool forward, int pageIndex)
    {
        isTurning = true;
        PlayPageTurnSound();
        
        // Явно используем эйлеровы углы для оси Z (от 0 до 180), чтобы Unity не применял Slerp в обратную сторону или через ось X/Y (что вызывало "странное" листание)
        float startAngle = forward ? 0f : 180f;
        float endAngle = forward ? 180f : 0f;
        
        // Вычисляем высоту страницы в правой и левой стопке.
        // Чтобы на левой стороне перевернутые страницы тоже ровно складывались и не наслаивались друг на друга:
        float rightY = -pageIndex * 0.005f; 
        float leftY = -(spawnedPages.Count - pageIndex) * 0.005f;

        float startY = forward ? rightY : leftY;
        float endY = forward ? leftY : rightY;

        float progress = 0;
        while (progress < 1f)
        {
            progress += Time.deltaTime * turnSpeed;
            float smooth = Mathf.SmoothStep(0f, 1f, progress);
            
            float currentAngle = Mathf.Lerp(startAngle, endAngle, smooth);
            float currentY = Mathf.Lerp(startY, endY, smooth);

            page.transform.localPosition = new Vector3(0, currentY, 0);
            page.transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
            yield return null;
        }

        page.transform.localPosition = new Vector3(0, endY, 0);
        page.transform.localRotation = Quaternion.Euler(0, 0, endAngle);
        isTurning = false;
    }
}
