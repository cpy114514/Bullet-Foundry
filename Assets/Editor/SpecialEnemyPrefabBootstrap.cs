using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

[InitializeOnLoad]
public static class SpecialEnemyPrefabBootstrap
{
    private const string PigSpritePath = "Assets/Image/PigLeader.png";
    private const string FrogSpritePath = "Assets/Image/FrogPrincess.png";
    private const string TongueLineSpritePath = "Assets/Image/UI2.png";
    private const string GoblinPrefabPath = "Assets/Prefab/Goblin.prefab";
    private const string PigPrefabPath = "Assets/Prefab/PigLeader.prefab";
    private const string FrogPrefabPath = "Assets/Prefab/FrogPrincess.prefab";
    private const string PigControllerPath = "Assets/Animation/PigLeader.controller";
    private const string FrogControllerPath = "Assets/Animation/FrogPrincess.controller";

    private static readonly PartDefinition[] PigParts =
    {
        new("Pigman_0", "Left Arm", 2, "Left Upper Arm Bone", "Left Forearm Bone"),
        new("Pigman_1", "Body", 1, "Torso Bone", "Neck Bone", "Head Bone", "Left Ear Bone", "Right Ear Bone"),
        new("Pigman_2", "Right Arm", 2, "Right Upper Arm Bone", "Right Forearm Bone"),
        new("Pigman_3", "Left Leg", 0, "Left Upper Leg Bone", "Left Lower Leg Bone"),
        new("Pigman_4", "Right Leg", 0, "Right Upper Leg Bone", "Right Lower Leg Bone")
    };

    private static readonly PartDefinition[] FrogParts =
    {
        new("beautifulFrog 1_0", "Heart Large", 4, "Heart Large Bone"),
        new("beautifulFrog 1_1", "Heart Small", 4, "Heart Small Bone"),
        new("beautifulFrog 1_2", "Heart Medium", 4, "Heart Medium Bone"),
        new("beautifulFrog 1_3", "Tongue Tip", 3, "Tongue Base Bone", "Tongue Middle Bone", "Tongue Tip Bone"),
        new("beautifulFrog 1_4", "Body", 1, "Torso Bone", "Upper Torso Bone", "Neck Bone", "Head Bone", "Crown Bone"),
        new("beautifulFrog 1_5", "Left Arm", 2, "Left Upper Arm Bone", "Left Forearm Bone"),
        new("beautifulFrog 1_6", "Right Arm", 2, "Right Upper Arm Bone", "Right Forearm Bone"),
        new("beautifulFrog 1_7", "Left Leg", 0, "Left Thigh Bone", "Left Knee Bone", "Left Shin Bone", "Left Ankle Bone", "Left Foot Bone"),
        new("beautifulFrog 1_8", "Right Leg", 0, "Right Thigh Bone", "Right Knee Bone", "Right Shin Bone", "Right Ankle Bone", "Right Foot Bone")
    };

    static SpecialEnemyPrefabBootstrap()
    {
        EditorApplication.delayCall += EnsurePrefabs;
    }

    [MenuItem("Tools/Bullet Foundry/Create Pig Leader and Frog Princess Prefabs")]
    public static void EnsurePrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        CreatePigLeaderPrefab();
        CreateFrogPrincessPrefab();
    }

    private static void CreatePigLeaderPrefab()
    {
        AnimatorController controller = EnsureController(
            PigControllerPath,
            "pigleader_walk",
            "pigleader_attack",
            "pigleader_summon",
            "pigleader_die");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PigPrefabPath);
        if (existing != null && HasExpectedHierarchy(existing, PigParts.Length))
        {
            ConfigureExistingPigPrefab(controller);
            return;
        }

        GameObject root = BuildRiggedRoot(
            "PigLeader",
            PigSpritePath,
            PigParts,
            2.45f);
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
                45,
                0.6f,
                3,
                1.05f,
                "pigleader_walk",
                "pigleader_attack",
                "pigleader_die",
                1.25f);

            PigLeaderSummoner summoner = root.AddComponent<PigLeaderSummoner>();
            ConfigurePigSummoner(summoner, enemy);
            FitHitbox(root);
            PrefabUtility.SaveAsPrefabAsset(root, PigPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("PigLeader prefab created from the rigged sprite parts.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateFrogPrincessPrefab()
    {
        AnimatorController controller = EnsureController(
            FrogControllerPath,
            "frogprincess_walk",
            "frogprincess_attack",
            "frogprincess_die");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FrogPrefabPath);
        if (existing != null && HasExpectedHierarchy(existing, FrogParts.Length))
        {
            ConfigureExistingFrogPrefab(controller);
            return;
        }

        GameObject root = BuildRiggedRoot(
            "FrogPrincess",
            FrogSpritePath,
            FrogParts,
            2.35f);
        if (root == null)
        {
            return;
        }

        try
        {
            Transform body = root.transform.Find("Body");
            Transform tongueTip = body.Find("Tongue Tip");
            Transform tongueOrigin = new GameObject("Tongue Origin").transform;
            tongueOrigin.SetParent(body, false);
            tongueOrigin.localPosition = tongueTip.localPosition;

            Sprite tongueLineSprite = AssetDatabase.LoadAllAssetsAtPath(TongueLineSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault(sprite => sprite.name == "UI2_0");
            GameObject tongueLineObject = new("Tongue Line");
            tongueLineObject.transform.SetParent(body, false);
            tongueLineObject.transform.localPosition = tongueOrigin.localPosition;
            SpriteRenderer tongueLine = tongueLineObject.AddComponent<SpriteRenderer>();
            tongueLine.sprite = tongueLineSprite;
            tongueLine.sortingOrder = 2;
            tongueLine.enabled = false;

            SetFrogEffectPartActive(body, "Heart Large", false);
            SetFrogEffectPartActive(body, "Heart Small", false);
            SetFrogEffectPartActive(body, "Heart Medium", false);
            tongueTip.gameObject.SetActive(false);

            AddEnemyFoundation(root, controller);
            GoblinEnemy enemy = root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(
                enemy,
                18,
                0.75f,
                0,
                1.8f,
                "frogprincess_walk",
                "frogprincess_attack",
                "frogprincess_die",
                1.2f);

            FrogPrincessEnemy frog = root.AddComponent<FrogPrincessEnemy>();
            ConfigureFrogEnemy(frog, enemy, tongueOrigin, tongueTip, tongueLine);
            FitHitbox(root);
            PrefabUtility.SaveAsPrefabAsset(root, FrogPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("FrogPrincess prefab created with a scalable tongue attack.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildRiggedRoot(
        string rootName,
        string spritePath,
        PartDefinition[] definitions,
        float targetHeight)
    {
        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();
        Sprite[] sprites = definitions
            .Select(definition => allSprites.FirstOrDefault(sprite =>
                string.Equals(sprite.name, definition.SpriteName, StringComparison.Ordinal)))
            .ToArray();
        if (sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            Debug.LogWarning($"{rootName} prefab was not created because its sprite bone data is incomplete.");
            return null;
        }

        int bodyIndex = Array.FindIndex(definitions, definition => definition.ObjectName == "Body");
        GameObject root = new(rootName);
        ConfigureRootScale(root.transform, sprites, targetHeight);

        Vector2 bodyCenter = sprites[bodyIndex].rect.center;
        float pixelsPerUnit = Mathf.Max(1f, sprites[bodyIndex].pixelsPerUnit);
        Transform[] parts = new Transform[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            Vector2 position = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
            parts[i] = CreateRiggedPart(
                root.transform,
                definitions[i],
                sprites[i],
                position);
        }

        Transform body = parts[bodyIndex];
        for (int i = 0; i < parts.Length; i++)
        {
            if (i != bodyIndex)
            {
                parts[i].SetParent(body, true);
            }
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

    private static void AddEnemyFoundation(GameObject root, RuntimeAnimatorController controller)
    {
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

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

    private static void ConfigurePigSummoner(PigLeaderSummoner summoner, GoblinEnemy enemy)
    {
        SerializedObject serialized = new(summoner);
        serialized.FindProperty("enemy").objectReferenceValue = enemy;
        serialized.FindProperty("goblinPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFrogEnemy(
        FrogPrincessEnemy frog,
        GoblinEnemy enemy,
        Transform origin,
        Transform tip,
        SpriteRenderer line)
    {
        SerializedObject serialized = new(frog);
        serialized.FindProperty("enemy").objectReferenceValue = enemy;
        serialized.FindProperty("tongueOrigin").objectReferenceValue = origin;
        serialized.FindProperty("tongueTip").objectReferenceValue = tip;
        serialized.FindProperty("tongueLine").objectReferenceValue = line;
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

    private static void ConfigureExistingPigPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PigPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            GoblinEnemy enemy = root.GetComponent<GoblinEnemy>() ?? root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(enemy, 45, 0.6f, 3, 1.05f, "pigleader_walk", "pigleader_attack", "pigleader_die", 1.25f);
            PigLeaderSummoner summoner = root.GetComponent<PigLeaderSummoner>() ?? root.AddComponent<PigLeaderSummoner>();
            ConfigurePigSummoner(summoner, enemy);
            PrefabUtility.SaveAsPrefabAsset(root, PigPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureExistingFrogPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(FrogPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            GoblinEnemy enemy = root.GetComponent<GoblinEnemy>() ?? root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(enemy, 18, 0.75f, 0, 1.8f, "frogprincess_walk", "frogprincess_attack", "frogprincess_die", 1.2f);
            FrogPrincessEnemy frog = root.GetComponent<FrogPrincessEnemy>() ?? root.AddComponent<FrogPrincessEnemy>();
            Transform body = root.transform.Find("Body");
            ConfigureFrogEnemy(
                frog,
                enemy,
                body.Find("Tongue Origin"),
                body.Find("Tongue Tip"),
                body.Find("Tongue Line").GetComponent<SpriteRenderer>());
            PrefabUtility.SaveAsPrefabAsset(root, FrogPrefabPath);
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
        float minY = sprites.Min(sprite => sprite.rect.yMin);
        float maxY = sprites.Max(sprite => sprite.rect.yMax);
        float sourceHeight = (maxY - minY) / Mathf.Max(1f, sprites[0].pixelsPerUnit);
        float scale = sourceHeight > 0f ? targetHeight / sourceHeight : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static bool HasExpectedHierarchy(GameObject prefab, int partCount)
    {
        Transform body = prefab.transform.Find("Body");
        return body != null &&
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length == partCount &&
            body.Find("Left Arm") != null &&
            body.Find("Right Arm") != null &&
            body.Find("Left Leg") != null &&
            body.Find("Right Leg") != null;
    }

    private static void SetFrogEffectPartActive(Transform body, string name, bool active)
    {
        Transform part = body.Find(name);
        if (part != null)
        {
            part.gameObject.SetActive(active);
        }
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
