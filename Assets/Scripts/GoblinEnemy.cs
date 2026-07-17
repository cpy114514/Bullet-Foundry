using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GoblinEnemy : MonoBehaviour
{
    private const int EnemyCoinCountDivisor = 5;
    private const int EnemyCoinValueMultiplier = 5;
    [Header("Stats")]
    [SerializeField, Min(1)]
    private int maxHealth = 3;

    [SerializeField, Min(0f)]
    private float moveSpeed = 1f;

    [SerializeField, Min(0f)]
    private int contactDamage = 1;

    [Header("Placement")]
    [SerializeField]
    private int spawnFootLaneOffset;

    [Header("Attack")]
    [SerializeField, Min(0.01f)]
    private float attackCooldown = 1f;

    [SerializeField, Min(0.01f)]
    private float attackAnimationDurationFallback = 0.45f;

    [SerializeField, Min(0f)]
    private float towerAttackRange = 0.02f;

    [SerializeField, Min(0.01f)]
    private float towerLaneTolerance = 0.6f;

    [SerializeField]
    private float towerLaneCenterOffset;

    [SerializeField]
    private bool useBoundsVerticalAttackRange;

    [SerializeField, Min(0f)]
    private float boundsVerticalAttackPadding = 0.15f;

    [SerializeField]
    private bool attackAllTowersInRange;

    [SerializeField]
    private bool destroyTowersInOneHit;

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

    [SerializeField, Range(0.1f, 1f)]
    private float slowTintMultiplier = 0.75f;

    [SerializeField, Range(0.05f, 1f)]
    private float stunTintMultiplier = 0.45f;

    [Header("Coin Drop")]
    [SerializeField]
    private bool dropCoinsOnDeath = true;

    [SerializeField, Min(0)]
    private int coinDropCount = 1;

    [SerializeField, Min(1)]
    private int coinDropMultiplier = 1;

    [SerializeField, Min(1)]
    private int coinDropValue = 5;

    [SerializeField]
    private CoinPickup coinPickupPrefab;

    [SerializeField]
    private Sprite coinPickupSprite;

    [SerializeField, Min(0f)]
    private float coinDropScatterRadius = 0.45f;

    private Animator animator;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalSpriteColors;
    private Coroutine hitFlashRoutine;
    private Coroutine attackRoutine;
    private int currentHealth;
    private float nextAttackTime;
    private float slowMultiplier = 1f;
    private float slowUntilTime;
    private float stunUntilTime;
    private string currentAnimationState;
    private Vector3 movementPosition;
    private float pendingMoveSpeed;
    private bool hasMovementPosition;
    private bool shouldMoveThisFrame;
    private bool coinsDropped;
    private bool isDead;
    private bool slowVisualActive;
    private bool stunVisualActive;
    private bool animationPausedByStun;
    private float animatorSpeedBeforeStun = 1f;
    private bool attackActionActive;
    private bool temporaryActionActive;
    private float temporaryActionUntilTime;
    private float movementHoldUntilTime;
    private string movementHoldStateName;
    private static Sprite fallbackCoinSprite;

    public bool IsDead => isDead;

    public int CurrentHealth => currentHealth;

    public int MaxHealth => maxHealth;

    public int SpawnFootLaneOffset => spawnFootLaneOffset;

    public float MoveSpeed => moveSpeed;

    public bool IsActionBlocked => isDead || attackActionActive || Time.time < stunUntilTime;

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
        ResetMovementPosition();
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        ResetMovementPosition();
        CacheSpriteRenderers();
        currentHealth = maxHealth;
        shouldMoveThisFrame = false;
        pendingMoveSpeed = 0f;
        coinsDropped = false;
        isDead = false;
        nextAttackTime = 0f;
        attackRoutine = null;
        attackActionActive = false;
        slowMultiplier = 1f;
        slowUntilTime = 0f;
        stunUntilTime = 0f;
        temporaryActionActive = false;
        temporaryActionUntilTime = 0f;
        movementHoldUntilTime = 0f;
        movementHoldStateName = null;
        slowVisualActive = false;
        stunVisualActive = false;
        SetStunAnimationPaused(false);
        RestoreSpriteColors();
        PlayState(walkStateName, false);
    }

    private void OnDisable()
    {
        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        attackActionActive = false;
        slowVisualActive = false;
        stunVisualActive = false;
        SetStunAnimationPaused(false);
        RestoreSpriteColors();
    }

    private void Update()
    {
        shouldMoveThisFrame = false;
        pendingMoveSpeed = 0f;

        RefreshSlowState();
        RefreshStunState();
        RefreshTemporaryAction();

        if (isDead)
        {
            return;
        }

        if (IsStunned())
        {
            PlayState(walkStateName, false);
            return;
        }

        if (temporaryActionActive)
        {
            return;
        }

        if (attackActionActive)
        {
            return;
        }

        if (IsMovementHeld())
        {
            PlayState(!string.IsNullOrWhiteSpace(movementHoldStateName)
                ? movementHoldStateName
                : walkStateName,
                false);
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

    private void LateUpdate()
    {
        if (!hasMovementPosition)
        {
            ResetMovementPosition();
        }

        if (!isDead && shouldMoveThisFrame && pendingMoveSpeed > 0f)
        {
            movementPosition += Vector3.left * (pendingMoveSpeed * Time.deltaTime);
        }

        ApplyMovementPosition();
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

    public void SetAnimationStateNames(string walkState, string attackState, string dieState)
    {
        if (!string.IsNullOrWhiteSpace(walkState))
        {
            walkStateName = walkState;
        }

        if (!string.IsNullOrWhiteSpace(attackState))
        {
            attackStateName = attackState;
        }

        if (!string.IsNullOrWhiteSpace(dieState))
        {
            dieStateName = dieState;
        }

        currentAnimationState = null;
        if (!isDead)
        {
            PlayState(walkStateName, false);
        }
    }

    public void SetCoinDropsEnabled(bool enabled)
    {
        dropCoinsOnDeath = enabled;
    }

    public void HoldPosition(float duration, string stateName = null)
    {
        if (isDead || duration <= 0f)
        {
            return;
        }

        movementHoldUntilTime = Mathf.Max(
            movementHoldUntilTime,
            Time.time + duration);
        movementHoldStateName = stateName;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    public void SyncMovementPositionToTransform()
    {
        ResetMovementPosition();
        ApplyMovementPosition();
    }

    public void PlayTemporaryAction(string stateName, float duration)
    {
        if (isDead || duration <= 0f)
        {
            return;
        }

        temporaryActionActive = true;
        temporaryActionUntilTime = Mathf.Max(
            temporaryActionUntilTime,
            Time.time + duration);
        PlayState(stateName, true);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    public float GetActionAnimationDuration(string stateName, float fallbackDuration)
    {
        return GetAnimationDuration(stateName, fallbackDuration);
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
        SetSlowVisualActive(true);
    }

    public void ApplyStun(float duration)
    {
        if (isDead || duration <= 0f)
        {
            return;
        }

        stunUntilTime = Mathf.Max(stunUntilTime, Time.time + duration);
        SetStunVisualActive(true);
        SetStunAnimationPaused(true);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
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

    public void ApplyEndlessScaling(float healthMultiplier, float speedMultiplier)
    {
        if (isDead)
        {
            return;
        }

        RefillHealth(Mathf.CeilToInt(maxHealth * Mathf.Max(1f, healthMultiplier)));
        moveSpeed *= Mathf.Max(0.1f, speedMultiplier);
    }

    private void Walk()
    {
        PlayState(walkStateName, false);
        pendingMoveSpeed = GetCurrentMoveSpeed();
        shouldMoveThisFrame = pendingMoveSpeed > 0f;
    }

    private float GetCurrentMoveSpeed()
    {
        RefreshSlowState();
        return moveSpeed * slowMultiplier;
    }

    private void RefreshSlowState()
    {
        if (Time.time < slowUntilTime)
        {
            return;
        }

        slowMultiplier = 1f;
        SetSlowVisualActive(false);
    }

    private bool IsStunned()
    {
        RefreshStunState();
        return Time.time < stunUntilTime;
    }

    private void RefreshStunState()
    {
        if (Time.time < stunUntilTime)
        {
            return;
        }

        SetStunVisualActive(false);
        SetStunAnimationPaused(false);
    }

    private void RefreshTemporaryAction()
    {
        if (!temporaryActionActive || Time.time < temporaryActionUntilTime)
        {
            return;
        }

        temporaryActionActive = false;
        temporaryActionUntilTime = 0f;
        currentAnimationState = null;
    }

    private bool IsMovementHeld()
    {
        if (Time.time < movementHoldUntilTime)
        {
            return true;
        }

        movementHoldStateName = null;
        return false;
    }

    private void SetSlowVisualActive(bool active)
    {
        if (slowVisualActive == active)
        {
            return;
        }

        slowVisualActive = active;
        if (hitFlashRoutine == null)
        {
            RestoreSpriteColors();
        }
    }

    private void SetStunVisualActive(bool active)
    {
        if (stunVisualActive == active)
        {
            return;
        }

        stunVisualActive = active;
        if (hitFlashRoutine == null)
        {
            RestoreSpriteColors();
        }
    }

    private void SetStunAnimationPaused(bool paused)
    {
        if (animator == null || animationPausedByStun == paused)
        {
            return;
        }

        if (paused)
        {
            animatorSpeedBeforeStun = animator.speed;
            animator.speed = 0f;
            animationPausedByStun = true;
            return;
        }

        animator.speed = animatorSpeedBeforeStun;
        animationPausedByStun = false;
    }

    private void AttackTower(TowerHealth tower)
    {
        if (tower == null || tower.IsDestroyed)
        {
            return;
        }

        if (attackActionActive)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            PlayState(walkStateName, false);
            return;
        }

        attackRoutine = StartCoroutine(AttackTowerRoutine(tower));
    }

    private IEnumerator AttackTowerRoutine(TowerHealth tower)
    {
        attackActionActive = true;
        PlayState(attackStateName, true);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        float attackDuration = GetAnimationDuration(attackStateName, attackAnimationDurationFallback);
        if (attackDuration > 0f)
        {
            yield return new WaitForSeconds(attackDuration);
        }

        if (!isDead && Time.time >= stunUntilTime)
        {
            ResolveAttackDamage(tower);
        }

        nextAttackTime = Time.time + attackCooldown;
        attackActionActive = false;
        attackRoutine = null;
    }

    private void ResolveAttackDamage(TowerHealth tower)
    {
        if (attackAllTowersInRange)
        {
            List<TowerHealth> towers = FindTowersInAttackRange();
            for (int i = 0; i < towers.Count; i++)
            {
                DamageTower(towers[i]);
            }

            return;
        }

        if (IsTowerInCurrentAttackRange(tower))
        {
            DamageTower(tower);
        }
    }

    private void DamageTower(TowerHealth tower)
    {
        if (tower == null || tower.IsDestroyed)
        {
            return;
        }

        int damage = destroyTowersInOneHit
            ? Mathf.Max(contactDamage, tower.MaxHealth)
            : contactDamage;
        tower.TakeDamage(damage);
    }

    private void Die()
    {
        isDead = true;
        SetStunAnimationPaused(false);
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        attackActionActive = false;
        temporaryActionActive = false;
        temporaryActionUntilTime = 0f;
        PlayState(dieStateName, true);
        DropCoins();

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

    private void DropCoins()
    {
        if (coinsDropped || !dropCoinsOnDeath || coinDropCount <= 0)
        {
            return;
        }

        coinsDropped = true;
        Vector3 spawnPosition = CalculateDropPosition();
        int previousCoinDropTotal = coinDropCount * Mathf.Max(1, coinDropMultiplier);
        int totalCoinDrops = Mathf.Max(
            1,
            Mathf.CeilToInt(previousCoinDropTotal / (float)EnemyCoinCountDivisor));
        int valuePerCoin = Mathf.Max(1, coinDropValue) * EnemyCoinValueMultiplier;

        for (int i = 0; i < totalCoinDrops; i++)
        {
            CoinPickup pickup = SpawnCoinPickup(spawnPosition);
            if (pickup == null)
            {
                continue;
            }

            pickup.SetValue(valuePerCoin);
            pickup.ScatterTo(GetCoinScatterPosition(spawnPosition, i, totalCoinDrops));
        }
    }

    private CoinPickup SpawnCoinPickup(Vector3 spawnPosition)
    {
        if (coinPickupPrefab != null)
        {
            return Instantiate(coinPickupPrefab, spawnPosition, Quaternion.identity);
        }

        GameObject pickupObject = new("EnemyCoinPickup");
        pickupObject.transform.position = spawnPosition;
        SpriteRenderer pickupRenderer = pickupObject.AddComponent<SpriteRenderer>();
        pickupRenderer.sprite = coinPickupSprite != null
            ? coinPickupSprite
            : GetFallbackCoinSprite();
        pickupRenderer.color = Color.white;
        pickupRenderer.sortingOrder = 6;
        pickupObject.transform.localScale = Vector3.one * 0.65f;

        CoinPickup pickup = pickupObject.AddComponent<CoinPickup>();
        return pickup;
    }

    private static Sprite GetFallbackCoinSprite()
    {
        if (fallbackCoinSprite != null)
        {
            return fallbackCoinSprite;
        }

        const int size = 16;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[(y * size) + x] = distance <= radius
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        fallbackCoinSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        return fallbackCoinSprite;
    }

    private Vector3 CalculateDropPosition()
    {
        Bounds bounds = CalculateCombatBounds();
        Vector3 position = bounds.center;
        position.z = transform.position.z;
        return position;
    }

    private Vector3 GetCoinScatterPosition(Vector3 center, int coinIndex, int coinCount)
    {
        Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude <= 0.001f)
        {
            randomDirection = Vector2.up;
        }

        randomDirection.Normalize();
        float radius = coinDropScatterRadius * UnityEngine.Random.Range(0.65f, 1f);
        Vector3 position = center + new Vector3(
            randomDirection.x * radius,
            randomDirection.y * radius,
            0f);

        if (coinCount > 1)
        {
            float centeredIndex = coinIndex - ((coinCount - 1) * 0.5f);
            position.x += centeredIndex * 0.18f;
        }

        return position;
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

        Bounds goblinBounds = CalculateCombatBounds();
        Vector3 laneCenter = goblinBounds.center + (Vector3.up * towerLaneCenterOffset);
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
            if (!IsTowerInAttackLane(towerBounds, goblinBounds, laneCenter))
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

    private List<TowerHealth> FindTowersInAttackRange()
    {
        TowerHealth[] towers = FindObjectsByType<TowerHealth>(FindObjectsSortMode.None);
        List<TowerHealth> targets = new();
        if (towers.Length == 0)
        {
            return targets;
        }

        Bounds goblinBounds = CalculateCombatBounds();
        Vector3 laneCenter = goblinBounds.center + (Vector3.up * towerLaneCenterOffset);
        for (int i = 0; i < towers.Length; i++)
        {
            TowerHealth tower = towers[i];
            if (tower == null || tower.IsDestroyed || !tower.isActiveAndEnabled)
            {
                continue;
            }

            Bounds towerBounds = tower.GetWorldBounds();
            if (!IsTowerInAttackLane(towerBounds, goblinBounds, laneCenter))
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

            targets.Add(tower);
        }

        return targets;
    }

    private bool IsTowerInAttackLane(Bounds towerBounds, Bounds enemyBounds, Vector3 laneCenter)
    {
        if (!useBoundsVerticalAttackRange)
        {
            return Mathf.Abs(towerBounds.center.y - laneCenter.y) <= towerLaneTolerance;
        }

        float padding = Mathf.Max(0f, boundsVerticalAttackPadding);
        return towerBounds.max.y >= enemyBounds.min.y - padding &&
            towerBounds.min.y <= enemyBounds.max.y + padding;
    }

    private bool IsTowerInCurrentAttackRange(TowerHealth tower)
    {
        if (tower == null || tower.IsDestroyed || !tower.isActiveAndEnabled)
        {
            return false;
        }

        Bounds enemyBounds = CalculateCombatBounds();
        Bounds towerBounds = tower.GetWorldBounds();
        Vector3 laneCenter = enemyBounds.center + (Vector3.up * towerLaneCenterOffset);
        if (!IsTowerInAttackLane(towerBounds, enemyBounds, laneCenter))
        {
            return false;
        }

        if (towerBounds.center.x > enemyBounds.center.x + 0.1f)
        {
            return false;
        }

        if (enemyBounds.max.x < towerBounds.min.x)
        {
            return false;
        }

        float horizontalGap = enemyBounds.min.x - towerBounds.max.x;
        return horizontalGap <= towerAttackRange;
    }

    private void PlayState(string stateName, bool restart)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        if (!restart && currentAnimationState == stateName)
        {
            return;
        }

        currentAnimationState = stateName;
        if (restart)
        {
            animator.Play(stateName, 0, 0f);
            return;
        }

        animator.CrossFade(stateName, animationFadeTime);
    }

    private float GetAnimationDuration(string stateName, float fallbackDuration)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return Mathf.Max(0.01f, fallbackDuration);
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
            {
                float speed = Mathf.Abs(animator.speed);
                return speed > 0.001f ? clip.length / speed : clip.length;
            }
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.length > 0f)
        {
            float speed = Mathf.Abs(animator.speed);
            return speed > 0.001f ? stateInfo.length / speed : stateInfo.length;
        }

        return Mathf.Max(0.01f, fallbackDuration);
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

    private void ResetMovementPosition()
    {
        movementPosition = transform.position;
        hasMovementPosition = true;
    }

    private void ApplyMovementPosition()
    {
        if (body != null)
        {
            body.position = movementPosition;
        }

        transform.position = movementPosition;
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
                Color original = originalSpriteColors[i];
                float multiplier = stunVisualActive
                    ? stunTintMultiplier
                    : slowVisualActive
                        ? slowTintMultiplier
                        : 1f;
                spriteRenderers[i].color = new Color(
                    original.r * multiplier,
                    original.g * multiplier,
                    original.b * multiplier,
                    original.a);
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

    private Bounds CalculateCombatBounds()
    {
        if (hitbox != null && hitbox.enabled)
        {
            return hitbox.bounds;
        }

        return CalculateRendererBounds();
    }
}
