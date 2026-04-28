#if UNITY_EDITOR
using GrimoireOfTheVoid.Crafting;
using GrimoireOfTheVoid.Game;
using GrimoireOfTheVoid.UI;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editor tools to quickly drop Goal UI + GameDirector + delivery trigger into the current scene.
/// </summary>
public static class GameGoalSetupTool
{
    [MenuItem("Grimoire/🛠 Create Game Goal HUD (Auto-Setup)")]
    public static void CreateGoalHud()
    {
        // Avoid Unity inspector holding onto a dead selection target (can throw GameObjectInspector m_Targets MissingReferenceException).
        Selection.activeObject = null;
        InternalEditorUtility.RepaintAllViews();

        Canvas canvas = FindOrCreateCanvas();
        FindOrCreateEventSystem();

        // Root
        GameObject hudRoot = new GameObject("GameGoalHUD");
        Undo.RegisterCreatedObjectUndo(hudRoot, "Create GameGoalHUD");
        hudRoot.transform.SetParent(canvas.transform, false);

        var hud = hudRoot.AddComponent<GoalHUD>();

        // Goal block (top-left)
        GameObject goalBlock = new GameObject("GoalBlock");
        Undo.RegisterCreatedObjectUndo(goalBlock, "Create GoalBlock");
        goalBlock.transform.SetParent(hudRoot.transform, false);
        RectTransform goalBlockRt = goalBlock.AddComponent<RectTransform>();
        goalBlockRt.anchorMin = new Vector2(0f, 1f);
        goalBlockRt.anchorMax = new Vector2(0f, 1f);
        goalBlockRt.pivot = new Vector2(0f, 1f);
        goalBlockRt.anchoredPosition = new Vector2(24f, -24f);
        goalBlockRt.sizeDelta = new Vector2(520f, 120f);

        TextMeshProUGUI goalLabel = CreateTmpText(goalBlock.transform, "GoalLabel", "Цель:", 36, TextAlignmentOptions.Left);
        SetRect(goalLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(200f, 48f), new Vector2(0f, 1f));

        TextMeshProUGUI goalName = CreateTmpText(goalBlock.transform, "GoalName", "—", 40, TextAlignmentOptions.Left);
        SetRect(goalName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -52f), new Vector2(420f, 52f), new Vector2(0f, 1f));

        Image goalIcon = CreateImage(goalBlock.transform, "GoalIcon");
        RectTransform goalIconRt = goalIcon.rectTransform;
        goalIconRt.anchorMin = new Vector2(1f, 1f);
        goalIconRt.anchorMax = new Vector2(1f, 1f);
        goalIconRt.pivot = new Vector2(1f, 1f);
        goalIconRt.anchoredPosition = new Vector2(0f, 0f);
        goalIconRt.sizeDelta = new Vector2(96f, 96f);
        goalIcon.gameObject.SetActive(false);

        // Timer block (top-center)
        GameObject timerBlock = new GameObject("TimerBlock");
        Undo.RegisterCreatedObjectUndo(timerBlock, "Create TimerBlock");
        timerBlock.transform.SetParent(hudRoot.transform, false);
        RectTransform timerBlockRt = timerBlock.AddComponent<RectTransform>();
        timerBlockRt.anchorMin = new Vector2(0.5f, 1f);
        timerBlockRt.anchorMax = new Vector2(0.5f, 1f);
        timerBlockRt.pivot = new Vector2(0.5f, 1f);
        timerBlockRt.anchoredPosition = new Vector2(0f, -24f);
        timerBlockRt.sizeDelta = new Vector2(520f, 96f);

        Image timerBg = CreateImage(timerBlock.transform, "TimerBg");
        timerBg.color = new Color(0f, 0f, 0f, 0.45f);
        timerBg.rectTransform.anchorMin = Vector2.zero;
        timerBg.rectTransform.anchorMax = Vector2.one;
        timerBg.rectTransform.offsetMin = Vector2.zero;
        timerBg.rectTransform.offsetMax = Vector2.zero;

        Image timerFill = CreateImage(timerBlock.transform, "TimerFill");
        timerFill.color = new Color(0.9f, 0.82f, 0.6f, 0.75f);
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        timerFill.fillAmount = 1f;
        timerFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        timerFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        timerFill.rectTransform.offsetMin = new Vector2(8f, 8f);
        timerFill.rectTransform.offsetMax = new Vector2(-8f, -8f);

        TextMeshProUGUI timerText = CreateTmpText(timerBlock.transform, "TimerText", "0:00", 44, TextAlignmentOptions.Center);
        timerText.color = Color.white;
        timerText.rectTransform.anchorMin = Vector2.zero;
        timerText.rectTransform.anchorMax = Vector2.one;
        timerText.rectTransform.offsetMin = Vector2.zero;
        timerText.rectTransform.offsetMax = Vector2.zero;

        // End panels
        GameObject gameOverPanel = CreateEndPanel(hudRoot.transform, "GameOverPanel", "GAME OVER");
        GameObject victoryPanel = CreateEndPanel(hudRoot.transform, "VictoryPanel", "VICTORY");
        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);

        Button restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Restart", new Vector2(0.5f, 0.35f));
        Button quitButton = CreateButton(gameOverPanel.transform, "QuitButton", "Quit", new Vector2(0.5f, 0.2f));
        Button victoryRestartButton = CreateButton(victoryPanel.transform, "VictoryRestartButton", "Restart", new Vector2(0.5f, 0.25f));

        // Wire up private serialized fields via SerializedObject.
        // IMPORTANT: Do this in delayCall to avoid Unity Inspector grabbing half-created objects (can throw SerializedObjectNotCreatableException).
        EditorApplication.delayCall += () =>
        {
            if (hud == null) return;
            if (goalLabel == null || goalName == null || goalIcon == null || timerText == null || timerFill == null) return;

            var so = new SerializedObject(hud);
            so.FindProperty("goalLabelText").objectReferenceValue = goalLabel;
            so.FindProperty("goalNameText").objectReferenceValue = goalName;
            so.FindProperty("goalIcon").objectReferenceValue = goalIcon;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("timerFill").objectReferenceValue = timerFill;
            so.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            so.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("victoryRestartButton").objectReferenceValue = victoryRestartButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Do not force-select newly created objects: Selection changes are a common trigger for Inspector null-target bugs.
            InternalEditorUtility.RepaintAllViews();
            Debug.Log("<b>[Grimoire]</b> Game Goal HUD создан. Добавь/создай GameDirector, чтобы HUD начал обновляться.");
        };
    }

    [MenuItem("Grimoire/🛠 Create GameDirector (Game Goal)")]
    public static void CreateGameDirector()
    {
        Selection.activeObject = null;
        InternalEditorUtility.RepaintAllViews();

        GameDirector existing = Object.FindFirstObjectByType<GameDirector>();
        if (existing != null)
        {
            // Don't auto-select; see note above.
            Debug.Log("<b>[Grimoire]</b> GameDirector уже есть в сцене.");
            return;
        }

        GameObject go = new GameObject("GameDirector");
        Undo.RegisterCreatedObjectUndo(go, "Create GameDirector");
        go.AddComponent<GameDirector>();
        InternalEditorUtility.RepaintAllViews();
        Debug.Log("<b>[Grimoire]</b> GameDirector создан.");
    }

    [MenuItem("Grimoire/🛠 Create Goal Delivery Spot (Trigger)")]
    public static void CreateGoalDeliverySpot()
    {
        Selection.activeObject = null;
        InternalEditorUtility.RepaintAllViews();

        // Zone host
        GameObject zoneGo = new GameObject("GoalDeliveryZone");
        Undo.RegisterCreatedObjectUndo(zoneGo, "Create GoalDeliveryZone");
        zoneGo.AddComponent<GoalDeliveryZone>();

        // Trigger child
        GameObject triggerGo = new GameObject("GoalDeliveryTrigger");
        Undo.RegisterCreatedObjectUndo(triggerGo, "Create GoalDeliveryTrigger");
        triggerGo.transform.SetParent(zoneGo.transform, false);

        BoxCollider col = triggerGo.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.35f, 0.2f, 0.35f);

        GoalDeliveryZoneTrigger trig = triggerGo.AddComponent<GoalDeliveryZoneTrigger>();
        EditorApplication.delayCall += () =>
        {
            if (trig == null) return;
            var so = new SerializedObject(trig);
            so.FindProperty("zone").objectReferenceValue = zoneGo.GetComponent<GoalDeliveryZone>();
            so.FindProperty("thisTriggerCollider").objectReferenceValue = col;
            so.ApplyModifiedPropertiesWithoutUndo();

            InternalEditorUtility.RepaintAllViews();
            Debug.Log("<b>[Grimoire]</b> GoalDeliveryZone + trigger созданы. Перемести их в нужное место на столе.");
        };
    }

    [MenuItem("Grimoire/🛠 Create Aspect сдача (Goal Submit Field)")]
    public static void CreateAspectSubmitField()
    {
        // Alias for clarity in your workflow.
        CreateGoalDeliverySpot();
    }

    [MenuItem("Grimoire/🛠 Setup Crafting Table Physics (Selected)")]
    public static void SetupCraftingTablePhysicsOnSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("<b>[Grimoire]</b> Ничего не выделено. Выдели объект(ы) стола/коллайдеров и повтори.");
            return;
        }

        int added = 0;
        int touchedColliders = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            GameObject go = selected[i];
            if (go == null) continue;

            // Поддержка кейса: выделили родителя, а коллайдеры в детях.
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            if (cols == null || cols.Length == 0)
            {
                // Если коллайдеров нет — всё равно можем повесить на сам объект, если так удобнее.
                CraftingTableSurface existingOnGo = go.GetComponent<CraftingTableSurface>();
                if (existingOnGo == null)
                {
                    CraftingTableSurface s = Undo.AddComponent<CraftingTableSurface>(go);
                    if (s != null) added++;
                }
                continue;
            }

            for (int c = 0; c < cols.Length; c++)
            {
                if (cols[c] == null) continue;
                touchedColliders++;
                GameObject host = cols[c].gameObject;
                if (host.GetComponent<CraftingTableSurface>() != null) continue;
                CraftingTableSurface s = Undo.AddComponent<CraftingTableSurface>(host);
                if (s != null) added++;
            }
        }

        Debug.Log($"<b>[Grimoire]</b> CraftingTableSurface добавлен(о): {added}. Коллайдеров обработано: {touchedColliders}.");
    }

    [MenuItem("Grimoire/🛠 Setup Cauldron Drop Zone (Selected Collider)")]
    public static void SetupCauldronDropZoneOnSelectedCollider()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("<b>[Grimoire]</b> Выдели GameObject с Collider (зона приёма котла) и повтори.");
            return;
        }

        // Поддержка кейса: выделили родителя, но коллайдер на дочернем.
        Collider c = go.GetComponent<Collider>();
        if (c == null)
        {
            c = go.GetComponentInChildren<Collider>(true);
            if (c == null)
            {
                Debug.LogWarning("<b>[Grimoire]</b> На выделенном объекте и в детях нет Collider. Выдели объект с Collider и повтори.");
                return;
            }
            go = c.gameObject;
        }

        CauldronDropZone dz = go.GetComponent<CauldronDropZone>();
        if (dz == null)
        {
            dz = Undo.AddComponent<CauldronDropZone>(go);
        }

        CauldronController pot = go.GetComponentInParent<CauldronController>();
        if (pot == null)
        {
            pot = Object.FindFirstObjectByType<CauldronController>();
        }

        if (pot != null)
        {
            var so = new SerializedObject(dz);
            so.FindProperty("cauldronOverride").objectReferenceValue = pot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("<b>[Grimoire]</b> CauldronDropZone добавлен на выделенный Collider. Теперь дроп в котёл стабилен.");
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null) return canvas;

        GameObject canvasObj = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void FindOrCreateEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es != null) return;

        GameObject esObj = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        esObj.AddComponent<EventSystem>();
        esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static TextMeshProUGUI CreateTmpText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create TMP Text");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        return tmp;
    }

    private static Image CreateImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Image");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        return img;
    }

    private static GameObject CreateEndPanel(Transform parent, string name, string title)
    {
        GameObject panel = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(panel, "Create End Panel");
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        TextMeshProUGUI titleTmp = CreateTmpText(panel.transform, "Title", title, 72, TextAlignmentOptions.Center);
        titleTmp.color = Color.white;
        titleTmp.rectTransform.anchorMin = new Vector2(0.5f, 0.7f);
        titleTmp.rectTransform.anchorMax = new Vector2(0.5f, 0.7f);
        titleTmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        titleTmp.rectTransform.sizeDelta = new Vector2(900f, 140f);
        titleTmp.rectTransform.anchoredPosition = Vector2.zero;

        return panel;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
    {
        GameObject btnObj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(btnObj, "Create Button");
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(260f, 70f);
        rt.anchoredPosition = Vector2.zero;

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.25f, 0.22f, 0.18f, 1f);

        Button btn = btnObj.AddComponent<Button>();

        TextMeshProUGUI tmp = CreateTmpText(btnObj.transform, "Text", label, 34, TextAlignmentOptions.Center);
        tmp.color = Color.white;
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = Vector2.zero;
        tmp.rectTransform.offsetMax = Vector2.zero;

        return btn;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }
}
#endif

