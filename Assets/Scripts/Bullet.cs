using UnityEngine;

public enum BulletElement
{
    Normal,
    Fire,
    Ice
}

[DisallowMultipleComponent]
public sealed class Bullet : MonoBehaviour
{
    private const int NormalDamage = 3;
    private const int FireDamage = 5;
    private const int IceDamage = 3;
    private const float IceSlowMultiplier = 0.5f;
    private const float IceSlowDuration = 2f;

    [SerializeField, Min(0f)]
    private float moveSpeed = 10f;

    [SerializeField]
    private Vector2 moveDirection = Vector2.right;

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

    public BulletElement Element => element;

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
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasImpacted)
        {
            return;
        }

        transform.Translate((Vector3)(moveDirection.normalized * (moveSpeed * Time.deltaTime)), Space.World);
        UpdateSpriteAnimation();
    }

    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        moveDirection = direction.normalized;
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
        SetSpriteAnimation(frames, frameDuration);
        SetVisualScale(visualScale);
    }

    public void ResetToNormal()
    {
        CacheNormalVisual();
        element = BulletElement.Normal;
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
        lifetime = source.lifetime;
        spawnImpactEffect = source.spawnImpactEffect;
        element = source.element;
        normalSprite = source.normalSprite;
        normalScale = source.normalScale;
        normalVisualCached = source.normalVisualCached;

        animationFrames = source.animationFrames.Length == 0
            ? System.Array.Empty<Sprite>()
            : (Sprite[])source.animationFrames.Clone();
        animationFrameDuration = source.animationFrameDuration;
        animationTimer = source.animationTimer;
        animationFrameIndex = source.animationFrameIndex;

        transform.localScale = source.transform.localScale;
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

        if (spawnImpactEffect && impactEffect != null)
        {
            BeginImpactEffect();
            return;
        }

        Destroy(gameObject);
    }

    private int GetDamage()
    {
        return element switch
        {
            BulletElement.Fire => FireDamage,
            BulletElement.Ice => IceDamage,
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
            return;
        }

        moveDirection.Normalize();
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
