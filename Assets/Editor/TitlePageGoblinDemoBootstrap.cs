using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TitlePageGoblinDemoBootstrap
{
    private const string ScenePath = "Assets/Scenes/TitlePage.unity";
    private const string RootName = "Title Page Goblin Demo";
    private const string GoblinPrefabPath = "Assets/Prefab/Goblin.prefab";
    private const string SpeedGoblinPrefabPath = "Assets/Prefab/SpeedGoblin.prefab";
    private const string BulletPrefabPath = "Assets/Prefab/Bullet.prefab";
    private const string FireTowerPrefabPath = "Assets/Prefab/FireTower.prefab";
    private const string IceTowerPrefabPath = "Assets/Prefab/IceTower.prefab";
    private static readonly string[] BurstBulletNames =
    {
        "Title Demo Normal Bullet",
        "Title Demo Fire Bullet A",
        "Title Demo Ice Bullet A",
        "Title Demo Normal Bullet B",
        "Title Demo Fire Bullet B",
        "Title Demo Ice Bullet B",
        "Title Demo Normal Bullet C"
    };

    [MenuItem("Tools/Bullet Foundry/Setup Title Page Goblin Demo")]
    public static void EnsureDemoMenuItem()
    {
        EnsureDemoIfTitlePageLoaded();
    }

    private static void EnsureDemoIfTitlePageLoaded()
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

        GameObject goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
        GameObject speedGoblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpeedGoblinPrefabPath);
        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
        if (goblinPrefab == null || speedGoblinPrefab == null || bulletPrefab == null)
        {
            return;
        }

        Transform root = FindRoot(scene, RootName);
        if (root != null && HasCompleteBurstLayout(root))
        {
            return;
        }

        if (root == null)
        {
            GameObject rootObject = new(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            rootObject.transform.position = CalculateTopRightPlacement(scene);
            rootObject.transform.localScale = new Vector3(2f, 2f, 1f);
            root = rootObject.transform;
        }

        EnsureBurstDemoLayout(scene, root, goblinPrefab, speedGoblinPrefab, bulletPrefab);

        EditorUtility.SetDirty(root.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform FindRoot(Scene scene, string rootName)
    {
        return scene.GetRootGameObjects()
            .Select(gameObject => gameObject.transform)
            .FirstOrDefault(transform =>
                string.Equals(transform.name, rootName, StringComparison.Ordinal));
    }

    private static Vector3 CalculateTopRightPlacement(Scene scene)
    {
        Camera camera = FindInScene<Camera>(scene);
        if (camera != null && camera.orthographic)
        {
            float right = camera.transform.position.x + camera.orthographicSize * camera.aspect;
            float top = camera.transform.position.y + camera.orthographicSize;
            return new Vector3(right - 2.35f, top - 1.45f, 0f);
        }

        return new Vector3(5.6f, 3.4f, 0f);
    }

    private static bool HasCompleteBurstLayout(Transform root)
    {
        for (int i = 0; i < BurstBulletNames.Length; i++)
        {
            if (FindChild(root, BurstBulletNames[i]) == null)
            {
                return false;
            }
        }

        TitlePageGoblinDemo demo = root.GetComponent<TitlePageGoblinDemo>();
        if (demo == null)
        {
            return false;
        }

        if (FindChild(root, "Title Demo Speed Goblin") == null ||
            FindChild(root, "Title Demo Normal Goblin") == null)
        {
            return false;
        }

        SerializedObject serializedDemo = new(demo);
        if (serializedDemo.FindProperty("speedGoblinAnimator")?.objectReferenceValue == null ||
            serializedDemo.FindProperty("normalGoblinAnimator")?.objectReferenceValue == null)
        {
            return false;
        }

        SerializedProperty bulletTransforms = serializedDemo.FindProperty("bulletTransforms");
        return bulletTransforms != null && bulletTransforms.arraySize == BurstBulletNames.Length;
    }

    private static void EnsureBurstDemoLayout(
        Scene scene,
        Transform root,
        GameObject goblinPrefab,
        GameObject speedGoblinPrefab,
        GameObject bulletPrefab)
    {
        TitlePageGoblinDemo demo = root.GetComponent<TitlePageGoblinDemo>();
        if (demo == null)
        {
            demo = root.gameObject.AddComponent<TitlePageGoblinDemo>();
        }

        BoxCollider2D clickArea = EnsureClickArea(root);
        Animator speedGoblinAnimator = EnsureSpeedGoblin(scene, root, speedGoblinPrefab);
        Animator normalGoblinAnimator = EnsureNormalGoblin(scene, root, goblinPrefab);
        Transform goblinTarget = EnsureTarget(root);
        Transform[] bullets = EnsureBurstBullets(scene, root, bulletPrefab);

        WireDemo(
            demo,
            clickArea,
            speedGoblinAnimator,
            speedGoblinAnimator != null ? speedGoblinAnimator.gameObject : null,
            normalGoblinAnimator,
            normalGoblinAnimator != null ? normalGoblinAnimator.gameObject : null,
            goblinTarget,
            bullets);
    }

    private static BoxCollider2D EnsureClickArea(Transform parent)
    {
        Transform existing = FindChild(parent, "Goblin Demo Click Area");
        if (existing != null && existing.TryGetComponent(out BoxCollider2D existingClickArea))
        {
            existingClickArea.isTrigger = true;
            existingClickArea.size = new Vector2(3.65f, 2.35f);
            return existingClickArea;
        }

        return CreateClickArea(parent);
    }

    private static BoxCollider2D CreateClickArea(Transform parent)
    {
        GameObject clickAreaObject = new("Goblin Demo Click Area");
        clickAreaObject.transform.SetParent(parent, false);
        clickAreaObject.transform.localPosition = new Vector3(-0.18f, 0.04f, 0f);

        BoxCollider2D clickArea = clickAreaObject.AddComponent<BoxCollider2D>();
        clickArea.isTrigger = true;
        clickArea.size = new Vector2(3.65f, 2.35f);
        return clickArea;
    }

    private static Animator EnsureGoblin(Scene scene, Transform parent, GameObject goblinPrefab)
    {
        Transform existing = FindChild(parent, "Title Demo Goblin");
        if (existing != null && existing.TryGetComponent(out Animator existingAnimator))
        {
            PrepareGoblin(existing.gameObject);
            return existingAnimator;
        }

        return CreateGoblin(scene, parent, goblinPrefab);
    }

    private static Animator EnsureSpeedGoblin(
        Scene scene,
        Transform parent,
        GameObject speedGoblinPrefab)
    {
        Transform existing = FindChild(parent, "Title Demo Speed Goblin");
        if (existing == null)
        {
            GameObject speedGoblinObject = InstantiatePrefabInScene(speedGoblinPrefab, scene, parent);
            speedGoblinObject.name = "Title Demo Speed Goblin";
            speedGoblinObject.transform.localPosition = new Vector3(0.8f, -0.05f, 0f);
            speedGoblinObject.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
            existing = speedGoblinObject.transform;
        }

        existing.gameObject.SetActive(true);
        PrepareGoblin(existing.gameObject);

        Animator animator = existing.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = 1f;
        }

        return animator;
    }

    private static Animator EnsureNormalGoblin(
        Scene scene,
        Transform parent,
        GameObject goblinPrefab)
    {
        Transform existing = FindChild(parent, "Title Demo Normal Goblin");
        if (existing == null)
        {
            existing = FindChild(parent, "Title Demo Goblin");
        }

        if (existing == null)
        {
            GameObject goblinObject = InstantiatePrefabInScene(goblinPrefab, scene, parent);
            goblinObject.name = "Title Demo Normal Goblin";
            goblinObject.transform.localPosition = new Vector3(0.8f, -0.05f, 0f);
            goblinObject.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
            existing = goblinObject.transform;
        }

        existing.name = "Title Demo Normal Goblin";
        existing.localPosition = new Vector3(0.8f, -0.05f, 0f);
        existing.localScale = new Vector3(0.12f, 0.12f, 1f);
        PrepareGoblin(existing.gameObject);
        existing.gameObject.SetActive(false);
        return existing.GetComponent<Animator>();
    }

    private static Animator CreateGoblin(Scene scene, Transform parent, GameObject goblinPrefab)
    {
        GameObject goblinObject = InstantiatePrefabInScene(goblinPrefab, scene, parent);
        goblinObject.name = "Title Demo Goblin";
        goblinObject.transform.localPosition = new Vector3(0.8f, -0.05f, 0f);
        goblinObject.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        PrepareGoblin(goblinObject);
        return goblinObject.GetComponent<Animator>();
    }

    private static void PrepareGoblin(GameObject goblinObject)
    {
        if (goblinObject.TryGetComponent(out GoblinEnemy goblinEnemy))
        {
            goblinEnemy.enabled = false;
        }

        if (goblinObject.TryGetComponent(out Rigidbody2D body))
        {
            body.simulated = false;
        }

        Collider2D[] colliders = goblinObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        SetSortingOrder(goblinObject, 40);

        Animator animator = goblinObject.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    private static Transform EnsureTarget(Transform parent)
    {
        Transform existing = FindChild(parent, "Goblin Demo Target");
        if (existing != null)
        {
            existing.localPosition = new Vector3(0.68f, 0f, 0f);
            return existing;
        }

        return CreateTarget(parent);
    }

    private static Transform CreateTarget(Transform parent)
    {
        GameObject targetObject = new("Goblin Demo Target");
        targetObject.transform.SetParent(parent, false);
        targetObject.transform.localPosition = new Vector3(0.68f, 0f, 0f);
        return targetObject.transform;
    }

    private static Transform[] EnsureBurstBullets(
        Scene scene,
        Transform parent,
        GameObject bulletPrefab)
    {
        Sprite fireSprite = LoadFirstFrameSprite(FireTowerPrefabPath, "fireFrames");
        Sprite iceSprite = LoadFirstFrameSprite(IceTowerPrefabPath, "iceFrames");

        return new[]
        {
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Normal Bullet",
                new Vector3(-1.55f, 0.62f, 0f), null, new Vector3(0.8f, 0.8f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Fire Bullet A",
                new Vector3(-1.2f, 0.36f, 0f), fireSprite, new Vector3(0.55f, 0.55f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Ice Bullet A",
                new Vector3(-1.46f, 0.12f, 0f), iceSprite, new Vector3(0.55f, 0.55f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Normal Bullet B",
                new Vector3(-0.98f, -0.08f, 0f), null, new Vector3(0.8f, 0.8f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Fire Bullet B",
                new Vector3(-1.34f, -0.32f, 0f), fireSprite, new Vector3(0.55f, 0.55f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Ice Bullet B",
                new Vector3(-1.02f, -0.56f, 0f), iceSprite, new Vector3(0.55f, 0.55f, 1f)),
            EnsureBullet(scene, parent, bulletPrefab, "Title Demo Normal Bullet C",
                new Vector3(-1.68f, -0.75f, 0f), null, new Vector3(0.8f, 0.8f, 1f))
        };
    }

    private static Transform EnsureBullet(
        Scene scene,
        Transform parent,
        GameObject bulletPrefab,
        string name,
        Vector3 localPosition,
        Sprite overrideSprite,
        Vector3 localScale)
    {
        Transform bulletTransform = FindChild(parent, name);
        if (bulletTransform == null)
        {
            if (name == "Title Demo Fire Bullet A")
            {
                bulletTransform = FindChild(parent, "Title Demo Fire Bullet");
            }
            else if (name == "Title Demo Ice Bullet A")
            {
                bulletTransform = FindChild(parent, "Title Demo Ice Bullet");
            }
        }

        if (bulletTransform == null)
        {
            bulletTransform = CreateBullet(
                scene,
                parent,
                bulletPrefab,
                name,
                localPosition,
                overrideSprite,
                localScale);
        }
        else
        {
            bulletTransform.name = name;
            bulletTransform.localPosition = localPosition;
            bulletTransform.localRotation = Quaternion.identity;
            bulletTransform.localScale = localScale;
            PrepareBullet(bulletTransform.gameObject, overrideSprite);
        }

        return bulletTransform;
    }

    private static Transform CreateBullet(
        Scene scene,
        Transform parent,
        GameObject bulletPrefab,
        string name,
        Vector3 localPosition,
        Sprite overrideSprite,
        Vector3 localScale)
    {
        GameObject bulletObject = InstantiatePrefabInScene(bulletPrefab, scene, parent);
        bulletObject.name = name;
        bulletObject.transform.localPosition = localPosition;
        bulletObject.transform.localRotation = Quaternion.identity;
        bulletObject.transform.localScale = localScale;

        PrepareBullet(bulletObject, overrideSprite);
        return bulletObject.transform;
    }

    private static void PrepareBullet(GameObject bulletObject, Sprite overrideSprite)
    {
        if (bulletObject.TryGetComponent(out Bullet bullet))
        {
            bullet.enabled = false;
        }

        if (bulletObject.TryGetComponent(out BulletImpactEffect impactEffect))
        {
            impactEffect.enabled = false;
        }

        if (bulletObject.TryGetComponent(out Rigidbody2D body))
        {
            body.simulated = false;
        }

        Collider2D[] colliders = bulletObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        SpriteRenderer renderer = bulletObject.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            if (overrideSprite != null)
            {
                renderer.sprite = overrideSprite;
            }

            renderer.color = Color.white;
            renderer.sortingOrder = 45;
        }
    }

    private static GameObject InstantiatePrefabInScene(
        GameObject prefab,
        Scene scene,
        Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static Sprite LoadFirstFrameSprite(string prefabPath, string framesPropertyName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return null;
        }

        Component component = prefab.GetComponent(framesPropertyName == "fireFrames"
            ? typeof(FireTower)
            : typeof(IceTower));
        if (component == null)
        {
            return null;
        }

        SerializedObject serializedObject = new(component);
        SerializedProperty framesProperty = serializedObject.FindProperty(framesPropertyName);
        if (framesProperty == null || !framesProperty.isArray || framesProperty.arraySize == 0)
        {
            return null;
        }

        return framesProperty.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
    }

    private static void WireDemo(
        TitlePageGoblinDemo demo,
        Collider2D clickArea,
        Animator goblinAnimator,
        GameObject speedGoblinObject,
        Animator normalGoblinAnimator,
        GameObject normalGoblinObject,
        Transform goblinTarget,
        Transform[] bullets)
    {
        SerializedObject serializedDemo = new(demo);
        serializedDemo.FindProperty("clickArea").objectReferenceValue = clickArea;
        serializedDemo.FindProperty("goblinAnimator").objectReferenceValue = goblinAnimator;
        serializedDemo.FindProperty("speedGoblinObject").objectReferenceValue = speedGoblinObject;
        serializedDemo.FindProperty("speedGoblinAnimator").objectReferenceValue = goblinAnimator;
        serializedDemo.FindProperty("normalGoblinObject").objectReferenceValue = normalGoblinObject;
        serializedDemo.FindProperty("normalGoblinAnimator").objectReferenceValue = normalGoblinAnimator;
        serializedDemo.FindProperty("goblinTarget").objectReferenceValue = goblinTarget;

        SerializedProperty bulletTransforms = serializedDemo.FindProperty("bulletTransforms");
        bulletTransforms.arraySize = bullets.Length;
        for (int i = 0; i < bullets.Length; i++)
        {
            bulletTransforms.GetArrayElementAtIndex(i).objectReferenceValue = bullets[i];
        }

        serializedDemo.FindProperty("deathStateName").stringValue = "goblin_die";
        serializedDemo.FindProperty("speedDanceStateName").stringValue = "speedGoblin_dance";
        serializedDemo.FindProperty("speedDeathStateName").stringValue = "goblin_die";
        serializedDemo.FindProperty("normalDeathStateName").stringValue = "goblin_die";
        serializedDemo.FindProperty("bulletTravelDuration").floatValue = 0.48f;
        serializedDemo.FindProperty("bulletStagger").floatValue = 0.08f;
        serializedDemo.FindProperty("deathFreezeDelay").floatValue = 1.1f;
        serializedDemo.FindProperty("speedDeathDelay").floatValue = 0.65f;
        serializedDemo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(demo);
    }

    private static void SetSortingOrder(GameObject root, int sortingOrder)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = sortingOrder;
        }
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject.scene != scene)
            {
                continue;
            }

            if (component.hideFlags != HideFlags.None)
            {
                continue;
            }

            return component;
        }

        return null;
    }
}
