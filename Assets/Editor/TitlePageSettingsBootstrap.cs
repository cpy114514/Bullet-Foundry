using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitlePageSettingsBootstrap
{
    private const string ScenePath = "Assets/Scenes/TitlePage.unity";
    private const string RootName = "Title Page Systems";
    private const string LevelSelectSceneName = "LevelSelect";
    private const string SettingsCanvasName = "Settings UI Canvas";
    private const string SettingsPanelName = "Settings Panel";

    static TitlePageSettingsBootstrap()
    {
        // Intentionally no automatic scene mutation here.
        // The title/settings setup can still be run manually from the Tools menu.
    }

    [MenuItem("Tools/Bullet Foundry/Setup Title Page Settings")]
    public static void EnsureTitlePageSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        bool changed = false;
        Transform root = scene.GetRootGameObjects()
            .Select(gameObject => gameObject.transform)
            .FirstOrDefault(transform =>
                string.Equals(transform.name, RootName, StringComparison.Ordinal));

        if (root == null)
        {
            GameObject rootObject = new(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            root = rootObject.transform;
            changed = true;
        }

        TitlePageController titleController = FindInScene<TitlePageController>(scene);
        if (titleController == null)
        {
            titleController = root.gameObject.AddComponent<TitlePageController>();
            changed = true;
        }

        SettingsMenuController settingsMenu = FindInScene<SettingsMenuController>(scene);
        if (settingsMenu == null)
        {
            settingsMenu = root.gameObject.AddComponent<SettingsMenuController>();
            changed = true;
        }

        SimpleClickEffect clickEffect = FindInScene<SimpleClickEffect>(scene);
        if (clickEffect == null)
        {
            root.gameObject.AddComponent<SimpleClickEffect>();
            changed = true;
        }

        SerializedObject serializedTitle = new(titleController);
        SerializedProperty gameSceneNameProperty = serializedTitle.FindProperty("gameSceneName");
        if (gameSceneNameProperty != null &&
            gameSceneNameProperty.stringValue != LevelSelectSceneName)
        {
            gameSceneNameProperty.stringValue = LevelSelectSceneName;
            changed = true;
        }

        SerializedProperty settingsMenuProperty = serializedTitle.FindProperty("settingsMenu");
        if (settingsMenuProperty != null &&
            settingsMenuProperty.objectReferenceValue != settingsMenu)
        {
            settingsMenuProperty.objectReferenceValue = settingsMenu;
            changed = true;
        }

        if (changed)
        {
            serializedTitle.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(titleController);
        }

        if (EnsureSettingsMenuDefaults(settingsMenu))
        {
            changed = true;
        }

        if (WireExistingSettingsPanel(scene, settingsMenu))
        {
            changed = true;
        }

        if (EnsureSceneSettingsPanel(scene, settingsMenu))
        {
            changed = true;
        }

        if (WireTitleButtonsByPosition(scene, titleController))
        {
            changed = true;
        }

        if (TryWireExistingSettingsButton(scene, titleController))
        {
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool EnsureSettingsMenuDefaults(SettingsMenuController settingsMenu)
    {
        if (settingsMenu == null)
        {
            return false;
        }

        SerializedObject serializedSettings = new(settingsMenu);
        bool changed = false;
        bool hasScenePanel = serializedSettings.FindProperty("settingsPanel")?.objectReferenceValue != null;
        changed |= SetBool(serializedSettings.FindProperty("buildDefaultUiIfMissing"), !hasScenePanel);
        changed |= SetBool(serializedSettings.FindProperty("startClosed"), true);
        changed |= SetFloat(serializedSettings.FindProperty("fontScale"), 1.75f);

        if (changed)
        {
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsMenu);
        }

        return changed;
    }

    private static bool EnsureSceneSettingsPanel(
        Scene scene,
        SettingsMenuController settingsMenu)
    {
        if (settingsMenu == null)
        {
            return false;
        }

        SerializedObject serializedSettings = new(settingsMenu);
        SerializedProperty panelProperty = serializedSettings.FindProperty("settingsPanel");
        if (panelProperty == null || panelProperty.objectReferenceValue != null)
        {
            return false;
        }

        Sprite panelSprite = LoadSprite("Assets/Image/UI.png", "UI_1");
        Sprite buttonSprite = LoadSprite("Assets/Image/UI2.png", "UI2_8");
        Sprite circleSprite = LoadSprite("Assets/Image/UI2.png", "UI2_4");

        GameObject canvasObject = new(SettingsCanvasName);
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlay = CreateRectObject("Settings Overlay", canvasObject.transform);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.62f);
        overlayImage.raycastTarget = false;

        GameObject panelShadow = CreateRectObject("Settings Panel Shadow", canvasObject.transform);
        SetRect(panelShadow.GetComponent<RectTransform>(), new Vector2(12f, -12f), new Vector2(840f, 720f));
        Image shadowImage = panelShadow.AddComponent<Image>();
        shadowImage.sprite = panelSprite;
        shadowImage.color = new Color(0f, 0f, 0f, 0.52f);
        shadowImage.raycastTarget = false;

        GameObject panel = CreateRectObject(SettingsPanelName, canvasObject.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(840f, 720f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = new Color(0.94f, 0.94f, 0.94f, 1f);

        CreateText(panel.transform, "Settings Title", "SETTINGS", 60, TextAnchor.MiddleCenter,
            new Vector2(0f, 278f), new Vector2(560f, 74f));
        CreateText(panel.transform, "Settings Hint", "Video, audio, and input options", 24, TextAnchor.MiddleCenter,
            new Vector2(0f, 234f), new Vector2(560f, 38f));
        CreateSectionTitle(panel.transform, "VIDEO", new Vector2(-302f, 175f));

        Dropdown resolutionDropdown = CreateDropdownRow(
            panel.transform, "RESOLUTION", new Vector2(0f, 132f), buttonSprite);
        Toggle fullscreenToggle = CreateToggleRow(
            panel.transform, "FULLSCREEN", new Vector2(0f, 74f), circleSprite);
        CreateSectionTitle(panel.transform, "AUDIO", new Vector2(-302f, 28f));
        CreateToggleSliderRow(
            panel.transform,
            "VOLUME",
            new Vector2(0f, -16f),
            circleSprite,
            out Toggle masterVolumeToggle,
            out Slider masterVolumeSlider,
            out Text masterVolumeValueText);
        CreateToggleSliderRow(
            panel.transform,
            "MUSIC",
            new Vector2(0f, -82f),
            circleSprite,
            out Toggle musicToggle,
            out Slider musicVolumeSlider,
            out Text musicVolumeValueText);
        CreateToggleSliderRow(
            panel.transform,
            "SOUND EFFECTS",
            new Vector2(0f, -148f),
            circleSprite,
            out Toggle soundEffectsToggle,
            out Slider soundEffectsVolumeSlider,
            out Text soundEffectsVolumeValueText);
        CreateSectionTitle(panel.transform, "INPUT", new Vector2(-302f, -212f));
        Toggle clickEffectToggle = CreateToggleRow(
            panel.transform, "CLICK EFFECT", new Vector2(0f, -256f), circleSprite);

        Button closeButton = CreateButton(panel.transform, "Close Settings Button", "BACK",
            new Vector2(302f, 278f), new Vector2(150f, 50f), buttonSprite);

        SetObject(serializedSettings.FindProperty("settingsPanel"), panel);
        SetObject(serializedSettings.FindProperty("closeSettingsButton"), closeButton);
        SetObject(serializedSettings.FindProperty("resolutionDropdown"), resolutionDropdown);
        SetObject(serializedSettings.FindProperty("fullscreenToggle"), fullscreenToggle);
        SetObject(serializedSettings.FindProperty("masterVolumeToggle"), masterVolumeToggle);
        SetObject(serializedSettings.FindProperty("masterVolumeSlider"), masterVolumeSlider);
        SetObject(serializedSettings.FindProperty("masterVolumeValueText"), masterVolumeValueText);
        SetObject(serializedSettings.FindProperty("musicToggle"), musicToggle);
        SetObject(serializedSettings.FindProperty("musicVolumeSlider"), musicVolumeSlider);
        SetObject(serializedSettings.FindProperty("musicVolumeValueText"), musicVolumeValueText);
        SetObject(serializedSettings.FindProperty("soundEffectsToggle"), soundEffectsToggle);
        SetObject(serializedSettings.FindProperty("soundEffectsVolumeSlider"), soundEffectsVolumeSlider);
        SetObject(serializedSettings.FindProperty("soundEffectsVolumeValueText"), soundEffectsVolumeValueText);
        SetObject(serializedSettings.FindProperty("clickEffectToggle"), clickEffectToggle);
        SetBool(serializedSettings.FindProperty("buildDefaultUiIfMissing"), false);
        SetBool(serializedSettings.FindProperty("startClosed"), true);
        SetFloat(serializedSettings.FindProperty("fontScale"), 1.75f);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(settingsMenu);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(canvasObject);
        return true;
    }

    private static bool WireExistingSettingsPanel(
        Scene scene,
        SettingsMenuController settingsMenu)
    {
        if (settingsMenu == null)
        {
            return false;
        }

        Transform panel = FindSceneTransform(scene, SettingsPanelName);
        if (panel == null)
        {
            return false;
        }

        SerializedObject serializedSettings = new(settingsMenu);
        bool changed = false;

        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("settingsPanel"),
            panel.gameObject);
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("closeSettingsButton"),
            FindChildComponent<Button>(panel, "Close Settings Button"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("resolutionDropdown"),
            FindChildComponent<Dropdown>(panel, "RESOLUTION Dropdown"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("fullscreenToggle"),
            FindChildComponent<Toggle>(panel, "FULLSCREEN Toggle"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("masterVolumeToggle"),
            FindChildComponent<Toggle>(panel, "VOLUME Toggle", "MASTER VOLUME Toggle"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("masterVolumeSlider"),
            FindChildComponent<Slider>(panel, "VOLUME Slider", "MASTER VOLUME Slider"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("masterVolumeValueText"),
            FindChildComponent<Text>(panel, "VOLUME Value", "MASTER VOLUME Value"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("musicToggle"),
            FindChildComponent<Toggle>(panel, "MUSIC Toggle"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("musicVolumeSlider"),
            FindChildComponent<Slider>(panel, "MUSIC Slider", "MUSIC VOLUME Slider"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("musicVolumeValueText"),
            FindChildComponent<Text>(panel, "MUSIC Value", "MUSIC VOLUME Value"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("soundEffectsToggle"),
            FindChildComponent<Toggle>(panel, "SOUND EFFECTS Toggle"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("soundEffectsVolumeSlider"),
            FindChildComponent<Slider>(panel, "SOUND EFFECTS Slider", "SFX VOLUME Slider"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("soundEffectsVolumeValueText"),
            FindChildComponent<Text>(panel, "SOUND EFFECTS Value", "SFX VOLUME Value"));
        changed |= SetObjectIfDifferent(
            serializedSettings.FindProperty("clickEffectToggle"),
            FindChildComponent<Toggle>(panel, "CLICK EFFECT Toggle"));
        changed |= SetBool(serializedSettings.FindProperty("buildDefaultUiIfMissing"), false);

        if (changed)
        {
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsMenu);
        }

        return changed;
    }

    private static Dropdown CreateDropdownRow(
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Sprite buttonSprite)
    {
        GameObject row = CreateSettingsRow(parent, label, anchoredPosition);
        Dropdown dropdown = CreateDropdown(row.transform, $"{label} Dropdown", buttonSprite);
        SetRect(dropdown.GetComponent<RectTransform>(), new Vector2(165f, 0f), new Vector2(330f, 48f));
        return dropdown;
    }

    private static Toggle CreateToggleRow(
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Sprite circleSprite)
    {
        GameObject row = CreateSettingsRow(parent, label, anchoredPosition);
        Toggle toggle = CreateToggle(row.transform, $"{label} Toggle", circleSprite);
        SetRect(toggle.GetComponent<RectTransform>(), new Vector2(130f, 0f), new Vector2(70f, 48f));
        return toggle;
    }

    private static Slider CreateSliderRow(
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Sprite handleSprite,
        out Text valueText)
    {
        GameObject row = CreateSettingsRow(parent, label, anchoredPosition);
        Slider slider = CreateSlider(row.transform, $"{label} Slider", handleSprite);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(130f, 0f), new Vector2(260f, 48f));
        valueText = CreateText(row.transform, $"{label} Value", "100%", 24, TextAnchor.MiddleRight,
            new Vector2(318f, 0f), new Vector2(84f, 42f));
        return slider;
    }

    private static void CreateToggleSliderRow(
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Sprite handleSprite,
        out Toggle toggle,
        out Slider slider,
        out Text valueText)
    {
        GameObject row = CreateSettingsRow(parent, label, anchoredPosition);
        toggle = CreateToggle(row.transform, $"{label} Toggle", handleSprite);
        SetRect(toggle.GetComponent<RectTransform>(), new Vector2(34f, 0f), new Vector2(70f, 48f));
        slider = CreateSlider(row.transform, $"{label} Slider", handleSprite);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(185f, 0f), new Vector2(230f, 48f));
        valueText = CreateText(row.transform, $"{label} Value", "100%", 24, TextAnchor.MiddleRight,
            new Vector2(326f, 0f), new Vector2(84f, 42f));
    }

    private static GameObject CreateSettingsRow(
        Transform parent,
        string label,
        Vector2 anchoredPosition)
    {
        GameObject row = CreateRectObject($"{label} Row", parent);
        SetRect(row.GetComponent<RectTransform>(), anchoredPosition, new Vector2(690f, 50f));
        Image rowImage = row.AddComponent<Image>();
        rowImage.color = new Color(0.86f, 0.86f, 0.86f, 0.46f);
        rowImage.raycastTarget = false;
        CreateText(row.transform, $"{label} Label", label, 27, TextAnchor.MiddleLeft,
            new Vector2(-188f, 0f), new Vector2(310f, 42f));
        return row;
    }

    private static void CreateSectionTitle(Transform parent, string title, Vector2 anchoredPosition)
    {
        Text sectionTitle = CreateText(parent, $"{title} Section", title, 22, TextAnchor.MiddleLeft,
            anchoredPosition, new Vector2(160f, 28f));
        sectionTitle.color = new Color(0.18f, 0.18f, 0.18f, 1f);
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Sprite buttonSprite)
    {
        GameObject buttonObject = CreateRectObject(objectName, parent);
        SetRect(buttonObject.GetComponent<RectTransform>(), anchoredPosition, size);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text buttonText = CreateText(buttonObject.transform, "Text", label, 34, TextAnchor.MiddleCenter,
            Vector2.zero, size - new Vector2(18f, 8f));
        buttonText.resizeTextForBestFit = true;
        buttonText.resizeTextMinSize = 18;
        buttonText.resizeTextMaxSize = 40;
        buttonText.raycastTarget = false;
        buttonText.transform.SetAsLastSibling();
        return button;
    }

    private static Dropdown CreateDropdown(
        Transform parent,
        string objectName,
        Sprite buttonSprite)
    {
        GameObject dropdownObject = CreateRectObject(objectName, parent);
        Image image = dropdownObject.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = image;
        Text caption = CreateText(dropdownObject.transform, "Label", "", 24, TextAnchor.MiddleLeft,
            new Vector2(-8f, 0f), new Vector2(282f, 42f));
        ConfigureDropdownText(caption, 16, 24);
        Text arrow = CreateText(dropdownObject.transform, "Arrow", "v", 22, TextAnchor.MiddleCenter,
            new Vector2(135f, 0f), new Vector2(34f, 38f));
        arrow.raycastTarget = false;
        dropdown.captionText = caption;

        Text itemText = CreateDropdownTemplate(dropdownObject.transform, dropdown);
        dropdown.itemText = itemText;
        return dropdown;
    }

    private static Text CreateDropdownTemplate(Transform parent, Dropdown dropdown)
    {
        GameObject templateObject = CreateRectObject("Template", parent);
        templateObject.SetActive(false);
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -6f);
        templateRect.sizeDelta = new Vector2(0f, 230f);

        Image templateImage = templateObject.AddComponent<Image>();
        templateImage.color = new Color(0.93f, 0.93f, 0.93f, 0.98f);
        ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        dropdown.template = templateRect;

        GameObject viewportObject = CreateRectObject("Viewport", templateObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        scrollRect.viewport = viewportRect;

        GameObject contentObject = CreateRectObject("Content", viewportObject.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 48f);
        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        GameObject itemObject = CreateRectObject("Item", contentObject.transform);
        RectTransform itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0f, 48f);
        Image itemBackground = itemObject.AddComponent<Image>();
        itemBackground.color = new Color(0.86f, 0.86f, 0.86f, 1f);
        Toggle itemToggle = itemObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;

        Text itemLabel = CreateText(itemObject.transform, "Item Label", "Option", 24,
            TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(302f, 42f));
        ConfigureDropdownText(itemLabel, 16, 24);
        return itemLabel;
    }

    private static void ConfigureDropdownText(Text text, int minSize, int maxSize)
    {
        if (text == null)
        {
            return;
        }

        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private static Toggle CreateToggle(
        Transform parent,
        string objectName,
        Sprite circleSprite)
    {
        GameObject toggleObject = CreateRectObject(objectName, parent);
        Toggle toggle = toggleObject.AddComponent<Toggle>();

        GameObject backgroundObject = CreateRectObject("Background", toggleObject.transform);
        SetRect(backgroundObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(48f, 48f));
        Image background = backgroundObject.AddComponent<Image>();
        background.sprite = circleSprite;
        background.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        GameObject checkmarkObject = CreateRectObject("Checkmark", backgroundObject.transform);
        SetRect(checkmarkObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(24f, 24f));
        Image checkmark = checkmarkObject.AddComponent<Image>();
        checkmark.sprite = circleSprite;
        checkmark.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private static Slider CreateSlider(
        Transform parent,
        string objectName,
        Sprite handleSprite)
    {
        GameObject sliderObject = CreateRectObject(objectName, parent);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        RectTransform background = CreateImageRect(sliderObject.transform, "Background",
            new Color(0.12f, 0.12f, 0.12f, 1f), null);
        background.anchorMin = new Vector2(0f, 0.43f);
        background.anchorMax = new Vector2(1f, 0.57f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        RectTransform fillArea = CreateRectObject("Fill Area", sliderObject.transform)
            .GetComponent<RectTransform>();
        fillArea.anchorMin = new Vector2(0f, 0.43f);
        fillArea.anchorMax = new Vector2(1f, 0.57f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreateImageRect(fillArea, "Fill",
            new Color(0.55f, 0.55f, 0.55f, 1f), null);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        RectTransform handle = CreateImageRect(sliderObject.transform, "Handle",
            Color.white, handleSprite);
        handle.sizeDelta = new Vector2(34f, 34f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string text,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject = CreateRectObject(objectName, parent);
        SetRect(textObject.GetComponent<RectTransform>(), anchoredPosition, size);
        Text textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static RectTransform CreateImageRect(
        Transform parent,
        string objectName,
        Color color,
        Sprite sprite)
    {
        GameObject imageObject = CreateRectObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return imageObject.GetComponent<RectTransform>();
    }

    private static GameObject CreateRectObject(string objectName, Transform parent)
    {
        GameObject gameObject = new(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<RectTransform>();
        return gameObject;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == spriteName);
    }

    private static void SetObject(SerializedProperty property, UnityEngine.Object value)
    {
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static bool SetObjectIfDifferent(SerializedProperty property, UnityEngine.Object value)
    {
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static T FindChildComponent<T>(Transform root, params string[] childNames)
        where T : Component
    {
        Transform child = FindChild(root, childNames);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindSceneTransform(Scene scene, string transformName)
    {
        return scene.GetRootGameObjects()
            .Select(gameObject => FindChild(gameObject.transform, transformName))
            .FirstOrDefault(transform => transform != null);
    }

    private static Transform FindChild(Transform root, params string[] childNames)
    {
        if (root == null || childNames == null || childNames.Length == 0)
        {
            return null;
        }

        HashSet<string> names = new(childNames);
        Stack<Transform> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            if (names.Contains(current.name))
            {
                return current;
            }

            for (int i = current.childCount - 1; i >= 0; i--)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return null;
    }

    private static bool WireTitleButtonsByPosition(
        Scene scene,
        TitlePageController titleController)
    {
        SpriteRenderer[] buttonRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()
            .Where(renderer =>
                renderer != null &&
                renderer.gameObject.scene == scene &&
                renderer.transform.position.y < -2f &&
                renderer.bounds.size.x > 1f &&
                renderer.bounds.size.y > 1f)
            .OrderBy(renderer => renderer.transform.position.x)
            .ToArray();

        if (buttonRenderers.Length == 0)
        {
            return false;
        }

        bool changed = false;
        HashSet<GameObject> configuredButtons = new();
        foreach (SpriteRenderer renderer in buttonRenderers)
        {
            if (TryGetButtonActionFromName(renderer.name, out TitlePageSpriteButton.ButtonAction action) ||
                TryGetButtonActionFromSprite(renderer.sprite, out action))
            {
                changed |= ConfigureButton(renderer.gameObject, titleController, action);
                configuredButtons.Add(renderer.gameObject);
            }
        }

        if (buttonRenderers.Length >= 3)
        {
            changed |= ConfigureButtonIfMissing(
                buttonRenderers[0].gameObject,
                configuredButtons,
                titleController,
                TitlePageSpriteButton.ButtonAction.QuitGame);
            changed |= ConfigureButtonIfMissing(
                buttonRenderers[1].gameObject,
                configuredButtons,
                titleController,
                TitlePageSpriteButton.ButtonAction.StartGame);
            changed |= ConfigureButtonIfMissing(
                buttonRenderers[2].gameObject,
                configuredButtons,
                titleController,
                TitlePageSpriteButton.ButtonAction.OpenSettings);
        }

        return changed;
    }

    private static bool ConfigureButton(
        GameObject buttonObject,
        TitlePageController titleController,
        TitlePageSpriteButton.ButtonAction action)
    {
        if (buttonObject.GetComponent<BoxCollider2D>() == null)
        {
            buttonObject.AddComponent<BoxCollider2D>();
        }

        TitlePageSpriteButton button = buttonObject.GetComponent<TitlePageSpriteButton>();
        if (button == null)
        {
            button = buttonObject.AddComponent<TitlePageSpriteButton>();
        }

        button.Configure(titleController, action);
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(buttonObject);
        return true;
    }

    private static bool ConfigureButtonIfMissing(
        GameObject buttonObject,
        HashSet<GameObject> configuredButtons,
        TitlePageController titleController,
        TitlePageSpriteButton.ButtonAction action)
    {
        if (configuredButtons.Contains(buttonObject))
        {
            return false;
        }

        return ConfigureButton(buttonObject, titleController, action);
    }

    private static T FindInScene<T>(Scene scene)
        where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene == scene);
    }

    private static bool TryWireExistingSettingsButton(
        Scene scene,
        TitlePageController titleController)
    {
        Transform settingsButton = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate =>
                candidate != null &&
                candidate.gameObject.scene == scene &&
                candidate.GetComponent<SpriteRenderer>() != null &&
                IsSettingsButtonName(candidate.name))
            .OrderBy(candidate => candidate.name.Length)
            .FirstOrDefault();

        if (settingsButton == null)
        {
            return false;
        }

        bool changed = false;
        TitlePageSpriteButton button =
            settingsButton.GetComponent<TitlePageSpriteButton>();
        if (button == null)
        {
            button = settingsButton.gameObject.AddComponent<TitlePageSpriteButton>();
            changed = true;
        }

        button.Configure(
            titleController,
            TitlePageSpriteButton.ButtonAction.OpenSettings);
        EditorUtility.SetDirty(button);
        changed = true;
        return changed;
    }

    private static bool IsSettingsButtonName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalizedName = objectName.ToLowerInvariant();
        return normalizedName.Contains("setting", StringComparison.Ordinal) ||
            normalizedName.Contains("settings", StringComparison.Ordinal) ||
            normalizedName.Contains("option", StringComparison.Ordinal) ||
            normalizedName.Contains("gear", StringComparison.Ordinal);
    }

    private static bool TryGetButtonActionFromName(
        string objectName,
        out TitlePageSpriteButton.ButtonAction action)
    {
        action = TitlePageSpriteButton.ButtonAction.StartGame;

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalizedName = objectName.ToLowerInvariant();
        if (normalizedName.Contains("start", StringComparison.Ordinal) ||
            normalizedName.Contains("play", StringComparison.Ordinal))
        {
            action = TitlePageSpriteButton.ButtonAction.StartGame;
            return true;
        }

        if (normalizedName.Contains("exit", StringComparison.Ordinal) ||
            normalizedName.Contains("quit", StringComparison.Ordinal))
        {
            action = TitlePageSpriteButton.ButtonAction.QuitGame;
            return true;
        }

        if (IsSettingsButtonName(objectName))
        {
            action = TitlePageSpriteButton.ButtonAction.OpenSettings;
            return true;
        }

        return false;
    }

    private static bool TryGetButtonActionFromSprite(
        Sprite sprite,
        out TitlePageSpriteButton.ButtonAction action)
    {
        action = TitlePageSpriteButton.ButtonAction.StartGame;

        if (sprite == null)
        {
            return false;
        }

        switch (sprite.name)
        {
            case "titlePage_24":
                action = TitlePageSpriteButton.ButtonAction.QuitGame;
                return true;
            case "titlePage_25":
                action = TitlePageSpriteButton.ButtonAction.StartGame;
                return true;
            case "titlePage_26":
                action = TitlePageSpriteButton.ButtonAction.OpenSettings;
                return true;
            default:
                return false;
        }
    }

    private static bool SetBool(SerializedProperty property, bool value)
    {
        if (property == null || property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool SetFloat(SerializedProperty property, float value)
    {
        if (property == null || Mathf.Approximately(property.floatValue, value))
        {
            return false;
        }

        property.floatValue = value;
        return true;
    }

}
