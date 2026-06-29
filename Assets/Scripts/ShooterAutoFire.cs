using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShooterAutoFire : MonoBehaviour
{
    [SerializeField]
    private GameObject bulletPrefab;

    [SerializeField, Min(0.05f)]
    private float fireInterval = 0.5f;

    [SerializeField]
    private Vector3 spawnOffset = new(1.2f, 0f, 0f);

    private float fireTimer;

    private void Update()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f)
        {
            return;
        }

        Fire();
        fireTimer = Mathf.Max(0.05f, fireInterval);
    }

    private void Fire()
    {
        if (bulletPrefab == null)
        {
            return;
        }

        Instantiate(bulletPrefab, transform.position + spawnOffset, Quaternion.identity);
    }
}
