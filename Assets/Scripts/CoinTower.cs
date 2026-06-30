using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinTower : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int coinsPerBullet = 1;

    private readonly HashSet<Bullet> rewardedBullets = new();
    private SpriteRenderer spriteRenderer;
    private CoinWallet wallet;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ResolveWallet();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReward(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);

        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null && bullet.isActiveAndEnabled && IsInsideTower(bullet.transform.position))
            {
                TryReward(bullet);
            }
        }
    }

    private void TryReward(Bullet bullet)
    {
        if (bullet == null || !rewardedBullets.Add(bullet))
        {
            return;
        }

        ResolveWallet();
        if (wallet != null)
        {
            wallet.AddCoins(coinsPerBullet);
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

    private void ResolveWallet()
    {
        if (wallet != null)
        {
            return;
        }

        wallet = CoinWallet.Instance;
        if (wallet == null)
        {
            wallet = FindFirstObjectByType<CoinWallet>();
        }
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
