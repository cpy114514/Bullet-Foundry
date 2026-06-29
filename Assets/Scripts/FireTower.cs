using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireTower : MonoBehaviour
{
    [SerializeField]
    private Sprite[] fireFrames = new Sprite[4];

    [SerializeField, Min(0.01f)]
    private float frameDuration = 0.08f;

    [SerializeField]
    private Vector2 fireBulletScale = Vector2.one;

    private readonly HashSet<Bullet> convertedBullets = new();
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureTriggerCollider();
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
            if (bullet != null && bullet.isActiveAndEnabled && IsInsideTower(bullet.transform.position))
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

        bullet.SetSpriteAnimation(fireFrames, frameDuration);
        bullet.SetVisualScale(fireBulletScale);
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
