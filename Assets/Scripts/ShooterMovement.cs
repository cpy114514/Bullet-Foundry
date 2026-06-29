using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ShooterMovement : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    [SerializeField]
    private Transform[] lanePoints = System.Array.Empty<Transform>();

    [SerializeField]
    private string lanePointParentName = "Shooter Lane Points";

    [SerializeField, Min(2)]
    private int laneCount = 5;

    [SerializeField]
    private string railwayObjectName = "railway";

    [SerializeField, Min(0.01f)]
    private float fallbackLaneSpacing = 1.25f;

    private Vector3[] fallbackLanePositions = System.Array.Empty<Vector3>();
    private int currentLaneIndex;

    private void Awake()
    {
        RefreshLanePointsFromSceneIfNeeded();
        RebuildFallbackLanePositions();
        SnapToNearestLane();
    }

    private void Update()
    {
        RefreshLanePointsFromSceneIfNeeded();

        int laneTotal = GetLaneTotal();
        if (laneTotal == 0)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
        {
            MoveToLane(currentLaneIndex + 1);
        }

        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
        {
            MoveToLane(currentLaneIndex - 1);
        }

        currentLaneIndex = Mathf.Clamp(currentLaneIndex, 0, laneTotal - 1);

        Vector3 targetPosition = GetLanePosition(currentLaneIndex);
        targetPosition.z = transform.position.z;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);
    }

    private void RefreshLanePointsFromSceneIfNeeded()
    {
        if (HasManualLanePoints())
        {
            return;
        }

        GameObject lanePointParent = GameObject.Find(lanePointParentName);
        if (lanePointParent == null)
        {
            return;
        }

        Transform parentTransform = lanePointParent.transform;
        lanePoints = new Transform[parentTransform.childCount];

        for (int i = 0; i < lanePoints.Length; i++)
        {
            lanePoints[i] = parentTransform.GetChild(i);
        }
    }

    private void RebuildFallbackLanePositions()
    {
        fallbackLanePositions = new Vector3[Mathf.Max(2, laneCount)];

        GameObject railway = GameObject.Find(railwayObjectName);
        SpriteRenderer railwayRenderer = railway != null
            ? railway.GetComponent<SpriteRenderer>()
            : null;

        if (railwayRenderer != null)
        {
            Bounds railwayBounds = railwayRenderer.bounds;
            float laneStep = railwayBounds.size.y / (fallbackLanePositions.Length + 1);

            for (int i = 0; i < fallbackLanePositions.Length; i++)
            {
                fallbackLanePositions[i] = new Vector3(
                    transform.position.x,
                    railwayBounds.min.y + laneStep * (i + 1),
                    transform.position.z);
            }

            return;
        }

        float middleY = transform.position.y;
        float firstLaneY = middleY - fallbackLaneSpacing * (fallbackLanePositions.Length - 1) * 0.5f;

        for (int i = 0; i < fallbackLanePositions.Length; i++)
        {
            fallbackLanePositions[i] = new Vector3(
                transform.position.x,
                firstLaneY + fallbackLaneSpacing * i,
                transform.position.z);
        }
    }

    private void SnapToNearestLane()
    {
        int laneTotal = GetLaneTotal();
        if (laneTotal == 0)
        {
            return;
        }

        currentLaneIndex = FindNearestLaneIndex(transform.position);

        Vector3 targetPosition = GetLanePosition(currentLaneIndex);
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
    }

    private int FindNearestLaneIndex(Vector3 position)
    {
        int nearestLaneIndex = 0;
        float nearestDistance = Vector2.Distance(position, GetLanePosition(0));

        for (int i = 1; i < GetLaneTotal(); i++)
        {
            float distance = Vector2.Distance(position, GetLanePosition(i));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestLaneIndex = i;
            }
        }

        return nearestLaneIndex;
    }

    private void MoveToLane(int laneIndex)
    {
        int laneTotal = GetLaneTotal();
        if (laneTotal == 0)
        {
            return;
        }

        currentLaneIndex = Mathf.Clamp(laneIndex, 0, laneTotal - 1);
    }

    private bool HasManualLanePoints()
    {
        if (lanePoints == null || lanePoints.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < lanePoints.Length; i++)
        {
            if (lanePoints[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private int GetLaneTotal()
    {
        if (HasManualLanePoints())
        {
            return lanePoints.Length;
        }

        return fallbackLanePositions.Length;
    }

    private Vector3 GetLanePosition(int laneIndex)
    {
        if (HasManualLanePoints())
        {
            return lanePoints[laneIndex].position;
        }

        return fallbackLanePositions[laneIndex];
    }
}
