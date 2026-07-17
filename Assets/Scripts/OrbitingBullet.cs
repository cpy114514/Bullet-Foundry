using UnityEngine;

[DisallowMultipleComponent]
public sealed class OrbitingBullet : MonoBehaviour
{
    private const float DefaultOrbitDuration = 2.5f;

    [SerializeField, Min(0.05f)]
    private float radius = 0.5f;

    [SerializeField, Min(1f)]
    private float angularSpeedDegrees = 220f;

    [SerializeField, Min(0.1f)]
    private float orbitDuration = DefaultOrbitDuration;

    private Bullet bullet;
    private Transform orbitCenter;
    private float angle;
    private float startTime;

    public void Begin(Transform center, float newRadius, float newAngularSpeedDegrees, float newDuration)
    {
        orbitCenter = center;
        radius = Mathf.Max(0.05f, newRadius);
        angularSpeedDegrees = Mathf.Max(1f, newAngularSpeedDegrees);
        orbitDuration = newDuration > 0f ? newDuration : DefaultOrbitDuration;
        startTime = Time.time;

        bullet = GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.SetManualMotion(true);
        }

        Vector2 offset = transform.position - orbitCenter.position;
        angle = offset.sqrMagnitude > 0.001f
            ? Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg
            : 0f;
    }

    private void Update()
    {
        if (orbitCenter == null)
        {
            Destroy(this);
            return;
        }

        if (Time.time - startTime >= orbitDuration)
        {
            Destroy(gameObject);
            return;
        }

        angle += angularSpeedDegrees * Time.deltaTime;
        float radians = angle * Mathf.Deg2Rad;
        Vector3 center = orbitCenter.position;
        Vector3 nextPosition = center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius;
        nextPosition.z = transform.position.z;

        Vector2 tangent = new(-Mathf.Sin(radians), Mathf.Cos(radians));
        transform.position = nextPosition;
        if (bullet != null)
        {
            bullet.SetDirection(tangent);
            bullet.SetManualMotion(true);
        }
    }

    private void OnDisable()
    {
        if (bullet != null)
        {
            bullet.SetManualMotion(false);
        }
    }
}
