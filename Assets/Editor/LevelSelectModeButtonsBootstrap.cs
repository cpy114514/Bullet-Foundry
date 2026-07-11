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

        // UI2_8 is the wide paper-button artwork already used by the game's
        // other menu controls.  Keep these two mode buttons editable in scene.
        Sprite buttonSprite = LoadSprite(Ui2Path, "UI2_8");
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
            "Level Editor Button",
            "EDITOR",
            LevelSceneMode.LevelEditor,
            settingsLocalPosition + new Vector3(-5f, 0f, 0f),
            buttonSprite);

        CreateModeButton(
            hud,
            "Sandbox Button",
            "SANDBOX",
            LevelSceneMode.Sandbox,
            settingsLocalPosition + new Vector3(-2.45f, 0f, 0f),
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
        Vector2 targetSize = new(2.45f, 0.94f);
        buttonTransform.localScale = new Vector3(
            targetSize.x / Mathf.Max(0.001f, spriteSize.x),
            targetSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);

        BoxCollider2D collider = button.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = spriteSize;

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
        text.fontSize = 128;
        // Sandbox has one extra character, so it is just slightly smaller
        // while still filling the same rectangular button cleanly.
        text.characterSize = mode == LevelSceneMode.Sandbox ? 0.075f : 0.09f;
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
