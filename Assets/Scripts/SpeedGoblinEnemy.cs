using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinEnemy))]
public sealed class SpeedGoblinEnemy : MonoBehaviour
{
    [SerializeField]
    private GoblinEnemy enemy;

    [SerializeField]
    private GameObject hatObject;

    [SerializeField]
    private GameObject normalGoblinPrefab;

    [SerializeField, Min(1)]
    private int speedGoblinHealth = 5;

    [SerializeField, Min(0f)]
    private float hattedMoveSpeed = 1.8f;

    private bool replacementSpawned;

    private void Awake()
    {
        ResolveEnemy();
    }

    private void OnEnable()
    {
        ResolveEnemy();
        replacementSpawned = false;

        if (hatObject != null)
        {
            hatObject.SetActive(true);
        }

        if (enemy != null)
        {
            enemy.Died += HandleHatDestroyed;
            enemy.SetMoveSpeed(hattedMoveSpeed);
            enemy.RefillHealth(speedGoblinHealth);
        }
    }

    private void OnDisable()
    {
        if (enemy != null)
        {
            enemy.Died -= HandleHatDestroyed;
        }
    }

    private void HandleHatDestroyed()
    {
        if (replacementSpawned)
        {
            return;
        }

        replacementSpawned = true;
        HideBodyAndKeepHat();

        if (normalGoblinPrefab == null)
        {
            Debug.LogWarning("Speed Goblin has no normal Goblin prefab assigned.", this);
            return;
        }

        Instantiate(
            normalGoblinPrefab,
            transform.position,
            transform.rotation,
            transform.parent);
    }

    private void HideBodyAndKeepHat()
    {
        SpriteRenderer[] hatRenderers = hatObject != null
            ? hatObject.GetComponentsInChildren<SpriteRenderer>(true)
            : System.Array.Empty<SpriteRenderer>();
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            SpriteRenderer renderer = allRenderers[i];
            bool belongsToHat = System.Array.IndexOf(hatRenderers, renderer) >= 0;
            renderer.enabled = belongsToHat;
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
