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
    private const string ChickenRigSchema = "BulletFoundrySkinnedChickenClean:v7";

    private static readonly ChickenPart[] Parts =
    {
        new("Chichen_0", "Body", 1),
        new("Chichen_1", "Poop", 2)
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
        if (existing != null && HasExpectedPrefab(existing))
        {
            ConfigureEnemy(existing, controller);
            return;
        }

        GameObject chicken = BuildChicken(controller);
        if (chicken == null)
        {
            return;
        }

        try
        {
            PrefabUtility.SaveAsPrefabAsset(chicken, ChickenPrefabPath);
            StampPrefabUserData(ChickenPrefabPath, ChickenRigSchema);
            AssetDatabase.SaveAssets();
            Debug.Log("Chicken prefab rebuilt from Chichen.png SpriteSkin data.");
        }
        finally
        {
            Object.DestroyImmediate(chicken);
        }
    }

    private static GameObject BuildChicken(AnimatorController controller)
    {
        Sprite[] sprites = LoadSprites();
        if (sprites.Any(sprite => sprite == null || sprite.GetBones().Length == 0))
        {
            Debug.LogWarning("Chicken prefab was not created because Chichen.png sprite or bone data is incomplete.");
            return null;
        }

        GameObject root = new("Chicken");
        SetRootScale(root.transform, sprites[0], 1.55f);

        Vector2 bodyCenter = sprites[0].rect.center;
        float pixelsPerUnit = Mathf.Max(1f, sprites[0].pixelsPerUnit);

        for (int i = 0; i < Parts.Length; i++)
        {
            Vector2 partOffset = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
            CreateSkinnedPart(root.transform, Parts[i], sprites[i], partOffset);
        }

        Transform poop = root.transform.Find("Poop");
        if (poop != null)
        {
            poop.gameObject.SetActive(false);
        }

        AddEnemyComponents(root, controller);
        FitHitbox(root);
        return root;
    }

    private static void CreateSkinnedPart(
        Transform parent,
        ChickenPart partDefinition,
        Sprite sprite,
        Vector2 localPosition)
    {
        GameObject partObject = new(partDefinition.ObjectName);
        Transform part = partObject.transform;
        part.SetParent(parent, false);
        part.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = partDefinition.SortingOrder;
        renderer.color = Color.white;

        Transform rigRoot = new GameObject($"{partDefinition.ObjectName} Rig Root").transform;
        rigRoot.SetParent(part, false);
        rigRoot.localPosition = Vector3.zero;
        rigRoot.localRotation = Quaternion.identity;
        rigRoot.localScale = Vector3.one;

        SpriteBone[] spriteBones = sprite.GetBones();
        Transform[] boneTransforms = new Transform[spriteBones.Length];
        for (int i = 0; i < spriteBones.Length; i++)
        {
            CreateBone(i, spriteBones, boneTransforms, rigRoot);
        }

        SpriteSkin skin = partObject.AddComponent<SpriteSkin>();
        skin.SetRootBone(rigRoot);
        skin.SetBoneTransforms(boneTransforms);
        skin.alwaysUpdate = true;
    }

    private static void CreateBone(
        int index,
        SpriteBone[] spriteBones,
        Transform[] boneTransforms,
        Transform rigRoot)
    {
        if (boneTransforms[index] != null)
        {
            return;
        }

        SpriteBone spriteBone = spriteBones[index];
        if (spriteBone.parentId >= 0)
        {
            CreateBone(spriteBone.parentId, spriteBones, boneTransforms, rigRoot);
        }

        GameObject boneObject = new(string.IsNullOrWhiteSpace(spriteBone.name)
            ? $"{rigRoot.parent.name} Bone {index + 1}"
            : spriteBone.name);
        Transform bone = boneObject.transform;
        bone.SetParent(spriteBone.parentId >= 0 ? boneTransforms[spriteBone.parentId] : rigRoot, false);
        bone.localPosition = spriteBone.position;
        bone.localRotation = spriteBone.rotation;
        bone.localScale = Vector3.one;
        boneTransforms[index] = bone;
    }

    private static void AddEnemyComponents(GameObject root, AnimatorController controller)
    {
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;

        BoxCollider2D hitbox = root.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;

        GoblinEnemy enemy = root.AddComponent<GoblinEnemy>();
        ConfigureGoblinEnemy(enemy);
    }

    private static void ConfigureEnemy(GameObject prefab, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ChickenPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            Rigidbody2D body = root.GetComponent<Rigidbody2D>() ?? root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            BoxCollider2D hitbox = root.GetComponent<BoxCollider2D>() ?? root.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;

            GoblinEnemy enemy = root.GetComponent<GoblinEnemy>() ?? root.AddComponent<GoblinEnemy>();
            ConfigureGoblinEnemy(enemy);
            FitHitbox(root);
            PrefabUtility.SaveAsPrefabAsset(root, ChickenPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureGoblinEnemy(GoblinEnemy enemy)
    {
        SerializedObject serialized = new(enemy);
        serialized.FindProperty("maxHealth").intValue = 12;
        serialized.FindProperty("moveSpeed").floatValue = 1.15f;
        serialized.FindProperty("contactDamage").intValue = 1;
        serialized.FindProperty("attackCooldown").floatValue = 1.2f;
        serialized.FindProperty("walkStateName").stringValue = "chicken_walk";
        serialized.FindProperty("attackStateName").stringValue = "chicken_attack";
        serialized.FindProperty("dieStateName").stringValue = "chicken_die";
        serialized.FindProperty("destroyDelayAfterDeath").floatValue = 0.8f;
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
        AnimatorState defaultState = null;
        for (int i = 0; i < stateNames.Length; i++)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateNames[i]);
            if (state == null)
            {
                state = stateMachine.AddState(
                    stateNames[i],
                    new Vector3(220f + (i % 2 * 230f), i / 2 * 100f, 0f));
            }

            defaultState ??= state;
        }

        stateMachine.defaultState = defaultState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static bool HasExpectedPrefab(GameObject prefab)
    {
        if (prefab == null ||
            prefab.name != "Chicken" ||
            prefab.GetComponentsInChildren<SpriteSkin>(true).Length != Parts.Length ||
            !HasPrefabUserData(prefab, ChickenRigSchema))
        {
            return false;
        }

        Sprite[] sprites = LoadSprites();
        for (int i = 0; i < Parts.Length; i++)
        {
            Transform part = FindDescendant(prefab.transform, Parts[i].ObjectName);
            SpriteSkin skin = part != null ? part.GetComponent<SpriteSkin>() : null;
            Sprite sourceSprite = i < sprites.Length ? sprites[i] : null;
            SerializedProperty bonesProperty = skin != null
                ? new SerializedObject(skin).FindProperty("m_BoneTransforms")
                : null;

            if (part == null ||
                skin == null ||
                sourceSprite == null ||
                bonesProperty == null ||
                bonesProperty.arraySize != sourceSprite.GetBones().Length ||
                part.Find($"{Parts[i].ObjectName} Visual") != null)
            {
                return false;
            }
        }

        return true;
    }

    private static Sprite[] LoadSprites()
    {
        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(ChickenSpritePath)
            .OfType<Sprite>()
            .ToArray();

        return Parts
            .Select(part => allSprites.FirstOrDefault(sprite => sprite.name == part.SpriteName))
            .ToArray();
    }

    private static void SetRootScale(Transform root, Sprite bodySprite, float targetHeight)
    {
        float bodyHeight = bodySprite.rect.height / Mathf.Max(1f, bodySprite.pixelsPerUnit);
        float scale = bodyHeight > 0f ? targetHeight / bodyHeight : 1f;
        root.localScale = new Vector3(scale, scale, 1f);
    }

    private static void FitHitbox(GameObject root)
    {
        BoxCollider2D hitbox = root.GetComponent<BoxCollider2D>();
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(false)
            .Where(renderer => renderer.enabled && renderer.color.a > 0f)
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

    private static Transform FindDescendant(Transform root, string objectName)
    {
        return root == null
            ? null
            : root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
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

    private readonly struct ChickenPart
    {
        public ChickenPart(string spriteName, string objectName, int sortingOrder)
        {
            SpriteName = spriteName;
            ObjectName = objectName;
            SortingOrder = sortingOrder;
        }

        public string SpriteName { get; }

        public string ObjectName { get; }

        public int SortingOrder { get; }
    }
}
