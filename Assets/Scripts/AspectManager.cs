using System.Collections.Generic;
using UnityEngine;
using GrimoireOfTheVoid.Crafting;

[DefaultExecutionOrder(-200)]
public class AspectManager : MonoBehaviour
{
    public static AspectManager Instance { get; private set; }

    [Header("Каталог (главный)")]
    [Tooltip("Перетащи сюда все OccultAspect, которые должны быть в книге. Пусто = только Resources (+ ad-hoc при крафте).")]
    [SerializeField] private List<OccultAspect> aspectCatalog;
    [SerializeField] private bool mergeResourcesIntoCatalog = true;

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
            return;
        }

        OccultAspectRegistry.Initialize(aspectCatalog, mergeResourcesIntoCatalog);
    }

    public void UnlockAspect(OccultAspect aspect)
    {
        if (aspect == null) return;
        // Единая точка: реестр + все PhysicalRecipeBook / RecipeBook (как в котле без ссылки на менеджер)
        OccultAspectRegistry.UnlockAndNotifyUI(aspect);
    }
}
