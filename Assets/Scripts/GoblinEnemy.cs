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
    [SerializeField]
    private Transform target;

    [SerializeField]
    private string targetObjectName = "Shooter";

    [SerializeField, Min(0.01f)]
    private float attackRange = 0.7f;

    [SerializeField, Min(0.01f)]
    private float attackCooldown = 1f;

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
    private Color hitFlashColor = Color.white;

    [SerializeField, Min(0f)]
    private float hitFlashDuration = 0.08f;

    private Animator animator;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalSpriteColors;
    private Coroutine hitFlashRoutine;
    private int currentHealth;
    private float nextAttackTime;
    private string currentAnimationState;
    private bool isDead;

    public bool IsDead => isDead;

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
        PlayState(walkStateName);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        ResolveTargetIfNeeded();

        if (IsTargetInAttackRange())
        {
            Attack();
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
        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void Walk()
    {
        PlayState(walkStateName);
        transform.Translate(Vector3.left * (moveSpeed * Time.deltaTime), Space.World);
    }

    private void Attack()
    {
        PlayState(attackStateName);

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        _ = contactDamage;
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

        Destroy(gameObject, destroyDelayAfterDeath);
    }

    private void ResolveTargetIfNeeded()
    {
        if (target != null || string.IsNullOrWhiteSpace(targetObjectName))
        {
            return;
        }

        GameObject targetObject = GameObject.Find(targetObjectName);
        if (targetObject != null)
        {
            target = targetObject.transform;
        }
    }

    private bool IsTargetInAttackRange()
    {
        if (target == null)
        {
            return false;
        }

        return Vector2.Distance(transform.position, target.position) <= attackRange;
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
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
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
