using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GrimoireOfTheVoid.Crafting;

public class PhysicalPage : MonoBehaviour
{
    [Header("Front Layout")]
    public TextMeshProUGUI frontNameText;
    public TextMeshProUGUI frontDescText;
    public Image frontAspectImage;
    public GameObject frontEmptyUI;

    [Header("Back Layout")]
    public TextMeshProUGUI backNameText;
    public TextMeshProUGUI backDescText;
    public Image backAspectImage;
    public GameObject backEmptyUI;

    private void EnsureReferences()
    {
        // Принудительно очищаем ссылки чтобы брать СВОИ объекты, 
        // иначе клон может случайно менять текст у оригинального префаба!
        
        Transform frontCanvas = transform.Find("FrontCanvas");
        if (frontCanvas != null)
        {
            frontNameText = frontCanvas.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            frontDescText = frontCanvas.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            frontAspectImage = frontCanvas.Find("Image")?.GetComponent<Image>();
            frontEmptyUI = frontCanvas.Find("EmptyState")?.gameObject;

            // Сдвигаем изображение вверх на 125 единиц
            if (frontAspectImage != null)
            {
                Vector2 pos = frontAspectImage.rectTransform.anchoredPosition;
                frontAspectImage.rectTransform.anchoredPosition = new Vector2(pos.x, 125f);
            }
        }

        Transform backCanvas = transform.Find("BackCanvas");
        if (backCanvas != null)
        {
            backNameText = backCanvas.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            backDescText = backCanvas.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            backAspectImage = backCanvas.Find("Image")?.GetComponent<Image>();
            backEmptyUI = backCanvas.Find("EmptyState")?.gameObject;
            
            // Сдвигаем изображение вверх на 125 единиц
            if (backAspectImage != null)
            {
                Vector2 pos = backAspectImage.rectTransform.anchoredPosition;
                backAspectImage.rectTransform.anchoredPosition = new Vector2(pos.x, 125f);
            }

            // ФИКС для обратной стороны листа - она перевернута по умолчанию, из-за чего читалась снизу вверх / задом наперед.
            // LookRotation выставляет вектора Canvas строго правильно для левой части книги (World +Y, +Z)
            backCanvas.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        }
    }

    public void SetupFront(OccultAspect data)
    {
        EnsureReferences();
        if (frontEmptyUI != null) frontEmptyUI.SetActive(false);
        
        if (frontNameText != null) frontNameText.text = string.IsNullOrEmpty(data.DisplayName) ? data.ID : data.DisplayName;
        
        string baseDesc = string.IsNullOrEmpty(data.description) ? "Описание отсутствует." : data.description;
        
        // Автоматически открываем базовые элементы (у которых нет состава) или те, где стоит галочка isUnlocked
        bool isBaseAspect = string.IsNullOrWhiteSpace(data.ingredientsText) || data.ingredientsText.Trim() == "Нет данных";
        bool isRevealed = OccultAspectRegistry.IsRevealedForPage(data);

        string ingredients = isRevealed 
            ? (isBaseAspect ? "\n\n<b>Состав:</b>\n<color=#a2a2a2>Базовый элемент</color>" : $"\n\n<b>Состав:</b>\n{data.ingredientsText}") 
            : "\n\n<b>Состав:</b>\n<color=#888888>??? (Сначала создайте в котле)</color>";

        if (frontDescText != null) frontDescText.text = baseDesc + ingredients;
        
        if (frontAspectImage != null)
        {
            bool showImage = OccultAspectRegistry.IsImageRevealedForPage(data) && data.aspectIcon != null;
            if (showImage)
            {
                frontAspectImage.gameObject.SetActive(true);
                frontAspectImage.sprite = data.aspectIcon;
                frontAspectImage.color = Color.white;
            }
            else
            {
                frontAspectImage.gameObject.SetActive(false);
                if (OccultAspectRegistry.IsImageRevealedForPage(data) && data.aspectIcon == null)
                {
                    Debug.LogWarning($"[PhysicalPage] У аспекта '{data.DisplayName}' раскрыта картинка, но нет спрайта (Aspect Icon).");
                }
            }
        }
    }

    public void SetupFrontEmpty()
    {
        EnsureReferences();
        if (frontEmptyUI != null) frontEmptyUI.SetActive(true);
        if (frontNameText != null) frontNameText.text = "";
        if (frontDescText != null) frontDescText.text = "";
        if (frontAspectImage != null) frontAspectImage.gameObject.SetActive(false);
    }

    public void SetupBack(OccultAspect data)
    {
        EnsureReferences();
        if (backEmptyUI != null) backEmptyUI.SetActive(false);
        
        if (backNameText != null) backNameText.text = string.IsNullOrEmpty(data.DisplayName) ? data.ID : data.DisplayName;
        
        string baseDesc = string.IsNullOrEmpty(data.description) ? "Описание отсутствует." : data.description;
        
        // Автоматически открываем базовые элементы (у которых нет состава) или те, где стоит галочка isUnlocked
        bool isBaseAspect = string.IsNullOrWhiteSpace(data.ingredientsText) || data.ingredientsText.Trim() == "Нет данных";
        bool isRevealed = OccultAspectRegistry.IsRevealedForPage(data);

        string ingredients = isRevealed 
            ? (isBaseAspect ? "\n\n<b>Состав:</b>\n<color=#a2a2a2>Базовый элемент</color>" : $"\n\n<b>Состав:</b>\n{data.ingredientsText}") 
            : "\n\n<b>Состав:</b>\n<color=#888888>??? (Сначала создайте в котле)</color>";

        if (backDescText != null) backDescText.text = baseDesc + ingredients;
        
        if (backAspectImage != null)
        {
            bool showImage = OccultAspectRegistry.IsImageRevealedForPage(data) && data.aspectIcon != null;
            if (showImage)
            {
                backAspectImage.gameObject.SetActive(true);
                backAspectImage.sprite = data.aspectIcon;
                backAspectImage.color = Color.white;
            }
            else
            {
                backAspectImage.gameObject.SetActive(false);
            }
        }
    }

    public void SetupBackEmpty()
    {
        EnsureReferences();
        if (backEmptyUI != null) backEmptyUI.SetActive(true);
        if (backNameText != null) backNameText.text = "";
        if (backDescText != null) backDescText.text = "";
        if (backAspectImage != null) backAspectImage.gameObject.SetActive(false);
    }
}
