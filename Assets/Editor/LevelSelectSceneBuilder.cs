using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSelectSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LevelSelect.unity";
    private const string ParentName = "Level Select Map";
    private const string Ui2Path = "Assets/Image/UI2.png";
    private const string DefaultTargetSceneName = "Level_test";
    private const string TitleSceneName = "TitlePage";
    private const string LevelSelectSceneName = "LevelSelect";
    private const string FontPath = "Assets/Fonts/IndieFlower-Regular.ttf";
    private const float CameraStartX = -7.2f;
    private const float CameraMinX = -7.2f;
    private const float CameraMaxX = 12.8f;

    private static readonly Vector3[] NodePositions =
    {
        new(-13.1f, -2.25f, 0f),
        new(-10.55f, -0.85f, 0f),
        new(-7.9f, -2.15f, 0f),
        new(-5.15f, -0.65f, 0f),
        new(-2.45f, -1.95f, 0f),
        new(0.35f, -0.35f, 0f),
        new(3.0f, -1.65f, 0f),
        new(5.65f, -0.15f, 0f),
        new(8.3f, -1.25f, 0f),
        new(10.95f, 0.3f, 0f),
        new(13.5f, 1.65f, 0f),
        new(16.2f, 2.75f, 0f),
        new(18.85f, 1.45f, 0f),
    };

    [MenuItem("Tools/Bullet Foundry/Build Level Select Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before rebuilding the level select scene.");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        Sprite normalCircle = LoadSprite(Ui2Path, "UI2_4");
        Sprite lineSprite = LoadSprite(Ui2Path, "UI2_2");
        Sprite bossCircle = LoadSprite(Ui2Path, "UI2_9");
        Sprite backIcon = LoadSprite(Ui2Path, "UI2_5");

        if (normalCircle == null || lineSprite == null || bossCircle == null || backIcon == null)
        {
            Debug.LogError("Could not build LevelSelect: missing UI2 circle, line, skull-circle, or back icon sprite.");
            return;
        }

        GameObject oldRoot = GameObject.Find(ParentName);
        if (oldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject(ParentName);
        root.transform.position = Vector3.zero;

        ConfigureCamera();
        BuildPath(root.transform, lineSprite);
        BuildNodes(root.transform, normalCircle, bossCircle);
        BuildFixedHud(backIcon);
        EnsureBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Built LevelSelect scene with 12 normal levels and 1 boss level.");
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        bool createdCamera = false;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            createdCamera = true;
        }

        camera.transform.position = new Vector3(CameraStartX, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 4.6f;
        if (createdCamera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        FireTowerPlacementSystem placementSystem = camera.GetComponent<FireTowerPlacementSystem>();
        if (placementSystem != null)
        {
            UnityEngine.Object.DestroyImmediate(placementSystem);
        }

        LevelSelectCameraScroll scroll = camera.GetComponent<LevelSelectCameraScroll>();
        if (scroll == null)
        {
            scroll = camera.gameObject.AddComponent<LevelSelectCameraScroll>();
        }

        scroll.Configure(CameraMinX, CameraMaxX);

        Transform oldHud = camera.transform.Find("Level Select HUD");
        if (oldHud != null)
        {
            UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
        }
    }

    private static void BuildPath(Transform parent, Sprite lineSprite)
    {
        GameObject pathRoot = new GameObject("Path Lines");
        pathRoot.transform.SetParent(parent, false);

        for (int i = 0; i < NodePositions.Length - 1; i++)
        {
            CreateLine(
                $"Path {i + 1:00}-{i + 2:00}",
                NodePositions[i],
                NodePositions[i + 1],
                lineSprite,
                pathRoot.transform);
        }
    }

    private static void BuildNodes(Transform parent, Sprite normalCircle, Sprite bossCircle)
    {
        GameObject nodeRoot = new GameObject("Level Nodes");
        nodeRoot.transform.SetParent(parent, false);

        for (int i = 0; i < 12; i++)
        {
            int levelNumber = i + 1;
            GameObject node = CreateSpriteObject(
                $"Level {levelNumber:00}",
                normalCircle,
                NodePositions[i],
                new Vector2(1.36f, 1.36f),
                nodeRoot.transform,
                10);

            AddClickableNode(node, levelNumber, false);
            CreateText(
                levelNumber.ToString(),
                NodePositions[i] + new Vector3(0f, -0.04f, -0.05f),
                0.58f,
                Color.black,
                nodeRoot.transform,
                $"Level {levelNumber:00} Number",
                25);
        }

        GameObject boss = CreateSpriteObject(
            "Boss Level",
            bossCircle,
            NodePositions[12],
            new Vector2(1.72f, 1.72f),
            nodeRoot.transform,
            10);
        AddClickableNode(boss, 13, true);

        CreateText(
            "BOSS",
            NodePositions[12] + new Vector3(0f, -1.38f, -0.05f),
            0.46f,
            Color.white,
            nodeRoot.transform,
            "Boss Label",
            25);
    }

    private static GameObject CreateSpriteObject(
        string name,
        Sprite sprite,
        Vector3 position,
        Vector2 worldSize,
        Transform parent,
        int sortingOrder)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;

        Vector2 spriteSize = sprite.bounds.size;
        gameObject.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.001f, spriteSize.x),
            worldSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);

        return gameObject;
    }

    private static void CreateLine(string name, Vector3 start, Vector3 end, Sprite sprite, Transform parent)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        Vector3 midpoint = (start + end) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        GameObject line = new GameObject(name);
        line.transform.SetParent(parent, false);
        line.transform.position = midpoint + new Vector3(0f, 0f, 0.1f);
        line.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 0;
        renderer.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        Vector2 spriteSize = sprite.bounds.size;
        line.transform.localScale = new Vector3(
            0.46f / Mathf.Max(0.001f, spriteSize.x),
            length / Mathf.Max(0.001f, spriteSize.y),
            1f);
    }

    private static void AddClickableNode(GameObject node, int levelNumber, bool boss)
    {
        CircleCollider2D collider = node.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        float largestScale = Mathf.Max(
            Mathf.Abs(node.transform.lossyScale.x),
            Mathf.Abs(node.transform.lossyScale.y),
            0.001f);
        collider.radius = 0.85f / largestScale;

        LevelSelectNode selector = node.AddComponent<LevelSelectNode>();
        selector.Configure(levelNumber, boss, DefaultTargetSceneName);
    }

    private static void BuildFixedHud(Sprite backIcon)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        GameObject hudRoot = new GameObject("Level Select HUD");
        hudRoot.transform.SetParent(camera.transform, false);
        hudRoot.transform.localPosition = new Vector3(0f, 0f, 10f);

        GameObject title = CreateText(
            "LEVEL SELECT",
            Vector3.zero,
            0.72f,
            Color.white,
            hudRoot.transform,
            "Title",
            30);
        title.transform.localPosition = new Vector3(-4.65f, 3.75f, 0f);

        GameObject backButton = CreateSpriteObject(
            "Back Button",
            backIcon,
            Vector3.zero,
            new Vector2(0.66f, 0.5f),
            hudRoot.transform,
            40);
        backButton.transform.localPosition = new Vector3(-7.35f, 3.55f, 0f);
        backButton.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        BoxCollider2D collider = backButton.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(
            0.9f / Mathf.Max(0.001f, Mathf.Abs(backButton.transform.localScale.x)),
            0.75f / Mathf.Max(0.001f, Mathf.Abs(backButton.transform.localScale.y)));

        LevelSelectReturnButton returnButton = backButton.AddComponent<LevelSelectReturnButton>();
        returnButton.Configure(TitleSceneName);
    }

    private static GameObject CreateText(
        string value,
        Vector3 worldPosition,
        float size,
        Color color,
        Transform parent,
        string name,
        int sortingOrder)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        textObject.transform.position = worldPosition;

        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 96;
        text.characterSize = size * 0.1f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font != null)
        {
            text.font = font;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = font.material;
        }

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = sortingOrder;
        return textObject;
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => string.Equals(sprite.name, spriteName, StringComparison.Ordinal));
    }

    private static void EnsureBuildSettings()
    {
        string[] requiredScenes =
        {
            $"Assets/Scenes/{TitleSceneName}.unity",
            $"Assets/Scenes/{LevelSelectSceneName}.unity",
            $"Assets/Scenes/{DefaultTargetSceneName}.unity",
        };

        EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
        var mergedScenes = existingScenes
            .Select(scene => new EditorBuildSettingsScene(scene.path, scene.enabled))
            .ToList();

        foreach (string requiredScene in requiredScenes)
        {
            if (mergedScenes.Any(scene => string.Equals(scene.path, requiredScene, StringComparison.Ordinal)))
            {
                continue;
            }

            mergedScenes.Add(new EditorBuildSettingsScene(requiredScene, true));
        }

        EditorBuildSettings.scenes = mergedScenes.ToArray();
    }
}
