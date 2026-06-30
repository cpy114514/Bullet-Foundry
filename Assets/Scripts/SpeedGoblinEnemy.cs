using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinEnemy))]
public sealed class SpeedGoblinEnemy : MonoBehaviour
{
    [SerializeField]
    private GoblinEnemy enemy;

    [SerializeField]
    private GameObject hatObject;

    [SerializeField, Min(0f)]
    private float hattedMoveSpeed = 1.8f;

    [SerializeField, Min(0f)]
    private float unhattedMoveSpeed = 1f;

    private bool hasHat = true;

    public bool HasHat => hasHat;

    private void Awake()
    {
        ResolveEnemy();
    }

    private void OnEnable()
    {
        ResolveEnemy();
        if (enemy != null)
        {
            enemy.HealthChanged += HandleHealthChanged;
        }

        RestoreHat();
    }

    private void OnDisable()
    {
        if (enemy != null)
        {
            enemy.HealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (!hasHat || maxHealth <= 0 || currentHealth * 2 > maxHealth)
        {
            return;
        }

        RemoveHat();
    }

    private void RestoreHat()
    {
        hasHat = true;
        if (hatObject != null)
        {
            hatObject.SetActive(true);
        }

        if (enemy != null)
        {
            enemy.SetMoveSpeed(hattedMoveSpeed);
        }
    }

    private void RemoveHat()
    {
        hasHat = false;
        if (hatObject != null)
        {
            hatObject.SetActive(false);
        }

        if (enemy != null)
        {
            enemy.SetMoveSpeed(unhattedMoveSpeed);
        }
    }

    private void ResolveEnemy()
    {
        if (enemy == null)
        {
            enemy = GetComponent<GoblinEnemy>();
        }
    }
}
