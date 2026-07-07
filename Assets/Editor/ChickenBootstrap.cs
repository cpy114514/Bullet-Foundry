using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

[InitializeOnLoad]
public static class ChickenBootstrap
{
    private const string ChickenSpritePath = "Assets/Image/Chichen.png";
    private const string ChickenPrefabPath = "Assets/Prefab/Chicken.prefab";
    private const string ChickenControllerPath = "Assets/Animation/Chicken.controller";
    private const string ChickenRigSchema = "BulletFoundryChickenUnityBones:v1";

    private static readonly PartDefinition[] ChickenParts =
    {
        new("Chichen_0", "Body", 1,
            "Body Root Bone",
            "Body Mid Bone",
            "Tail Base Bone",
            "Left Leg Bone",
            "Right Leg Bone",
            "Wing Root Bone",
            "Wing Mid Bone",
            "Wing Tip Bone",
            "Left Foot Bone",
            "Right Foot Bone",
            "Neck Bone",
            "Head Bone",
            "Beak Bone",
            "Back Bone",
            "Tail Feather Bone"),
        new("Chichen_1", "Poop", 2, "Poop Bone")
    };

    static ChickenBootstrap()
    {
        EditorApplication.delayCall += EnsureChickenPrefab;
    }

    [MenuItem("Tools/Bullet Foundry/Rebuild Chicken Prefab")]
    public static void RebuildChickenPrefab()
    {
        AssetDatabase.DeleteAsset(ChickenPrefabPath);
        EnsureChickenPrefab();
    }

    public static void EnsureChickenPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AnimatorController controller = EnsureController(
            ChickenControllerPath,
            "chicken_walk",
            "chicken_attack",
            "chicken_die");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChickenPrefabPath);
        if (existing != null && HasExpectedHierarchy(existing))
        {
            ConfigureExistingChickenPrefab(controller);
            return;
        }

        GameObject root = BuildChickenRoot(1.55f);
        if (root == null)
        {
            return;
        }

        try
        {
            AddEnemyFoundation(root, controller);
            GoblinEnemy enemy = root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(
                enemy,
                12,
                1.15f,
                1,
                1.2f,
                "chicken_walk",
                "chicken_attack",
                "chicken_die",
                0.8f);

            Transform poop = root.transform.Find("Poop");
            if (poop != null)
            {
                poop.gameObject.SetActive(false);
            }

            FitHitbox(root);
            PrefabUtility.SaveAsPrefabAsset(root, ChickenPrefabPath);
            StampPrefabUserData(ChickenPrefabPath, ChickenRigSchema);
            AssetDatabase.SaveAssets();
            Debug.Log("Chicken prefab created with Unity SpriteSkin bones.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildChickenRoot(float targetHeight)
    {
        Sprite[] sprites = LoadSprites();
        if (sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            Debug.LogWarning("Chicken prefab was not created because Chichen.png sprite/bone data is incomplete.");
            return null;
        }

        GameObject root = new("Chicken");
        ConfigureRootScale(root.transform, sprites, targetHeight);

        Sprite bodySprite = sprites[0];
        Vector2 bodyCenter = bodySprite.rect.center;
        float pixelsPerUnit = Mathf.Max(1f, bodySprite.pixelsPerUnit);
        for (int i = 0; i < ChickenParts.Length; i++)
        {
            Vector2 localPosition = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
            CreateRiggedPart(root.transform, ChickenParts[i], sprites[i], localPosition);
        }

        return root;
    }

    private static Transform CreateRiggedPart(
        Transform parent,
        PartDefinition definition,
        Sprite sprite,
        Vector2 localPosition)
    {
        GameObject partObject = new(definition.ObjectName);
        Transform part = partObject.transform;
        part.SetParent(parent, false);
        part.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = definition.SortingOrder;

        SpriteBone[] spriteBones = sprite.GetBones();
        Transform[] boneTransforms = new Transform[spriteBones.Length];
        Transform rootBone = null;
        for (int i = 0; i < spriteBones.Length; i++)
        {
            CreateBone(i, spriteBones, definition.BoneNames, boneTransforms, part);
            if (spriteBones[i].parentId < 0 && rootBone == null)
            {
                rootBone = boneTransforms[i];
            }
        }

        SpriteSkin skin = partObject.AddComponent<SpriteSkin>();
        skin.SetRootBone(rootBone);
        skin.SetBoneTransforms(boneTransforms);
        skin.alwaysUpdate = true;
        return part;
    }

    private static void CreateBone(
        int index,
        SpriteBone[] spriteBones,
        string[] names,
        Transform[] transforms,
        Transform partRoot)
    {
        if (transforms[index] != null)
        {
            return;
        }

        SpriteBone spriteBone = spriteBones[index];
        if (spriteBone.parentId >= 0)
        {
            CreateBone(spriteBone.parentId, spriteBones, names, transforms, partRoot);
        }

        string boneName = index < names.Length
            ? names[index]
            : $"{partRoot.name} Bone {index + 1}";
        Transform bone = new GameObject(boneName).transform;
        bone.SetParent(
            spriteBone.parentId >= 0 ? transforms[spriteBone.parentId] : partRoot,
            false);
        bone.localPosition = spriteBone.position;
        bone.localRotation = spriteBone.rotation;
        bone.localScale = Vector3.one;
        transforms[index] = bone;
    }

    private static void AddEnemyFoundation(GameObject root, AnimatorController controller)
    {
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;

        BoxCollider2D hitbox = root.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
    }

    private static void ConfigureGoblinEnemy(
        GoblinEnemy enemy,
        int health,
        float speed,
        int damage,
        float cooldown,
        string walk,
        string attack,
        string die,
        float deathDelay)
    {
        SerializedObject serialized = new(enemy);
        serialized.FindProperty("maxHealth").intValue = health;
        serialized.FindProperty("moveSpeed").floatValue = speed;
        serialized.FindProperty("contactDamage").intValue = damage;
        serialized.FindProperty("attackCooldown").floatValue = cooldown;
        serialized.FindProperty("walkStateName").stringValue = walk;
        serialized.FindProperty("attackStateName").stringValue = attack;
        serialized.FindProperty("dieStateName").stringValue = die;
        serialized.FindProperty("destroyDelayAfterDeath").floatValue = deathDelay;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AnimatorController EnsureController(string path, params string[] stateNames)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState firstState = null;
        for (int i = 0; i < stateNames.Length; i++)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateNames[i]);
            if (state == null)
            {
                state = stateMachine.AddState(
                    stateNames[i],
                    new Vector3(220f + ((i % 2) * 230f), (i / 2) * 100f, 0f));
            }

            firstState ??= state;
        }

        stateMachine.defaultState = firstState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigureExistingChickenPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ChickenPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            GoblinEnemy enemy = root.GetComponent<GoblinEnemy>() ?? root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(enemy, 12, 1.15f, 1, 1.2f, "chicken_walk", "chicken_attack", "chicken_die", 0.8f);
            PrefabUtility.SaveAsPrefabAsset(root, ChickenPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void FitHitbox(GameObject root)
    {
        BoxCollider2D hitbox = root.GetComponent<BoxCollider2D>();
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(false)
            .Where(renderer => renderer.enabled)
            .ToArray();
        if (hitbox == null || renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        hitbox.offset = root.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = root.transform.lossyScale;
        hitbox.size = new Vector2(
            scale.x != 0f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            scale.y != 0f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y);
    }

    private static void ConfigureRootScale(Transform root, Sprite[] sprites, float targetHeight)
    {
        Sprite body = sprites[0];
        float sourceHeight = body.rect.height / Mathf.Max(1f, body.pixelsPerUnit);
        float scale = sourceHeight > 0f ? targetHeight / sourceHeight : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static bool HasExpectedHierarchy(GameObject prefab)
    {
        if (prefab == null ||
            prefab.name != "Chicken" ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != ChickenParts.Length ||
            !HasPrefabUserData(prefab, ChickenRigSchema))
        {
            return false;
        }

        for (int i = 0; i < ChickenParts.Length; i++)
        {
            Transform part = FindDescendant(prefab.transform, ChickenParts[i].ObjectName);
            SpriteSkin skin = part != null ? part.GetComponent<SpriteSkin>() : null;
            if (skin == null)
            {
                return false;
            }
        }

        return true;
    }

    private static Sprite[] LoadSprites()
    {
        Sprite[] sourceSprites = AssetDatabase.LoadAllAssetsAtPath(ChickenSpritePath)
            .OfType<Sprite>()
            .ToArray();

        return ChickenParts
            .Select(definition => sourceSprites.FirstOrDefault(sprite =>
                sprite.name == definition.SpriteName))
            .ToArray();
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        return descendants.FirstOrDefault(candidate => candidate.name == objectName);
    }

    private static bool HasPrefabUserData(GameObject prefab, string expectedUserData)
    {
        AssetImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(prefab));
        return importer != null && importer.userData == expectedUserData;
    }

    private static void StampPrefabUserData(string prefabPath, string userData)
    {
        AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
        if (importer == null)
        {
            return;
        }

        importer.userData = userData;
        EditorUtility.SetDirty(importer);
        AssetDatabase.WriteImportSettingsIfDirty(prefabPath);
    }

    private readonly struct PartDefinition
    {
        public PartDefinition(
            string spriteName,
            string objectName,
            int sortingOrder,
            params string[] boneNames)
        {
            SpriteName = spriteName;
            ObjectName = objectName;
            SortingOrder = sortingOrder;
            BoneNames = boneNames;
        }

        public string SpriteName { get; }

        public string ObjectName { get; }

        public int SortingOrder { get; }

        public string[] BoneNames { get; }
    }
}
