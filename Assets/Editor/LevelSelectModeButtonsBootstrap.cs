using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSelectModeButtonsBootstrap
{
    private const string LevelSelectScenePath = "Assets/Scenes/LevelSelect.unity";
    private const string LevelsScenePath = "Assets/Scenes/Levels.unity";
    private const string TargetSceneName = "Levels";
    private const string Ui2Path = "Assets/Image/UI2.png";
    private const string FontPath = "Assets/Fonts/IndieFlower-Regular.ttf";

    [MenuItem("Tools/Bullet Foundry/Setup Level Select Mode Buttons")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before setting up Level Select mode buttons.");
            return;
        }

        Scene originalScene = EditorSceneManager.GetActiveScene();
        SetupLevelSelectScene();
        SetupLevelsScene();

        if (!string.IsNullOrWhiteSpace(originalScene.path) &&
            originalScene.path != LevelSelectScenePath &&
            originalScene.path != LevelsScenePath)
        {
            EditorSceneManager.OpenScene(originalScene.path);
        }

        Debug.Log("Set up Level Editor and Sandbox buttons on LevelSelect.");
    }

    private static void SetupLevelSelectScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LevelSelectScenePath);
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("Could not set up LevelSelect mode buttons: Main Camera missing.");
            return;
        }

        Transform hud = camera.transform.Find("Level Select HUD");
        if (hud == null)
        {
            Debug.LogError("Could not set up LevelSelect mode buttons: Level Select HUD missing.");
            return;
        }

        Sprite buttonSprite = LoadSprite(Ui2Path, "UI2_4") ?? LoadSprite(Ui2Path, "UI2_9");
        if (buttonSprite == null)
        {
            Debug.LogError("Could not set up LevelSelect mode buttons: missing UI2 button sprite.");
            return;
        }

        Transform settingsButton = hud.Find("Settings Button");
        Vector3 settingsLocalPosition = settingsButton != null
            ? settingsButton.localPosition
            : new Vector3(7.25f, 3.45f, 0f);

        RemoveIfExists(hud, "Level Editor Button");
        RemoveIfExists(hud, "Sandbox Button");

        CreateModeButton(
            hud,
            "Sandbox Button",
            "SANDBOX",
            LevelSceneMode.Sandbox,
            settingsLocalPosition + new Vector3(-1.45f, 0f, 0f),
            buttonSprite);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupLevelsScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LevelsScenePath);
        GameObject controllerObject = GameObject.Find("Level Scene Mode Controller");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("Level Scene Mode Controller");
        }

        LevelSceneModeController controller =
            controllerObject.GetComponent<LevelSceneModeController>() ??
            controllerObject.AddComponent<LevelSceneModeController>();

        GameObject editorRoot = GameObject.Find("Level Editor");
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("levelEditorRoot").objectReferenceValue = editorRoot;
        GameObject sandboxRoot = GameObject.Find("Sandbox Mode UI");
        serializedController.FindProperty("sandboxRoot").objectReferenceValue = sandboxRoot;
        serializedController.FindProperty("hideLevelEditorInNormalAndSandbox").boolValue = true;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void CreateModeButton(
        Transform parent,
        string objectName,
        string label,
        LevelSceneMode mode,
        Vector3 localPosition,
        Sprite buttonSprite)
    {
        GameObject button = new(objectName);
        Transform buttonTransform = button.transform;
        buttonTransform.SetParent(parent, false);
        buttonTransform.localPosition = localPosition;
        buttonTransform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = button.AddComponent<SpriteRenderer>();
        renderer.sprite = buttonSprite;
        renderer.sortingOrder = 42;
        renderer.color = Color.white;

        Vector2 spriteSize = buttonSprite.bounds.size;
        Vector2 targetSize = mode == LevelSceneMode.Sandbox
            ? new Vector2(1.15f, 1.15f)
            : new Vector2(1.0f, 1.0f);
        buttonTransform.localScale = new Vector3(
            targetSize.x / Mathf.Max(0.001f, spriteSize.x),
            targetSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);

        CircleCollider2D collider = button.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        float largestScale = Mathf.Max(
            Mathf.Abs(buttonTransform.lossyScale.x),
            Mathf.Abs(buttonTransform.lossyScale.y),
            0.001f);
        collider.radius = 0.72f / largestScale;

        LevelSelectModeButton modeButton = button.AddComponent<LevelSelectModeButton>();
        modeButton.Configure(mode, TargetSceneName);

        GameObject textObject = new($"{objectName} Label");
        Transform textTransform = textObject.transform;
        textTransform.SetParent(buttonTransform, false);
        textTransform.localPosition = new Vector3(0f, -0.02f, -0.05f);
        textTransform.localRotation = Quaternion.identity;
        textTransform.localScale = Vector3.one;

        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = label;
        text.fontSize = 72;
        text.characterSize = mode == LevelSceneMode.Sandbox ? 0.035f : 0.042f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.black;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font != null)
        {
            text.font = font;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = font.material;
        }

        MeshRenderer meshRenderer = textObject.GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 45;
    }

    private static void RemoveIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => string.Equals(sprite.name, spriteName, StringComparison.Ordinal));
    }
}
