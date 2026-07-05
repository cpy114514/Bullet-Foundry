using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShieldTower : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int shieldHealth = 20;

    [SerializeField]
    private Sprite shieldSprite;

    [SerializeField]
    private Vector2 shieldOffset = new(0.18f, 0f);

    [SerializeField]
    private Vector2 shieldScale = new(1.5f, 1.5f);

    [SerializeField]
    private Color shieldColor = new(0.85f, 0.85f, 0.85f, 0.55f);

    private readonly HashSet<Bullet> handledBullets = new();
    private SpriteRenderer spriteRenderer;
    private TowerHealth activeShieldHealth;
    private bool shieldUsed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
        HideExistingShield();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other.GetComponentInParent<Bullet>());
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
                TryActivate(bullet);
            }
        }
    }

    private void TryActivate(Bullet bullet)
    {
        if (bullet == null || shieldUsed || !handledBullets.Add(bullet))
        {
            return;
        }

        shieldUsed = true;
        CreateShield();
    }

    private void CreateShield()
    {
        GameObject shield = new("ShieldBubble");
        shield.transform.SetParent(transform, false);
        shield.transform.localPosition = shieldOffset;
        shield.transform.localScale = new Vector3(shieldScale.x, shieldScale.y, 1f);

        SpriteRenderer shieldRenderer = shield.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = shieldSprite != null
            ? shieldSprite
            : spriteRenderer != null
                ? spriteRenderer.sprite
                : null;
        shieldRenderer.color = shieldColor;
        shieldRenderer.sortingOrder = 4;

        BoxCollider2D shieldCollider = shield.AddComponent<BoxCollider2D>();
        shieldCollider.isTrigger = true;
        if (shieldRenderer.sprite != null)
        {
            shieldCollider.size = shieldRenderer.sprite.bounds.size;
        }

        activeShieldHealth = shield.AddComponent<TowerHealth>();
        activeShieldHealth.SetMaxHealth(shieldHealth);
    }

    private void HideExistingShield()
    {
        Transform existingShield = transform.Find("ShieldBubble");
        if (existingShield != null)
        {
            existingShield.gameObject.SetActive(false);
        }
    }
}
