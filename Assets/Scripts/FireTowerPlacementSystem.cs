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

    [Header("Tower Removal")]
    [SerializeField, Range(0f, 1f)]
    private float towerRemovalRefundRatio = 0.75f;

    private CardView selectedCard;
    private Color selectedCardNormalColor = Color.white;
    private GameObject previewObject;
    private SpriteRenderer previewRenderer;
    private CoinWallet wallet;
    private bool cardPointerDown;
    private bool dragMoved;
    private bool pressedCardWasSelected;
    private Vector2 dragStartScreenPosition;
    private bool towerRemovalMode;
    private bool towerPointerDown;
    private bool towerDragMoved;
    private Vector2 towerDragStartScreenPosition;
    private Transform draggedTower;
    private LandPlot draggedTowerOriginalLand;
    private Vector3 draggedTowerOriginalPosition;
    private Vector3 draggedTowerOffset;

    public bool IsTowerRemovalModeActive => towerRemovalMode;

    private void Awake()
    {
        ResolveCamera();
        ResolveWallet();
    }

    private void OnEnable()
    {
        ResolveCamera();
        ClearSelection();
    }

    private void OnDisable()
    {
        CancelTowerMove();
        ClearSelection();
    }

    private void Update()
    {
        if (CardSelectionMenu.IsOpen)
        {
            CancelTowerMove();
            ClearSelection();
            HidePlacementPreview();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelTowerMove();
            ClearSelection();
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            CancelTowerMove();
            HidePlacementPreview();
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelTowerMove();
            ClearSelection();
            return;
        }

        if (!TryGetPointerWorldPosition(mouse, out Vector2 worldPosition))
        {
            CancelTowerMove();
            HidePlacementPreview();
            return;
        }

        if (towerRemovalMode || towerPointerDown)
        {
            HidePlacementPreview();
        }
        else
        {
            UpdatePlacementPreview(worldPosition);
        }

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
                if (towerRemovalMode)
                {
                    TryRemoveTower(worldPosition);
                }
                else if (selectedCard == null &&
                    TryBeginTowerMove(worldPosition, screenPosition))
                {
                    return;
                }
                else
                {
                    TryPlaceSelectedTower(worldPosition);
                }
            }

            return;
        }

        if (towerPointerDown)
        {
            if (mouse.leftButton.isPressed)
            {
                HandleTowerMoveDragged(screenPosition);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                HandleTowerMoveReleased(screenPosition);
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

    public void ToggleTowerRemovalMode()
    {
        SetTowerRemovalMode(!towerRemovalMode);
    }

    public void SetTowerRemovalMode(bool active)
    {
        if (active)
        {
            ClearSelection();
            towerRemovalMode = true;
            HidePlacementPreview();
            return;
        }

        towerRemovalMode = false;
    }

    public bool TryRemoveTowerAtWorldPosition(Vector2 worldPosition)
    {
        return TryRemoveTower(worldPosition);
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

        ResolveWallet();
        int price = selectedCard.Price;
        if (price > 0 && (wallet == null || !wallet.TrySpendCoins(price)))
        {
            return false;
        }

        PlaceTower(land, selectedCard.TowerPrefab, price);
        ClearSelection();
        return true;
    }

    private bool TryRemoveTower(Vector2 worldPosition)
    {
        LandPlot land = FindLandWithTowerAt(worldPosition);
        if (land == null)
        {
            land = FindLandAt(worldPosition);
        }

        if (land == null)
        {
            return false;
        }

        Transform tower = land.CurrentTower;
        if (tower == null)
        {
            return false;
        }

        int originalPrice = ResolveTowerOriginalPrice(tower.gameObject);
        int refund = Mathf.FloorToInt(originalPrice * towerRemovalRefundRatio);
        if (refund > 0)
        {
            ResolveWallet();
            wallet?.AddCoins(refund);
        }

        tower.SetParent(null, true);
        Destroy(tower.gameObject);
        land.ClearTowerOccupancy();
        towerRemovalMode = false;
        return true;
    }

    private void PlaceTower(LandPlot land, GameObject towerPrefab, int originalPrice)
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
        PlacedTowerCost towerCost = tower.GetComponent<PlacedTowerCost>();
        if (towerCost == null)
        {
            towerCost = tower.AddComponent<PlacedTowerCost>();
        }

        towerCost.SetOriginalPrice(originalPrice);
        land.SetTower(tower.transform);
    }

    private void SelectCard(CardView card)
    {
        ClearSelection();
        if (card == null || card.TowerPrefab == null || card.BackgroundRenderer == null)
        {
            return;
        }

        towerRemovalMode = false;
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
        towerRemovalMode = false;
        DestroyPlacementPreview();
    }

    private int ResolveTowerOriginalPrice(GameObject tower)
    {
        if (tower == null)
        {
            return 0;
        }

        PlacedTowerCost cost = tower.GetComponent<PlacedTowerCost>();
        if (cost != null && cost.OriginalPrice > 0)
        {
            return cost.OriginalPrice;
        }

        if (cardCatalog == null)
        {
            cardCatalog = FindFirstObjectByType<CardCatalog>();
        }

        if (cardCatalog == null)
        {
            return 0;
        }

        string towerName = tower.name.Replace("(Clone)", string.Empty).Trim();
        for (int i = 0; i < cardCatalog.Cards.Count; i++)
        {
            CardEntry entry = cardCatalog.Cards[i];
            if (entry == null || entry.TowerPrefab == null)
            {
                continue;
            }

            if (entry.TowerPrefab.name == towerName)
            {
                return entry.Price;
            }
        }

        return 0;
    }

    private bool TryBeginTowerMove(Vector2 worldPosition, Vector2 screenPosition)
    {
        LandPlot land = FindLandWithTowerAt(worldPosition);
        if (land == null || land.CurrentTower == null)
        {
            return false;
        }

        draggedTowerOriginalLand = land;
        draggedTower = land.CurrentTower;
        draggedTowerOriginalPosition = draggedTower.position;
        draggedTowerOffset = draggedTower.position - new Vector3(worldPosition.x, worldPosition.y, draggedTower.position.z);
        towerDragStartScreenPosition = screenPosition;
        towerPointerDown = true;
        towerDragMoved = false;

        draggedTower.SetParent(null, true);
        land.ClearTowerOccupancy();
        HidePlacementPreview();
        return true;
    }

    private void HandleTowerMoveDragged(Vector2 screenPosition)
    {
        if (!towerPointerDown || draggedTower == null)
        {
            CancelTowerMove();
            return;
        }

        if (Vector2.Distance(screenPosition, towerDragStartScreenPosition) >= DragThresholdPixels)
        {
            towerDragMoved = true;
        }

        if (!towerDragMoved ||
            !TryGetPointerWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            return;
        }

        Vector3 position = new Vector3(
            worldPosition.x + draggedTowerOffset.x,
            worldPosition.y + draggedTowerOffset.y,
            draggedTowerOriginalPosition.z);
        draggedTower.position = position;
    }

    private void HandleTowerMoveReleased(Vector2 screenPosition)
    {
        if (!towerPointerDown)
        {
            return;
        }

        if (draggedTower == null)
        {
            ClearTowerMoveState();
            return;
        }

        if (!towerDragMoved ||
            !TryGetPointerWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            ReturnDraggedTowerToOriginalLand();
            return;
        }

        LandPlot targetLand = FindLandAt(worldPosition);
        bool canDropOnTarget = targetLand != null &&
            (targetLand == draggedTowerOriginalLand || !targetLand.IsOccupied);

        if (!canDropOnTarget)
        {
            ReturnDraggedTowerToOriginalLand();
            return;
        }

        MoveDraggedTowerToLand(targetLand);
        ClearTowerMoveState();
    }

    private void CancelTowerMove()
    {
        if (!towerPointerDown)
        {
            return;
        }

        ReturnDraggedTowerToOriginalLand();
    }

    private void ReturnDraggedTowerToOriginalLand()
    {
        if (draggedTower != null && draggedTowerOriginalLand != null)
        {
            draggedTower.position = draggedTowerOriginalPosition;
            draggedTowerOriginalLand.SetTower(draggedTower);
        }

        ClearTowerMoveState();
    }

    private void MoveDraggedTowerToLand(LandPlot targetLand)
    {
        if (draggedTower == null || targetLand == null)
        {
            ReturnDraggedTowerToOriginalLand();
            return;
        }

        Vector3 position = targetLand.transform.position;
        position.z = draggedTowerOriginalPosition.z;
        draggedTower.position = position;
        targetLand.SetTower(draggedTower);
    }

    private void ClearTowerMoveState()
    {
        towerPointerDown = false;
        towerDragMoved = false;
        draggedTower = null;
        draggedTowerOriginalLand = null;
        draggedTowerOriginalPosition = default;
        draggedTowerOffset = default;
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

    private static LandPlot FindLandWithTowerAt(Vector2 worldPosition)
    {
        LandPlot[] lands = FindObjectsByType<LandPlot>(FindObjectsSortMode.None);
        for (int i = 0; i < lands.Length; i++)
        {
            LandPlot land = lands[i];
            if (land == null || !land.isActiveAndEnabled || land.CurrentTower == null)
            {
                continue;
            }

            if (IsPointInsideTower(land.CurrentTower, worldPosition))
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

    private static bool IsPointInsideTower(Transform tower, Vector2 worldPosition)
    {
        if (tower == null)
        {
            return false;
        }

        Collider2D[] colliders = tower.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D towerCollider = colliders[i];
            if (towerCollider != null &&
                towerCollider.enabled &&
                towerCollider.gameObject.activeInHierarchy &&
                towerCollider.OverlapPoint(worldPosition))
            {
                return true;
            }
        }

        SpriteRenderer[] renderers = tower.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsPointInsideRenderer(renderers[i], worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveCamera()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void ResolveWallet()
    {
        if (wallet != null)
        {
            return;
        }

        wallet = CoinWallet.Instance;
        if (wallet == null)
        {
            wallet = FindFirstObjectByType<CoinWallet>();
        }
    }
}
