using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SplitterTower : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float splitVerticalOffset = 0.35f;

    [SerializeField]
    private bool destroyIncomingBullet = true;

    [SerializeField, Min(0f)]
    private float shotInterval = 0.12f;

    [SerializeField, Min(0f)]
    private float launchForwardDistance = 0.35f;

    [SerializeField, Min(0f)]
    private float outputForwardStagger = 0.08f;

    [SerializeField, Range(0f, 1f)]
    private float requiredRearDirectionDot = 0.5f;

    private readonly HashSet<Bullet> handledBullets = new();
    private readonly Queue<Bullet> pendingShots = new();
    private SpriteRenderer spriteRenderer;
    private float nextShotTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        QueueShot(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);

        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null && bullet.isActiveAndEnabled && IsInsideTower(bullet.transform.position))
            {
                QueueShot(bullet);
            }
        }

        ProcessShotQueue();
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

    private void QueueShot(Bullet incomingBullet)
    {
        if (incomingBullet == null
            || !CanAcceptBullet(incomingBullet)
            || !handledBullets.Add(incomingBullet))
        {
            return;
        }

        incomingBullet.PauseForTowerQueue();
        pendingShots.Enqueue(incomingBullet);
    }

    private bool CanAcceptBullet(Bullet incomingBullet)
    {
        if (Vector2.Dot(incomingBullet.Direction, Vector2.right) < requiredRearDirectionDot)
        {
            return false;
        }

        return incomingBullet.transform.position.x <= GetTowerCenter().x;
    }

    private void ProcessShotQueue()
    {
        if (pendingShots.Count == 0 || Time.time < nextShotTime)
        {
            return;
        }

        Bullet sourceBullet = pendingShots.Dequeue();
        if (sourceBullet != null)
        {
            FireSplitPair(sourceBullet);

            if (destroyIncomingBullet)
            {
                Destroy(sourceBullet.gameObject);
            }
            else
            {
                sourceBullet.ResumeFromTowerQueue();
                sourceBullet.SetDirection(Vector2.right);
            }
        }

        nextShotTime = Time.time + shotInterval;
    }

    private void FireSplitPair(Bullet sourceBullet)
    {
        Vector2 outputDirection = Vector2.right;
        Vector2 perpendicular = Vector2.up;
        Vector3 launchOrigin = GetLaunchOrigin(sourceBullet.transform.position.z);

        SpawnSplitBullet(
            sourceBullet,
            launchOrigin,
            GetOutputTargetPosition(
                launchOrigin,
                outputDirection,
                perpendicular,
                splitVerticalOffset,
                outputForwardStagger * 0.5f,
                sourceBullet.transform.position.z),
            outputDirection);

        SpawnSplitBullet(
            sourceBullet,
            launchOrigin,
            GetOutputTargetPosition(
                launchOrigin,
                outputDirection,
                perpendicular,
                -splitVerticalOffset,
                -outputForwardStagger * 0.5f,
                sourceBullet.transform.position.z),
            outputDirection);
    }

    private Bullet SpawnSplitBullet(
        Bullet sourceBullet,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        Vector2 direction)
    {
        Bullet spawnedBullet = Instantiate(sourceBullet, spawnPosition, Quaternion.identity);
        spawnedBullet.CopyRuntimeStateFrom(sourceBullet);
        spawnedBullet.ResumeFromTowerQueue();
        spawnedBullet.FlyToThenContinue(targetPosition, direction);
        handledBullets.Add(spawnedBullet);
        return spawnedBullet;
    }

    private Vector3 GetLaunchOrigin(float sourceZ)
    {
        Vector3 origin = GetTowerCenter();
        origin.z = sourceZ;
        return origin;
    }

    private Vector3 GetTowerCenter()
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            return spriteRenderer.bounds.center;
        }

        Collider2D towerCollider = GetComponent<Collider2D>();
        return towerCollider != null && towerCollider.enabled
            ? towerCollider.bounds.center
            : transform.position;
    }

    private Vector3 GetOutputTargetPosition(
        Vector3 launchOrigin,
        Vector2 outputDirection,
        Vector2 perpendicular,
        float perpendicularOffset,
        float forwardStagger,
        float z)
    {
        Vector3 forward = new(outputDirection.x, outputDirection.y, 0f);
        Vector3 sideways = new(perpendicular.x, perpendicular.y, 0f);
        float forwardDistance = Mathf.Max(0.05f, launchForwardDistance + forwardStagger);
        Vector3 targetPosition = launchOrigin
            + forward * forwardDistance
            + sideways * perpendicularOffset;
        targetPosition.z = z;
        return targetPosition;
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
