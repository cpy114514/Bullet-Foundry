using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HomingTower : MonoBehaviour
{
    [SerializeField]
    private Color homingFlashColor = new(1f, 1f, 1f, 1f);

    private readonly HashSet<Bullet> convertedBullets = new();
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryConvert(other.GetComponentInParent<Bullet>());
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
                TryConvert(bullet);
            }
        }
    }

    private void TryConvert(Bullet bullet)
    {
        if (bullet == null || !convertedBullets.Add(bullet))
        {
            return;
        }

        BrightenBullet(bullet);
        if (bullet.GetComponent<HomingBullet>() == null)
        {
            bullet.gameObject.AddComponent<HomingBullet>();
        }

        bullet.SpawnConversionEffect(BulletElement.Homing);
    }

    private void BrightenBullet(Bullet bullet)
    {
        SpriteRenderer bulletRenderer = bullet.GetComponent<SpriteRenderer>();
        if (bulletRenderer == null)
        {
            return;
        }

        Color currentColor = bulletRenderer.color;
        bulletRenderer.color = new Color(
            Mathf.Max(currentColor.r, homingFlashColor.r),
            Mathf.Max(currentColor.g, homingFlashColor.g),
            Mathf.Max(currentColor.b, homingFlashColor.b),
            currentColor.a);
    }
}
