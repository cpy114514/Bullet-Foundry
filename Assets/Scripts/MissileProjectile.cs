using UnityEngine;

[DisallowMultipleComponent]
public sealed class MissileProjectile : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int damage = 10;

    [SerializeField, Min(0.1f)]
    private float moveSpeed = 5f;

    [SerializeField, Min(0.1f)]
    private float lifetime = 5f;

    [SerializeField]
    private bool rotateToMoveDirection = true;

    private Vector2 direction = Vector2.right;
    private SpriteRenderer spriteRenderer;
    private Collider2D missileCollider;
    private BulletImpactEffect impactEffect;
    private bool impacted;
    private float spawnTime;

    public void Launch(GoblinEnemy newTarget, int newDamage, float newMoveSpeed, Sprite missileSprite)
    {
        damage = Mathf.Max(1, newDamage);
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        direction = Vector2.right;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && missileSprite != null)
        {
            spriteRenderer.sprite = missileSprite;
            spriteRenderer.color = Color.white;
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 2;
        }

        if (!TryGetComponent(out missileCollider))
        {
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
            circleCollider.radius = 0.18f;
            missileCollider = circleCollider;
        }
        else
        {
            missileCollider.isTrigger = true;
        }

        if (!TryGetComponent(out Rigidbody2D body))
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        impactEffect = GetComponent<BulletImpactEffect>();
        if (impactEffect == null)
        {
            impactEffect = gameObject.AddComponent<BulletImpactEffect>();
        }

        spawnTime = Time.time;
    }

    private void Update()
    {
        if (impacted)
        {
            return;
        }

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.Translate((Vector3)(direction * (moveSpeed * Time.deltaTime)), Space.World);
        ApplyRotation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impacted)
        {
            return;
        }

        GoblinEnemy enemy = other.GetComponentInParent<GoblinEnemy>();
        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        enemy.TakeDamage(damage);
        BeginImpact();
    }

    private void ApplyRotation()
    {
        if (!rotateToMoveDirection || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void BeginImpact()
    {
        impacted = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (missileCollider != null)
        {
            missileCollider.enabled = false;
        }

        float delay = impactEffect != null
            ? impactEffect.PlayImpact(transform.position, direction, BulletElement.Missile, 0.55f)
            : 0f;
        Destroy(gameObject, delay);
    }
}
