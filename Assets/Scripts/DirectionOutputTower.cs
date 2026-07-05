using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectionOutputTower : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float shotInterval = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float requiredIncomingDot = 0.5f;

    [SerializeField, Min(0.01f)]
    private float launchForwardDistance = 0.4f;

    [SerializeField]
    private bool acceptFromLeft = true;

    [SerializeField]
    private bool acceptFromUp = true;

    [SerializeField]
    private bool acceptFromDown = true;

    private readonly HashSet<Bullet> handledBullets = new();
    private readonly Queue<Bullet> pendingShots = new();
    private SpriteRenderer spriteRenderer;
    private float nextShotTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        QueueBullet(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null
                && bullet.isActiveAndEnabled
                && BulletTowerUtility.IsInsideTower(transform, spriteRenderer, bullet.transform.position))
            {
                QueueBullet(bullet);
            }
        }

        ProcessQueue();
    }

    private void QueueBullet(Bullet bullet)
    {
        if (bullet == null || !CanAcceptBullet(bullet) || !handledBullets.Add(bullet))
        {
            return;
        }

        bullet.PauseForTowerQueue();
        pendingShots.Enqueue(bullet);
    }

    private bool CanAcceptBullet(Bullet bullet)
    {
        return BulletTowerUtility.AcceptsIncomingDirection(
            bullet,
            BulletTowerUtility.GetTowerCenter(transform, spriteRenderer),
            acceptFromLeft,
            acceptFromUp,
            acceptFromDown,
            requiredIncomingDot);
    }

    private void ProcessQueue()
    {
        if (pendingShots.Count == 0 || Time.time < nextShotTime)
        {
            return;
        }

        Bullet sourceBullet = pendingShots.Dequeue();
        if (sourceBullet != null)
        {
            FireOutputBullet(sourceBullet);
            Destroy(sourceBullet.gameObject);
        }

        nextShotTime = Time.time + shotInterval;
    }

    private void FireOutputBullet(Bullet sourceBullet)
    {
        Vector3 origin = BulletTowerUtility.GetTowerCenter(transform, spriteRenderer);
        origin.z = sourceBullet.transform.position.z;

        Bullet outputBullet = Instantiate(sourceBullet, origin, Quaternion.identity);
        outputBullet.CopyRuntimeStateFrom(sourceBullet);
        outputBullet.ResumeFromTowerQueue();

        Vector3 target = origin + Vector3.right * launchForwardDistance;
        outputBullet.FlyToThenContinue(target, Vector2.right);
        handledBullets.Add(outputBullet);
    }
}
