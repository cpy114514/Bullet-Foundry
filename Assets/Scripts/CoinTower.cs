using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinTower : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int coinsPerBullet = 1;

    [SerializeField, Min(1)]
    private int coinValue = 5;

    [Header("Production Limit")]
    [SerializeField, Min(0.1f)]
    private float maxProductionEventsPerSecond = 5f;

    [SerializeField]
    private CoinPickup coinPickupPrefab;

    [SerializeField]
    private Sprite coinPickupSprite;

    [SerializeField, Min(0f)]
    private float coinSpawnSpread = 0.25f;

    [SerializeField, Min(0f)]
    private float coinScatterRadius = 0.45f;

    private readonly HashSet<Bullet> rewardedBullets = new();
    private SpriteRenderer spriteRenderer;
    private float productionWindowEndTime;
    private int productionEventsInWindow;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        if (bullet == null || rewardedBullets.Contains(bullet))
        {
            return;
        }

        // A tower receives five immediate production charges per second. This
        // avoids inserting a forced 0.2-second dead spot between every bullet.
        if (Time.time >= productionWindowEndTime)
        {
            productionWindowEndTime = Time.time + 1f;
            productionEventsInWindow = 0;
        }

        int productionLimit = Mathf.Max(1, Mathf.FloorToInt(maxProductionEventsPerSecond));
        if (productionEventsInWindow >= productionLimit)
        {
            return;
        }

        rewardedBullets.Add(bullet);
        productionEventsInWindow++;

        for (int i = 0; i < coinsPerBullet; i++)
        {
            SpawnCoinPickup(i, coinsPerBullet);
        }
    }

    private void SpawnCoinPickup(int coinIndex, int coinCount)
    {
        Vector3 spawnPosition = BulletTowerUtility.GetTowerCenter(transform, spriteRenderer);
        Vector3 targetPosition = GetCoinSpawnPosition(coinIndex, coinCount);
        CoinPickup pickup = null;

        if (coinPickupPrefab != null)
        {
            pickup = Instantiate(coinPickupPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            GameObject pickupObject = new("CoinPickup");
            pickupObject.transform.position = spawnPosition;
            pickup = pickupObject.AddComponent<CoinPickup>();

            SpriteRenderer pickupRenderer = pickupObject.AddComponent<SpriteRenderer>();
            pickupRenderer.sprite = coinPickupSprite != null
                ? coinPickupSprite
                : spriteRenderer != null
                    ? spriteRenderer.sprite
                    : null;
            pickupRenderer.color = Color.white;
            pickupRenderer.sortingOrder = 5;
            pickupObject.transform.localScale = Vector3.one * 0.65f;
        }

        if (pickup != null)
        {
            pickup.SetValue(coinValue);
            pickup.ScatterTo(targetPosition);
        }
    }

    private Vector3 GetCoinSpawnPosition(int coinIndex, int coinCount)
    {
        Vector3 center = BulletTowerUtility.GetTowerCenter(transform, spriteRenderer);
        Vector2 randomDirection = Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude <= 0.001f)
        {
            randomDirection = Vector2.up;
        }

        randomDirection.Normalize();
        center += new Vector3(
            randomDirection.x * coinScatterRadius,
            randomDirection.y * coinScatterRadius,
            0f);

        if (coinCount <= 1 || coinSpawnSpread <= 0f)
        {
            return center;
        }

        float centeredIndex = coinIndex - ((coinCount - 1) * 0.5f);
        center.x += centeredIndex * coinSpawnSpread;
        center.y += Mathf.Abs(centeredIndex) * 0.05f;
        return center;
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
