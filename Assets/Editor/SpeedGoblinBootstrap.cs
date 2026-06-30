using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

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
    private const string NormalGoblinPrefabPath = "Assets/Prefab/Goblin.prefab";
    private const float TargetHeight = 2.2f;
    private const int RequiredSkinCount = 6;

    private static readonly PartDefinition[] Parts =
    {
        new PartDefinition(
            "speedGoblin_0",
            "Body",
            1,
            "Torso Bone",
            "Head Bone",
            "Head Tip Bone"),
        new PartDefinition(
            "speedGoblin_1",
            "Hat",
            3,
            "Hat Bone"),
        new PartDefinition(
            "speedGoblin_2",
            "Left Arm",
            2,
            "Left Upper Arm Bone",
            "Left Forearm Bone"),
        new PartDefinition(
            "speedGoblin_3",
            "Right Arm",
            2,
            "Right Upper Arm Bone",
            "Right Forearm Bone"),
        new PartDefinition(
            "speedGoblin_4",
            "Left Leg",
            0,
            "Left Upper Leg Bone",
            "Left Lower Leg Bone"),
        new PartDefinition(
            "speedGoblin_5",
            "Right Leg",
            0,
            "Right Upper Leg Bone",
            "Right Lower Leg Bone")
    };

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

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null && HasExpectedHierarchy(existingPrefab))
        {
            AssignControllerToExistingPrefab(speedController);
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
                "Speed Goblin prefab was not rebuilt because one or more required sprites have no bone data.");
            return;
        }

        GameObject root = new GameObject("SpeedGoblin");
        try
        {
            ConfigureRootScale(root.transform, sprites);

            Vector2 bodyCenter = sprites[0].rect.center;
            float pixelsPerUnit = Mathf.Max(1f, sprites[0].pixelsPerUnit);
            RiggedPart[] riggedParts = new RiggedPart[Parts.Length];

            for (int i = 0; i < Parts.Length; i++)
            {
                Vector2 position = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
                riggedParts[i] = CreateRiggedPart(root.transform, Parts[i], sprites[i], position);
            }

            Transform body = riggedParts[0].Root;
            for (int i = 2; i < riggedParts.Length; i++)
            {
                riggedParts[i].Root.SetParent(body, true);
            }

            Transform headBone = riggedParts[0].Bones.Length > 1
                ? riggedParts[0].Bones[1]
                : riggedParts[0].Bones[0];
            riggedParts[1].Root.SetParent(headBone, true);

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
            ConfigureSpeedEnemy(speedEnemy, enemy, riggedParts[1].Root.gameObject);

            FitHitbox(root.transform, hitbox);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Speed Goblin prefab rebuilt from the rigged speedGoblin sprites.");
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

        return new RiggedPart(part, boneTransforms);
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

    private static void ConfigureSpeedEnemy(
        SpeedGoblinEnemy speedEnemy,
        GoblinEnemy enemy,
        GameObject hat)
    {
        SerializedObject serializedSpeedEnemy = new SerializedObject(speedEnemy);
        serializedSpeedEnemy.FindProperty("enemy").objectReferenceValue = enemy;
        serializedSpeedEnemy.FindProperty("hatObject").objectReferenceValue = hat;
        serializedSpeedEnemy.FindProperty("normalGoblinPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(NormalGoblinPrefabPath);
        serializedSpeedEnemy.FindProperty("speedGoblinHealth").intValue = 5;
        serializedSpeedEnemy.FindProperty("hattedMoveSpeed").floatValue = 1.8f;
        serializedSpeedEnemy.ApplyModifiedPropertiesWithoutUndo();
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
        MoveLimbAnimationBindingsUnderBody(walkClip);
        MoveLimbAnimationBindingsUnderBody(attackClip);
        MoveLimbAnimationBindingsUnderBody(dieClip);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        SetStateMotion(stateMachine, "goblin_walk", walkClip);
        SetStateMotion(stateMachine, "goblin_attack", attackClip);
        SetStateMotion(stateMachine, "goblin_die", dieClip);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void MoveLimbAnimationBindingsUnderBody(AnimationClip clip)
    {
        EditorCurveBinding[] floatBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding oldBinding in floatBindings)
        {
            string newPath = GetBodyChildPath(oldBinding.path);
            if (newPath == oldBinding.path)
            {
                continue;
            }

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, oldBinding);
            EditorCurveBinding newBinding = oldBinding;
            newBinding.path = newPath;
            AnimationUtility.SetEditorCurve(clip, oldBinding, null);
            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
        }

        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (EditorCurveBinding oldBinding in objectBindings)
        {
            string newPath = GetBodyChildPath(oldBinding.path);
            if (newPath == oldBinding.path)
            {
                continue;
            }

            ObjectReferenceKeyframe[] curve =
                AnimationUtility.GetObjectReferenceCurve(clip, oldBinding);
            EditorCurveBinding newBinding = oldBinding;
            newBinding.path = newPath;
            AnimationUtility.SetObjectReferenceCurve(clip, oldBinding, null);
            AnimationUtility.SetObjectReferenceCurve(clip, newBinding, curve);
        }

        EditorUtility.SetDirty(clip);
    }

    private static string GetBodyChildPath(string path)
    {
        string[] limbRoots = { "Left Arm", "Right Arm", "Left Leg", "Right Leg" };
        foreach (string limbRoot in limbRoots)
        {
            if (path == limbRoot || path.StartsWith(limbRoot + "/", StringComparison.Ordinal))
            {
                return "Body/" + path;
            }
        }

        return path;
    }

    private static bool HasExpectedHierarchy(GameObject prefab)
    {
        if (prefab.GetComponentsInChildren<SpriteSkin>(true).Length < RequiredSkinCount)
        {
            return false;
        }

        Transform body = prefab.transform.Find("Body");
        SpeedGoblinEnemy speedEnemy = prefab.GetComponent<SpeedGoblinEnemy>();
        SerializedObject serializedSpeedEnemy = speedEnemy != null
            ? new SerializedObject(speedEnemy)
            : null;
        SerializedProperty normalGoblinPrefab =
            serializedSpeedEnemy?.FindProperty("normalGoblinPrefab");
        return body != null &&
            body.Find("Left Arm") != null &&
            body.Find("Right Arm") != null &&
            body.Find("Left Leg") != null &&
            body.Find("Right Leg") != null &&
            normalGoblinPrefab != null &&
            normalGoblinPrefab.objectReferenceValue != null;
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
        public RiggedPart(Transform root, Transform[] bones)
        {
            Root = root;
            Bones = bones;
        }

        public Transform Root { get; }
        public Transform[] Bones { get; }
    }
}
