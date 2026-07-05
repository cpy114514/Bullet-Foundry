using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MissileTower : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int energyRequired = 12;

    [SerializeField, Min(1)]
    private int missileDamage = 10;

    [SerializeField, Min(0.1f)]
    private float missileSpeed = 5f;

    [SerializeField, Min(0f)]
    private float launchOffset = 0.35f;

    [SerializeField]
    private MissileProjectile missilePrefab;

    [SerializeField]
    private Sprite missileSprite;

    [Header("Energy Bar")]
    [SerializeField]
    private Vector2 barSize = new(0.8f, 0.08f);

    [SerializeField]
    private Vector2 barOffset = new(0f, 0.65f);

    private readonly HashSet<Bullet> handledBullets = new();
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer barBackRenderer;
    private SpriteRenderer barFillRenderer;
    private int currentEnergy;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
        EnsureEnergyBar();
        RefreshEnergyBar();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCharge(other.GetComponentInParent<Bullet>());
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
                TryCharge(bullet);
            }
        }
    }

    private void TryCharge(Bullet bullet)
    {
        if (bullet == null || !handledBullets.Add(bullet))
        {
            return;
        }

        currentEnergy += bullet.CurrentDamage;
        Destroy(bullet.gameObject);

        while (currentEnergy >= energyRequired)
        {
            currentEnergy -= energyRequired;
            FireMissile();
        }

        RefreshEnergyBar();
    }

    private void FireMissile()
    {
        GoblinEnemy target = FindNearestEnemy();
        Vector3 origin = BulletTowerUtility.GetTowerCenter(transform, spriteRenderer) + Vector3.right * launchOffset;

        MissileProjectile projectile;
        if (missilePrefab != null)
        {
            projectile = Instantiate(missilePrefab, origin, Quaternion.identity);
        }
        else
        {
            GameObject missileObject = new("MissileProjectile");
            missileObject.transform.position = origin;
            missileObject.transform.localScale = Vector3.one * 0.45f;
            projectile = missileObject.AddComponent<MissileProjectile>();
        }

        projectile.Launch(target, missileDamage, missileSpeed, missileSprite);
    }

    private GoblinEnemy FindNearestEnemy()
    {
        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(FindObjectsSortMode.None);
        GoblinEnemy nearest = null;
        float nearestDistance = float.PositiveInfinity;
        Vector3 center = BulletTowerUtility.GetTowerCenter(transform, spriteRenderer);

        for (int i = 0; i < enemies.Length; i++)
        {
            GoblinEnemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            float distance = (enemy.GetWorldBounds().center - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void EnsureEnergyBar()
    {
        Transform existingBack = transform.Find("EnergyBarBack");
        if (existingBack != null)
        {
            barBackRenderer = existingBack.GetComponent<SpriteRenderer>();
        }

        if (barBackRenderer == null)
        {
            GameObject back = new("EnergyBarBack");
            back.transform.SetParent(transform, false);
            back.transform.localPosition = barOffset;
            barBackRenderer = back.AddComponent<SpriteRenderer>();
            barBackRenderer.color = new Color(0f, 0f, 0f, 0.55f);
            barBackRenderer.sortingOrder = 7;
        }

        Transform existingFill = transform.Find("EnergyBarFill");
        if (existingFill != null)
        {
            barFillRenderer = existingFill.GetComponent<SpriteRenderer>();
        }

        if (barFillRenderer == null)
        {
            GameObject fill = new("EnergyBarFill");
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = barOffset;
            barFillRenderer = fill.AddComponent<SpriteRenderer>();
            barFillRenderer.color = Color.white;
            barFillRenderer.sortingOrder = 8;
        }

        Texture2D whiteTexture = Texture2D.whiteTexture;
        Sprite barSprite = Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            whiteTexture.width);
        barBackRenderer.sprite = barSprite;
        barFillRenderer.sprite = barSprite;
        barBackRenderer.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
    }

    private void RefreshEnergyBar()
    {
        if (barFillRenderer == null)
        {
            return;
        }

        float fill = energyRequired > 0
            ? Mathf.Clamp01((float)currentEnergy / energyRequired)
            : 0f;
        barFillRenderer.transform.localScale = new Vector3(barSize.x * fill, barSize.y, 1f);
        barFillRenderer.transform.localPosition = new Vector3(
            barOffset.x - (barSize.x * (1f - fill) * 0.5f),
            barOffset.y,
            0f);
    }
}
