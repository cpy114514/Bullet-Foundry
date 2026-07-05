using UnityEngine;

[DisallowMultipleComponent]
public sealed class HomingBullet : MonoBehaviour
{
    [SerializeField, Min(1f)]
    private float maxTurnDegreesPerSecond = 70f;

    [SerializeField, Min(0f)]
    private float retargetInterval = 0.12f;

    private Bullet bullet;
    private GoblinEnemy target;
    private float nextRetargetTime;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();
    }

    private void Update()
    {
        if (bullet == null)
        {
            return;
        }

        if (target == null || target.IsDead || Time.time >= nextRetargetTime)
        {
            target = FindNearestEnemy();
            nextRetargetTime = Time.time + retargetInterval;
        }

        if (target == null)
        {
            return;
        }

        Vector3 targetCenter = target.GetWorldBounds().center;
        Vector2 desiredDirection = targetCenter - transform.position;
        if (desiredDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        bullet.SetDirection(RotateToward(
            bullet.Direction,
            desiredDirection.normalized,
            maxTurnDegreesPerSecond * Time.deltaTime));
    }

    private GoblinEnemy FindNearestEnemy()
    {
        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(FindObjectsSortMode.None);
        GoblinEnemy nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < enemies.Length; i++)
        {
            GoblinEnemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            float distance = (enemy.GetWorldBounds().center - transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static Vector2 RotateToward(Vector2 currentDirection, Vector2 targetDirection, float maxDegrees)
    {
        if (currentDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return targetDirection.normalized;
        }

        float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(0f, maxDegrees));
        float radians = nextAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }
}
