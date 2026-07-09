using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CardSelectionMenu : MonoBehaviour
{
    private const string CardsResourcePath = "Cards";
    private const int MaxSelectedCards = 9;
    private const float DefaultCardScale = 0.72f;

    [Header("Scene Layout")]
    [SerializeField]
    private GameObject cardsPrefab;

    [SerializeField]
    private Transform cardsLayer;

    [SerializeField]
    private Transform cardSlotsRoot;

    [SerializeField]
    private TextMesh titleText;

    [SerializeField]
    private TextMesh selectedCountText;

    [SerializeField]
    private Collider2D startButtonCollider;

    [SerializeField]
    private Collider2D backButtonCollider;

    [Header("Card Feedback")]
    [SerializeField]
    private Color selectedColor = new(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField]
    private Color normalColor = Color.white;

    [SerializeField]
    private Color restrictedColor = new(0.35f, 0.35f, 0.35f, 1f);

    [Header("UI Animation")]
    [SerializeField]
    private Color hoverColor = new(0.86f, 0.86f, 0.86f, 1f);

    [SerializeField, Min(1f)]
    private float tintAnimationSpeed = 14f;

    [SerializeField, Range(1f, 1.2f)]
    private float buttonHoverScale = 1.07f;

    [SerializeField, Min(0.05f)]
    private float dockMoveDuration = 0.22f;

    [SerializeField, Range(0.2f, 1.2f)]
    private float cardScale = DefaultCardScale;

    [SerializeField, Range(0.2f, 1f)]
    private float uniformCardScaleMultiplier = 1f;

    [Header("Vertical Scrolling")]
    [SerializeField]
    private SpriteRenderer scrollViewport;

    [SerializeField, Min(0f)]
    private float scrollPadding = 0.12f;

    [SerializeField, Min(0.05f)]
    private float scrollUnitsPerStep = 0.65f;

    [SerializeField, Min(0.01f)]
    private float scrollSmoothTime = 0.1f;

    [SerializeField, Min(1f)]
    private float scrollDragThresholdPixels = 8f;

    private readonly List<CardView> selectedCards = new();
    private readonly Dictionary<SpriteRenderer, Color> normalRendererColors = new();
    private readonly Dictionary<SpriteRenderer, Color> targetRendererColors = new();
    private readonly Dictionary<CardView, bool> cardVisibility = new();
    private readonly HashSet<CardView> restrictedCards = new();
    private readonly List<CardMotionAnimation> cardMotionAnimations = new();

    private Camera worldCamera;
    private GameObject cardsObject;
    private CardCatalog cardCatalog;
    private GameObject selectedDockObject;
    private CardCatalog selectedDockCatalog;
    private LevelSelectCameraScroll cameraScroll;
    private string targetSceneName = "Level_test";
    private bool pausedTimeScale;
    private float previousTimeScale = 1f;
    private Vector3 cardsLayerStartLocalPosition;
    private Vector3 cardSlotsStartLocalPosition;
    private float currentScrollOffset;
    private float targetScrollOffset;
    private float scrollVelocity;
    private float minimumScrollOffset;
    private float maximumScrollOffset;
    private bool scrollInitialized;
    private bool scrollPointerDown;
    private bool scrollPointerDragged;
    private Vector2 scrollPointerStartPosition;
    private Vector2 scrollPointerLastPosition;
    private CardView hoveredCard;
    private Vector3 startButtonBaseScale;
    private Vector3 backButtonBaseScale;
    private bool buttonScalesCaptured;

    public static bool IsOpen { get; private set; }

    public static void Show(string targetSceneName)
    {
        if (IsOpen)
        {
            return;
        }

        CardSelectionMenu menu = FindSceneMenu();
        if (menu == null)
        {
            Debug.LogWarning("No Card Selection Menu exists in this scene.");
            return;
        }

        menu.Open(targetSceneName);
    }

    public static void HideAll()
    {
        CardSelectionMenu[] menus = FindObjectsByType<CardSelectionMenu>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] == null)
            {
                continue;
            }

            menus[i].Close(true);
            menus[i].enabled = false;
            if (menus[i].gameObject != null)
            {
                menus[i].gameObject.SetActive(false);
            }
        }

        IsOpen = false;
    }

    private void Awake()
    {
        ResolveSceneReferences();
        enabled = IsOpen;
    }

    private void OnValidate()
    {
        ResolveSceneReferences();
        cardScale = Mathf.Clamp(cardScale, 0.2f, 1.2f);
        uniformCardScaleMultiplier = Mathf.Clamp(uniformCardScaleMultiplier, 0.2f, 1f);
        tintAnimationSpeed = Mathf.Max(1f, tintAnimationSpeed);
        buttonHoverScale = Mathf.Clamp(buttonHoverScale, 1f, 1.2f);
        dockMoveDuration = Mathf.Max(0.05f, dockMoveDuration);
        scrollPadding = Mathf.Max(0f, scrollPadding);
        scrollUnitsPerStep = Mathf.Max(0.05f, scrollUnitsPerStep);
        scrollSmoothTime = Mathf.Max(0.01f, scrollSmoothTime);
        scrollDragThresholdPixels = Mathf.Max(1f, scrollDragThresholdPixels);
    }

    public void Open(string sceneName)
    {
        IsOpen = true;
        enabled = true;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        ResolveSceneReferences();
        selectedCards.Clear();
        normalRendererColors.Clear();
        targetRendererColors.Clear();
        cardVisibility.Clear();
        restrictedCards.Clear();
        cardMotionAnimations.Clear();
        hoveredCard = null;
        ClearRuntimeCards();
        ClearSelectedDockPreview();

        targetSceneName = string.IsNullOrWhiteSpace(sceneName)
            ? "Level_test"
            : sceneName;
        worldCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        cameraScroll = FindFirstObjectByType<LevelSelectCameraScroll>();
        if (cameraScroll != null)
        {
            cameraScroll.enabled = false;
        }

        CardSelectionState.BeginSelection(targetSceneName);
        PauseGameplay();
        PositionAtCameraCenter();
        CaptureButtonScales();
        LoadCardsIntoSceneSlots();
        RefreshSelectedVisuals();
    }

    private void OnDestroy()
    {
        Close(false);
    }

    private void Update()
    {
        UpdateCardMotionAnimations();
        UpdateCardTintAnimations();
        UpdateHoverAnimations();
        UpdateVerticalScroll();

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginScrollDrag(pointerPosition);
        }

        if (mouse.leftButton.isPressed)
        {
            UpdateScrollDrag(pointerPosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame && !EndScrollDrag())
        {
            HandleClick(pointerPosition);
        }
#else
        Vector2 pointerPosition = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            BeginScrollDrag(pointerPosition);
        }

        if (Input.GetMouseButton(0))
        {
            UpdateScrollDrag(pointerPosition);
        }

        if (Input.GetMouseButtonUp(0) && !EndScrollDrag())
        {
            HandleClick(pointerPosition);
        }
#endif
    }

    private void LoadCardsIntoSceneSlots()
    {
        EnsureCardsPrefab();

        if (cardsPrefab == null)
        {
            SetCountText("Missing Resources/Cards prefab");
            return;
        }

        Transform parent = cardsLayer != null ? cardsLayer : transform;
        cardsObject = Instantiate(cardsPrefab, parent);
        cardsObject.name = "Runtime Selectable Cards";
        cardsObject.transform.localPosition = Vector3.zero;
        cardsObject.transform.localRotation = Quaternion.identity;
        cardsObject.transform.localScale = Vector3.one;

        cardCatalog = cardsObject.GetComponent<CardCatalog>();
        if (cardCatalog == null)
        {
            return;
        }

        cardCatalog.BuildCards();
        ResolveRestrictedCards();

        PlaceCardsOnSceneSlots();
        InitializeVerticalScroll();
    }

    private void ResolveRestrictedCards()
    {
        restrictedCards.Clear();

        LevelDefinition levelDefinition = FindFirstObjectByType<LevelDefinition>();
        if (levelDefinition == null || !levelDefinition.HasCardRules())
        {
            return;
        }

        HashSet<string> availableTowerNames = new(
            levelDefinition.GetAvailableTowerNames(cardCatalog.Cards, null));
        IReadOnlyList<CardView> cards = cardCatalog.ActiveCards;
        for (int i = 0; i < cards.Count; i++)
        {
            CardView card = cards[i];
            GameObject towerPrefab = card != null ? card.TowerPrefab : null;
            if (card == null || towerPrefab == null ||
                !availableTowerNames.Contains(towerPrefab.name))
            {
                if (card != null)
                {
                    restrictedCards.Add(card);
                }
            }
        }
    }

    private void PlaceCardsOnSceneSlots()
    {
        CardSlotPoint[] slots = FindSceneCardSlots();
        IReadOnlyList<CardView> cards = cardCatalog.ActiveCards;

        for (int i = 0; i < cards.Count; i++)
        {
            CardView card = cards[i];
            if (card == null)
            {
                continue;
            }

            if (i < slots.Length && slots[i] != null)
            {
                float scale = GetScaleForSlot(card, slots[i]);
                ApplyCardPlacement(card, slots[i].transform.position, slots[i].transform.rotation, Vector3.one * scale);
            }
            else if (TryGetOverflowPlacement(
                i,
                slots,
                card,
                out Vector3 overflowPosition,
                out Quaternion overflowRotation,
                out Vector3 overflowScale))
            {
                ApplyCardPlacement(card, overflowPosition, overflowRotation, overflowScale);
            }
            else
            {
                SetWorldScale(card.transform, Vector3.one * cardScale * uniformCardScaleMultiplier);
            }

            if (card.BackgroundRenderer != null)
            {
                CacheCardRendererColors(card);
                SetCardSortingOrder(card, 92);
                SetCardAlpha(card, 0f);
            }
        }
    }

    private float GetScaleForSlot(CardView card, CardSlotPoint slot)
    {
        if (card == null || slot == null || card.BackgroundRenderer == null ||
            card.BackgroundRenderer.sprite == null)
        {
            return cardScale * uniformCardScaleMultiplier;
        }

        Vector2 slotSize = slot.CardSize;
        Vector2 cardSize = card.BackgroundRenderer.sprite.bounds.size;
        if (slotSize.x <= 0f || slotSize.y <= 0f || cardSize.x <= 0f || cardSize.y <= 0f)
        {
            return cardScale * uniformCardScaleMultiplier;
        }

        float scale = Mathf.Min(slotSize.x / cardSize.x, slotSize.y / cardSize.y);
        return Mathf.Clamp(scale, 0.2f, 1.2f) * uniformCardScaleMultiplier;
    }

    private bool TryGetOverflowPlacement(
        int cardIndex,
        CardSlotPoint[] slots,
        CardView card,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 scale)
    {
        position = default;
        rotation = Quaternion.identity;
        scale = Vector3.one * cardScale * uniformCardScaleMultiplier;

        if (slots == null || slots.Length == 0 || slots[0] == null)
        {
            return false;
        }

        float firstRowY = slots[0].transform.position.y;
        int columns = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null ||
                Mathf.Abs(slots[i].transform.position.y - firstRowY) > 0.05f)
            {
                break;
            }

            columns++;
        }

        columns = Mathf.Max(1, columns);
        int columnIndex = cardIndex % columns;
        int rowIndex = cardIndex / columns;
        CardSlotPoint columnTemplate = slots[Mathf.Min(columnIndex, columns - 1)];

        float rowSpacing = columnTemplate.CardSize.y + 0.15f;
        for (int i = columns; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                continue;
            }

            float measuredSpacing = Mathf.Abs(firstRowY - slots[i].transform.position.y);
            if (measuredSpacing > 0.05f)
            {
                rowSpacing = measuredSpacing;
                break;
            }
        }

        position = columnTemplate.transform.position + Vector3.down * (rowSpacing * rowIndex);
        rotation = columnTemplate.transform.rotation;
        scale = Vector3.one * GetScaleForSlot(card, columnTemplate);
        return true;
    }

    private void HandleClick(Vector2 screenPosition)
    {
        if (!TryGetWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            return;
        }

        if (startButtonCollider != null && startButtonCollider.OverlapPoint(worldPosition))
        {
            ConfirmSelection();
            return;
        }

        if (backButtonCollider != null && backButtonCollider.OverlapPoint(worldPosition))
        {
            Close(true);
            return;
        }

        CardView clickedCard = FindCardAt(worldPosition);
        if (clickedCard != null)
        {
            ToggleCard(clickedCard);
            return;
        }

        CardView clickedDockCard = FindSelectedDockCardAt(worldPosition);
        if (clickedDockCard != null && clickedDockCard.TowerPrefab != null)
        {
            CardView selectedSourceCard = selectedCards.FirstOrDefault(card =>
                card != null &&
                card.TowerPrefab != null &&
                card.TowerPrefab.name == clickedDockCard.TowerPrefab.name);
            if (selectedSourceCard != null)
            {
                ToggleCard(selectedSourceCard);
            }
        }
    }

    private void ToggleCard(CardView card)
    {
        if (card == null || restrictedCards.Contains(card))
        {
            return;
        }

        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            RefreshSelectedVisuals();
            RefreshSelectedDockPreview();
            return;
        }

        if (selectedCards.Count >= MaxSelectedCards)
        {
            return;
        }

        selectedCards.Add(card);
        RefreshSelectedVisuals();
        RefreshSelectedDockPreview();
    }

    private static void ApplyCardPlacement(CardView card, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        card.transform.position = position;
        card.transform.rotation = rotation;
        SetWorldScale(card.transform, scale);
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        if (target == null)
        {
            return;
        }

        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            Mathf.Abs(parentScale.x) > 0.0001f ? worldScale.x / parentScale.x : worldScale.x,
            Mathf.Abs(parentScale.y) > 0.0001f ? worldScale.y / parentScale.y : worldScale.y,
            Mathf.Abs(parentScale.z) > 0.0001f ? worldScale.z / parentScale.z : worldScale.z);
    }

    private void ConfirmSelection()
    {
        CompleteCardMotionAnimations();
        CardSelectionState.ConfirmSelection(selectedCards);

        CardCatalog dockCatalog = selectedDockCatalog;
        selectedDockCatalog = null;
        selectedDockObject = null;

        Close(true);

        CardRuntimeLoader loader = FindFirstObjectByType<CardRuntimeLoader>();
        if (loader != null)
        {
            if (dockCatalog != null)
            {
                loader.AdoptLoadedCatalog(dockCatalog);
            }
            else
            {
                loader.LoadCards();
            }
        }
    }

    private void RefreshSelectedVisuals()
    {
        if (cardCatalog != null)
        {
            foreach (CardView card in cardCatalog.ActiveCards)
            {
                if (card == null || card.BackgroundRenderer == null)
                {
                    continue;
                }

                if (restrictedCards.Contains(card))
                {
                    SetCardTintTarget(card, restrictedColor);
                }
                else if (selectedCards.Contains(card))
                {
                    SetCardTintTarget(card, selectedColor);
                }
                else
                {
                    SetCardTintTarget(card, card == hoveredCard ? hoverColor : normalColor);
                }
            }
        }

        SetCountText($"Selected {selectedCards.Count}/{MaxSelectedCards}");
    }

    private CardView FindCardAt(Vector2 worldPosition)
    {
        if (cardCatalog == null)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            CardView card = hits[i] != null
                ? hits[i].GetComponentInParent<CardView>()
                : null;
            if (card != null && cardCatalog.ActiveCards.Contains(card))
            {
                return card;
            }
        }

        return cardCatalog.ActiveCards.FirstOrDefault(card =>
            card != null &&
            card.BackgroundRenderer != null &&
            card.BackgroundRenderer.enabled &&
            card.BackgroundRenderer.bounds.Contains(new Vector3(
                worldPosition.x,
                worldPosition.y,
                card.BackgroundRenderer.bounds.center.z)));
    }

    private CardView FindSelectedDockCardAt(Vector2 worldPosition)
    {
        if (selectedDockCatalog == null)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            CardView card = hits[i] != null
                ? hits[i].GetComponentInParent<CardView>()
                : null;
            if (card != null && selectedDockCatalog.ActiveCards.Contains(card))
            {
                return card;
            }
        }

        return selectedDockCatalog.ActiveCards.FirstOrDefault(card =>
            card != null &&
            card.BackgroundRenderer != null &&
            card.BackgroundRenderer.enabled &&
            card.BackgroundRenderer.bounds.Contains(new Vector3(
                worldPosition.x,
                worldPosition.y,
                card.BackgroundRenderer.bounds.center.z)));
    }

    private bool TryGetWorldPosition(Vector2 screenPosition, out Vector2 worldPosition)
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (worldCamera == null)
        {
            worldPosition = default;
            return false;
        }

        float distance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distance);
        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(screenPoint);
        worldPosition = worldPoint;
        return true;
    }

    private void PauseGameplay()
    {
        if (pausedTimeScale)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedTimeScale = true;
    }

    private void Close(bool hideObject)
    {
        if (!IsOpen && cardsObject == null)
        {
            return;
        }

        IsOpen = false;
        ResetVerticalScroll();
        selectedCards.Clear();
        normalRendererColors.Clear();
        targetRendererColors.Clear();
        cardVisibility.Clear();
        restrictedCards.Clear();
        cardMotionAnimations.Clear();
        hoveredCard = null;
        ClearRuntimeCards();
        ClearSelectedDockPreview();
        ResetButtonScales();
        RestoreGameplayTimeScale();

        if (cameraScroll != null)
        {
            cameraScroll.enabled = true;
        }

        enabled = false;

        if (hideObject && gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    private void RestoreGameplayTimeScale()
    {
        if (!pausedTimeScale)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        pausedTimeScale = false;
    }

    private void ClearRuntimeCards()
    {
        cardCatalog = null;
        if (cardsObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(cardsObject);
        }
        else
        {
            DestroyImmediate(cardsObject);
        }

        cardsObject = null;
    }

    private void RefreshSelectedDockPreview()
    {
        if (selectedCards.Count == 0)
        {
            ClearSelectedDockPreview();
            return;
        }

        EnsureCardsPrefab();
        if (cardsPrefab == null)
        {
            return;
        }

        bool createdDock = false;
        if (selectedDockObject == null)
        {
            selectedDockObject = Instantiate(cardsPrefab);
            selectedDockObject.name = "Selected Cards Dock";
            selectedDockCatalog = selectedDockObject.GetComponent<CardCatalog>();
            createdDock = true;
        }

        if (selectedDockCatalog == null)
        {
            ClearSelectedDockPreview();
            return;
        }

        Dictionary<string, TransformSnapshot> previousCardTransforms = new();
        if (!createdDock)
        {
            IReadOnlyList<CardView> previousCards = selectedDockCatalog.ActiveCards;
            for (int i = 0; i < previousCards.Count; i++)
            {
                CardView previousCard = previousCards[i];
                if (previousCard != null && previousCard.TowerPrefab != null)
                {
                    previousCardTransforms[previousCard.TowerPrefab.name] =
                        new TransformSnapshot(previousCard.transform);
                }
            }
        }

        cardMotionAnimations.Clear();
        CardView[] existingCards = selectedDockCatalog.GetComponentsInChildren<CardView>(true);
        for (int i = 0; i < existingCards.Length; i++)
        {
            SetCardVisibility(existingCards[i], false);
        }

        List<string> selectedTowerNames = selectedCards
            .Where(card => card != null && card.TowerPrefab != null)
            .Select(card => card.TowerPrefab.name)
            .Distinct()
            .ToList();
        selectedDockCatalog.BuildCardsInOrder(selectedTowerNames);

        CardSlotPoint[] dockSlots = FindGameplayDockSlots();
        IReadOnlyList<CardView> dockCards = selectedDockCatalog.ActiveCards;
        for (int i = 0; i < dockCards.Count; i++)
        {
            CardView dockCard = dockCards[i];
            if (dockCard == null)
            {
                continue;
            }

            SetCardVisibility(dockCard, true);
            if (i < dockSlots.Length && dockSlots[i] != null)
            {
                float scale = GetScaleForSlot(dockCard, dockSlots[i]);
                Vector3 targetScale = Vector3.one * scale;
                string towerName = dockCard.TowerPrefab != null
                    ? dockCard.TowerPrefab.name
                    : string.Empty;

                if (previousCardTransforms.TryGetValue(towerName, out TransformSnapshot previousTransform))
                {
                    ApplyCardPlacement(
                        dockCard,
                        previousTransform.Position,
                        previousTransform.Rotation,
                        previousTransform.Scale);
                }
                else
                {
                    CardView sourceCard = selectedCards.FirstOrDefault(card =>
                        card != null &&
                        card.TowerPrefab != null &&
                        card.TowerPrefab.name == towerName);
                    Vector3 startPosition = sourceCard != null
                        ? sourceCard.transform.position
                        : dockCard.transform.position;
                    Quaternion startRotation = sourceCard != null
                        ? sourceCard.transform.rotation
                        : dockCard.transform.rotation;
                    ApplyCardPlacement(
                        dockCard,
                        startPosition,
                        startRotation,
                        targetScale * 0.82f);
                }

                StartCardMotion(
                    dockCard,
                    dockSlots[i].transform.position,
                    dockSlots[i].transform.rotation,
                    targetScale);
            }

            SetCardSortingOrder(dockCard, 94);
        }
    }

    private CardSlotPoint[] FindGameplayDockSlots()
    {
        return FindObjectsByType<CardSlotPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(slot =>
                slot != null &&
                slot.gameObject.activeInHierarchy &&
                !slot.transform.IsChildOf(transform))
            .OrderBy(slot => slot.SlotIndex)
            .ThenBy(slot => slot.name)
            .ToArray();
    }

    private void ClearSelectedDockPreview()
    {
        selectedDockCatalog = null;
        if (selectedDockObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(selectedDockObject);
        }
        else
        {
            DestroyImmediate(selectedDockObject);
        }

        selectedDockObject = null;
    }

    private void ResolveSceneReferences()
    {
        if (cardsLayer == null)
        {
            Transform found = transform.Find("Cards Layer");
            if (found != null)
            {
                cardsLayer = found;
            }
        }

        if (cardSlotsRoot == null)
        {
            Transform found = transform.Find("Card Slots");
            if (found != null)
            {
                cardSlotsRoot = found;
            }
        }

        if (scrollViewport == null)
        {
            Transform found = transform.Find("Panel");
            scrollViewport = found != null ? found.GetComponent<SpriteRenderer>() : null;
        }

        TextMesh[] textMeshes = GetComponentsInChildren<TextMesh>(true);
        if (titleText == null)
        {
            titleText = textMeshes.FirstOrDefault(text =>
                text != null && text.name.Contains("Title", System.StringComparison.OrdinalIgnoreCase));
        }

        if (selectedCountText == null)
        {
            selectedCountText = textMeshes.FirstOrDefault(text =>
                text != null && text.name.Contains("Selected Count", System.StringComparison.OrdinalIgnoreCase));
        }

        if (startButtonCollider == null)
        {
            Transform found = transform.Find("Start Button");
            startButtonCollider = found != null ? found.GetComponent<Collider2D>() : null;
        }

        if (backButtonCollider == null)
        {
            Transform found = transform.Find("Back Button");
            backButtonCollider = found != null ? found.GetComponent<Collider2D>() : null;
        }
    }

    private CardSlotPoint[] FindSceneCardSlots()
    {
        Transform root = cardSlotsRoot != null ? cardSlotsRoot : transform;
        return root.GetComponentsInChildren<CardSlotPoint>(true)
            .Where(slot => slot != null && slot.gameObject.activeInHierarchy)
            .OrderBy(slot => slot.SlotIndex)
            .ThenBy(slot => slot.name)
            .ToArray();
    }

    private void EnsureCardsPrefab()
    {
        if (cardsPrefab == null)
        {
            cardsPrefab = Resources.Load<GameObject>(CardsResourcePath);
        }
    }

    private void PositionAtCameraCenter()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (worldCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = worldCamera.transform.position;
        transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
    }

    private void SetCountText(string text)
    {
        if (selectedCountText != null)
        {
            selectedCountText.text = text;
        }
    }

    private void InitializeVerticalScroll()
    {
        if (cardsLayer == null || cardCatalog == null)
        {
            scrollInitialized = false;
            return;
        }

        cardsLayerStartLocalPosition = cardsLayer.localPosition;
        if (cardSlotsRoot != null)
        {
            cardSlotsStartLocalPosition = cardSlotsRoot.localPosition;
        }

        currentScrollOffset = 0f;
        targetScrollOffset = 0f;
        scrollVelocity = 0f;
        minimumScrollOffset = 0f;
        maximumScrollOffset = 0f;

        if (scrollViewport != null && TryGetCardContentBounds(out Bounds contentBounds))
        {
            Bounds viewportBounds = scrollViewport.bounds;
            float viewportTop = viewportBounds.max.y - scrollPadding;
            float viewportBottom = viewportBounds.min.y + scrollPadding;

            minimumScrollOffset = Mathf.Min(0f, viewportTop - contentBounds.max.y);
            maximumScrollOffset = Mathf.Max(0f, viewportBottom - contentBounds.min.y);
        }

        scrollInitialized = true;
        ApplyScrollOffset();
    }

    private void UpdateVerticalScroll()
    {
        if (!scrollInitialized || cardsLayer == null)
        {
            return;
        }

        float rawScrollDelta = 0f;
        Vector2 pointerPosition = default;
        bool hasPointer = false;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            rawScrollDelta = mouse.scroll.ReadValue().y;
            pointerPosition = mouse.position.ReadValue();
            hasPointer = true;
        }
#else
        rawScrollDelta = Input.mouseScrollDelta.y;
        pointerPosition = Input.mousePosition;
        hasPointer = true;
#endif

        if (!Mathf.Approximately(rawScrollDelta, 0f) &&
            hasPointer && IsPointerOverScrollViewport(pointerPosition))
        {
            float scrollSteps = Mathf.Abs(rawScrollDelta) > 10f
                ? rawScrollDelta / 120f
                : rawScrollDelta;
            targetScrollOffset = Mathf.Clamp(
                targetScrollOffset - (scrollSteps * scrollUnitsPerStep),
                minimumScrollOffset,
                maximumScrollOffset);
        }

        currentScrollOffset = Mathf.SmoothDamp(
            currentScrollOffset,
            targetScrollOffset,
            ref scrollVelocity,
            scrollSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        ApplyScrollOffset();
    }

    private void ApplyScrollOffset()
    {
        if (cardsLayer != null)
        {
            cardsLayer.localPosition = cardsLayerStartLocalPosition +
                new Vector3(0f, currentScrollOffset, 0f);
        }

        if (cardSlotsRoot != null)
        {
            cardSlotsRoot.localPosition = cardSlotsStartLocalPosition +
                new Vector3(0f, currentScrollOffset, 0f);
        }

        RefreshCardVisibility();
    }

    private void ResetVerticalScroll()
    {
        if (scrollInitialized)
        {
            if (cardsLayer != null)
            {
                cardsLayer.localPosition = cardsLayerStartLocalPosition;
            }

            if (cardSlotsRoot != null)
            {
                cardSlotsRoot.localPosition = cardSlotsStartLocalPosition;
            }
        }

        currentScrollOffset = 0f;
        targetScrollOffset = 0f;
        scrollVelocity = 0f;
        scrollInitialized = false;
        scrollPointerDown = false;
        scrollPointerDragged = false;
    }

    private bool IsPointerOverScrollViewport(Vector2 screenPosition)
    {
        if (scrollViewport == null)
        {
            return true;
        }

        return TryGetWorldPosition(screenPosition, out Vector2 worldPosition) &&
            scrollViewport.bounds.Contains(new Vector3(
                worldPosition.x,
                worldPosition.y,
            scrollViewport.bounds.center.z));
    }

    private void BeginScrollDrag(Vector2 screenPosition)
    {
        bool hasScrollableContent = maximumScrollOffset - minimumScrollOffset > 0.001f;
        scrollPointerDown = scrollInitialized &&
            hasScrollableContent &&
            IsPointerOverScrollViewport(screenPosition);
        scrollPointerDragged = false;
        scrollPointerStartPosition = screenPosition;
        scrollPointerLastPosition = screenPosition;
    }

    private void UpdateScrollDrag(Vector2 screenPosition)
    {
        if (!scrollPointerDown)
        {
            return;
        }

        if (!scrollPointerDragged &&
            Mathf.Abs(screenPosition.y - scrollPointerStartPosition.y) >= scrollDragThresholdPixels)
        {
            scrollPointerDragged = true;
        }

        if (scrollPointerDragged &&
            TryGetWorldPosition(scrollPointerLastPosition, out Vector2 previousWorldPosition) &&
            TryGetWorldPosition(screenPosition, out Vector2 currentWorldPosition))
        {
            targetScrollOffset = Mathf.Clamp(
                targetScrollOffset + (currentWorldPosition.y - previousWorldPosition.y),
                minimumScrollOffset,
                maximumScrollOffset);
        }

        scrollPointerLastPosition = screenPosition;
    }

    private bool EndScrollDrag()
    {
        bool consumedClick = scrollPointerDown && scrollPointerDragged;
        scrollPointerDown = false;
        scrollPointerDragged = false;
        return consumedClick;
    }

    private bool TryGetCardContentBounds(out Bounds contentBounds)
    {
        contentBounds = default;
        bool hasBounds = false;
        IReadOnlyList<CardView> cards = cardCatalog.ActiveCards;

        for (int i = 0; i < cards.Count; i++)
        {
            SpriteRenderer renderer = cards[i] != null
                ? cards[i].BackgroundRenderer
                : null;
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                contentBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                contentBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void RefreshCardVisibility()
    {
        if (cardCatalog == null || scrollViewport == null)
        {
            return;
        }

        Bounds viewportBounds = scrollViewport.bounds;
        float viewportTop = viewportBounds.max.y;
        float viewportBottom = viewportBounds.min.y;
        IReadOnlyList<CardView> cards = cardCatalog.ActiveCards;

        for (int i = 0; i < cards.Count; i++)
        {
            CardView card = cards[i];
            if (card == null || card.BackgroundRenderer == null ||
                card.BackgroundRenderer.sprite == null)
            {
                continue;
            }

            float halfHeight = card.BackgroundRenderer.sprite.bounds.extents.y *
                Mathf.Abs(card.transform.lossyScale.y);
            float cardCenter = card.transform.position.y;
            bool isVisible = cardCenter + halfHeight >= viewportBottom &&
                cardCenter - halfHeight <= viewportTop;

            if (!cardVisibility.TryGetValue(card, out bool wasVisible) ||
                wasVisible != isVisible)
            {
                SetCardVisibility(card, isVisible);
                cardVisibility[card] = isVisible;
            }
        }
    }

    private static void SetCardVisibility(CardView card, bool isVisible)
    {
        SpriteRenderer[] spriteRenderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i].GetComponentInParent<CardView>() == card)
            {
                spriteRenderers[i].enabled = isVisible;
            }
        }

        MeshRenderer[] meshRenderers = card.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i].GetComponentInParent<CardView>() == card)
            {
                meshRenderers[i].enabled = isVisible;
            }
        }

        Collider2D[] colliders = card.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].GetComponentInParent<CardView>() == card)
            {
                colliders[i].enabled = isVisible;
            }
        }
    }

    private static CardSelectionMenu FindSceneMenu()
    {
        CardSelectionMenu[] menus = FindObjectsByType<CardSelectionMenu>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return menus.FirstOrDefault(menu => menu != null);
    }

    private void CacheCardRendererColors(CardView card)
    {
        SpriteRenderer[] spriteRenderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null &&
                renderer.GetComponentInParent<CardView>() == card &&
                !normalRendererColors.ContainsKey(renderer))
            {
                normalRendererColors[renderer] = renderer.color;
            }
        }
    }

    private void SetCardTintTarget(CardView card, Color tint)
    {
        CacheCardRendererColors(card);

        SpriteRenderer[] spriteRenderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || renderer.GetComponentInParent<CardView>() != card)
            {
                continue;
            }

            Color baseColor = normalRendererColors.TryGetValue(renderer, out Color storedColor)
                ? storedColor
                : Color.white;
            targetRendererColors[renderer] = new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * tint.a);
        }
    }

    private static void SetCardAlpha(CardView card, float alpha)
    {
        SpriteRenderer[] spriteRenderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || renderer.GetComponentInParent<CardView>() != card)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void UpdateCardTintAnimations()
    {
        float t = 1f - Mathf.Exp(-tintAnimationSpeed * Time.unscaledDeltaTime);
        SpriteRenderer[] renderers = targetRendererColors.Keys.ToArray();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                targetRendererColors.Remove(renderer);
                continue;
            }

            renderer.color = Color.Lerp(renderer.color, targetRendererColors[renderer], t);
        }
    }

    private void UpdateHoverAnimations()
    {
        Vector2 screenPosition;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        screenPosition = mouse.position.ReadValue();
#else
        screenPosition = Input.mousePosition;
#endif

        if (!TryGetWorldPosition(screenPosition, out Vector2 worldPosition))
        {
            return;
        }

        CardView nextHoveredCard = FindCardAt(worldPosition);
        if (nextHoveredCard != null && restrictedCards.Contains(nextHoveredCard))
        {
            nextHoveredCard = null;
        }

        if (hoveredCard != nextHoveredCard)
        {
            hoveredCard = nextHoveredCard;
            RefreshSelectedVisuals();
        }

        AnimateButtonScale(startButtonCollider, startButtonBaseScale, worldPosition);
        AnimateButtonScale(backButtonCollider, backButtonBaseScale, worldPosition);
    }

    private void CaptureButtonScales()
    {
        if (startButtonCollider != null)
        {
            startButtonBaseScale = startButtonCollider.transform.localScale;
        }

        if (backButtonCollider != null)
        {
            backButtonBaseScale = backButtonCollider.transform.localScale;
        }

        buttonScalesCaptured = true;
    }

    private void ResetButtonScales()
    {
        if (!buttonScalesCaptured)
        {
            return;
        }

        if (startButtonCollider != null)
        {
            startButtonCollider.transform.localScale = startButtonBaseScale;
        }

        if (backButtonCollider != null)
        {
            backButtonCollider.transform.localScale = backButtonBaseScale;
        }

        buttonScalesCaptured = false;
    }

    private void AnimateButtonScale(Collider2D button, Vector3 baseScale, Vector2 worldPosition)
    {
        if (!buttonScalesCaptured || button == null)
        {
            return;
        }

        Vector3 targetScale = baseScale * (button.OverlapPoint(worldPosition) ? buttonHoverScale : 1f);
        float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        button.transform.localScale = Vector3.Lerp(button.transform.localScale, targetScale, t);
    }

    private void StartCardMotion(
        CardView card,
        Vector3 targetPosition,
        Quaternion targetRotation,
        Vector3 targetScale)
    {
        if (card == null)
        {
            return;
        }

        cardMotionAnimations.RemoveAll(animation => animation.Card == null || animation.Card == card);
        cardMotionAnimations.Add(new CardMotionAnimation(
            card,
            card.transform.position,
            targetPosition,
            card.transform.rotation,
            targetRotation,
            card.transform.lossyScale,
            targetScale,
            dockMoveDuration));
    }

    private void UpdateCardMotionAnimations()
    {
        for (int i = 0; i < cardMotionAnimations.Count;)
        {
            CardMotionAnimation animation = cardMotionAnimations[i];
            if (animation.Card == null)
            {
                cardMotionAnimations.RemoveAt(i);
                continue;
            }

            animation.Elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(animation.Elapsed / animation.Duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            ApplyCardPlacement(
                animation.Card,
                Vector3.Lerp(animation.StartPosition, animation.TargetPosition, eased),
                Quaternion.Slerp(animation.StartRotation, animation.TargetRotation, eased),
                Vector3.Lerp(animation.StartScale, animation.TargetScale, eased));

            if (progress >= 1f)
            {
                cardMotionAnimations.RemoveAt(i);
            }
            else
            {
                cardMotionAnimations[i] = animation;
                i++;
            }
        }
    }

    private void CompleteCardMotionAnimations()
    {
        for (int i = 0; i < cardMotionAnimations.Count; i++)
        {
            CardMotionAnimation animation = cardMotionAnimations[i];
            if (animation.Card != null)
            {
                ApplyCardPlacement(
                    animation.Card,
                    animation.TargetPosition,
                    animation.TargetRotation,
                    animation.TargetScale);
            }
        }

        cardMotionAnimations.Clear();
    }

    private static void SetCardSortingOrder(CardView card, int backgroundOrder)
    {
        if (card == null)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null || renderer.GetComponentInParent<CardView>() != card)
            {
                continue;
            }

            renderer.sortingOrder = renderer == card.BackgroundRenderer
                ? backgroundOrder
                : backgroundOrder + 1;
        }

        MeshRenderer[] meshRenderers = card.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null &&
                meshRenderers[i].GetComponentInParent<CardView>() == card)
            {
                meshRenderers[i].sortingOrder = backgroundOrder + 2;
            }
        }
    }

    private readonly struct TransformSnapshot
    {
        public TransformSnapshot(Transform transform)
        {
            Position = transform.position;
            Rotation = transform.rotation;
            Scale = transform.lossyScale;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
    }

    private struct CardMotionAnimation
    {
        public CardMotionAnimation(
            CardView card,
            Vector3 startPosition,
            Vector3 targetPosition,
            Quaternion startRotation,
            Quaternion targetRotation,
            Vector3 startScale,
            Vector3 targetScale,
            float duration)
        {
            Card = card;
            StartPosition = startPosition;
            TargetPosition = targetPosition;
            StartRotation = startRotation;
            TargetRotation = targetRotation;
            StartScale = startScale;
            TargetScale = targetScale;
            Duration = Mathf.Max(0.01f, duration);
            Elapsed = 0f;
        }

        public CardView Card { get; }
        public Vector3 StartPosition { get; }
        public Vector3 TargetPosition { get; }
        public Quaternion StartRotation { get; }
        public Quaternion TargetRotation { get; }
        public Vector3 StartScale { get; }
        public Vector3 TargetScale { get; }
        public float Duration { get; }
        public float Elapsed { get; set; }
    }

}
