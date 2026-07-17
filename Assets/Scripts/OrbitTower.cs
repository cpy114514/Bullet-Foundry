using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OrbitTower : MonoBehaviour
{
    private const float DefaultOrbitDuration = 2.5f;

    [SerializeField, Min(0.05f)]
    private float orbitRadius = 0.55f;

    [SerializeField, Min(1f)]
    private float angularSpeedDegrees = 220f;

    [SerializeField, Min(0.1f)]
    private float orbitDuration = DefaultOrbitDuration;

    private readonly HashSet<Bullet> handledBullets = new();
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartOrbit(other.GetComponentInParent<Bullet>());
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
                TryStartOrbit(bullet);
            }
        }
    }

    private void TryStartOrbit(Bullet bullet)
    {
        if (bullet == null || !handledBullets.Add(bullet))
        {
            return;
        }

        OrbitingBullet orbitingBullet = bullet.GetComponent<OrbitingBullet>();
        if (orbitingBullet == null)
        {
            orbitingBullet = bullet.gameObject.AddComponent<OrbitingBullet>();
        }

        orbitingBullet.Begin(
            transform,
            orbitRadius,
            angularSpeedDegrees,
            orbitDuration > 0f ? orbitDuration : DefaultOrbitDuration);
    }

    private void OnValidate()
    {
        if (orbitDuration <= 0f)
        {
            orbitDuration = DefaultOrbitDuration;
        }
    }
}
