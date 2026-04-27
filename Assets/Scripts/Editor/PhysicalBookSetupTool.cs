#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PhysicalBookSetupTool : EditorWindow
{
    [MenuItem("Grimoire/🛠 Create PHYSICAL Recipe Book (3D)")]
    public static void CreatePhysicalBook()
    {
        // 1. Создаем корень физической книги
        GameObject bookRoot = new GameObject("Physical_RecipeBook");
        PhysicalRecipeBook bookScript = bookRoot.AddComponent<PhysicalRecipeBook>();

        // 2. Создаем визуальную 3D обложку (опционально, используем кубы)
        GameObject bookCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bookCover.name = "BookCover_Base";
        bookCover.transform.SetParent(bookRoot.transform);
        bookCover.transform.localPosition = new Vector3(0.5f, -0.05f, 0); // смещение чтобы быть обложкой под страницами
        bookCover.transform.localScale = new Vector3(1.1f, 0.05f, 1.5f);
        DestroyImmediate(bookCover.GetComponent<BoxCollider>());
        SetBrownMaterial(bookCover);

        // 3. Создаем точку вращения корешка (Spine Pivot)
        GameObject spinePivot = new GameObject("SpinePivot");
        spinePivot.transform.SetParent(bookRoot.transform);
        spinePivot.transform.localPosition = new Vector3(0, 0, 0); // Левый край страницы
        bookScript.spinePivot = spinePivot.transform;

        // 4. Генерируем префаб физической страницы
        GameObject pagePrefab = CreatePagePrefab();
        bookScript.pagePrefab = pagePrefab;

        // 5. Создаем 3D кнопки перелистывания (Вместо UI)
        Create3DButton(bookRoot.transform, "PrevPageButton", new Vector3(-0.5f, 0, 0.8f), bookScript, false);
        Create3DButton(bookRoot.transform, "NextPageButton", new Vector3(1.5f, 0, 0.8f), bookScript, true);

        Undo.RegisterCreatedObjectUndo(bookRoot, "Create Physical Book");
        Selection.activeGameObject = bookRoot;

        Debug.Log("<b>[Grimoire]</b> Физическая 3D Книга успешно создана! Она будет лежать на нулевых координатах. " +
                  "Аспекты отображаются физически на страницах при запуске.");
    }

    private static void SetBrownMaterial(GameObject obj)
    {
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material mat = CreateHDRPCompatibleMaterial(new Color(0.25f, 0.15f, 0.08f));
            renderer.sharedMaterial = mat;
        }
    }

    private static Material CreateHDRPCompatibleMaterial(Color color)
    {
        // В HDRP "Standard" сломан. Лучше использовать Unlit или UI/Default для гарантированного цвета
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null) unlitShader = Shader.Find("UI/Default");
        
        Material mat = new Material(unlitShader);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        
        return mat;
    }

    private static GameObject CreatePagePrefab()
    {
        // Базовый объект страницы, который будет вращаться
        GameObject pageSpine = new GameObject("Page_Prefab");
        pageSpine.SetActive(false); // Делаем его префабом-болванкой
        
        PhysicalPage pageScript = pageSpine.AddComponent<PhysicalPage>();

        // Сама физическая плоскость страницы (сдвигаем её вправо от корешка)
        // Создаем ДВЕ стороны (Front и Back), так как одинарный Quad сзади невидим
        GameObject pageMeshFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pageMeshFront.name = "PaperQuad_Front";
        pageMeshFront.transform.SetParent(pageSpine.transform);
        pageMeshFront.transform.localPosition = new Vector3(0.5f, 0, 0); 
        pageMeshFront.transform.localRotation = Quaternion.Euler(90, 0, 0); 
        pageMeshFront.transform.localScale = new Vector3(1, 1.4f, 1);
        DestroyImmediate(pageMeshFront.GetComponent<MeshCollider>()); 

        GameObject pageMeshBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pageMeshBack.name = "PaperQuad_Back";
        pageMeshBack.transform.SetParent(pageSpine.transform);
        pageMeshBack.transform.localPosition = new Vector3(0.5f, -0.001f, 0); 
        pageMeshBack.transform.localRotation = Quaternion.Euler(270, 0, 0); // Лицом вниз
        pageMeshBack.transform.localScale = new Vector3(1, 1.4f, 1);
        DestroyImmediate(pageMeshBack.GetComponent<MeshCollider>()); 

        Material paperMat = CreateHDRPCompatibleMaterial(new Color(0.95f, 0.90f, 0.82f));
        pageMeshFront.GetComponent<MeshRenderer>().sharedMaterial = paperMat;
        pageMeshBack.GetComponent<MeshRenderer>().sharedMaterial = paperMat;

        // Создаем World Space Canvas для текста ПЕРЕДНЕЙ стороны
        Canvas frontCanvas = CreateWorldCanvas(pageSpine.transform, "FrontCanvas", new Vector3(0.5f, 0.002f, 0), Quaternion.Euler(90, 0, 0));
        SetupCanvasUI(frontCanvas.transform, out pageScript.frontNameText, out pageScript.frontDescText, out pageScript.frontAspectImage, out pageScript.frontEmptyUI);

        // Создаем World Space Canvas для ЗАДНЕЙ стороны (когда страница перевернута)
        Canvas backCanvas = CreateWorldCanvas(pageSpine.transform, "BackCanvas", new Vector3(0.5f, -0.002f, 0), Quaternion.Euler(90, 180, 0)); // Разворачиваем на 180 градусов
        SetupCanvasUI(backCanvas.transform, out pageScript.backNameText, out pageScript.backDescText, out pageScript.backAspectImage, out pageScript.backEmptyUI);

        return pageSpine;
    }

    private static Canvas CreateWorldCanvas(Transform parent, string name, Vector3 localPos, Quaternion localRot)
    {
        GameObject canvasObj = new GameObject(name);
        canvasObj.transform.SetParent(parent);
        canvasObj.transform.localPosition = localPos;
        canvasObj.transform.localRotation = localRot;
        canvasObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f); // Уменьшаем под 3D масштаб

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector3(900, 1300); // Разрешение холста страницы

        return canvas;
    }

    private static void SetupCanvasUI(Transform canvasRoot, out TextMeshProUGUI nameTxt, out TextMeshProUGUI descTxt, out Image img, out GameObject emptyUI)
    {
        // 1. Имя
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(canvasRoot, false);
        nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
        SetupRect(nameTxt.rectTransform, new Vector2(0.5f, 0.85f), new Vector2(800, 150));
        nameTxt.text = "Aspect Name";
        nameTxt.fontSize = 90;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.black;

        // 2. Картинка
        GameObject imgObj = new GameObject("Image");
        imgObj.transform.SetParent(canvasRoot, false);
        img = imgObj.AddComponent<Image>();
        SetupRect(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 400));

        // 3. Описание
        GameObject descObj = new GameObject("DescText");
        descObj.transform.SetParent(canvasRoot, false);
        descTxt = descObj.AddComponent<TextMeshProUGUI>();
        SetupRect(descTxt.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(800, 400));
        descTxt.text = "Description goes here...";
        descTxt.fontSize = 45;
        descTxt.alignment = TextAlignmentOptions.TopLeft;
        descTxt.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // 4. Пустой UI (если на странице нет аспекта)
        emptyUI = new GameObject("EmptyState");
        emptyUI.transform.SetParent(canvasRoot, false);
        SetupRect(emptyUI.AddComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(800, 200));
        TextMeshProUGUI emptyTxt = emptyUI.AddComponent<TextMeshProUGUI>();
        emptyTxt.text = "Пустая страница...";
        emptyTxt.fontSize = 60;
        emptyTxt.alignment = TextAlignmentOptions.Center;
        emptyTxt.color = new Color(0, 0, 0, 0.3f);
        emptyUI.SetActive(false);
    }

    private static void SetupRect(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    private static void Create3DButton(Transform parent, string name, Vector3 localPos, PhysicalRecipeBook bookScript, bool isNext)
    {
        GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btnObj.name = name;
        btnObj.transform.SetParent(parent);
        btnObj.transform.localPosition = localPos;
        btnObj.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
        
        Material mat = CreateHDRPCompatibleMaterial(isNext ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.6f, 0.2f, 0.2f));
        btnObj.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Скрипт-интерактор для клика мышкой (если игра от первого лица, придется вызывать его через Raycast)
        PhysicalBookButton interaction = btnObj.AddComponent<PhysicalBookButton>();
        interaction.bookScript = bookScript;
        interaction.isNextPage = isNext;
    }
}
#endif
