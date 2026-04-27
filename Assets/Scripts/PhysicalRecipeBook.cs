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

    [Header("Data")]
    public List<OccultAspect> allAspects = new List<OccultAspect>();
    private List<OccultAspect> unlockedAspects = new List<OccultAspect>();
    
    [Header("Debug")]
    public bool ignoreUnlockForDebug = true; // Покажет все рецепты для проверки

    private List<GameObject> spawnedPages = new List<GameObject>();
    private int currentPageIndex = 0; // 0 means page 0 is front. Page index increments by 1 per leaf.
    private bool isTurning = false;

    private void Start()
    {
        LoadAspectsFromResources();
        UpdateUnlockedAspects();
        GeneratePages();
        CreateClickZones();
    }

    private void Update()
    {
        // Поддержка перелистывания с клавиатуры (A / D или Стрелочки)
        if (UnityEngine.InputSystem.Keyboard.current != null)
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

    private void LoadAspectsFromResources()
    {
        // Мы теперь грузим ВСЕ OccultAspects со всего проекта! (потому что они могут быть разбросаны)
        OccultAspect[] loadedAspects = Resources.LoadAll<OccultAspect>("");
        if (loadedAspects.Length > 0)
        {
            allAspects = new List<OccultAspect>(loadedAspects);
            
            // ПРИ ЗАПУСКЕ ИГРЫ: сбрасываем состояние сессии на оригинальное базовое значение 
            foreach (var a in allAspects)
            {
                if (a != null) a.sessionUnlocked = a.isUnlocked;
            }
            
            Debug.Log($"[PhysicalRecipeBook] Погружено аспектов: {allAspects.Count}");
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

    public void RefreshBook(OccultAspect newAspect = null)
    {
        UpdateUnlockedAspects();
        
        // Обновляем визуальное содержимое уже созданных страниц (без пересоздания)
        // spawnedPages[0] - передняя обложка
        // spawnedPages[1] до [N-1] - внутренние страницы
        // spawnedPages[N] - задняя обложка
        
        int totalLeaves = Mathf.CeilToInt((float)unlockedAspects.Count / 2f);
        if (totalLeaves == 0) totalLeaves = 1;

        for (int i = 0; i < totalLeaves; i++)
        {
            // spawnedPages[0] — это передняя обложка
            int pageObjIndex = i + 1;
            if (pageObjIndex < spawnedPages.Count - 1)
            {
                PhysicalPage pp = spawnedPages[pageObjIndex].GetComponent<PhysicalPage>();
                if (pp != null)
                {
                    int frontIndex = i * 2;
                    if (frontIndex < unlockedAspects.Count)
                        pp.SetupFront(unlockedAspects[frontIndex]);
                    else
                        pp.SetupFrontEmpty();

                    int backIndex = i * 2 + 1;
                    if (backIndex < unlockedAspects.Count)
                        pp.SetupBack(unlockedAspects[backIndex]);
                    else
                        pp.SetupBackEmpty();
                }
            }
        }

        // Плавный переход к странице с новым аспектом
        if (newAspect != null)
        {
            int aspectIndex = unlockedAspects.IndexOf(newAspect);
            if (aspectIndex != -1)
            {
                // Индекс листа (0 для рецептов 0 и 1, 1 для 2 и 3, и т.д.)
                int targetLeafIndex = aspectIndex / 2;
                
                // +1 потому что currentPageIndex так же считает перелистывания?
                // Смотрим: изначально currentPageIndex = 0 (показывается обложка).
                // При NextPage currentPageIndex становится 1, и обложка переворачивается (показывая 0 и 1 рецепт).
                int targetPageIndex = targetLeafIndex + 1;
                
                if (currentPageIndex != targetPageIndex)
                {
                    GoToPage(targetPageIndex);
                }
            }
        }
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
