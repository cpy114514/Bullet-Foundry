using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LevelEditorSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LevelEditor.unity";

    [MenuItem("Bullet Foundry/Rebuild Level Editor Scene")]
    public static void Rebuild()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        DeleteIfExists("Level Editor Canvas");
        DeleteIfExists("Level Editor EventSystem");
        DeleteIfExists("Level Editor Controller");
        DeleteIfExists("Level Editor Runtime Root");

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        RectTransform root = canvas.GetComponent<RectTransform>();
        Image background = CreateImage("Editor Background", root, new Color(0.94f, 0.94f, 0.91f, 1f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Text title = CreateText("Title", root, "LEVEL EDITOR", 48, TextAnchor.MiddleLeft, Color.black);
        TopLeft(title.rectTransform, 28f, 18f, 470f, 64f);

        RectTransform enemyPanel = CreatePanel(root, "Enemy Panel", new Color(1f, 1f, 1f, 0.88f));
        Stretch(enemyPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 96f), new Vector2(326f, -96f));
        CreatePanelLabel(enemyPanel, "ENEMIES");
        ScrollRect enemyScroll = CreateScroll(enemyPanel, "Enemy Scroll", out RectTransform enemyContent, new Vector2(18f, 18f), new Vector2(-18f, -76f));
        GridLayoutGroup enemyGrid = enemyContent.gameObject.AddComponent<GridLayoutGroup>();
        enemyGrid.cellSize = new Vector2(126f, 54f);
        enemyGrid.spacing = new Vector2(12f, 12f);
        enemyGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        enemyGrid.constraintCount = 2;
        ContentSizeFitter enemyFit = enemyContent.gameObject.AddComponent<ContentSizeFitter>();
        enemyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        enemyScroll.horizontal = false;

        RectTransform timelinePanel = CreatePanel(root, "Timeline Panel", new Color(1f, 1f, 1f, 0.9f));
        Stretch(timelinePanel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(348f, 96f), new Vector2(-588f, -96f));
        CreatePanelLabel(timelinePanel, "TIMELINE");

        RectTransform toolbar = CreateRect("Timeline Toolbar", timelinePanel);
        TopStretch(toolbar, 18f, 68f, 18f, 42f);
        HorizontalLayoutGroup toolbarLayout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.spacing = 10f;
        toolbarLayout.childControlHeight = true;
        toolbarLayout.childControlWidth = false;
        toolbarLayout.childForceExpandHeight = true;
        toolbarLayout.childForceExpandWidth = false;

        Button undoButton = CreateButton("Undo Button", toolbar, "UNDO", 22);
        AddLayout(undoButton.gameObject, 100f, 42f);
        Button clearButton = CreateButton("Clear Button", toolbar, "CLEAR", 22);
        AddLayout(clearButton.gameObject, 100f, 42f);
        Button deleteModeButton = CreateButton("Delete Mode Button", toolbar, "DELETE: OFF", 18);
        AddLayout(deleteModeButton.gameObject, 150f, 42f);

        Text timelineHint = CreateText("Timeline Hint", timelinePanel, "DRAG ENEMY CARDS ONTO TRACKS. SELECT/DRAG MARKERS. DELETE/BACKSPACE REMOVES. CTRL + WHEEL ZOOMS.", 18, TextAnchor.MiddleLeft, Color.black);
        TopStretch(timelineHint.rectTransform, 18f, 118f, 18f, 32f);

        ScrollRect timelineScroll = CreateTimelineScrollArea(timelinePanel, out RectTransform timelineViewport, out RectTransform timelineArea, out RectTransform timelineGuideRoot, out RectTransform markerRoot);
        Stretch(timelineScroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(18f, 26f), new Vector2(-18f, -160f));

        RectTransform settingsPanel = CreatePanel(root, "Settings Panel", new Color(1f, 1f, 1f, 0.9f));
        Stretch(settingsPanel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-560f, 96f), new Vector2(-24f, -96f));
        CreatePanelLabel(settingsPanel, "LEVEL DATA");

        InputField levelIdInput = CreateInput(settingsPanel, "Level Id Input", "level-001", "Level Id", 82f);
        InputField displayNameInput = CreateInput(settingsPanel, "Display Name Input", "Custom Level", "Display Name", 154f);
        InputField startingCoinsInput = CreateInput(settingsPanel, "Starting Coins Input", "75", "Starting Coins", 226f);
        InputField durationInput = CreateInput(settingsPanel, "Timeline Duration Input", "60", "Timeline Seconds", 298f);
        InputField outputInput = CreateInput(settingsPanel, "Output File Input", "CustomLevel.json", "Output Json File", 370f);

        RectTransform actionRow = CreateRect("Action Row", settingsPanel);
        TopStretch(actionRow, 18f, 444f, 18f, 58f);
        HorizontalLayoutGroup actionLayout = actionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 10f;
        actionLayout.childControlWidth = true;
        actionLayout.childForceExpandWidth = true;
        Button saveButton = CreateButton("Save Button", actionRow, "SAVE", 20);
        Button loadButton = CreateButton("Load Button", actionRow, "LOAD", 20);
        Button testButton = CreateButton("Test Button", actionRow, "TEST", 20);

        Button backButton = CreateButton("Back Button", settingsPanel, "BACK", 22);
        Stretch(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-18f, 68f));

        Text towersLabel = CreateText("Tower Allow Label", settingsPanel, "ALLOWED TOWERS", 22, TextAnchor.MiddleLeft, Color.black);
        TopStretch(towersLabel.rectTransform, 18f, 548f, 18f, 28f);
        ScrollRect towerScroll = CreateScroll(settingsPanel, "Tower Scroll", out RectTransform towerContent, new Vector2(18f, 90f), new Vector2(-18f, -592f));
        VerticalLayoutGroup towerLayout = towerContent.gameObject.AddComponent<VerticalLayoutGroup>();
        towerLayout.spacing = 8f;
        towerLayout.childControlHeight = true;
        towerLayout.childControlWidth = true;
        towerLayout.childForceExpandHeight = false;
        towerLayout.childForceExpandWidth = true;
        ContentSizeFitter towerFit = towerContent.gameObject.AddComponent<ContentSizeFitter>();
        towerFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        towerScroll.horizontal = false;

        Text statusText = CreateText("Status Text", root, "Click an enemy, choose a lane, then click the timeline.", 22, TextAnchor.MiddleLeft, Color.black);
        Stretch(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 24f), new Vector2(-28f, 72f));

        GameObject buttonTemplate = CreateButton("Level Editor Button Template", root, "BUTTON", 20).gameObject;
        AddLayout(buttonTemplate, 150f, 48f);
        buttonTemplate.SetActive(false);
        RectTransform buttonTemplateRect = buttonTemplate.GetComponent<RectTransform>();
        BottomLeft(buttonTemplateRect, -500f, -500f, 140f, 54f);

        GameObject markerTemplate = CreateMarkerTemplate(root);
        markerTemplate.SetActive(false);

        GameObject controllerObject = new GameObject("Level Editor Controller");
        LevelEditorController controller = controllerObject.AddComponent<LevelEditorController>();
        LevelEditorTimelineClickArea clickArea = timelineArea.gameObject.GetComponent<LevelEditorTimelineClickArea>();
        clickArea.SetController(controller);

        SerializedObject serializedController = new SerializedObject(controller);
        SetObject(serializedController, "uiFont", GetDefaultFont());
        SetObject(serializedController, "levelIdInput", levelIdInput);
        SetObject(serializedController, "displayNameInput", displayNameInput);
        SetObject(serializedController, "startingCoinsInput", startingCoinsInput);
        SetObject(serializedController, "timelineDurationInput", durationInput);
        SetObject(serializedController, "outputFileInput", outputInput);
        SetObject(serializedController, "enemyListRoot", enemyContent);
        SetObject(serializedController, "towerListRoot", towerContent);
        SetObject(serializedController, "laneButtonRoot", null);
        SetObject(serializedController, "timelineArea", timelineArea);
        SetObject(serializedController, "timelineViewport", timelineViewport);
        SetObject(serializedController, "timelineScrollRect", timelineScroll);
        SetObject(serializedController, "timelineGuideRoot", timelineGuideRoot);
        SetObject(serializedController, "markerRoot", markerRoot);
        SetObject(serializedController, "statusText", statusText);
        SetObject(serializedController, "saveButton", saveButton);
        SetObject(serializedController, "loadButton", loadButton);
        SetObject(serializedController, "testButton", testButton);
        SetObject(serializedController, "backButton", backButton);
        SetObject(serializedController, "undoButton", undoButton);
        SetObject(serializedController, "clearButton", clearButton);
        SetObject(serializedController, "deleteModeButton", deleteModeButton);
        SetObject(serializedController, "buttonPrefab", buttonTemplate);
        SetObject(serializedController, "markerPrefab", markerTemplate);
        SetString(serializedController, "levelId", "level-001");
        SetString(serializedController, "displayName", "Custom Level");
        SetInt(serializedController, "startingCoins", 75);
        SetFloat(serializedController, "timelineDuration", 60f);
        SetInt(serializedController, "laneCount", 5);
        SetFloat(serializedController, "spawnX", 8.5f);
        SetFloat(serializedController, "timelinePixelsPerSecond", 90f);
        SetFloat(serializedController, "timelineLaneHeight", 96f);
        SetFloat(serializedController, "timelineHeaderHeight", 42f);
        SetString(serializedController, "outputFileName", "CustomLevel.json");
        SetString(serializedController, "playSceneName", "Levels");
        SetString(serializedController, "levelSelectSceneName", "LevelSelect");
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Level Editor Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 6f;
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("Level Editor EventSystem", typeof(EventSystem));
        Type inputSystemModuleType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        if (inputSystemModuleType != null)
        {
            eventSystem.AddComponent(inputSystemModuleType);
        }
        else
        {
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        Selection.activeGameObject = eventSystem;
    }

    private static RectTransform CreatePanel(RectTransform parent, string name, Color color)
    {
        Image image = CreateImage(name, parent, color);
        image.type = Image.Type.Sliced;
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        return image.rectTransform;
    }

    private static void CreatePanelLabel(RectTransform parent, string text)
    {
        Text label = CreateText("Label", parent, text, 26, TextAnchor.MiddleLeft, Color.black);
        TopStretch(label.rectTransform, 18f, 14f, 18f, 40f);
    }

    private static ScrollRect CreateScroll(RectTransform parent, string name, out RectTransform content, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform scrollRectTransform = CreateRect(name, parent);
        Stretch(scrollRectTransform, Vector2.zero, Vector2.one, offsetMin, offsetMax);

        Image viewportImage = CreateImage("Viewport", scrollRectTransform, new Color(1f, 1f, 1f, 0.18f));
        Stretch(viewportImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Mask mask = viewportImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        content = CreateRect("Content", viewportImage.rectTransform);
        Stretch(content, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(0.5f, 1f);

        ScrollRect scroll = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewportImage.rectTransform;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f;
        return scroll;
    }

    private static ScrollRect CreateTimelineScrollArea(
        RectTransform parent,
        out RectTransform viewport,
        out RectTransform content,
        out RectTransform guideRoot,
        out RectTransform markerRoot)
    {
        RectTransform scrollRoot = CreateRect("Timeline Scroll", parent);
        Image scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.88f, 0.88f, 0.84f, 1f);
        Outline outline = scrollRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        viewport = CreateRect("Timeline Viewport", scrollRoot);
        Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(0f, 18f), new Vector2(0f, 0f));
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.04f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Timeline Content", typeof(RectTransform), typeof(Image), typeof(LevelEditorTimelineClickArea));
        contentObject.transform.SetParent(viewport, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(5400f, 522f);
        Image contentImage = contentObject.GetComponent<Image>();
        contentImage.color = new Color(1f, 1f, 1f, 0.001f);
        contentImage.raycastTarget = true;

        guideRoot = CreateRect("Timeline Guide Root", content);
        Stretch(guideRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        markerRoot = CreateRect("Marker Root", content);
        Stretch(markerRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Scrollbar horizontalScrollbar = CreateHorizontalScrollbar(scrollRoot);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.horizontalScrollbar = horizontalScrollbar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.scrollSensitivity = 42f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return scroll;
    }

    private static Scrollbar CreateHorizontalScrollbar(RectTransform parent)
    {
        Image background = CreateImage("Timeline Horizontal Scrollbar", parent, new Color(1f, 1f, 1f, 0.85f));
        Stretch(background.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 16f));
        Scrollbar scrollbar = background.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;

        Image slidingArea = CreateImage("Sliding Area", background.rectTransform, new Color(1f, 1f, 1f, 0f));
        Stretch(slidingArea.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 3f), new Vector2(-6f, -3f));

        Image handle = CreateImage("Handle", slidingArea.rectTransform, new Color(0.22f, 0.22f, 0.22f, 1f));
        Stretch(handle.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        return scrollbar;
    }

    private static InputField CreateInput(RectTransform parent, string name, string value, string label, float top)
    {
        Text labelText = CreateText(name + " Label", parent, label.ToUpperInvariant(), 17, TextAnchor.MiddleLeft, Color.black);
        TopStretch(labelText.rectTransform, 18f, top, 18f, 24f);

        Image background = CreateImage(name, parent, Color.white);
        Outline outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);
        TopStretch(background.rectTransform, 18f, top + 26f, 18f, 44f);

        Text text = CreateText("Text", background.rectTransform, value, 20, TextAnchor.MiddleLeft, Color.black);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));

        InputField input = background.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.text = value;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private static Button CreateButton(string name, RectTransform parent, string label, int fontSize)
    {
        Image image = CreateImage(name, parent, Color.white);
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Text", image.rectTransform, label, fontSize, TextAnchor.MiddleCenter, Color.black);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private static GameObject CreateMarkerTemplate(RectTransform parent)
    {
        Image marker = CreateImage("Level Editor Marker Template", parent, new Color(0.08f, 0.08f, 0.08f, 1f));
        marker.raycastTarget = true;
        Center(marker.rectTransform, 0f, 0f, 142f, 38f);
        Outline outline = marker.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);
        Button button = marker.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        colors.highlightedColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        colors.pressedColor = new Color(0.34f, 0.34f, 0.34f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Text", marker.rectTransform, "Goblin 0s L1", 16, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
        return marker.gameObject;
    }

    private static Text CreateText(string name, RectTransform parent, string value, int fontSize, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = GetDefaultFont();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
    {
        LayoutElement element = target.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void TopStretch(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static void TopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void BottomLeft(RectTransform rect, float left, float bottom, float width, float height)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void Center(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void SetObject(SerializedObject target, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetInt(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static Font GetDefaultFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/IndieFlower-Regular.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return font;
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void DeleteIfExists(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }
    }
}
