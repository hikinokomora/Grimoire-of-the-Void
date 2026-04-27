using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

    [Header("Data (кэш реестра)")]
    public List<OccultAspect> allAspects = new List<OccultAspect>();
    private readonly List<OccultAspect> unlockedAspects = new List<OccultAspect>();

    private int currentPageIndex = 0;

    private void Awake()
    {
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
    }

    private void Start()
    {
        OccultAspectRegistry.EnsureDefaultFromResources();
        SyncFromRegistry();
        if (bookPanel != null) bookPanel.SetActive(false);
    }

    public void SyncFromRegistry()
    {
        OccultAspectRegistry.EnsureDefaultFromResources();
        allAspects = OccultAspectRegistry.CloneOrderedList();
        UpdateUnlockedAspects();
    }

    public void ToggleBook()
    {
        if (bookPanel == null) return;

        bookPanel.SetActive(!bookPanel.activeSelf);
        if (bookPanel.activeSelf)
        {
            SyncFromRegistry();
            ShowPage(currentPageIndex);
        }
    }

    public void UpdateUnlockedAspects()
    {
        unlockedAspects.Clear();
        foreach (OccultAspect aspect in allAspects)
        {
            if (aspect != null && OccultAspectRegistry.IsRevealedForPage(aspect))
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
            if (descriptionText != null) descriptionText.text = "Создай аспекты в котле, чтобы появились записи.";
            if (aspectImage != null) aspectImage.gameObject.SetActive(false);
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }

        index = Mathf.Clamp(index, 0, unlockedAspects.Count - 1);
        currentPageIndex = index;
        OccultAspect currentAspect = unlockedAspects[index];

        if (nameText != null) nameText.text = currentAspect.DisplayName;
        if (descriptionText != null) descriptionText.text = currentAspect.description;
        if (aspectImage != null)
        {
            bool showImage = OccultAspectRegistry.IsImageRevealedForPage(currentAspect) && currentAspect.aspectIcon != null;
            aspectImage.gameObject.SetActive(showImage);
            if (showImage) aspectImage.sprite = currentAspect.aspectIcon;
        }

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
