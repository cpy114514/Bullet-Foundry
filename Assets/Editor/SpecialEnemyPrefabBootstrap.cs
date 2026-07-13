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
    private const string CoinPickupPrefabPath = "Assets/Prefab/CoinPickup.prefab";
    private const string GoblinPrefabPath = "Assets/Prefab/Goblin.prefab";
    private const string PigPrefabPath = "Assets/Prefab/PigLeader.prefab";
    private const string FrogPrefabPath = "Assets/Prefab/FrogPrincess.prefab";
    private const string PigControllerPath = "Assets/Animation/PigLeader.controller";
    private const string FrogControllerPath = "Assets/Animation/FrogPrincess.controller";
    private const string RigSignaturePrefix = "BulletFoundryRig:";
    private const string PigRigSchema = "BulletFoundryHybridPigBones:v1";
    private const string FrogRigSchema = "BulletFoundryUnityBonesNoHeart:v1";

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
        new("beautifulFrog 1_3", "Tongue Tip", 3, "Tongue Base Bone", "Tongue Middle Bone", "Tongue Tip Bone"),
        new("beautifulFrog 1_4", "Body", 1, "Torso Bone", "Upper Torso Bone", "Neck Bone", "Head Bone", "Crown Bone"),
        new("beautifulFrog 1_5", "Left Arm", 2, "Left Upper Arm Bone", "Left Forearm Bone"),
        new("beautifulFrog 1_6", "Right Arm", 2, "Right Upper Arm Bone", "Right Forearm Bone"),
        new("beautifulFrog 1_7", "Left Leg", 0, "Left Thigh Bone", "Left Knee Bone", "Left Shin Bone", "Left Ankle Bone", "Left Foot Bone"),
        new("beautifulFrog 1_8", "Right Leg", 0, "Right Thigh Bone", "Right Knee Bone", "Right Shin Bone", "Right Ankle Bone", "Right Foot Bone")
    };

    private static readonly Vector2[] FrogAssemblyPositions =
    {
        new(-2.8f, 1.425f),
        Vector2.zero,
        new(-1.64f, -0.1f),
        new(2.03f, 0.11f),
        new(-0.74f, -4f),
        new(1.12f, -3.97f)
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

    [MenuItem("Tools/Bullet Foundry/Rebuild Frog Princess Prefab")]
    public static void RebuildFrogPrincessPrefab()
    {
        AssetDatabase.DeleteAsset(FrogPrefabPath);
        CreateFrogPrincessPrefab(true);
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
        if (existing != null && HasExpectedPigHybridHierarchy(
                existing,
                PigParts,
                PigRigSchema))
        {
            ConfigureExistingPigPrefab(controller);
            return;
        }

        GameObject root = BuildPigHybridRoot(
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
            StampPrefabUserData(PigPrefabPath, PigRigSchema);
            AssetDatabase.SaveAssets();
            Debug.Log("PigLeader prefab rebuilt with Unity SpriteSkin bones plus stable visible cutout sprites.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Bullet Foundry/Rebuild Pig Leader Prefab")]
    public static void RebuildPigLeaderPrefab()
    {
        AssetDatabase.DeleteAsset(PigPrefabPath);
        CreatePigLeaderPrefab();
    }

    private static void CreateFrogPrincessPrefab(bool forceRebuild = false)
    {
        AnimatorController controller = EnsureController(
            FrogControllerPath,
            "frogprincess_walk",
            "frogprincess_attack",
            "frogprincess_die");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FrogPrefabPath);
        if (!forceRebuild && existing != null && HasExpectedWeightedFrogHierarchy(existing))
        {
            // Preserve manual rig, animation and transform edits after the prefab
            // has been created. Rebuilding is now an explicit menu action only.
            return;
        }

        GameObject root = BuildFrogRiggedRoot(
            "FrogPrincess",
            FrogSpritePath,
            FrogParts,
            2.35f);
        if (root == null)
        {
            Debug.LogWarning("FrogPrincess prefab rebuild stopped because the weighted prefab hierarchy could not be built.");
            return;
        }

        try
        {
            Transform body = root.transform.Find("Body");
            Transform bodyAttachmentBone = FindDescendant(body, "Torso Bone") ?? body;
            Transform tongueTip = FindDescendant(body, "Tongue Tip");
            Transform tongueOrigin = new GameObject("Tongue Origin").transform;
            tongueOrigin.SetParent(bodyAttachmentBone, false);
            tongueOrigin.position = tongueTip.position;

            Sprite tongueLineSprite = AssetDatabase.LoadAllAssetsAtPath(TongueLineSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault(sprite => sprite.name == "UI2_0");
            GameObject tongueLineObject = new("Tongue Line");
            tongueLineObject.transform.SetParent(bodyAttachmentBone, false);
            tongueLineObject.transform.position = tongueOrigin.position;
            SpriteRenderer tongueLine = tongueLineObject.AddComponent<SpriteRenderer>();
            tongueLine.sprite = tongueLineSprite;
            tongueLine.sortingOrder = 2;
            tongueLine.enabled = false;

            tongueTip.gameObject.SetActive(true);

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
            StampPrefabUserData(FrogPrefabPath, FrogRigSchema);
            AssetDatabase.SaveAssets();
            Debug.Log("FrogPrincess prefab rebuilt with Unity SpriteSkin bones, no hearts, visible tongue tip, and scalable tongue line.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject BuildFrogRigidRoot(
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
        if (sprites.Length != FrogAssemblyPositions.Length ||
            sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            string missingSprites = string.Join(
                ", ",
                definitions
                    .Where((definition, index) => sprites[index] == null)
                    .Select(definition => definition.SpriteName));
            string missingBones = string.Join(
                ", ",
                definitions
                    .Where((definition, index) => sprites[index] != null && sprites[index].GetBones().Length == 0)
                    .Select(definition => definition.SpriteName));
            Debug.LogWarning(
                $"{rootName} prefab was not created because its rigid sprite data is incomplete. Missing sprites: [{missingSprites}], missing bones: [{missingBones}]");
            return null;
        }

        int bodyIndex = Array.FindIndex(
            definitions,
            definition => definition.ObjectName == "Body");
        GameObject root = new(rootName);
        RigidPart[] parts = new RigidPart[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            parts[i] = CreateRigidPart(
                root.transform,
                definitions[i],
                sprites[i],
                FrogAssemblyPositions[i]);
        }

        Transform bodyAttachmentBone = parts[bodyIndex].RootBone ?? parts[bodyIndex].Part;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i != bodyIndex)
            {
                parts[i].Part.SetParent(bodyAttachmentBone, true);
            }
        }

        ScaleVisibleRootToTargetHeight(root.transform, targetHeight);
        return root;
    }

    private static void ScaleVisibleRootToTargetHeight(Transform root, float targetHeight)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(false)
            .Where(renderer => renderer.enabled && renderer.gameObject.name != "Tongue Tip Visual")
            .ToArray();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float scale = bounds.size.y > 0f ? targetHeight / bounds.size.y : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static GameObject BuildFrogRiggedRoot(
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
        if (sprites.Length != FrogAssemblyPositions.Length ||
            sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            string missingSprites = string.Join(
                ", ",
                definitions
                    .Where((definition, index) => sprites[index] == null)
                    .Select(definition => definition.SpriteName));
            string missingBones = string.Join(
                ", ",
                definitions
                    .Where((definition, index) => sprites[index] != null && sprites[index].GetBones().Length == 0)
                    .Select(definition => definition.SpriteName));
            Debug.LogWarning(
                $"{rootName} prefab was not created because its weighted sprite data is incomplete. Missing sprites: [{missingSprites}], missing bones: [{missingBones}]");
            return null;
        }

        int bodyIndex = Array.FindIndex(
            definitions,
            definition => definition.ObjectName == "Body");
        GameObject root = new(rootName);
        Transform[] parts = new Transform[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            parts[i] = CreateRiggedPart(
                root.transform,
                definitions[i],
                sprites[i],
                FrogAssemblyPositions[i]);
        }

        Transform body = parts[bodyIndex];
        Transform bodyAttachmentBone =
            body.Find(definitions[bodyIndex].BoneNames[0]) ?? body;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i != bodyIndex)
            {
                parts[i].SetParent(bodyAttachmentBone, true);
            }
        }

        ScaleFrogToTargetHeight(root.transform, targetHeight);
        return root;
    }

    private static void ScaleFrogToTargetHeight(Transform root, float targetHeight)
    {
        string[] visiblePartNames =
        {
            "Body",
            "Left Arm",
            "Right Arm",
            "Left Leg",
            "Right Leg"
        };
        SpriteRenderer[] renderers = visiblePartNames
            .Select(name => FindDescendant(root, name)?.GetComponent<SpriteRenderer>())
            .Where(renderer => renderer != null)
            .ToArray();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float scale = bounds.size.y > 0f ? targetHeight / bounds.size.y : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static GameObject BuildRigidRoot(
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
        RigidPart[] parts = new RigidPart[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            Vector2 position = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
            parts[i] = CreateRigidPart(
                root.transform,
                definitions[i],
                sprites[i],
                position);
        }

        Transform bodyAttachmentBone = parts[bodyIndex].RootBone ?? parts[bodyIndex].Part;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i != bodyIndex)
            {
                parts[i].Part.SetParent(bodyAttachmentBone, true);
            }
        }

        return root;
    }

    private static GameObject BuildPigHybridRoot(
        string rootName,
        string spritePath,
        PartDefinition[] definitions,
        float targetHeight)
    {
        GameObject root = BuildRiggedRoot(
            rootName,
            spritePath,
            definitions,
            targetHeight,
            true);
        if (root == null)
        {
            return null;
        }

        AddStableVisualsToRiggedParts(root.transform, definitions);
        return root;
    }

    private static void AddStableVisualsToRiggedParts(
        Transform root,
        PartDefinition[] definitions)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            Transform part = definitions[i].ObjectName == "Body"
                ? root.Find("Body")
                : FindDescendant(root, definitions[i].ObjectName);
            if (part == null)
            {
                continue;
            }

            SpriteRenderer sourceRenderer = part.GetComponent<SpriteRenderer>();
            if (sourceRenderer == null || sourceRenderer.sprite == null)
            {
                continue;
            }

            Transform rootBone = FindDescendant(part, definitions[i].BoneNames[0]) ?? part;
            string visualName = $"{definitions[i].ObjectName} Visual";
            Transform existingVisual = part.Find(visualName);
            if (existingVisual != null)
            {
                UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);
            }

            GameObject visualObject = new(visualName);
            Transform visual = visualObject.transform;
            visual.SetParent(part, false);
            SpriteRenderer visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = sourceRenderer.sprite;
            visualRenderer.sortingOrder = definitions[i].SortingOrder;
            visualRenderer.color = Color.white;
            visual.SetParent(rootBone, true);

            // Keep SpriteSkin enabled so Unity's 2D Animation bone overlay and
            // animation workflow still work, but use the bone-mounted visual as
            // the reliable visible cutout sprite. PigLeader's current skin data
            // can disappear in the editor even though the renderer has bounds.
            Color hiddenSkinColor = sourceRenderer.color;
            hiddenSkinColor.a = 0f;
            sourceRenderer.color = hiddenSkinColor;
        }
    }

    private static RigidPart CreateRigidPart(
        Transform parent,
        PartDefinition definition,
        Sprite sprite,
        Vector2 localPosition)
    {
        Transform part = new GameObject(definition.ObjectName).transform;
        part.SetParent(parent, false);
        part.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

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

        GameObject visualObject = new($"{definition.ObjectName} Visual");
        Transform visual = visualObject.transform;
        visual.SetParent(part, false);
        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = definition.SortingOrder;

        // The source art currently has invalid zero/non-normalized skin weights.
        // Parenting the intact sprite to its root bone keeps cutout animation stable
        // while still allowing the body bone to carry every limb.
        if (rootBone != null)
        {
            visual.SetParent(rootBone, true);
        }

        return new RigidPart(part, rootBone);
    }

    private static GameObject BuildRiggedRoot(
        string rootName,
        string spritePath,
        PartDefinition[] definitions,
        float targetHeight,
        bool attachPartsToBodyBone)
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
        Transform bodyAttachmentBone = attachPartsToBodyBone
            ? body.Find(definitions[bodyIndex].BoneNames[0]) ?? body
            : body;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i != bodyIndex)
            {
                parts[i].SetParent(bodyAttachmentBone, true);
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
        serialized.FindProperty("coinDropValue").intValue = 5;

        GameObject coinPickupObject = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPickupPrefabPath);
        CoinPickup coinPickup = coinPickupObject != null
            ? coinPickupObject.GetComponent<CoinPickup>()
            : null;
        if (coinPickup != null)
        {
            serialized.FindProperty("coinPickupPrefab").objectReferenceValue = coinPickup;

            SpriteRenderer coinRenderer = coinPickup.GetComponent<SpriteRenderer>();
            if (coinRenderer != null)
            {
                serialized.FindProperty("coinPickupSprite").objectReferenceValue = coinRenderer.sprite;
            }
        }

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
                FindDescendant(body, "Tongue Origin"),
                FindDescendant(body, "Tongue Tip"),
                FindDescendant(body, "Tongue Line").GetComponent<SpriteRenderer>());
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

    private static bool HasExpectedHierarchy(
        GameObject prefab,
        string spritePath,
        PartDefinition[] definitions,
        bool requireBodyBoneAttachment)
    {
        Transform body = prefab.transform.Find("Body");
        if (body == null ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != definitions.Length)
        {
            return false;
        }

        if (requireBodyBoneAttachment && !HasCurrentRigSignature(prefab, spritePath))
        {
            return false;
        }

        Sprite[] sourceSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();
        for (int i = 0; i < definitions.Length; i++)
        {
            Transform part = definitions[i].ObjectName == "Body"
                ? body
                : FindDescendant(body, definitions[i].ObjectName);
            SpriteSkin skin = part != null ? part.GetComponent<SpriteSkin>() : null;
            Sprite sourceSprite = sourceSprites.FirstOrDefault(sprite =>
                sprite.name == definitions[i].SpriteName);
            if (skin == null || sourceSprite == null)
            {
                return false;
            }

            SerializedProperty bones = new SerializedObject(skin)
                .FindProperty("m_BoneTransforms");
            if (bones == null || bones.arraySize != sourceSprite.GetBones().Length)
            {
                return false;
            }
        }

        if (!requireBodyBoneAttachment)
        {
            return true;
        }

        Transform attachmentBone = body.Find(definitions.First(definition =>
            definition.ObjectName == "Body").BoneNames[0]);
        return attachmentBone != null &&
            FindDescendant(body, "Left Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Right Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Left Leg")?.parent == attachmentBone &&
            FindDescendant(body, "Right Leg")?.parent == attachmentBone;
    }

    private static bool HasExpectedRigidHierarchy(
        GameObject prefab,
        PartDefinition[] definitions,
        string rigSchema)
    {
        Transform body = prefab.transform.Find("Body");
        if (body == null ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != 0 ||
            !HasPrefabUserData(prefab, rigSchema))
        {
            return false;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            Transform part = definitions[i].ObjectName == "Body"
                ? body
                : FindDescendant(body, definitions[i].ObjectName);
            if (part == null || FindDescendant(part, $"{definitions[i].ObjectName} Visual") == null)
            {
                return false;
            }
        }

        Transform attachmentBone = FindDescendant(body, "Torso Bone");
        return attachmentBone != null &&
            FindDescendant(body, "Left Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Right Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Left Leg")?.parent == attachmentBone &&
            FindDescendant(body, "Right Leg")?.parent == attachmentBone;
    }

    private static bool HasExpectedPigHybridHierarchy(
        GameObject prefab,
        PartDefinition[] definitions,
        string rigSchema)
    {
        Transform body = prefab.transform.Find("Body");
        if (body == null ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != definitions.Length ||
            !HasPrefabUserData(prefab, rigSchema))
        {
            return false;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            Transform part = definitions[i].ObjectName == "Body"
                ? body
                : FindDescendant(body, definitions[i].ObjectName);
            if (part == null ||
                part.GetComponent<SpriteSkin>() == null ||
                FindDescendant(part, $"{definitions[i].ObjectName} Visual") == null)
            {
                return false;
            }
        }

        Transform attachmentBone = FindDescendant(body, "Torso Bone");
        return attachmentBone != null &&
            FindDescendant(body, "Left Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Right Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Left Leg")?.parent == attachmentBone &&
            FindDescendant(body, "Right Leg")?.parent == attachmentBone;
    }

    private static bool HasExpectedWeightedHierarchy(
        GameObject prefab,
        string spritePath,
        PartDefinition[] definitions,
        string rigSchema)
    {
        Transform body = prefab.transform.Find("Body");
        if (body == null ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != definitions.Length ||
            !HasPrefabUserData(prefab, rigSchema))
        {
            return false;
        }

        Sprite[] sourceSprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .ToArray();
        for (int i = 0; i < definitions.Length; i++)
        {
            Transform part = definitions[i].ObjectName == "Body"
                ? body
                : FindDescendant(body, definitions[i].ObjectName);
            SpriteSkin skin = part != null ? part.GetComponent<SpriteSkin>() : null;
            Sprite sourceSprite = sourceSprites.FirstOrDefault(sprite =>
                sprite.name == definitions[i].SpriteName);
            if (skin == null || sourceSprite == null)
            {
                return false;
            }

            SerializedProperty bones = new SerializedObject(skin)
                .FindProperty("m_BoneTransforms");
            if (bones == null || bones.arraySize != sourceSprite.GetBones().Length)
            {
                return false;
            }
        }

        PartDefinition bodyDefinition = definitions.First(definition =>
            definition.ObjectName == "Body");
        Transform attachmentBone = FindDescendant(body, bodyDefinition.BoneNames[0]);
        if (attachmentBone == null)
        {
            return false;
        }

        return definitions
            .Where(definition => definition.ObjectName != "Body")
            .All(definition => FindDescendant(body, definition.ObjectName)?.parent == attachmentBone);
    }

    private static bool HasExpectedWeightedFrogHierarchy(GameObject prefab)
    {
        Transform body = prefab.transform.Find("Body");
        if (body == null ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != FrogParts.Length ||
            !HasPrefabUserData(prefab, FrogRigSchema))
        {
            return false;
        }

        Sprite[] sourceSprites = AssetDatabase.LoadAllAssetsAtPath(FrogSpritePath)
            .OfType<Sprite>()
            .ToArray();
        for (int i = 0; i < FrogParts.Length; i++)
        {
            Transform part = FrogParts[i].ObjectName == "Body"
                ? body
                : FindDescendant(body, FrogParts[i].ObjectName);
            SpriteSkin skin = part != null ? part.GetComponent<SpriteSkin>() : null;
            Sprite sourceSprite = sourceSprites.FirstOrDefault(sprite =>
                sprite.name == FrogParts[i].SpriteName);
            if (skin == null || sourceSprite == null)
            {
                return false;
            }

            SerializedProperty bones = new SerializedObject(skin)
                .FindProperty("m_BoneTransforms");
            if (bones == null || bones.arraySize != sourceSprite.GetBones().Length)
            {
                return false;
            }
        }

        Transform attachmentBone = FindDescendant(body, "Torso Bone");
        return attachmentBone != null &&
            FindDescendant(body, "Left Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Right Arm")?.parent == attachmentBone &&
            FindDescendant(body, "Left Leg")?.parent == attachmentBone &&
            FindDescendant(body, "Right Leg")?.parent == attachmentBone;
    }

    private static void SetFrogEffectPartActive(Transform body, string name, bool active)
    {
        Transform part = FindDescendant(body, name);
        if (part != null)
        {
            part.gameObject.SetActive(active);
        }
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

    private static bool HasCurrentRigSignature(GameObject prefab, string spritePath)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
        return importer != null &&
            importer.userData == BuildRigSignature(spritePath);
    }

    private static void StampRigSignature(string prefabPath, string spritePath)
    {
        AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
        if (importer == null)
        {
            return;
        }

        importer.userData = BuildRigSignature(spritePath);
        EditorUtility.SetDirty(importer);
        AssetDatabase.WriteImportSettingsIfDirty(prefabPath);
    }

    private static string BuildRigSignature(string spritePath)
    {
        return RigSignaturePrefix + AssetDatabase.GetAssetDependencyHash(spritePath);
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

    private readonly struct RigidPart
    {
        public RigidPart(Transform part, Transform rootBone)
        {
            Part = part;
            RootBone = rootBone;
        }

        public Transform Part { get; }
        public Transform RootBone { get; }
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
