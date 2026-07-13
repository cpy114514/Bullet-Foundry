using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinEnemy))]
public sealed class FrogPrincessEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private GoblinEnemy enemy;

    [SerializeField]
    private Transform tongueOrigin;

    [SerializeField]
    private Transform tongueTip;

    [SerializeField]
    private SpriteRenderer tongueLine;

    [Header("Ranged Attack")]
    [SerializeField, Min(0.1f)]
    private float attackRange = 4f;

    [SerializeField, Min(0.05f)]
    private float attackCooldown = 1.8f;

    [SerializeField, Min(1)]
    private int attackDamage = 2;

    [SerializeField, Min(0.01f)]
    private float extendDuration = 0.18f;

    [SerializeField, Min(0f)]
    private float holdDuration = 0.06f;

    [SerializeField, Min(0.01f)]
    private float retractDuration = 0.2f;

    [SerializeField]
    private string attackStateName = "frogprincess_attack";

    private float nextAttackTime;
    private bool isAttacking;
    private Vector3 tongueTipRestLocalPosition;
    private TowerHealth lockedTarget;

    private void Awake()
    {
        ResolveReferences();
        CacheRestPose();
        RestoreTonguePose();
        SetTongueRestVisible();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheRestPose();
        nextAttackTime = Time.time + 0.5f;
        isAttacking = false;
        lockedTarget = null;
        RestoreTonguePose();
        SetTongueRestVisible();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isAttacking = false;
        lockedTarget = null;
        RestoreTonguePose();
        SetTongueRestVisible();
    }

    private void Update()
    {
        if (enemy == null || enemy.IsActionBlocked || isAttacking || Time.time < nextAttackTime)
        {
            return;
        }

        TowerHealth target = GetAttackTarget();
        if (target != null)
        {
            StartCoroutine(AttackRoutine(target));
        }
    }

    private IEnumerator AttackRoutine(TowerHealth target)
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        float totalDuration = extendDuration + holdDuration + retractDuration;
        enemy.PlayTemporaryAction(attackStateName, totalDuration);

        Vector3 start = GetTongueOriginPosition();
        Vector3 end = GetTargetPoint(target, start);
        SetTongueAttackVisible();

        yield return AnimateTongue(start, end, extendDuration, true);

        if (!enemy.IsActionBlocked && target != null && !target.IsDestroyed)
        {
            target.TakeDamage(attackDamage);
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        yield return AnimateTongue(start, end, retractDuration, false);
        RestoreTonguePose();
        SetTongueRestVisible();
        isAttacking = false;
    }

    private TowerHealth GetAttackTarget()
    {
        if (IsValidLockedTarget(lockedTarget))
        {
            return lockedTarget;
        }

        lockedTarget = FindRangedTarget();
        return lockedTarget;
    }

    private static bool IsValidLockedTarget(TowerHealth target)
    {
        return target != null && !target.IsDestroyed && target.isActiveAndEnabled;
    }

    private IEnumerator AnimateTongue(
        Vector3 start,
        Vector3 end,
        float duration,
        bool extending)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float amount = extending ? progress : 1f - progress;
            UpdateTongueVisual(start, Vector3.Lerp(start, end, amount));
            yield return null;
        }

        UpdateTongueVisual(start, extending ? end : start);
    }

    private void UpdateTongueVisual(Vector3 start, Vector3 tipPosition)
    {
        if (tongueTip != null)
        {
            if (!tongueTip.gameObject.activeSelf)
            {
                tongueTip.gameObject.SetActive(true);
            }

            tongueTip.position = tipPosition;
        }

        if (tongueLine == null || tongueLine.sprite == null)
        {
            return;
        }

        Vector3 delta = tipPosition - start;
        float distance = delta.magnitude;
        Transform lineTransform = tongueLine.transform;
        lineTransform.position = start + (delta * 0.5f);
        if (distance > 0.001f)
        {
            lineTransform.up = delta.normalized;
        }

        float sourceLength = Mathf.Max(0.001f, tongueLine.sprite.bounds.size.y);
        float parentWorldScale = lineTransform.parent != null
            ? Mathf.Max(0.001f, Mathf.Abs(lineTransform.parent.lossyScale.y))
            : 1f;
        Vector3 lineScale = lineTransform.localScale;
        lineScale.y = distance / (sourceLength * parentWorldScale);
        lineTransform.localScale = lineScale;
    }

    private TowerHealth FindRangedTarget()
    {
        TowerHealth[] towers = FindObjectsByType<TowerHealth>(FindObjectsSortMode.None);
        Bounds enemyBounds = enemy.GetWorldBounds();
        TowerHealth closest = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < towers.Length; i++)
        {
            TowerHealth tower = towers[i];
            if (tower == null || tower.IsDestroyed || !tower.isActiveAndEnabled)
            {
                continue;
            }

            Bounds towerBounds = tower.GetWorldBounds();
            float distance = GetBoundsDistance(enemyBounds, towerBounds);

            if (distance <= attackRange && distance < closestDistance)
            {
                closest = tower;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static float GetBoundsDistance(Bounds from, Bounds to)
    {
        float dx = Mathf.Max(0f, from.min.x - to.max.x, to.min.x - from.max.x);
        float dy = Mathf.Max(0f, from.min.y - to.max.y, to.min.y - from.max.y);
        return Mathf.Sqrt((dx * dx) + (dy * dy));
    }

    private static Vector3 GetTargetPoint(TowerHealth target, Vector3 origin)
    {
        if (target == null)
        {
            return origin;
        }

        Bounds bounds = target.GetWorldBounds();
        return new Vector3(bounds.max.x, origin.y, origin.z);
    }

    private Vector3 GetTongueOriginPosition()
    {
        return tongueOrigin != null ? tongueOrigin.position : transform.position;
    }

    private void ResolveReferences()
    {
        if (enemy == null)
        {
            enemy = GetComponent<GoblinEnemy>();
        }

        if (tongueOrigin == null)
        {
            tongueOrigin = FindDescendant("Tongue Origin");
        }

        if (tongueTip == null)
        {
            tongueTip = FindDescendant("Tongue Tip");
        }

        if (tongueLine == null)
        {
            Transform line = FindDescendant("Tongue Line");
            tongueLine = line != null ? line.GetComponent<SpriteRenderer>() : null;
        }
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
            {
                return descendants[i];
            }
        }

        return null;
    }

    private void CacheRestPose()
    {
        if (tongueTip != null)
        {
            tongueTipRestLocalPosition = tongueTip.localPosition;
        }
    }

    private void RestoreTonguePose()
    {
        if (tongueTip != null)
        {
            tongueTip.localPosition = tongueTipRestLocalPosition;
        }

        if (tongueLine != null)
        {
            tongueLine.transform.localScale = Vector3.one;
            if (tongueOrigin != null)
            {
                tongueLine.transform.position = tongueOrigin.position;
            }
        }
    }

    private void SetTongueRestVisible()
    {
        if (tongueTip != null)
        {
            tongueTip.gameObject.SetActive(true);
        }

        if (tongueLine != null)
        {
            tongueLine.enabled = false;
        }
    }

    private void SetTongueAttackVisible()
    {
        if (tongueTip != null)
        {
            tongueTip.gameObject.SetActive(true);
        }

        if (tongueLine != null)
        {
            tongueLine.enabled = true;
        }
    }
}
