using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LevelResultPanelBootstrap
{
    private const string ScenePath = "Assets/Scenes/Levels.unity";
    private const string RootName = "Result UI";
    private const string OverlayName = "Result Overlay";
    private const string UiPath = "Assets/Image/UI.png";
    private const string Ui2Path = "Assets/Image/UI2.png";
    private const string Ui3Path = "Assets/Image/UI3.png";
    private const string FontGuid = "e015b2af1fbc479696aba7fe0bcf7e27";

    [MenuItem("Tools/Bullet Foundry/Build Level Result Panels")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before rebuilding level result panels.");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        Sprite panelSprite = LoadSprite(Ui3Path, "UI3_0");
        Sprite buttonSprite = LoadSprite(Ui2Path, "UI2_8");
        Sprite successIcon = LoadSprite(UiPath, "UI_5");
        Sprite failureIcon = LoadSprite(Ui2Path, "UI2_9");
        Font font = LoadFont();

        if (panelSprite == null || buttonSprite == null)
        {
            Debug.LogError("Could not build result panels: missing UI3_0 panel sprite or UI2_8 button sprite.");
            return;
        }

        GameObject existing = FindSceneObject(RootName, scene);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LevelResultPanelController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetStretch(rootRect);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlay = new(OverlayName, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        SetStretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.56f);
        overlayImage.raycastTarget = true;

        GameObject successPanel = CreatePanel(
            overlay.transform,
            "Success Panel",
            "SUCCESS",
            "LEVEL CLEAR",
            successIcon,
            panelSprite,
            font);
        GameObject failurePanel = CreatePanel(
            overlay.transform,
            "Failure Panel",
            "FAILED",
            "SHOOTER DESTROYED",
            failureIcon,
            panelSprite,
            font);

        Button successRetry = CreateButton(successPanel.transform, "Success Retry", "RETRY", -170f, -190f, buttonSprite, font);
        Button successLevelSelect = CreateButton(successPanel.transform, "Success Level Select", "LEVEL SELECT", 170f, -190f, buttonSprite, font);
        Button failureRetry = CreateButton(failurePanel.transform, "Failure Retry", "RETRY", -170f, -190f, buttonSprite, font);
        Button failureLevelSelect = CreateButton(failurePanel.transform, "Failure Level Select", "LEVEL SELECT", 170f, -190f, buttonSprite, font);

        LevelResultPanelController controller = root.GetComponent<LevelResultPanelController>();
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("canvas").objectReferenceValue = canvas;
        serializedController.FindProperty("root").objectReferenceValue = overlay;
        serializedController.FindProperty("successPanel").objectReferenceValue = successPanel;
        serializedController.FindProperty("failurePanel").objectReferenceValue = failurePanel;
        serializedController.FindProperty("successRetryButton").objectReferenceValue = successRetry;
        serializedController.FindProperty("successLevelSelectButton").objectReferenceValue = successLevelSelect;
        serializedController.FindProperty("failureRetryButton").objectReferenceValue = failureRetry;
        serializedController.FindProperty("failureLevelSelectButton").objectReferenceValue = failureLevelSelect;

        LevelEnemySpawner spawner = Object.FindFirstObjectByType<LevelEnemySpawner>();
        if (spawner != null)
        {
            serializedController.FindProperty("spawner").objectReferenceValue = spawner;
        }

        GameObject shooter = GameObject.Find("Shooter");
        if (shooter != null)
        {
            serializedController.FindProperty("shooterHealth").objectReferenceValue = shooter.GetComponent<TowerHealth>();
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();

        overlay.SetActive(false);
        successPanel.SetActive(false);
        failurePanel.SetActive(false);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Built scene-based success and failure result panels for Levels.");
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        string title,
        string subtitle,
        Sprite icon,
        Sprite panelSprite,
        Font font)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        SetCentered(panel.GetComponent<RectTransform>(), 880f, 580f, 0f, 0f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.preserveAspect = false;

        CreateText(panel.transform, "Title", title, 74, 680f, 110f, 0f, 170f, font);
        CreateText(panel.transform, "Subtitle", subtitle, 34, 660f, 80f, 0f, 82f, font);

        if (icon != null)
        {
            CreateImage(panel.transform, "Icon", icon, 112f, 112f, 0f, -26f);
        }

        return panel;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        float x,
        float y,
        Sprite sprite,
        Font font)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetCentered(buttonObject.GetComponent<RectTransform>(), 300f, 108f, x, y);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;

        CreateText(buttonObject.transform, "Text", label, 34, 250f, 70f, 0f, 2f, font);
        return button;
    }

    private static Image CreateImage(
        Transform parent,
        string name,
        Sprite sprite,
        float width,
        float height,
        float x,
        float y)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetCentered(imageObject.GetComponent<RectTransform>(), width, height, x, y);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        int fontSize,
        float width,
        float height,
        float x,
        float y,
        Font font)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        SetCentered(textObject.GetComponent<RectTransform>(), width, height, x, y);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 18;
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, float width, float height, float x, float y)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == spriteName);
    }

    private static Font LoadFont()
    {
        string path = AssetDatabase.GUIDToAssetPath(FontGuid);
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        return font != null
            ? font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static GameObject FindSceneObject(string objectName, Scene scene)
    {
        return Resources
            .FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(gameObject => gameObject != null &&
                gameObject.name == objectName &&
                gameObject.scene == scene);
    }
}
