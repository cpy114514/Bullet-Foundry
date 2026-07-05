using UnityEngine;

public static class BulletTowerUtility
{
    public static void EnsureTriggerCollider(GameObject gameObject, SpriteRenderer spriteRenderer)
    {
        if (gameObject.TryGetComponent(out Collider2D collider2D))
        {
            collider2D.isTrigger = true;
            return;
        }

        BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = true;

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            boxCollider.size = spriteRenderer.sprite.bounds.size;
        }
    }

    public static bool IsInsideTower(Transform towerTransform, SpriteRenderer spriteRenderer, Vector3 position)
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            Bounds bounds = spriteRenderer.bounds;
            bounds.Expand(0.1f);
            return bounds.Contains(position);
        }

        return Vector2.Distance(towerTransform.position, position) <= 0.5f;
    }

    public static Vector3 GetTowerCenter(Transform towerTransform, SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
        {
            return spriteRenderer.bounds.center;
        }

        Collider2D collider2D = towerTransform.GetComponent<Collider2D>();
        return collider2D != null && collider2D.enabled
            ? collider2D.bounds.center
            : towerTransform.position;
    }

    public static bool AcceptsIncomingDirection(Bullet bullet, Vector3 towerCenter, bool acceptLeft, bool acceptUp, bool acceptDown, float requiredDot)
    {
        if (bullet == null)
        {
            return false;
        }

        Vector2 direction = bullet.Direction;
        Vector3 bulletPosition = bullet.transform.position;

        if (acceptLeft
            && bulletPosition.x <= towerCenter.x
            && Vector2.Dot(direction, Vector2.right) >= requiredDot)
        {
            return true;
        }

        if (acceptUp
            && bulletPosition.y >= towerCenter.y
            && Vector2.Dot(direction, Vector2.down) >= requiredDot)
        {
            return true;
        }

        if (acceptDown
            && bulletPosition.y <= towerCenter.y
            && Vector2.Dot(direction, Vector2.up) >= requiredDot)
        {
            return true;
        }

        return false;
    }
}
