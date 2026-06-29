using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class GoblinBootstrap
{
    private const string SessionKey = "BulletFoundry.GoblinBootstrap.Completed";
    private const string ControllerPath = "Assets/Animation/Goblin.controller";
    private const string WalkClipPath = "Assets/Animation/goblin_walk.anim";
    private const string AttackClipPath = "Assets/Animation/goblin_attack.anim";
    private const string DieClipPath = "Assets/Animation/goblin_die.anim";
    private const string GoblinPrefabPath = "Assets/Prefab/Goblin.prefab";

    static GoblinBootstrap()
    {
        EditorApplication.delayCall += SetupGoblin;
    }

    private static void SetupGoblin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SetupAnimatorController();
        SetupGoblinPrefab();

        SessionState.SetBool(SessionKey, true);
    }

    private static void SetupAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        AnimationClip dieClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DieClipPath);

        if (controller == null || walkClip == null || attackClip == null || dieClip == null)
        {
            return;
        }

        SetClipLooping(walkClip, true);
        SetClipLooping(attackClip, true);
        SetClipLooping(dieClip, false);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState walkState = EnsureState(stateMachine, "goblin_walk", walkClip, new Vector3(200f, 0f, 0f));
        EnsureState(stateMachine, "goblin_attack", attackClip, new Vector3(430f, 0f, 0f));
        EnsureState(stateMachine, "goblin_die", dieClip, new Vector3(430f, 120f, 0f));

        stateMachine.defaultState = walkState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        ChildAnimatorState childState = stateMachine.states
            .FirstOrDefault(state => state.state.name == stateName);

        AnimatorState animatorState;
        if (childState.state != null)
        {
            animatorState = childState.state;
        }
        else
        {
            animatorState = stateMachine.AddState(stateName, position);
        }

        animatorState.motion = motion;
        animatorState.writeDefaultValues = true;
        return animatorState;
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

    private static void SetupGoblinPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(GoblinPrefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            bool changed = false;

            if (prefabRoot.GetComponent<GoblinEnemy>() == null)
            {
                prefabRoot.AddComponent<GoblinEnemy>();
                changed = true;
            }

            Rigidbody2D body = prefabRoot.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = prefabRoot.AddComponent<Rigidbody2D>();
                changed = true;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            BoxCollider2D hitbox = prefabRoot.GetComponent<BoxCollider2D>();
            if (hitbox == null)
            {
                hitbox = prefabRoot.AddComponent<BoxCollider2D>();
                changed = true;
            }

            hitbox.isTrigger = true;
            Bounds bounds = CalculateRendererBounds(prefabRoot);
            if (bounds.size != Vector3.zero)
            {
                hitbox.offset = prefabRoot.transform.InverseTransformPoint(bounds.center);
                hitbox.size = ToLocalSize(prefabRoot.transform, bounds.size);
            }

            Animator animator = prefabRoot.GetComponent<Animator>();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (animator != null && controller != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, GoblinPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Vector2 ToLocalSize(Transform transform, Vector3 worldSize)
    {
        Vector3 scale = transform.lossyScale;
        return new Vector2(
            scale.x != 0f ? worldSize.x / Mathf.Abs(scale.x) : worldSize.x,
            scale.y != 0f ? worldSize.y / Mathf.Abs(scale.y) : worldSize.y);
    }
}
