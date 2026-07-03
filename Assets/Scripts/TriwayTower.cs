using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TriwayTower : MonoBehaviour
{
    [SerializeField, Range(1f, 179f)]
    private float branchAngle = 90f;

    [SerializeField, Range(0f, 1f)]
    private float requiredRearDirectionDot = 0.5f;

    [SerializeField, Min(0f)]
    private float parallelOutputSpacing = 0.16f;

    private readonly HashSet<Bullet> handledBullets = new();
    private readonly Queue<Bullet> pendingBullets = new();
    private SpriteRenderer[] spriteRenderers;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        QueueSplit(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null && bullet.isActiveAndEnabled && IsInsideTower(bullet.transform.position))
            {
                QueueSplit(bullet);
            }
        }

        ProcessPendingSplits();
    }

    private void QueueSplit(Bullet incomingBullet)
    {
        if (incomingBullet == null
            || !CanAcceptBullet(incomingBullet)
            || !handledBullets.Add(incomingBullet))
        {
            return;
        }

        pendingBullets.Enqueue(incomingBullet);
    }

    private void ProcessPendingSplits()
    {
        if (pendingBullets.Count == 0)
        {
            return;
        }

        List<Bullet> splitBatch = new();
        while (pendingBullets.Count > 0)
        {
            Bullet bullet = pendingBullets.Dequeue();
            if (bullet != null)
            {
                splitBatch.Add(bullet);
            }
        }

        if (splitBatch.Count == 0)
        {
            return;
        }

        Vector2 forward = Vector2.right;
        Vector2 upper = Rotate(forward, branchAngle);
        Vector2 lower = Rotate(forward, -branchAngle);

        for (int i = 0; i < splitBatch.Count; i++)
        {
            Bullet sourceBullet = splitBatch[i];
            if (sourceBullet == null)
            {
                continue;
            }

            SpawnOutputBullet(sourceBullet, forward, i, splitBatch.Count);
            SpawnOutputBullet(sourceBullet, upper, i, splitBatch.Count);
            SpawnOutputBullet(sourceBullet, lower, i, splitBatch.Count);
            Destroy(sourceBullet.gameObject);
        }
    }

    private void SpawnOutputBullet(
        Bullet sourceBullet,
        Vector2 outputDirection,
        int outputIndex,
        int outputCount)
    {
        Vector3 splitPosition = GetSplitPosition(sourceBullet.transform.position.z);
        splitPosition += GetParallelOffset(outputDirection, outputIndex, outputCount);
        Bullet spawnedBullet = SpawnBullet(sourceBullet, splitPosition, outputDirection);
        RememberSpawnedBullet(spawnedBullet);
    }

    private Vector3 GetParallelOffset(Vector2 outputDirection, int outputIndex, int outputCount)
    {
        if (outputCount <= 1 || parallelOutputSpacing <= 0f)
        {
            return Vector3.zero;
        }

        Vector2 perpendicular = new(-outputDirection.y, outputDirection.x);
        float centeredIndex = outputIndex - ((outputCount - 1) * 0.5f);
        Vector2 offset = perpendicular.normalized * (centeredIndex * parallelOutputSpacing);
        return new Vector3(offset.x, offset.y, 0f);
    }

    private bool CanAcceptBullet(Bullet incomingBullet)
    {
        Vector2 requiredDirection = Vector2.right;
        if (Vector2.Dot(incomingBullet.Direction, requiredDirection) < requiredRearDirectionDot)
        {
            return false;
        }

        return incomingBullet.transform.position.x <= GetTowerCenter().x;
    }

    private Vector3 GetSplitPosition(float bulletZ)
    {
        Collider2D towerCollider = GetComponent<Collider2D>();
        if (towerCollider != null && towerCollider.enabled)
        {
            Vector3 center = towerCollider.bounds.center;
            center.z = bulletZ;
            return center;
        }

        Bounds? combinedBounds = null;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (combinedBounds.HasValue)
            {
                Bounds bounds = combinedBounds.Value;
                bounds.Encapsulate(renderer.bounds);
                combinedBounds = bounds;
            }
            else
            {
                combinedBounds = renderer.bounds;
            }
        }

        Vector3 splitPosition = combinedBounds.HasValue
            ? combinedBounds.Value.center
            : transform.position;
        splitPosition.z = bulletZ;
        return splitPosition;
    }

    private Vector3 GetTowerCenter()
    {
        Collider2D towerCollider = GetComponent<Collider2D>();
        if (towerCollider != null && towerCollider.enabled)
        {
            return towerCollider.bounds.center;
        }

        Bounds? combinedBounds = null;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (combinedBounds.HasValue)
            {
                Bounds bounds = combinedBounds.Value;
                bounds.Encapsulate(renderer.bounds);
                combinedBounds = bounds;
            }
            else
            {
                combinedBounds = renderer.bounds;
            }
        }

        return combinedBounds.HasValue
            ? combinedBounds.Value.center
            : transform.position;
    }

    private Bullet SpawnBullet(Bullet source, Vector3 position, Vector2 direction)
    {
        Bullet spawnedBullet = Instantiate(source, position, source.transform.rotation);
        spawnedBullet.CopyRuntimeStateFrom(source);
        spawnedBullet.SetDirection(direction);
        return spawnedBullet;
    }

    private void RememberSpawnedBullet(Bullet bullet)
    {
        if (bullet != null)
        {
            handledBullets.Add(bullet);
        }
    }

    private bool IsInsideTower(Vector3 position)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            bounds.Expand(0.1f);
            if (bounds.Contains(position))
            {
                return true;
            }
        }

        return Vector2.Distance(transform.position, position) <= 0.5f;
    }

    private void EnsureTriggerCollider()
    {
        if (TryGetComponent(out Collider2D collider2D))
        {
            collider2D.isTrigger = true;
            return;
        }

        BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;
        FitColliderToRenderers(boxCollider);
    }

    private void FitColliderToRenderers(BoxCollider2D collider2D)
    {
        Bounds? combinedBounds = null;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (combinedBounds.HasValue)
            {
                Bounds bounds = combinedBounds.Value;
                bounds.Encapsulate(renderer.bounds);
                combinedBounds = bounds;
            }
            else
            {
                combinedBounds = renderer.bounds;
            }
        }

        if (!combinedBounds.HasValue)
        {
            return;
        }

        Bounds worldBounds = combinedBounds.Value;
        collider2D.offset = transform.InverseTransformPoint(worldBounds.center);
        Vector3 scale = transform.lossyScale;
        collider2D.size = new Vector2(
            scale.x != 0f ? worldBounds.size.x / Mathf.Abs(scale.x) : worldBounds.size.x,
            scale.y != 0f ? worldBounds.size.y / Mathf.Abs(scale.y) : worldBounds.size.y);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine).normalized;
    }
}
