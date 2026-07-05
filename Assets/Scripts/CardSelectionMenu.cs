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
    private const float CardSlotStartX = -5.4f;
    private const float CardSlotStartY = 1.45f;
    private const float CardSlotSpacingX = 1.8f;
    private const float CardSlotSpacingY = -2.15f;
    private const int CardsPerRow = 7;

    [SerializeField]
    private GameObject cardsPrefab;

    [SerializeField]
    private Color selectedColor = new(0.55f, 0.55f, 0.55f, 1f);

    [SerializeField]
    private Color normalColor = Color.white;

    private readonly List<CardView> selectedCards = new();
    private readonly Dictionary<CardView, Color> normalCardColors = new();

    private Camera worldCamera;
    private GameObject cardsObject;
    private CardCatalog cardCatalog;
    private TextMesh selectedCountText;
    private Collider2D startButtonCollider;
    private Collider2D backButtonCollider;
    private LevelSelectCameraScroll cameraScroll;
    private string targetSceneName = "Level_test";

    public static bool IsOpen { get; private set; }

    public static void Show(string targetSceneName)
    {
        if (IsOpen)
        {
            return;
        }

        GameObject menuObject = new("Card Selection Menu");
        CardSelectionMenu menu = menuObject.AddComponent<CardSelectionMenu>();
        menu.targetSceneName = string.IsNullOrWhiteSpace(targetSceneName)
            ? "Level_test"
            : targetSceneName;
    }

    private void Awake()
    {
        IsOpen = true;
        worldCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        cameraScroll = FindFirstObjectByType<LevelSelectCameraScroll>();
        if (cameraScroll != null)
        {
            cameraScroll.enabled = false;
        }

        CardSelectionState.BeginSelection(targetSceneName);
        BuildMenu();
    }

    private void OnDestroy()
    {
        IsOpen = false;
        if (cameraScroll != null)
        {
            cameraScroll.enabled = true;
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        HandleClick(mouse.position.ReadValue());
#else
        if (Input.GetMouseButtonUp(0))
        {
            HandleClick(Input.mousePosition);
        }
#endif
    }

    private void BuildMenu()
    {
        transform.position = Vector3.zero;
        CreatePanel();
        CreateText("SELECT YOUR CARDS", new Vector3(0f, 3.25f, -0.2f), 0.34f, TextAnchor.MiddleCenter);
        selectedCountText = CreateText(
            string.Empty,
            new Vector3(0f, -3.15f, -0.2f),
            0.18f,
            TextAnchor.MiddleCenter);

        CreateButton("BACK", new Vector3(-5.8f, -3.55f, -0.2f), out backButtonCollider);
        CreateButton("START", new Vector3(5.8f, -3.55f, -0.2f), out startButtonCollider);

        CreateCardSlots();
        LoadCards();
        RefreshSelectedVisuals();
    }

    private void CreatePanel()
    {
        Sprite sprite = GetWhiteSprite();
        GameObject panel = new("Card Selection Panel");
        panel.transform.SetParent(transform, false);
        panel.transform.position = new Vector3(0f, 0f, 2f);
        panel.transform.localScale = new Vector3(15.5f, 8.4f, 1f);

        SpriteRenderer renderer = panel.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0f, 0f, 0f, 0.82f);
        renderer.sortingOrder = 80;

        BoxCollider2D collider = panel.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
    }

    private void CreateCardSlots()
    {
        for (int i = 0; i < 14; i++)
        {
            GameObject slot = new($"CardSelectSlot_{i}");
            slot.transform.SetParent(transform, false);
            int row = i / CardsPerRow;
            int column = i % CardsPerRow;
            slot.transform.position = new Vector3(
                CardSlotStartX + (column * CardSlotSpacingX),
                CardSlotStartY + (row * CardSlotSpacingY),
                -0.1f);

            CardSlotPoint point = slot.AddComponent<CardSlotPoint>();
            point.SetSlotIndex(i);
        }
    }

    private void LoadCards()
    {
        if (cardsPrefab == null)
        {
            cardsPrefab = Resources.Load<GameObject>(CardsResourcePath);
        }

        if (cardsPrefab == null)
        {
            CreateText(
                "Missing Resources/Cards prefab",
                new Vector3(0f, 0f, -0.2f),
                0.22f,
                TextAnchor.MiddleCenter);
            return;
        }

        cardsObject = Instantiate(cardsPrefab, transform);
        cardsObject.name = "Selectable Cards";
        cardCatalog = cardsObject.GetComponent<CardCatalog>();
        if (cardCatalog == null)
        {
            return;
        }

        cardCatalog.BuildCards();
        foreach (CardView card in cardCatalog.ActiveCards)
        {
            if (card == null || card.BackgroundRenderer == null)
            {
                continue;
            }

            normalCardColors[card] = card.BackgroundRenderer.color;
            card.transform.localScale *= 0.72f;
            card.BackgroundRenderer.sortingOrder = Mathf.Max(card.BackgroundRenderer.sortingOrder, 90);
            SetChildSortingOrder(card.transform, 91);
        }
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
            Destroy(gameObject);
            return;
        }

        CardView clickedCard = FindCardAt(worldPosition);
        if (clickedCard != null)
        {
            ToggleCard(clickedCard);
        }
    }

    private void ToggleCard(CardView card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            RefreshSelectedVisuals();
            return;
        }

        if (selectedCards.Count >= MaxSelectedCards)
        {
            return;
        }

        selectedCards.Add(card);
        RefreshSelectedVisuals();
    }

    private void ConfirmSelection()
    {
        if (selectedCards.Count > 0)
        {
            CardSelectionState.SetSelection(selectedCards);
        }
        else
        {
            CardSelectionState.ClearSelection();
        }

        SceneTransitionController.LoadScene(CardSelectionState.TargetSceneName);
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

                if (!normalCardColors.ContainsKey(card))
                {
                    normalCardColors[card] = card.BackgroundRenderer.color;
                }

                card.BackgroundRenderer.color = selectedCards.Contains(card)
                    ? selectedColor
                    : normalCardColors[card];
            }
        }

        if (selectedCountText != null)
        {
            selectedCountText.text = $"Selected {selectedCards.Count}/{MaxSelectedCards}";
        }
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

    private TextMesh CreateText(string text, Vector3 position, float characterSize, TextAnchor anchor)
    {
        GameObject textObject = new($"Text - {text}");
        textObject.transform.SetParent(transform, false);
        textObject.transform.position = position;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = anchor;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = 120;
        textMesh.color = Color.white;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 110;
        }

        return textMesh;
    }

    private void CreateButton(string label, Vector3 position, out Collider2D buttonCollider)
    {
        GameObject button = new($"{label} Button");
        button.transform.SetParent(transform, false);
        button.transform.position = position;
        button.transform.localScale = new Vector3(2.1f, 0.75f, 1f);

        SpriteRenderer background = button.AddComponent<SpriteRenderer>();
        background.sprite = GetWhiteSprite();
        background.color = Color.white;
        background.sortingOrder = 100;

        buttonCollider = button.AddComponent<BoxCollider2D>();
        buttonCollider.isTrigger = true;

        TextMesh text = CreateText(label, position + new Vector3(0f, -0.08f, -0.05f), 0.22f, TextAnchor.MiddleCenter);
        text.color = Color.black;
    }

    private static void SetChildSortingOrder(Transform root, int sortingOrder)
    {
        SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].sortingOrder = Mathf.Max(spriteRenderers[i].sortingOrder, sortingOrder);
            }
        }

        MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null)
            {
                meshRenderers[i].sortingOrder = Mathf.Max(meshRenderers[i].sortingOrder, sortingOrder + 1);
            }
        }
    }

    private static Sprite whiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }
}
