using UnityEngine;

[DisallowMultipleComponent]
public sealed class Bullet : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float moveSpeed = 10f;

    [SerializeField]
    private Vector2 moveDirection = Vector2.right;

    [SerializeField, Min(0.1f)]
    private float lifetime = 5f;

    [SerializeField, Min(1)]
    private int damage = 1;

    [SerializeField]
    private bool spawnImpactEffect = true;

    private SpriteRenderer spriteRenderer;
    private Sprite[] animationFrames = System.Array.Empty<Sprite>();
    private float animationFrameDuration = 0.08f;
    private float animationTimer;
    private int animationFrameIndex;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsurePhysicsComponents();
    }

    private void OnEnable()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        GoblinEnemy goblin = other.GetComponentInParent<GoblinEnemy>();
        if (goblin == null || goblin.IsDead)
        {
            return;
        }

        if (spawnImpactEffect)
        {
            Vector3 impactPosition = other.ClosestPoint(transform.position);
            BulletImpactEffect.Spawn(impactPosition, moveDirection);
        }

        goblin.TakeDamage(damage);
        Destroy(gameObject);
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
        if (!TryGetComponent(out Collider2D _))
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
            circleCollider.radius = 0.1f;
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
