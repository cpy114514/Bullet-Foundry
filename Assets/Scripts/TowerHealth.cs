using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerHealth : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int maxHealth = 3;

    [SerializeField]
    private int currentHealth;

    private SpriteRenderer[] spriteRenderers;
    private bool isDestroyed;

    public int CurrentHealth => currentHealth;

    public int MaxHealth => maxHealth;

    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        CacheRenderers();
        ResetHealth();
    }

    private void OnEnable()
    {
        ResetHealth();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth > 0)
        {
            return;
        }

        isDestroyed = true;
        Destroy(gameObject);
    }

    public Bounds GetWorldBounds()
    {
        CacheRenderers();
        if (spriteRenderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one * 0.5f);
        }

        Bounds bounds = spriteRenderers[0].bounds;
        for (int i = 1; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].enabled)
            {
                bounds.Encapsulate(spriteRenderers[i].bounds);
            }
        }

        return bounds;
    }

    private void ResetHealth()
    {
        isDestroyed = false;
        currentHealth = maxHealth;
    }

    private void CacheRenderers()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}
