using UnityEngine;
using GrimoireOfTheVoid.Crafting;

public class AspectManager : MonoBehaviour
{
    public static AspectManager Instance { get; private set; }

    [Header("References")]
    public RecipeBook recipeBook;
    public PhysicalRecipeBook physicalBook;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UnlockAspect(OccultAspect aspect)
    {
        if (aspect != null && !aspect.sessionUnlocked)
        {
            aspect.sessionUnlocked = true;
            Debug.Log($"[AspectManager] Аспект разблокирован в рамках сессии: {aspect.DisplayName}");

            // Обновляем 2D книгу
            if (recipeBook != null && recipeBook.bookPanel != null && recipeBook.bookPanel.activeSelf)
            {
                recipeBook.UpdateUnlockedAspects();
            }

            // Обновляем 3D физическую книгу
            if (physicalBook == null)
            {
                // Пытаемся найти её на сцене, если ссылку забыли привязать в инспекторе
                physicalBook = Object.FindFirstObjectByType<PhysicalRecipeBook>();
            }

            if (physicalBook != null)
            {
                physicalBook.RefreshBook(aspect);
            }
        }
    }
}
