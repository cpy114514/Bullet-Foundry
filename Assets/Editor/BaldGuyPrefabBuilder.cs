using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>
/// Builds Assets/Prefab/BaldGuy.prefab from Assets/Image/BaldGuy.png.
///
/// The PNG is a sliced sprite sheet (BaldGuy_0..5) with bone data baked into
/// the TextureImporter. Each sub-sprite becomes a child body-part GameObject
/// with a SpriteRenderer + SpriteSkin and its own bone hierarchy. BaldGuy_0
/// is treated as the visual anchor — every other part is parented to it so
/// the whole character moves as one unit.
///
/// The created AnimatorController is intentionally empty — drop your
/// AnimationClips into it and you are ready to animate.
///
/// Run via menu: Tools > Bullet Foundry > Build BaldGuy Prefab.
///
/// NOTE: Sprite-to-sprite alignment follows the .meta rect positions
/// (BaldGuy_0 is the anchor, others offset by their rect-center delta).
/// If the resulting layout looks off, drag parts in the Scene view until
/// it composes correctly, then save the prefab.
/// </summary>
public static class BaldGuyPrefabBuilder
{
    private const string SpritePath = "Assets/Image/BaldGuy.png";
    private const string PrefabPath = "Assets/Prefab/BaldGuy.prefab";
    private const string ControllerPath = "Assets/Animation/BaldGuyController.controller";
    private const string BodySpriteName = "BaldGuy_0";
    private const string MenuPath = "Tools/Bullet Foundry/Build BaldGuy Prefab";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BaldGuyPrefabBuilder] Cannot build prefab while in play mode.");
            return;
        }

        // Make sure the texture is imported (catches the case where the menu
        // was opened right after pulling a fresh .meta).
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

        Sprite[] sprites = LoadBodyPartSprites();
        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        Sprite bodySprite = sprites.FirstOrDefault(sprite => sprite.name == BodySpriteName);
        if (bodySprite == null)
        {
            Debug.LogError(
                $"[BaldGuyPrefabBuilder] Body sprite '{BodySpriteName}' is missing from {SpritePath}. " +
                "Cannot anchor the character without it.");
            return;
        }

        if (sprites.Any(sprite => sprite.GetBones().Length == 0))
        {
            Debug.LogWarning(
                "[BaldGuyPrefabBuilder] One or more BaldGuy sprites have no bone data — " +
                "their SpriteSkin will not deform until the .meta is re-imported with bones.");
        }

        AnimatorController controller = EnsureController();

        GameObject root = new GameObject("BaldGuy");
        try
        {
            Vector2 bodyCenter = bodySprite.rect.center;
            float pixelsPerUnit = Mathf.Max(1f, bodySprite.pixelsPerUnit);

            // Build every body part as a direct child of the root first.
            RiggedPart[] parts = new RiggedPart[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                Vector2 localPosition = (sprites[i].rect.center - bodyCenter) / pixelsPerUnit;
                parts[i] = CreateRiggedPart(
                    root.transform,
                    sprites[i],
                    localPosition,
                    i);
            }

            // Re-parent everything (except the body) under the body, so the
            // whole character follows the body's transform.
            Transform bodyTransform = parts[0].Root;
            for (int i = 1; i < parts.Length; i++)
            {
                parts[i].Root.SetParent(bodyTransform, true);
            }

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[BaldGuyPrefabBuilder] Prefab saved → {PrefabPath} " +
                $"({sprites.Length} parts, controller = {ControllerPath})");
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null)
        {
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
    }

    private static Sprite[] LoadBodyPartSprites()
    {
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(SpritePath);
        if (allAssets == null || allAssets.Length == 0)
        {
            Debug.LogError(
                $"[BaldGuyPrefabBuilder] No assets loaded from {SpritePath}. " +
                "Confirm the file exists and the TextureImporter mode is 'Multiple'.");
            return null;
        }

        Sprite[] sprites = allAssets
            .OfType<Sprite>()
            .Where(sprite => sprite != null && sprite.name.StartsWith("BaldGuy_"))
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError(
                $"[BaldGuyPrefabBuilder] No sub-sprites named 'BaldGuy_*' found in {SpritePath}.");
        }

        return sprites;
    }

    private static AnimatorController EnsureController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            return controller;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
        {
            AssetDatabase.CreateFolder("Assets", "Animation");
        }

        return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
    }

    private static RiggedPart CreateRiggedPart(
        Transform parent,
        Sprite sprite,
        Vector2 localPosition,
        int sortingOrder)
    {
        GameObject partObject = new GameObject(sprite.name);
        Transform part = partObject.transform;
        part.SetParent(parent, false);
        part.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        SpriteBone[] spriteBones = sprite.GetBones();
        Transform[] boneTransforms = new Transform[spriteBones.Length];
        Transform rootBone = null;

        for (int i = 0; i < spriteBones.Length; i++)
        {
            CreateBoneRecursive(i, spriteBones, boneTransforms, part);
            if (spriteBones[i].parentId < 0 && rootBone == null)
            {
                rootBone = boneTransforms[i];
            }
        }

        SpriteSkin skin = partObject.AddComponent<SpriteSkin>();
        skin.SetRootBone(rootBone);
        skin.SetBoneTransforms(boneTransforms);

        return new RiggedPart(part);
    }

    private static void CreateBoneRecursive(
        int index,
        SpriteBone[] spriteBones,
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
            CreateBoneRecursive(spriteBone.parentId, spriteBones, transforms, partRoot);
        }

        GameObject boneObject = new GameObject($"Bone {index + 1}");
        Transform bone = boneObject.transform;
        bone.SetParent(
            spriteBone.parentId >= 0 ? transforms[spriteBone.parentId] : partRoot,
            false);
        bone.localPosition = spriteBone.position;
        bone.localRotation = spriteBone.rotation;
        bone.localScale = Vector3.one;
        transforms[index] = bone;
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
