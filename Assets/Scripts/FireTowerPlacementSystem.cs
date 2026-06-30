using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class FireTowerPlacementSystem : MonoBehaviour
{
    private const float DragThresholdPixels = 8f;

    [Header("References")]
    [SerializeField]
    private Camera worldCamera;

    [SerializeField]
    private CardCatalog cardCatalog;

    [Header("Selection Feedback")]
    [SerializeField]
    private Color selectedCardColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [SerializeField, Range(0.05f, 1f)]
    private float previewAlpha = 0.45f;

    private CardView selectedCard;
    private Color selectedCardNormalColor = Color.white;
    private GameObject previewObject;
    private SpriteRenderer previewRenderer;
    private bool cardPointerDown;
    private bool dragMoved;
    private bool pressedCardWasSelected;
    private Vector2 dragStartScreenPosition;

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnEnable()
    {
        ResolveCamera();
        ClearSelection();
    }

    private void OnDisable()
    {
        ClearSelection();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearSelection();
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            HidePlacementPreview();
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearSelection();
            return;
        }

        if (!TryGetPointerWorldPosition(mouse, out Vector2 worldPosition))
        {
            HidePlacementPreview();
            return;
        }

        UpdatePlacementPreview(worldPosition);

        Vector2 screenPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            CardView pressedCard = FindCardAt(worldPosition);
            if (pressedCard != null)
            {
                BeginCardPress(pressedCard, screenPosition);
                return;
            }

            if (!cardPointerDown)
            {
                TryPlaceSelectedTower(worldPosition);
            }

            return;
        }

        if (!cardPointerDown)
        {
            return;
        }

        if (mouse.leftButton.isPressed)
        {
            HandleCardDragged(screenPosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            HandleCardReleased(screenPosition);
        }
    }

    public void SetCardCatalog(CardCatalog catalog)
    {
        cardCatalog = catalog;
    }

    public void HandleCardPressed(CardView card, Vector2 screenPosition)
    {
        BeginCardPress(card, screenPosition);
    }

    public void HandleCardDragged(Vector2 screenPosition)
    {
        if (!cardPointerDown)
        {
            return;
        }

        if (Vector2.Distance(screenPosition, dragStartScreenPosition) >= DragThresholdPixels)
        {
            dragMoved = true;
        }

        if (TryGetPointerWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            UpdatePlacementPreview(worldPosition);
        }
    }

    public void HandleCardReleased(Vector2 screenPosition)
    {
        if (!cardPointerDown)
        {
            return;
        }

        if (!TryGetPointerWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            ClearSelection();
            return;
        }

        FinishCardDrag(worldPosition);
    }

    private void BeginCardPress(CardView card, Vector2 screenPosition)
    {
        bool wasSelected = card == selectedCard;
        if (!wasSelected)
        {
            SelectCard(card);
        }

        if (selectedCard == null)
        {
            return;
        }

        cardPointerDown = true;
        dragMoved = false;
        pressedCardWasSelected = wasSelected;
        dragStartScreenPosition = screenPosition;
    }

    private void FinishCardDrag(Vector2 worldPosition)
    {
        bool shouldToggleOff = pressedCardWasSelected && !dragMoved;
        bool shouldPlace = dragMoved;

        cardPointerDown = false;
        dragMoved = false;
        pressedCardWasSelected = false;

        if (shouldPlace)
        {
            if (!TryPlaceSelectedTower(worldPosition))
            {
                ClearSelection();
            }

            return;
        }

        if (shouldToggleOff)
        {
            ClearSelection();
        }
    }

    private bool TryPlaceSelectedTower(Vector2 worldPosition)
    {
        if (selectedCard == null || selectedCard.TowerPrefab == null)
        {
            return false;
        }

        LandPlot land = FindLandAt(worldPosition);
        if (land == null || land.IsOccupied)
        {
            return false;
        }

        PlaceTower(land, selectedCard.TowerPrefab);
        ClearSelection();
        return true;
    }

    private void PlaceTower(LandPlot land, GameObject towerPrefab)
    {
        if (land == null || towerPrefab == null)
        {
            return;
        }

        Vector3 placementPosition = land.transform.position;
        placementPosition.z = towerPrefab.transform.position.z;

        GameObject tower = Instantiate(
            towerPrefab,
            placementPosition,
            towerPrefab.transform.rotation);

        tower.name = towerPrefab.name;
        land.SetTower(tower.transform);
    }

    private void SelectCard(CardView card)
    {
        ClearSelection();
        if (card == null || card.TowerPrefab == null || card.BackgroundRenderer == null)
        {
            return;
        }

        selectedCard = card;
        selectedCardNormalColor = card.BackgroundRenderer.color;
        card.BackgroundRenderer.color = selectedCardColor;
        CreatePlacementPreview(card.TowerPrefab);
    }

    private void ClearSelection()
    {
        if (selectedCard != null && selectedCard.BackgroundRenderer != null)
        {
            selectedCard.BackgroundRenderer.color = selectedCardNormalColor;
        }

        selectedCard = null;
        cardPointerDown = false;
        dragMoved = false;
        pressedCardWasSelected = false;
        DestroyPlacementPreview();
    }

    private void CreatePlacementPreview(GameObject towerPrefab)
    {
        DestroyPlacementPreview();
        if (towerPrefab == null)
        {
            return;
        }

        SpriteRenderer sourceRenderer = towerPrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            return;
        }

        previewObject = new GameObject("Tower Placement Preview");
        previewObject.transform.rotation = towerPrefab.transform.rotation;
        previewObject.transform.localScale = towerPrefab.transform.localScale;

        previewRenderer = previewObject.AddComponent<SpriteRenderer>();
        previewRenderer.sprite = sourceRenderer.sprite;
        previewRenderer.flipX = sourceRenderer.flipX;
        previewRenderer.flipY = sourceRenderer.flipY;
        previewRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        previewRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        previewRenderer.sortingOrder = Mathf.Max(sourceRenderer.sortingOrder, 10);

        Color color = sourceRenderer.color;
        float gray = color.grayscale;
        previewRenderer.color = new Color(gray, gray, gray, previewAlpha);
        previewObject.SetActive(false);
    }

    private void UpdatePlacementPreview(Vector2 worldPosition)
    {
        if (selectedCard == null || previewObject == null)
        {
            HidePlacementPreview();
            return;
        }

        LandPlot land = FindLandAt(worldPosition);
        if (land == null || land.IsOccupied)
        {
            HidePlacementPreview();
            return;
        }

        Vector3 position = land.transform.position;
        position.z = selectedCard.TowerPrefab != null
            ? selectedCard.TowerPrefab.transform.position.z
            : 0f;
        previewObject.transform.position = position;
        previewObject.SetActive(true);
    }

    private void HidePlacementPreview()
    {
        if (previewObject != null)
        {
            previewObject.SetActive(false);
        }
    }

    private void DestroyPlacementPreview()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        previewObject = null;
        previewRenderer = null;
    }

    private bool TryGetPointerWorldPosition(Mouse mouse, out Vector2 worldPosition)
    {
        return TryGetPointerWorldPosition(mouse.position.ReadValue(), out worldPosition);
    }

    private bool TryGetPointerWorldPosition(
        Vector2 screenPosition,
        out Vector2 worldPosition)
    {
        ResolveCamera();
        if (worldCamera == null)
        {
            worldPosition = default;
            return false;
        }

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

    private static CardView FindCardAt(Vector2 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            CardView card = hits[i] != null
                ? hits[i].GetComponentInParent<CardView>()
                : null;
            if (card != null && card.isActiveAndEnabled)
            {
                return card;
            }
        }

        CardView[] cards = FindObjectsByType<CardView>(FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            CardView card = cards[i];
            if (card != null && card.isActiveAndEnabled &&
                IsPointInsideRenderer(card.BackgroundRenderer, worldPosition))
            {
                return card;
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
