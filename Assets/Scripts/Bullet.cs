using UnityEngine;

public enum BulletElement
{
    Normal,
    Fire,
    Ice,
    Lightning,
    Homing,
    Missile
}

[DisallowMultipleComponent]
public sealed class Bullet : MonoBehaviour
{
    private const int NormalDamage = 3;
    private const int FireDamage = 5;
    private const int IceDamage = 3;
    private const int LightningDamage = 3;
    private const int HomingDamage = 3;
    private const int MissileDamage = 8;
    private const float IceSlowMultiplier = 0.5f;
    private const float IceSlowDuration = 2f;
    private const float LightningStunDuration = 0.25f;

    [SerializeField, Min(0f)]
    private float moveSpeed = 10f;

    [SerializeField]
    private Vector2 moveDirection = Vector2.right;

    [SerializeField]
    private bool rotateToMoveDirection = true;

    [SerializeField]
    private float rotationAngleOffset;

    [SerializeField, Min(0.1f)]
    private float lifetime = 5f;

    [SerializeField]
    private bool spawnImpactEffect = true;

    private SpriteRenderer spriteRenderer;
    private Collider2D bulletCollider;
    private BulletImpactEffect impactEffect;
    private Sprite[] animationFrames = System.Array.Empty<Sprite>();
    private float animationFrameDuration = 0.08f;
    private float animationTimer;
    private int animationFrameIndex;
    private BulletElement element = BulletElement.Normal;
    private Sprite normalSprite;
    private Vector3 normalScale;
    private bool normalVisualCached;
    private bool hasImpacted;
    private bool hasLaunchTarget;
    private Vector3 launchTargetPosition;
    private Vector2 launchFinalDirection = Vector2.right;
    private bool isPausedForTowerQueue;
    private bool manualMotion;
    private float remainingLifetime;

    public BulletElement Element => element;

    public Vector2 Direction => moveDirection.normalized;

    public float MoveSpeed => moveSpeed;

    public int CurrentDamage => GetDamage();

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        impactEffect = GetComponent<BulletImpactEffect>();
        if (impactEffect == null)
        {
            impactEffect = gameObject.AddComponent<BulletImpactEffect>();
        }

        CacheNormalVisual();
        EnsurePhysicsComponents();
    }

    private void OnEnable()
    {
        remainingLifetime = lifetime;
        ApplyRotationToMoveDirection();
    }

    private void Update()
    {
        if (hasImpacted)
        {
            return;
        }

        if (isPausedForTowerQueue)
        {
            return;
        }

        if (!manualMotion)
        {
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (!manualMotion)
        {
            MoveBullet(Time.deltaTime);
        }

        UpdateSpriteAnimation();
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        hasLaunchTarget = false;
        isPausedForTowerQueue = false;
        manualMotion = false;
        moveDirection = direction.normalized;
        ApplyRotationToMoveDirection();
    }

    public void SetManualMotion(bool enabled)
    {
        manualMotion = enabled;
        if (!enabled)
        {
            ApplyRotationToMoveDirection();
        }
    }

    public void PauseForTowerQueue()
    {
        isPausedForTowerQueue = true;
        hasLaunchTarget = false;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (bulletCollider == null)
        {
            bulletCollider = GetComponent<Collider2D>();
        }

        if (bulletCollider != null)
        {
            bulletCollider.enabled = false;
        }
    }

    public void ResumeFromTowerQueue()
    {
        isPausedForTowerQueue = false;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (bulletCollider == null)
        {
            bulletCollider = GetComponent<Collider2D>();
        }

        if (bulletCollider != null)
        {
            bulletCollider.enabled = true;
        }
    }

    public void FlyToThenContinue(Vector3 targetPosition, Vector2 finalDirection)
    {
        if (finalDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            finalDirection = moveDirection.sqrMagnitude > Mathf.Epsilon
                ? moveDirection
                : Vector2.right;
        }

        launchFinalDirection = finalDirection.normalized;
        launchTargetPosition = targetPosition;
        launchTargetPosition.z = transform.position.z;

        Vector2 toTarget = launchTargetPosition - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            hasLaunchTarget = false;
            moveDirection = launchFinalDirection;
            ApplyRotationToMoveDirection();
            return;
        }

        hasLaunchTarget = true;
        moveDirection = toTarget.normalized;
        ApplyRotationToMoveDirection();
    }

    public void SetSpriteAnimation(Sprite[] frames, float frameDuration)
    {
        int validFrameCount = CountValidFrames(frames);
        if (validFrameCount == 0)
        {
            return;
        }

        animationFrames = new Sprite[validFrameCount];
        int nextFrameIndex = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
            {
                continue;
            }

            animationFrames[nextFrameIndex] = frames[i];
            nextFrameIndex++;
        }

        animationFrameDuration = Mathf.Max(0.01f, frameDuration);
        animationTimer = 0f;
        animationFrameIndex = 0;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && animationFrames[0] != null)
        {
            spriteRenderer.sprite = animationFrames[0];
        }
    }

    public void SetVisualScale(Vector2 scale)
    {
        transform.localScale = new Vector3(
            Mathf.Max(0.01f, scale.x),
            Mathf.Max(0.01f, scale.y),
            transform.localScale.z);
    }

    public void SetVisualColor(Color color)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    public void ApplyElement(
        BulletElement newElement,
        Sprite[] frames,
        float frameDuration,
        Vector2 visualScale)
    {
        if (newElement == BulletElement.Normal)
        {
            ResetToNormal();
            return;
        }

        CacheNormalVisual();
        element = newElement;
        if (newElement != BulletElement.Homing)
        {
            RemoveHomingBehavior();
        }

        if (newElement != BulletElement.Lightning)
        {
            RemoveLightningBehavior();
        }

        SetSpriteAnimation(frames, frameDuration);
        SetVisualScale(visualScale);
    }

    public void ResetToNormal()
    {
        CacheNormalVisual();
        element = BulletElement.Normal;
        RemoveHomingBehavior();
        RemoveLightningBehavior();
        animationFrames = System.Array.Empty<Sprite>();
        animationTimer = 0f;
        animationFrameIndex = 0;
        transform.localScale = normalScale;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = normalSprite;
            spriteRenderer.color = Color.white;
        }
    }

    public void CopyRuntimeStateFrom(Bullet source)
    {
        if (source == null || source == this)
        {
            return;
        }

        moveSpeed = source.moveSpeed;
        moveDirection = source.moveDirection;
        rotateToMoveDirection = source.rotateToMoveDirection;
        rotationAngleOffset = source.rotationAngleOffset;
        lifetime = source.lifetime;
        spawnImpactEffect = source.spawnImpactEffect;
        element = source.element;
        normalSprite = source.normalSprite;
        normalScale = source.normalScale;
        normalVisualCached = source.normalVisualCached;
        hasLaunchTarget = false;
        launchTargetPosition = Vector3.zero;
        launchFinalDirection = Vector2.right;
        isPausedForTowerQueue = false;
        manualMotion = false;
        remainingLifetime = source.remainingLifetime > 0f
            ? source.remainingLifetime
            : source.lifetime;

        animationFrames = source.animationFrames.Length == 0
            ? System.Array.Empty<Sprite>()
            : (Sprite[])source.animationFrames.Clone();
        animationFrameDuration = source.animationFrameDuration;
        animationTimer = source.animationTimer;
        animationFrameIndex = source.animationFrameIndex;

        transform.localScale = source.transform.localScale;
        ApplyRotationToMoveDirection();
        CopyRendererState(source);
    }

    private void CacheNormalVisual()
    {
        if (normalVisualCached)
        {
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        normalSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        normalScale = transform.localScale;
        normalVisualCached = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasImpacted)
        {
            return;
        }

        GoblinEnemy goblin = other.GetComponentInParent<GoblinEnemy>();
        if (goblin == null || goblin.IsDead)
        {
            return;
        }

        goblin.TakeDamage(GetDamage());
        if (element == BulletElement.Ice && !goblin.IsDead)
        {
            goblin.ApplySlow(IceSlowMultiplier, IceSlowDuration);
            EnemySlowEffect.Apply(goblin, IceSlowDuration);
        }
        else if (element == BulletElement.Lightning && !goblin.IsDead)
        {
            goblin.ApplyStun(LightningStunDuration);
        }

        if (spawnImpactEffect && impactEffect != null)
        {
            BeginImpactEffect();
            return;
        }

        Destroy(gameObject);
    }

    public int GetDamage()
    {
        return element switch
        {
            BulletElement.Fire => FireDamage,
            BulletElement.Ice => IceDamage,
            BulletElement.Lightning => LightningDamage,
            BulletElement.Homing => HomingDamage,
            BulletElement.Missile => MissileDamage,
            _ => NormalDamage
        };
    }

    public void SpawnConversionEffect(BulletElement effectElement)
    {
        if (impactEffect == null)
        {
            impactEffect = GetComponent<BulletImpactEffect>();
        }

        if (impactEffect != null)
        {
            impactEffect.PlayConversion(
                GetBulletWorldCenter(),
                effectElement,
                GetEffectWorldSize());
        }
    }

    private void BeginImpactEffect()
    {
        hasImpacted = true;
        Vector3 impactPosition = GetBulletWorldCenter();
        float effectWorldSize = GetEffectWorldSize();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (bulletCollider != null)
        {
            bulletCollider.enabled = false;
        }

        float effectDuration = impactEffect.PlayImpact(
            impactPosition,
            moveDirection,
            element,
            effectWorldSize);
        Destroy(gameObject, effectDuration);
    }

    private Vector3 GetBulletWorldCenter()
    {
        if (bulletCollider != null && bulletCollider.enabled)
        {
            return bulletCollider.bounds.center;
        }

        if (spriteRenderer != null)
        {
            return spriteRenderer.bounds.center;
        }

        return transform.position;
    }

    private float GetEffectWorldSize()
    {
        if (bulletCollider == null)
        {
            bulletCollider = GetComponent<Collider2D>();
        }

        if (bulletCollider != null)
        {
            Vector3 size = bulletCollider.bounds.size;
            return Mathf.Clamp(Mathf.Max(size.x, size.y), 0.08f, 0.6f);
        }

        if (spriteRenderer != null)
        {
            Vector3 size = spriteRenderer.bounds.size;
            return Mathf.Clamp(Mathf.Min(size.x, size.y), 0.08f, 0.6f);
        }

        return 0.2f;
    }

    private void OnValidate()
    {
        if (moveDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            moveDirection = Vector2.right;
        }

        moveDirection.Normalize();
        ApplyRotationToMoveDirection();
    }

    private void MoveBullet(float deltaTime)
    {
        if (!hasLaunchTarget)
        {
            ApplyRotationToMoveDirection();
            transform.Translate((Vector3)(moveDirection.normalized * (moveSpeed * deltaTime)), Space.World);
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 toTarget = launchTargetPosition - currentPosition;
        float stepDistance = moveSpeed * deltaTime;

        if (toTarget.sqrMagnitude <= stepDistance * stepDistance)
        {
            transform.position = launchTargetPosition;
            hasLaunchTarget = false;
            moveDirection = launchFinalDirection;
            ApplyRotationToMoveDirection();
            return;
        }

        moveDirection = toTarget.normalized;
        ApplyRotationToMoveDirection();
        transform.Translate((Vector3)(moveDirection * stepDistance), Space.World);
    }

    private void ApplyRotationToMoveDirection()
    {
        if (!rotateToMoveDirection || moveDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationAngleOffset);
    }

    private void EnsurePhysicsComponents()
    {
        if (!TryGetComponent(out bulletCollider))
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
            circleCollider.radius = 0.1f;
            bulletCollider = circleCollider;
        }

        if (!TryGetComponent(out Rigidbody2D rigidbody2D))
        {
            rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
        }

        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.gravityScale = 0f;
    }

    private void UpdateSpriteAnimation()
    {
        if (spriteRenderer == null || animationFrames.Length == 0)
        {
            return;
        }

        animationTimer += Time.deltaTime;
        if (animationTimer < animationFrameDuration)
        {
            return;
        }

        animationTimer -= animationFrameDuration;
        animationFrameIndex = (animationFrameIndex + 1) % animationFrames.Length;

        Sprite nextFrame = animationFrames[animationFrameIndex];
        if (nextFrame != null)
        {
            spriteRenderer.sprite = nextFrame;
        }
    }

    private void CopyRendererState(Bullet source)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (source.spriteRenderer == null)
        {
            source.spriteRenderer = source.GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null || source.spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = source.spriteRenderer.sprite;
        spriteRenderer.color = source.spriteRenderer.color;
        spriteRenderer.flipX = source.spriteRenderer.flipX;
        spriteRenderer.flipY = source.spriteRenderer.flipY;
        spriteRenderer.sharedMaterial = source.spriteRenderer.sharedMaterial;
    }

    private void RemoveHomingBehavior()
    {
        HomingBullet homingBullet = GetComponent<HomingBullet>();
        if (homingBullet != null)
        {
            Destroy(homingBullet);
        }
    }

    private void RemoveLightningBehavior()
    {
        LightningBulletEffect lightningEffect = GetComponent<LightningBulletEffect>();
        if (lightningEffect != null)
        {
            Destroy(lightningEffect);
        }
    }

    private static int CountValidFrames(Sprite[] frames)
    {
        if (frames == null)
        {
            return 0;
        }

        int validFrameCount = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                validFrameCount++;
            }
        }

        return validFrameCount;
    }
}
