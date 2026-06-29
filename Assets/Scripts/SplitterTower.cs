using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SplitterTower : MonoBehaviour
{
    [SerializeField]
    private Bullet bulletPrefab;

    [SerializeField, Min(0f)]
    private float spawnForwardOffset = 0.25f;

    [SerializeField, Min(0f)]
    private float splitVerticalOffset = 0.35f;

    [SerializeField, Min(0.01f)]
    private float overlapForwardOffset = 0.12f;

    [SerializeField]
    private Vector2 upperDirection = Vector2.right;

    [SerializeField]
    private Vector2 lowerDirection = Vector2.right;

    [SerializeField]
    private bool destroyIncomingBullet = true;

    private readonly HashSet<Bullet> handledBullets = new();
    private readonly List<Vector3> spawnedPositionsThisFrame = new();
    private SpriteRenderer spriteRenderer;
    private int spawnedPositionsFrame = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TrySplit(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);

        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null && bullet.isActiveAndEnabled && IsInsideTower(bullet.transform.position))
            {
                TrySplit(bullet);
            }
        }
    }

    private bool IsInsideTower(Vector3 position)
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            Bounds bounds = spriteRenderer.bounds;
            bounds.Expand(0.1f);
            return bounds.Contains(position);
        }

        return Vector2.Distance(transform.position, position) <= 0.5f;
    }

    private void TrySplit(Bullet incomingBullet)
    {
        if (incomingBullet == null || !handledBullets.Add(incomingBullet))
        {
            return;
        }

        Bullet sourcePrefab = bulletPrefab != null ? bulletPrefab : incomingBullet;
        Vector3 splitOrigin = GetSplitOrigin(incomingBullet.transform.position);

        SpawnSplitBullet(sourcePrefab, splitOrigin + Vector3.up * splitVerticalOffset, upperDirection);
        SpawnSplitBullet(sourcePrefab, splitOrigin + Vector3.down * splitVerticalOffset, lowerDirection);

        if (destroyIncomingBullet)
        {
            Destroy(incomingBullet.gameObject);
        }
    }

    private Vector3 GetSplitOrigin(Vector3 incomingBulletPosition)
    {
        float splitY = incomingBulletPosition.y;

        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            Bounds bounds = spriteRenderer.bounds;
            return new Vector3(bounds.max.x + spawnForwardOffset, splitY, transform.position.z);
        }

        return new Vector3(
            transform.position.x + spawnForwardOffset,
            splitY,
            transform.position.z);
    }

    private void SpawnSplitBullet(Bullet sourcePrefab, Vector3 position, Vector2 direction)
    {
        Bullet spawnedBullet = Instantiate(sourcePrefab, ResolveSpawnPosition(position), Quaternion.identity);
        spawnedBullet.SetDirection(direction);
    }

    private Vector3 ResolveSpawnPosition(Vector3 position)
    {
        if (spawnedPositionsFrame != Time.frameCount)
        {
            spawnedPositionsFrame = Time.frameCount;
            spawnedPositionsThisFrame.Clear();
        }

        Vector3 resolvedPosition = position;
        while (ContainsSpawnPosition(resolvedPosition))
        {
            resolvedPosition += Vector3.right * Mathf.Max(0.01f, overlapForwardOffset);
        }

        spawnedPositionsThisFrame.Add(resolvedPosition);
        return resolvedPosition;
    }

    private bool ContainsSpawnPosition(Vector3 position)
    {
        const float MinDistanceSqr = 0.0001f;

        for (int i = 0; i < spawnedPositionsThisFrame.Count; i++)
        {
            if ((spawnedPositionsThisFrame[i] - position).sqrMagnitude <= MinDistanceSqr)
            {
                return true;
            }
        }

        return false;
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

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            boxCollider.size = spriteRenderer.sprite.bounds.size;
        }
    }
}
