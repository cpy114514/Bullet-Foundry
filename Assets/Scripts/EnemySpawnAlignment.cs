using UnityEngine;

public static class EnemySpawnAlignment
{
    private const float RowMatchTolerance = 0.01f;

    public static GameObject InstantiateFootAligned(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        float targetFootY)
    {
        GameObject enemy = Object.Instantiate(prefab, position, rotation, parent);
        AlignFootToY(enemy, targetFootY);
        return enemy;
    }

    public static void AlignFootToY(GameObject enemy, float targetFootY)
    {
        if (enemy == null)
        {
            return;
        }

        float footY = GetFootY(enemy);
        Vector3 position = enemy.transform.position;
        position.y += targetFootY - footY;
        enemy.transform.position = position;

        GoblinEnemy goblin = enemy.GetComponent<GoblinEnemy>();
        if (goblin != null)
        {
            goblin.SyncMovementPositionToTransform();
        }
    }

    public static float GetFootY(GameObject enemy)
    {
        if (enemy == null)
        {
            return 0f;
        }

        if (TryGetRendererBounds(enemy, out Bounds rendererBounds))
        {
            return rendererBounds.min.y;
        }

        if (TryGetColliderBounds(enemy, out Bounds colliderBounds))
        {
            return colliderBounds.min.y;
        }

        return enemy.transform.position.y;
    }

    public static float GetLandBottomYForLane(int targetLaneIndex, Transform[] lanes, float fallbackY)
    {
        if (lanes == null || lanes.Length == 0)
        {
            return fallbackY;
        }

        int laneIndex = Mathf.Clamp(targetLaneIndex - 1, 0, lanes.Length - 1);
        Transform lane = lanes[laneIndex];
        if (lane == null)
        {
            return fallbackY;
        }

        return GetLandBottomYForLane(lane.position.y, fallbackY);
    }

    public static float GetLandBottomYForLane(float laneY, float fallbackY)
    {
        LandPlot[] lands = Object.FindObjectsByType<LandPlot>(FindObjectsSortMode.None);
        if (lands == null || lands.Length == 0)
        {
            return fallbackY;
        }

        float nearestRowDistance = float.PositiveInfinity;
        for (int i = 0; i < lands.Length; i++)
        {
            LandPlot land = lands[i];
            if (land == null)
            {
                continue;
            }

            nearestRowDistance = Mathf.Min(
                nearestRowDistance,
                Mathf.Abs(land.transform.position.y - laneY));
        }

        if (float.IsPositiveInfinity(nearestRowDistance))
        {
            return fallbackY;
        }

        bool foundBottom = false;
        float bottomY = fallbackY;
        float maxRowDistance = nearestRowDistance + RowMatchTolerance;
        for (int i = 0; i < lands.Length; i++)
        {
            LandPlot land = lands[i];
            if (land == null ||
                Mathf.Abs(land.transform.position.y - laneY) > maxRowDistance ||
                !TryGetLandRendererBounds(land, out Bounds bounds))
            {
                continue;
            }

            bottomY = foundBottom ? Mathf.Min(bottomY, bounds.min.y) : bounds.min.y;
            foundBottom = true;
        }

        return foundBottom ? bottomY : fallbackY;
    }

    private static bool TryGetRendererBounds(GameObject enemy, out Bounds bounds)
    {
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool TryGetLandRendererBounds(LandPlot land, out Bounds bounds)
    {
        SpriteRenderer renderer = land.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.enabled)
        {
            bounds = renderer.bounds;
            return true;
        }

        Renderer fallbackRenderer = land.GetComponent<Renderer>();
        if (fallbackRenderer != null && fallbackRenderer.enabled)
        {
            bounds = fallbackRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private static bool TryGetColliderBounds(GameObject enemy, out Bounds bounds)
    {
        Collider2D[] colliders = enemy.GetComponentsInChildren<Collider2D>(true);
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }
}
