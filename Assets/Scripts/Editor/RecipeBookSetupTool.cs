#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class RecipeBookSetupTool : EditorWindow
{
    [MenuItem("Grimoire/🛠 Create Recipe Book System (Auto-Setup)")]
    public static void CreateRecipeBookUI()
    {
        // 1. Убеждаемся, что есть Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        // 2. Убеждаемся, что есть EventSystem
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            
            // Пытаемся добавить модуль для Input System
            var standaloneModule = esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 3. Создаем базовый объект для Системы Книги (здесь будут висеть скрипты)
        GameObject systemObj = new GameObject("RecipeBookSystem");
        RecipeBook recipeBook = systemObj.AddComponent<RecipeBook>();
        RecipeBookInput inputBook = systemObj.AddComponent<RecipeBookInput>();
        inputBook.recipeBook = recipeBook;

        // 4. Создаем Панель Книги (UI) внутри Canvas
        GameObject panelObj = new GameObject("BookPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.2f, 0.1f);
        panelRect.anchorMax = new Vector2(0.8f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.12f, 0.11f, 0.1f, 0.98f); // Темный фон книги
        recipeBook.bookPanel = panelObj;

        // 5. Создаем NameText
        TextMeshProUGUI nameText = CreateText(panelObj.transform, "NameText", "Название аспекта", new Vector2(0.5f, 0.85f), 54, TextAlignmentOptions.Center);
        recipeBook.nameText = nameText;

        // 6. Создаем AspectImage
        GameObject imgObj = new GameObject("AspectImage");
        imgObj.transform.SetParent(panelObj.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.55f);
        imgRect.anchorMax = new Vector2(0.5f, 0.55f);
        imgRect.sizeDelta = new Vector2(300, 300);
        Image aspectImage = imgObj.AddComponent<Image>();
        recipeBook.aspectImage = aspectImage;

        // 7. Создаем DescriptionText
        TextMeshProUGUI descText = CreateText(panelObj.transform, "DescriptionText", "Здесь будет отображаться детальное описание каждого найденного вами аспекта.", new Vector2(0.5f, 0.2f), 32, TextAlignmentOptions.TopLeft);
        descText.rectTransform.sizeDelta = new Vector2(800, 250);
        recipeBook.descriptionText = descText;

        // 8. Создаем кнопки
        Button prevBtn = CreateButton(panelObj.transform, "PrevButton", "<", new Vector2(0.1f, 0.5f));
        Button nextBtn = CreateButton(panelObj.transform, "NextButton", ">", new Vector2(0.9f, 0.5f));
        recipeBook.prevButton = prevBtn;
        recipeBook.nextButton = nextBtn;

        // 9. Создаем глобальный AspectManager, если его еще нет
        AspectManager aspectManager = Object.FindFirstObjectByType<AspectManager>();
        if (aspectManager == null)
        {
            GameObject managerObj = new GameObject("AspectManager");
            aspectManager = managerObj.AddComponent<AspectManager>();
            Undo.RegisterCreatedObjectUndo(managerObj, "Create AspectManager");
        }
        aspectManager.recipeBook = recipeBook;

        Undo.RegisterCreatedObjectUndo(systemObj, "Create RecipeBook System");
        Undo.RegisterCreatedObjectUndo(panelObj, "Create RecipeBook UI");

        // Прячем панель по умолчанию
        panelObj.SetActive(false);

        Selection.activeGameObject = systemObj;
        Debug.Log("<b>[Grimoire]</b> Система Книги Рецептов успешно создана на сцене! Проверьте объект RecipeBookSystem.");
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchor, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(600, 100);
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = new Color(0.9f, 0.85f, 0.8f, 1f);

        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string text, Vector2 anchor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(80, 80);
        rt.anchoredPosition = Vector2.zero;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.25f, 0.22f, 1f);

        Button btn = btnObj.AddComponent<Button>();

        // Text inside button
        TextMeshProUGUI tmp = CreateText(btnObj.transform, "Text", text, new Vector2(0.5f, 0.5f), 40, TextAlignmentOptions.Center);
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.sizeDelta = Vector2.zero;
        tmp.color = Color.white;

        return btn;
    }
}
#endif
