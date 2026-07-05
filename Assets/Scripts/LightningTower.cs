using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningTower : MonoBehaviour
{
    [SerializeField]
    private Sprite[] lightningFrames = System.Array.Empty<Sprite>();

    [SerializeField, Min(0.01f)]
    private float frameDuration = 0.06f;

    [SerializeField]
    private Vector2 lightningBulletScale = new(0.75f, 0.75f);

    [SerializeField]
    private Color lightningBulletColor = new(0.9f, 0.9f, 0.9f, 1f);

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

        bullet.ApplyElement(BulletElement.Lightning, lightningFrames, frameDuration, lightningBulletScale);
        bullet.SetVisualColor(lightningBulletColor);
        if (bullet.GetComponent<LightningBulletEffect>() == null)
        {
            bullet.gameObject.AddComponent<LightningBulletEffect>();
        }

        bullet.SpawnConversionEffect(BulletElement.Lightning);
    }
}
