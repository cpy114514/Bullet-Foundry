using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class FireTowerPlacementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera worldCamera;

    [SerializeField]
    private SpriteRenderer fireTowerCardRenderer;

    [SerializeField]
    private GameObject fireTowerPrefab;

    [Header("Selection Feedback")]
    [SerializeField]
    private Color selectedCardColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private Color normalCardColor = Color.white;
    private bool isFireTowerSelected;

    private void Awake()
    {
        ResolveCamera();
        CacheNormalCardColor();
    }

    private void OnEnable()
    {
        ResolveCamera();
        CacheNormalCardColor();
        SetFireTowerSelected(false);
    }

    private void OnDisable()
    {
        RestoreCardColor();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetFireTowerSelected(false);
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            SetFireTowerSelected(false);
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame || !TryGetPointerWorldPosition(mouse, out Vector2 worldPosition))
        {
            return;
        }

        if (IsPointInsideRenderer(fireTowerCardRenderer, worldPosition))
        {
            SetFireTowerSelected(!isFireTowerSelected);
            return;
        }

        if (!isFireTowerSelected)
        {
            return;
        }

        LandPlot land = FindLandAt(worldPosition);
        if (land == null || land.IsOccupied)
        {
            return;
        }

        PlaceFireTower(land);
        SetFireTowerSelected(false);
    }

    private void PlaceFireTower(LandPlot land)
    {
        if (land == null || fireTowerPrefab == null)
        {
            return;
        }

        Vector3 placementPosition = land.transform.position;
        placementPosition.z = fireTowerPrefab.transform.position.z;

        GameObject tower = Instantiate(
            fireTowerPrefab,
            placementPosition,
            fireTowerPrefab.transform.rotation);

        tower.name = fireTowerPrefab.name;
        land.SetTower(tower.transform);
    }

    private void SetFireTowerSelected(bool selected)
    {
        isFireTowerSelected = selected && fireTowerPrefab != null;

        if (fireTowerCardRenderer == null)
        {
            return;
        }

        fireTowerCardRenderer.color = isFireTowerSelected
            ? selectedCardColor
            : normalCardColor;
    }

    private void CacheNormalCardColor()
    {
        if (fireTowerCardRenderer != null)
        {
            normalCardColor = fireTowerCardRenderer.color;
        }
    }

    private void RestoreCardColor()
    {
        if (fireTowerCardRenderer != null)
        {
            fireTowerCardRenderer.color = normalCardColor;
        }
    }

    private bool TryGetPointerWorldPosition(Mouse mouse, out Vector2 worldPosition)
    {
        ResolveCamera();
        if (worldCamera == null)
        {
            worldPosition = default;
            return false;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        Vector3 screenPoint = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(worldCamera.transform.position.z));

        Vector3 point = worldCamera.ScreenToWorldPoint(screenPoint);
        worldPosition = new Vector2(point.x, point.y);
        return true;
    }

    private static LandPlot FindLandAt(Vector2 worldPosition)
    {
        LandPlot[] lands = FindObjectsByType<LandPlot>(FindObjectsSortMode.None);
        for (int i = 0; i < lands.Length; i++)
        {
            LandPlot land = lands[i];
            if (land == null || !land.isActiveAndEnabled)
            {
                continue;
            }

            SpriteRenderer renderer = land.GetComponent<SpriteRenderer>();
            if (IsPointInsideRenderer(renderer, worldPosition))
            {
                return land;
            }
        }

        return null;
    }

    private static bool IsPointInsideRenderer(SpriteRenderer renderer, Vector2 worldPosition)
    {
        if (renderer == null || !renderer.enabled || renderer.sprite == null)
        {
            return false;
        }

        return renderer.bounds.Contains(new Vector3(
            worldPosition.x,
            worldPosition.y,
            renderer.bounds.center.z));
    }

    private void ResolveCamera()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }
}
