using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GoblinEnemy : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField, Min(1)]
    private int maxHealth = 3;

    [SerializeField, Min(0f)]
    private float moveSpeed = 1f;

    [SerializeField, Min(0f)]
    private int contactDamage = 1;

    [Header("Attack")]
    [SerializeField, Min(0.01f)]
    private float attackCooldown = 1f;

    [SerializeField, Min(0f)]
    private float towerAttackRange = 0.1f;

    [SerializeField, Min(0.01f)]
    private float towerLaneTolerance = 0.6f;

    [Header("Animation")]
    [SerializeField]
    private string walkStateName = "goblin_walk";

    [SerializeField]
    private string attackStateName = "goblin_attack";

    [SerializeField]
    private string dieStateName = "goblin_die";

    [SerializeField, Min(0f)]
    private float animationFadeTime = 0.08f;

    [SerializeField, Min(0f)]
    private float destroyDelayAfterDeath = 1.1f;

    [Header("Feedback")]
    [SerializeField]
    private Color hitFlashColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [SerializeField, Min(0f)]
    private float hitFlashDuration = 0.1f;

    private Animator animator;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalSpriteColors;
    private Coroutine hitFlashRoutine;
    private int currentHealth;
    private float nextAttackTime;
    private float slowMultiplier = 1f;
    private float slowUntilTime;
    private string currentAnimationState;
    private bool isDead;

    public bool IsDead => isDead;

    public int CurrentHealth => currentHealth;

    public int MaxHealth => maxHealth;

    public Bounds GetWorldBounds()
    {
        return CalculateRendererBounds();
    }

    public event Action<int, int> HealthChanged;

    public event Action Died;

    public event Action DeathAnimationFinished;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        CacheSpriteRenderers();
        EnsurePhysicsComponents();
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        CacheSpriteRenderers();
        RestoreSpriteColors();
        currentHealth = maxHealth;
        isDead = false;
        nextAttackTime = 0f;
        slowMultiplier = 1f;
        slowUntilTime = 0f;
        PlayState(walkStateName);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        TowerHealth towerTarget = FindTowerInAttackRange();
        if (towerTarget != null)
        {
            AttackTower(towerTarget);
            return;
        }

        Walk();
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        FlashOnHit();
        currentHealth = Mathf.Max(0, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth == 0)
        {
            Die();
        }
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }

    public void ApplySlow(float speedMultiplier, float duration)
    {
        if (isDead || duration <= 0f)
        {
            return;
        }

        if (Time.time >= slowUntilTime)
        {
            slowMultiplier = 1f;
        }

        slowMultiplier = Mathf.Min(
            slowMultiplier,
            Mathf.Clamp(speedMultiplier, 0.05f, 1f));
        slowUntilTime = Mathf.Max(slowUntilTime, Time.time + duration);
    }

    public void RefillHealth(int newMaxHealth)
    {
        if (isDead)
        {
            return;
        }

        maxHealth = Mathf.Max(1, newMaxHealth);
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Walk()
    {
        PlayState(walkStateName);
        transform.Translate(
            Vector3.left * (GetCurrentMoveSpeed() * Time.deltaTime),
            Space.World);
    }

    private float GetCurrentMoveSpeed()
    {
        if (Time.time >= slowUntilTime)
        {
            slowMultiplier = 1f;
        }

        return moveSpeed * slowMultiplier;
    }

    private void AttackTower(TowerHealth tower)
    {
        PlayState(attackStateName);

        if (tower == null || tower.IsDestroyed || Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        tower.TakeDamage(contactDamage);
    }

    private void Die()
    {
        isDead = true;
        PlayState(dieStateName);

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        Died?.Invoke();
        StartCoroutine(FinishDeathRoutine());
    }

    private IEnumerator FinishDeathRoutine()
    {
        if (destroyDelayAfterDeath > 0f)
        {
            yield return new WaitForSeconds(destroyDelayAfterDeath);
        }

        DeathAnimationFinished?.Invoke();
        Destroy(gameObject);
    }

    private TowerHealth FindTowerInAttackRange()
    {
        TowerHealth[] towers = FindObjectsByType<TowerHealth>(FindObjectsSortMode.None);
        if (towers.Length == 0)
        {
            return null;
        }

        Bounds goblinBounds = CalculateRendererBounds();
        TowerHealth closestTower = null;
        float closestTowerX = float.NegativeInfinity;

        for (int i = 0; i < towers.Length; i++)
        {
            TowerHealth tower = towers[i];
            if (tower == null || tower.IsDestroyed || !tower.isActiveAndEnabled)
            {
                continue;
            }

            Bounds towerBounds = tower.GetWorldBounds();
            if (Mathf.Abs(towerBounds.center.y - goblinBounds.center.y) > towerLaneTolerance)
            {
                continue;
            }

            if (towerBounds.center.x > goblinBounds.center.x + 0.1f)
            {
                continue;
            }

            if (goblinBounds.max.x < towerBounds.min.x)
            {
                continue;
            }

            float horizontalGap = goblinBounds.min.x - towerBounds.max.x;
            if (horizontalGap > towerAttackRange)
            {
                continue;
            }

            if (towerBounds.center.x > closestTowerX)
            {
                closestTower = tower;
                closestTowerX = towerBounds.center.x;
            }
        }

        return closestTower;
    }

    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || currentAnimationState == stateName)
        {
            return;
        }

        currentAnimationState = stateName;
        animator.CrossFade(stateName, animationFadeTime);
    }

    private void EnsurePhysicsComponents()
    {
        if (!TryGetComponent(out body))
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        if (!TryGetComponent(out hitbox))
        {
            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            boxCollider.size = CalculateSpriteBoundsSize();
            boxCollider.offset = CalculateSpriteBoundsCenter();
            hitbox = boxCollider;
        }
        else
        {
            hitbox.isTrigger = true;
        }
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalSpriteColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalSpriteColors[i] = spriteRenderers[i].color;
        }
    }

    private void FlashOnHit()
    {
        if (hitFlashDuration <= 0f || spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
        }

        hitFlashRoutine = StartCoroutine(FlashOnHitRoutine());
    }

    private IEnumerator FlashOnHitRoutine()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color flashColor = hitFlashColor;
            flashColor.a = originalSpriteColors[i].a;
            spriteRenderers[i].color = flashColor;
        }

        yield return new WaitForSeconds(hitFlashDuration);

        RestoreSpriteColors();
        hitFlashRoutine = null;
    }

    private void RestoreSpriteColors()
    {
        if (spriteRenderers == null || originalSpriteColors == null)
        {
            return;
        }

        int restoreCount = Mathf.Min(spriteRenderers.Length, originalSpriteColors.Length);
        for (int i = 0; i < restoreCount; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalSpriteColors[i];
            }
        }
    }

    private Vector2 CalculateSpriteBoundsSize()
    {
        Bounds bounds = CalculateRendererBounds();
        if (bounds.size == Vector3.zero)
        {
            return new Vector2(0.6f, 1f);
        }

        Vector3 scale = transform.lossyScale;
        return new Vector2(
            scale.x != 0f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            scale.y != 0f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y);
    }

    private Vector2 CalculateSpriteBoundsCenter()
    {
        Bounds bounds = CalculateRendererBounds();
        if (bounds.size == Vector3.zero)
        {
            return Vector2.zero;
        }

        return transform.InverseTransformPoint(bounds.center);
    }

    private Bounds CalculateRendererBounds()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
