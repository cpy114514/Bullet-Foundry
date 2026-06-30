using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class SpeedGoblinBootstrap
{
    private const string SpritePath = "Assets/Image/speedGoblin.png";
    private const string SourceControllerPath = "Assets/Animation/Goblin.controller";
    private const string SourceWalkClipPath = "Assets/Animation/goblin_walk.anim";
    private const string SourceAttackClipPath = "Assets/Animation/goblin_attack.anim";
    private const string SourceDieClipPath = "Assets/Animation/goblin_die.anim";
    private const string ControllerPath = "Assets/Animation/SpeedGoblin.controller";
    private const string WalkClipPath = "Assets/Animation/speed_goblin_walk.anim";
    private const string AttackClipPath = "Assets/Animation/speed_goblin_attack.anim";
    private const string DieClipPath = "Assets/Animation/speed_goblin_die.anim";
    private const string PrefabPath = "Assets/Prefab/SpeedGoblin.prefab";
    private const float TargetHeight = 2.2f;

    static SpeedGoblinBootstrap()
    {
        EditorApplication.delayCall += EnsureSpeedGoblinPrefab;
    }

    [MenuItem("Tools/Bullet Foundry/Create Speed Goblin Prefab")]
    public static void EnsureSpeedGoblinPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AnimatorController speedController = EnsureSeparateAnimationAssets();
        if (speedController == null)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            AssignControllerToExistingPrefab(speedController);
            return;
        }

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SpritePath)
            .OfType<Sprite>()
            .ToArray();

        Sprite bodySprite = FindSprite(sprites, "speedGoblin_0");
        Sprite hatSprite = FindSprite(sprites, "speedGoblin_1");
        Sprite leftArmSprite = FindSprite(sprites, "speedGoblin_2");
        Sprite rightArmSprite = FindSprite(sprites, "speedGoblin_3");
        Sprite leftLegSprite = FindSprite(sprites, "speedGoblin_4");
        Sprite rightLegSprite = FindSprite(sprites, "speedGoblin_5");

        if (bodySprite == null || hatSprite == null ||
            leftArmSprite == null || rightArmSprite == null ||
            leftLegSprite == null || rightLegSprite == null)
        {
            return;
        }

        GameObject root = new GameObject("SpeedGoblin");
        try
        {
            ConfigureRootScale(
                root.transform,
                new[]
                {
                    bodySprite,
                    hatSprite,
                    leftArmSprite,
                    rightArmSprite,
                    leftLegSprite,
                    rightLegSprite
                });
            Vector2 bodyCenter = bodySprite.rect.center;
            float pixelsPerUnit = bodySprite.pixelsPerUnit;

            Transform body = CreateTransform("Body", root.transform, Vector2.zero);
            Transform torsoBone = CreateTransform("Torso Bone", body, Vector2.zero);
            CreateVisual("Body Visual", torsoBone, bodySprite, Vector2.zero, -6.822f, 1);
            CreateTransform("Head Bone", torsoBone, Vector2.zero);

            Vector2 hatPosition = ToLocalPosition(hatSprite, bodyCenter, pixelsPerUnit);
            GameObject hat = CreateVisual("Hat", torsoBone, hatSprite, hatPosition, -6.822f, 2);

            CreateLimb(
                root.transform,
                "Left Arm",
                "Left Upper Arm Bone",
                "Left Forearm Bone",
                leftArmSprite,
                ToLocalPosition(leftArmSprite, bodyCenter, pixelsPerUnit),
                79.532f,
                2);

            CreateLimb(
                root.transform,
                "Right Arm",
                "Right Upper Arm Bone",
                "Right Forearm Bone",
                rightArmSprite,
                ToLocalPosition(rightArmSprite, bodyCenter, pixelsPerUnit),
                73.329f,
                2);

            CreateLimb(
                root.transform,
                "Left Leg",
                "Left Upper Leg Bone",
                "Left Lower Leg Bone",
                leftLegSprite,
                ToLocalPosition(leftLegSprite, bodyCenter, pixelsPerUnit),
                89.042f,
                0);

            CreateLimb(
                root.transform,
                "Right Leg",
                "Right Upper Leg Bone",
                "Right Lower Leg Bone",
                rightLegSprite,
                ToLocalPosition(rightLegSprite, bodyCenter, pixelsPerUnit),
                88.338f,
                0);

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = speedController;

            Rigidbody2D body2D = root.AddComponent<Rigidbody2D>();
            body2D.bodyType = RigidbodyType2D.Kinematic;
            body2D.gravityScale = 0f;
            body2D.freezeRotation = true;

            BoxCollider2D hitbox = root.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;

            GoblinEnemy enemy = root.AddComponent<GoblinEnemy>();
            SpeedGoblinEnemy speedEnemy = root.AddComponent<SpeedGoblinEnemy>();

            SerializedObject serializedSpeedEnemy = new SerializedObject(speedEnemy);
            serializedSpeedEnemy.FindProperty("enemy").objectReferenceValue = enemy;
            serializedSpeedEnemy.FindProperty("hatObject").objectReferenceValue = hat;
            serializedSpeedEnemy.FindProperty("hattedMoveSpeed").floatValue = 1.8f;
            serializedSpeedEnemy.FindProperty("unhattedMoveSpeed").floatValue = 1f;
            serializedSpeedEnemy.ApplyModifiedPropertiesWithoutUndo();

            FitHitbox(root.transform, hitbox);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static AnimatorController EnsureSeparateAnimationAssets()
    {
        EnsureAssetCopy(SourceWalkClipPath, WalkClipPath);
        EnsureAssetCopy(SourceAttackClipPath, AttackClipPath);
        EnsureAssetCopy(SourceDieClipPath, DieClipPath);
        EnsureAssetCopy(SourceControllerPath, ControllerPath);

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        AnimationClip dieClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DieClipPath);

        if (controller == null || walkClip == null || attackClip == null || dieClip == null)
        {
            return null;
        }

        SetClipLooping(walkClip, true);
        SetClipLooping(attackClip, true);
        SetClipLooping(dieClip, false);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        SetStateMotion(stateMachine, "goblin_walk", walkClip);
        SetStateMotion(stateMachine, "goblin_attack", attackClip);
        SetStateMotion(stateMachine, "goblin_die", dieClip);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void EnsureAssetCopy(string sourcePath, string destinationPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
        {
            return;
        }

        if (AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void SetStateMotion(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion)
    {
        AnimatorState state = stateMachine.states
            .Select(childState => childState.state)
            .FirstOrDefault(animatorState => animatorState.name == stateName);

        if (state != null && state.motion != motion)
        {
            state.motion = motion;
            EditorUtility.SetDirty(state);
        }
    }

    private static void SetClipLooping(AnimationClip clip, bool loopTime)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loopTime)
        {
            return;
        }

        settings.loopTime = loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void AssignControllerToExistingPrefab(AnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            Animator animator = prefabRoot.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureRootScale(Transform root, Sprite[] sprites)
    {
        float minY = sprites.Min(sprite => sprite.rect.yMin);
        float maxY = sprites.Max(sprite => sprite.rect.yMax);
        float pixelsPerUnit = sprites[0].pixelsPerUnit;
        float sourceHeight = (maxY - minY) / Mathf.Max(1f, pixelsPerUnit);
        float scale = sourceHeight > 0f ? TargetHeight / sourceHeight : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static void CreateLimb(
        Transform root,
        string limbName,
        string upperBoneName,
        string lowerBoneName,
        Sprite sprite,
        Vector2 position,
        float visualRotation,
        int sortingOrder)
    {
        Transform limb = CreateTransform(limbName, root, position);
        Transform upperBone = CreateTransform(upperBoneName, limb, Vector2.zero);
        CreateTransform(lowerBoneName, upperBone, Vector2.zero);
        CreateVisual(limbName + " Visual", upperBone, sprite, Vector2.zero, visualRotation, sortingOrder);
    }

    private static Transform CreateTransform(string name, Transform parent, Vector2 localPosition)
    {
        GameObject child = new GameObject(name);
        Transform transform = child.transform;
        transform.SetParent(parent, false);
        transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        return transform;
    }

    private static GameObject CreateVisual(
        string name,
        Transform parent,
        Sprite sprite,
        Vector2 localPosition,
        float localRotation,
        int sortingOrder)
    {
        GameObject visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, localRotation);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return visual;
    }

    private static Vector2 ToLocalPosition(Sprite sprite, Vector2 origin, float pixelsPerUnit)
    {
        return (sprite.rect.center - origin) / Mathf.Max(1f, pixelsPerUnit);
    }

    private static void FitHitbox(Transform root, BoxCollider2D hitbox)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        hitbox.offset = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        hitbox.size = new Vector2(
            scale.x != 0f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            scale.y != 0f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y);
    }

    private static Sprite FindSprite(Sprite[] sprites, string spriteName)
    {
        return sprites.FirstOrDefault(sprite =>
            string.Equals(sprite.name, spriteName, StringComparison.Ordinal));
    }
}
