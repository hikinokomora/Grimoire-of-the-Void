using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using GrimoireOfTheVoid.Crafting;

public class RecipeBook : MonoBehaviour
{
    [Header("UI References")]
    public GameObject bookPanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public Image aspectImage;
    public Button nextButton;
    public Button prevButton;

    [Header("Data")]
    public List<OccultAspect> allAspects = new List<OccultAspect>();
    private List<OccultAspect> unlockedAspects = new List<OccultAspect>();
    
    private int currentPageIndex = 0;

    private void Awake()
    {
        LoadAspectsFromResources();
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
    }

    private void Start()
    {
        UpdateUnlockedAspects();
        if (bookPanel != null) bookPanel.SetActive(false);
    }

    public void ToggleBook()
    {
        if (bookPanel == null) return;

        bookPanel.SetActive(!bookPanel.activeSelf);
        if (bookPanel.activeSelf)
        {
            UpdateUnlockedAspects();
            ShowPage(currentPageIndex);
        }
    }

    private void LoadAspectsFromResources()
    {
        // Загружаем все OccultAspects из всего проекта
        OccultAspect[] loadedAspects = Resources.LoadAll<OccultAspect>("");
        
        if (loadedAspects.Length > 0)
        {
            allAspects = new List<OccultAspect>(loadedAspects);
            
            // Также на всякий случай вызовем инициализацию для 2D-книги,
            // хотя обычно это уже сделает PhysicalRecipeBook, но мы перестрахуемся:
            foreach (var a in allAspects)
            {
                if (a != null) a.sessionUnlocked = a.isUnlocked;
            }

            Debug.Log($"[RecipeBook] Автоматически загружено аспектов: {allAspects.Count}");
        }
        else
        {
            Debug.LogWarning("[RecipeBook] Аспекты не найдены в Resources!");
        }
    }

    public void UpdateUnlockedAspects()
    {
        unlockedAspects.Clear();
        foreach (var aspect in allAspects)
        {
            if (aspect != null && aspect.sessionUnlocked)
            {
                unlockedAspects.Add(aspect);
            }
        }
        
        if (unlockedAspects.Count > 0 && currentPageIndex >= unlockedAspects.Count)
        {
            currentPageIndex = unlockedAspects.Count - 1;
        }
    }

    private void ShowPage(int index)
    {
        if (unlockedAspects.Count == 0)
        {
            if (nameText != null) nameText.text = "Книга пуста";
            if (descriptionText != null) descriptionText.text = "Откройте новые аспекты, чтобы они появились здесь.";
            if (aspectImage != null) aspectImage.gameObject.SetActive(false);
            
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }

        if (aspectImage != null) aspectImage.gameObject.SetActive(true);
        OccultAspect currentAspect = unlockedAspects[index];
        
        if (nameText != null) nameText.text = currentAspect.DisplayName;
        if (descriptionText != null) descriptionText.text = currentAspect.description;
        if (aspectImage != null) aspectImage.sprite = currentAspect.aspectIcon;

        if (prevButton != null) prevButton.interactable = (index > 0);
        if (nextButton != null) nextButton.interactable = (index < unlockedAspects.Count - 1);
    }

    public void NextPage()
    {
        if (currentPageIndex < unlockedAspects.Count - 1)
        {
            currentPageIndex++;
            ShowPage(currentPageIndex);
        }
    }

    public void PrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            ShowPage(currentPageIndex);
        }
    }
}
