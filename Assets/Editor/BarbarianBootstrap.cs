using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

[InitializeOnLoad]
public static class BarbarianBootstrap
{
    private const string SpritePath = "Assets/Image/Barbarian.png";
    private const string PrefabPath = "Assets/Prefab/Barbarian.prefab";
    private const string ControllerPath = "Assets/Animation/Barbarian.controller";
    private const string WalkClipPath = "Assets/Animation/barbarian_walk.anim";
    private const string AttackClipPath = "Assets/Animation/barbarian_attack.anim";
    private const string DieClipPath = "Assets/Animation/barbarian_die.anim";
    private const float TargetHeight = 2.4f;

    private static readonly PartDefinition[] Parts =
    {
        new PartDefinition(
            "Barbarian_0",
            "Left Arm",
            2,
            "Left Upper Arm Bone",
            "Left Forearm Bone"),
        new PartDefinition(
            "Barbarian_1",
            "Body",
            1,
            "Torso Bone",
            "Neck Bone",
            "Head Bone"),
        new PartDefinition(
            "Barbarian_2",
            "Right Arm",
            2,
            "Right Upper Arm Bone",
            "Right Forearm Bone"),
        new PartDefinition(
            "Barbarian_3",
            "Left Leg",
            0,
            "Left Upper Leg Bone",
            "Left Lower Leg Bone"),
        new PartDefinition(
            "Barbarian_4",
            "Right Leg",
            0,
            "Right Upper Leg Bone",
            "Right Lower Leg Bone")
    };

    static BarbarianBootstrap()
    {
        EditorApplication.delayCall += EnsureBarbarianPrefab;
    }

    [MenuItem("Tools/Bullet Foundry/Create Barbarian Prefab")]
    public static void EnsureBarbarianPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AnimatorController controller = SetupAnimatorController();
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null && HasExpectedHierarchy(existingPrefab))
        {
            ConfigureExistingPrefab(controller);
            return;
        }

        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(SpritePath)
            .OfType<Sprite>()
            .ToArray();
        Sprite[] sprites = Parts
            .Select(part => FindSprite(allSprites, part.SpriteName))
            .ToArray();

        if (sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            Debug.LogWarning(
                "Barbarian prefab was not created because one or more sprites have no bone data.");
            return;
        }

        GameObject root = new GameObject("Barbarian");
        try
        {
            ConfigureRootScale(root.transform, sprites);

            Vector2 bodyCenter = sprites[1].rect.center;
            float pixelsPerUnit = Mathf.Max(1f, sprites[1].pixelsPerUnit);
            RiggedPart[] riggedParts = new RiggedPart[Parts.Length];

            for (int i = 0; i < Parts.Length; i++)
            {
                Vector2 position = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
                riggedParts[i] = CreateRiggedPart(
                    root.transform,
                    Parts[i],
                    sprites[i],
                    position);
            }

            Transform bodyPart = riggedParts[1].Root;
            riggedParts[0].Root.SetParent(bodyPart, true);
            riggedParts[2].Root.SetParent(bodyPart, true);
            riggedParts[3].Root.SetParent(bodyPart, true);
            riggedParts[4].Root.SetParent(bodyPart, true);

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Rigidbody2D body2D = root.AddComponent<Rigidbody2D>();
            body2D.bodyType = RigidbodyType2D.Kinematic;
            body2D.gravityScale = 0f;
            body2D.freezeRotation = true;

            BoxCollider2D hitbox = root.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;

            GoblinEnemy enemy = root.AddComponent<GoblinEnemy>();
            ConfigureEnemy(enemy);
            FitHitbox(root.transform, hitbox);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Barbarian prefab created from the rigged Barbarian sprites.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static RiggedPart CreateRiggedPart(
        Transform parent,
        PartDefinition definition,
        Sprite sprite,
        Vector2 localPosition)
    {
        GameObject partObject = new GameObject(definition.ObjectName);
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

        return new RiggedPart(part);
    }

    private static void CreateBone(
        int index,
        SpriteBone[] spriteBones,
        string[] boneNames,
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
            CreateBone(spriteBone.parentId, spriteBones, boneNames, transforms, partRoot);
        }

        string boneName = index < boneNames.Length
            ? boneNames[index]
            : $"{partRoot.name} Bone {index + 1}";
        GameObject boneObject = new GameObject(boneName);
        Transform bone = boneObject.transform;
        bone.SetParent(
            spriteBone.parentId >= 0 ? transforms[spriteBone.parentId] : partRoot,
            false);
        bone.localPosition = spriteBone.position;
        bone.localRotation = spriteBone.rotation;
        bone.localScale = Vector3.one;
        transforms[index] = bone;
    }

    private static void ConfigureEnemy(GoblinEnemy enemy)
    {
        SerializedObject serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("maxHealth").intValue = 60;
        serializedEnemy.FindProperty("moveSpeed").floatValue = 0.65f;
        serializedEnemy.FindProperty("contactDamage").intValue = 4;
        serializedEnemy.FindProperty("attackCooldown").floatValue = 1f;
        serializedEnemy.FindProperty("walkStateName").stringValue = "barbarian_walk";
        serializedEnemy.FindProperty("attackStateName").stringValue = "barbarian_attack";
        serializedEnemy.FindProperty("dieStateName").stringValue = "barbarian_die";
        serializedEnemy.FindProperty("destroyDelayAfterDeath").floatValue = 1.3f;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AnimatorController SetupAnimatorController()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        AnimationClip dieClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DieClipPath);
        if (controller == null || walkClip == null || attackClip == null || dieClip == null)
        {
            return controller;
        }

        SetClipLooping(walkClip, true);
        SetClipLooping(attackClip, true);
        SetClipLooping(dieClip, false);

        if (controller.layers.Length == 0)
        {
            controller.AddLayer("Base Layer");
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState walkState = EnsureState(
            stateMachine,
            "barbarian_walk",
            walkClip,
            new Vector3(200f, 0f, 0f));
        EnsureState(
            stateMachine,
            "barbarian_attack",
            attackClip,
            new Vector3(430f, 0f, 0f));
        EnsureState(
            stateMachine,
            "barbarian_die",
            dieClip,
            new Vector3(430f, 120f, 0f));
        stateMachine.defaultState = walkState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = stateMachine.states
            .Select(childState => childState.state)
            .FirstOrDefault(candidate => candidate.name == stateName);
        if (state == null)
        {
            state = stateMachine.AddState(stateName, position);
        }

        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static void SetClipLooping(AnimationClip clip, bool shouldLoop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == shouldLoop)
        {
            return;
        }

        settings.loopTime = shouldLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void ConfigureExistingPrefab(AnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            GoblinEnemy enemy = prefabRoot.GetComponent<GoblinEnemy>();
            if (enemy != null)
            {
                ConfigureEnemy(enemy);
            }

            Animator animator = prefabRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabRoot.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool HasExpectedHierarchy(GameObject prefab)
    {
        Transform body = prefab.transform.Find("Body");
        return prefab.GetComponent<GoblinEnemy>() != null &&
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length == Parts.Length &&
            body != null &&
            body.Find("Left Arm") != null &&
            body.Find("Right Arm") != null &&
            body.Find("Left Leg") != null &&
            body.Find("Right Leg") != null;
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

    private static void FitHitbox(Transform root, BoxCollider2D hitbox)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
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

    private readonly struct RiggedPart
    {
        public RiggedPart(Transform root)
        {
            Root = root;
        }

        public Transform Root { get; }
    }
}
