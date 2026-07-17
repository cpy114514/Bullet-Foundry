using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerRemovalToolButton : MonoBehaviour
{
    private const float DragThresholdPixels = 8f;

    [SerializeField]
    private FireTowerPlacementSystem placementSystem;

    [Header("Visuals")]
    [SerializeField]
    private Color normalColor = Color.white;

    [SerializeField]
    private Color activeColor = new Color(0.42f, 0.42f, 0.42f, 1f);

    [SerializeField, Range(1f, 1.2f)]
    private float hoverScale = 1.08f;

    [SerializeField, Range(0.8f, 1f)]
    private float pressedScale = 0.94f;

    [SerializeField, Min(1f)]
    private float scaleSpeed = 14f;

    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale;
    private bool pointerIsOverButton;
    private bool pointerIsPressingButton;
    private GameObject dragPreviewObject;

#if ENABLE_INPUT_SYSTEM
    private bool pointerPressedOnThisButton;
    private bool pointerDragMoved;
    private Vector2 pointerPressScreenPosition;
#endif

    private void Reset()
    {
        ResolvePlacementSystem();
        FitColliderToSprite();
    }

    private void OnValidate()
    {
        CacheReferences();
        FitColliderToSprite();
        ApplyColor();
    }

    private void Awake()
    {
        CacheReferences();
        ResolvePlacementSystem();
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        pointerIsOverButton = false;
        pointerIsPressingButton = false;
        DestroyDragPreview();
        ApplyColor();
    }

    private void OnDisable()
    {
        DestroyDragPreview();
    }

#if ENABLE_INPUT_SYSTEM
    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || CardSelectionMenu.IsOpen)
        {
            pointerPressedOnThisButton = false;
            pointerDragMoved = false;
            pointerIsOverButton = false;
            pointerIsPressingButton = false;
            DestroyDragPreview();
            UpdateVisuals();
            return;
        }

        Vector2 screenPosition = pointer.position.ReadValue();
        pointerIsOverButton = PointerIsOverThisButton(screenPosition);

        if (pointer.press.wasPressedThisFrame)
        {
            pointerPressedOnThisButton = pointerIsOverButton;
            pointerIsPressingButton = pointerPressedOnThisButton;
            pointerDragMoved = false;
            pointerPressScreenPosition = screenPosition;
        }

        if (pointerPressedOnThisButton && pointer.press.isPressed)
        {
            UpdateDrag(screenPosition);
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            bool shouldDragRemove = pointerPressedOnThisButton && pointerDragMoved;
            bool shouldPress = pointerPressedOnThisButton && pointerIsOverButton && !pointerDragMoved;
            pointerPressedOnThisButton = false;
            pointerDragMoved = false;
            pointerIsPressingButton = false;

            if (shouldDragRemove)
            {
                TryRemoveAtScreenPosition(screenPosition);
                DestroyDragPreview();
            }
            else if (shouldPress)
            {
                Press();
            }
            else
            {
                DestroyDragPreview();
            }
        }

        if (!pointer.press.isPressed)
        {
            pointerIsPressingButton = false;
        }

        UpdateVisuals();
    }
#else
    private void Update()
    {
        UpdateVisuals();
    }

    private void OnMouseUpAsButton()
    {
        Press();
    }
#endif

    public void SetPlacementSystem(FireTowerPlacementSystem system)
    {
        placementSystem = system;
    }

    public void Press()
    {
        ResolvePlacementSystem();
        if (placementSystem == null)
        {
            Debug.LogWarning($"{name} cannot toggle tower removal because FireTowerPlacementSystem is missing.");
            return;
        }

        placementSystem.ToggleTowerRemovalMode();
        ApplyColor();
    }

#if ENABLE_INPUT_SYSTEM
    private void UpdateDrag(Vector2 screenPosition)
    {
        if (!pointerDragMoved &&
            Vector2.Distance(screenPosition, pointerPressScreenPosition) < DragThresholdPixels)
        {
            return;
        }

        pointerDragMoved = true;
        pointerIsPressingButton = false;
        EnsureDragPreview();
        if (dragPreviewObject == null)
        {
            return;
        }

        if (TryGetWorldPosition(screenPosition, out Vector3 worldPosition))
        {
            worldPosition.z = transform.position.z;
            dragPreviewObject.transform.position = worldPosition;
        }
    }

    private void TryRemoveAtScreenPosition(Vector2 screenPosition)
    {
        ResolvePlacementSystem();
        if (placementSystem == null ||
            !TryGetWorldPosition(screenPosition, out Vector3 worldPosition))
        {
            return;
        }

        placementSystem.TryRemoveTowerAtWorldPosition(new Vector2(
            worldPosition.x,
            worldPosition.y));
    }
#endif

    private void UpdateVisuals()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        float scaleMultiplier = pointerIsPressingButton
            ? pressedScale
            : pointerIsOverButton
                ? hoverScale
                : 1f;
        Vector3 targetScale = baseScale * scaleMultiplier;
        float t = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
        ApplyColor();
    }

    private void ApplyColor()
    {
        CacheReferences();
        if (spriteRenderer == null)
        {
            return;
        }

        bool active = placementSystem != null && placementSystem.IsTowerRemovalModeActive;
        spriteRenderer.color = active ? activeColor : normalColor;
    }

#if ENABLE_INPUT_SYSTEM
    private void EnsureDragPreview()
    {
        CacheReferences();
        if (spriteRenderer == null || spriteRenderer.sprite == null || dragPreviewObject != null)
        {
            return;
        }

        dragPreviewObject = new GameObject("Tower Removal Drag Preview");
        dragPreviewObject.transform.localScale = transform.lossyScale;
        SpriteRenderer dragPreviewRenderer = dragPreviewObject.AddComponent<SpriteRenderer>();
        dragPreviewRenderer.sprite = spriteRenderer.sprite;
        dragPreviewRenderer.color = new Color(0.72f, 0.72f, 0.72f, 0.75f);
        dragPreviewRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        dragPreviewRenderer.sortingOrder = spriteRenderer.sortingOrder + 20;
    }

    private bool PointerIsOverThisButton(Vector2 screenPosition)
    {
        if (!TryGetWorldPosition(screenPosition, out Vector3 worldPosition) ||
            !TryGetComponent(out Collider2D buttonCollider))
        {
            return false;
        }

        return buttonCollider.OverlapPoint(worldPosition);
    }

    private bool TryGetWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            worldPosition = default;
            return false;
        }

        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distanceFromCamera);
        worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        return true;
    }
#endif

    private void CacheReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ResolvePlacementSystem()
    {
        if (placementSystem == null)
        {
            placementSystem = FindFirstObjectByType<FireTowerPlacementSystem>();
        }
    }

    private void FitColliderToSprite()
    {
        if (!TryGetComponent(out BoxCollider2D boxCollider))
        {
            return;
        }

        CacheReferences();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        boxCollider.isTrigger = true;
        boxCollider.size = spriteRenderer.sprite.bounds.size;
            boxCollider.offset = spriteRenderer.sprite.bounds.center;
    }

    private void DestroyDragPreview()
    {
        if (dragPreviewObject != null)
        {
            Destroy(dragPreviewObject);
        }

        dragPreviewObject = null;
    }
}
