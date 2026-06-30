using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CardEntry
{
    [SerializeField]
    private Sprite image;

    [SerializeField]
    [TextArea(2, 5)]
    private string displayName;

    [SerializeField, HideInInspector]
    private GameObject towerPrefab;

    public Sprite Image => image;

    public string DisplayName => displayName;

    public GameObject TowerPrefab => towerPrefab;
}

[DisallowMultipleComponent]
public sealed class CardCatalog : MonoBehaviour
{
    [SerializeField]
    private List<CardEntry> cards = new List<CardEntry>();

    [SerializeField, HideInInspector, Min(0.1f)]
    private float cardSpacing = 3.6f;

    private readonly List<CardView> activeCards = new List<CardView>();

    public IReadOnlyList<CardEntry> Cards => cards;

    public IReadOnlyList<CardView> ActiveCards => activeCards;

    private void Awake()
    {
        BuildCards();
    }

    public void BuildCards()
    {
        activeCards.Clear();

        CardView template = GetComponent<CardView>();
        if (template == null || cards.Count == 0)
        {
            return;
        }

        CardSlotPoint[] slots = FindCardSlots();
        ConfigureCard(template, cards[0]);
        PlaceCard(template.transform, 0, slots);
        activeCards.Add(template);

        for (int i = 1; i < cards.Count; i++)
        {
            CardView card = GetOrCreateCard(template, i);
            ConfigureCard(card, cards[i]);
            PlaceCard(card.transform, i, slots);
            activeCards.Add(card);
        }

        RemoveUnusedGeneratedCards(cards.Count);
    }

    private CardView GetOrCreateCard(CardView template, int index)
    {
        Transform existing = transform.Find(GetGeneratedCardName(index));
        CardView existingView = existing != null ? existing.GetComponent<CardView>() : null;
        return existingView != null
            ? existingView
            : CreateCardFromTemplate(template, index);
    }

    private CardView CreateCardFromTemplate(CardView template, int index)
    {
        GameObject cardObject = new GameObject(GetGeneratedCardName(index));
        Transform cardTransform = cardObject.transform;
        cardTransform.SetParent(transform, false);
        cardTransform.localPosition = new Vector3(cardSpacing * index, 0f, 0f);
        cardTransform.localRotation = Quaternion.identity;
        cardTransform.localScale = Vector3.one;

        SpriteRenderer background = cardObject.AddComponent<SpriteRenderer>();
        CopySpriteRenderer(template.BackgroundRenderer, background);

        SpriteRenderer icon = null;
        if (template.IconRenderer != null)
        {
            icon = Instantiate(template.IconRenderer.gameObject, cardTransform)
                .GetComponent<SpriteRenderer>();
        }

        TextMesh label = null;
        if (template.LabelTextMesh != null)
        {
            label = Instantiate(template.LabelTextMesh.gameObject, cardTransform)
                .GetComponent<TextMesh>();
        }

        CardView cardView = cardObject.AddComponent<CardView>();
        cardView.SetReferences(background, icon, label);

        BoxCollider2D templateCollider = template.GetComponent<BoxCollider2D>();
        BoxCollider2D cardCollider = cardObject.AddComponent<BoxCollider2D>();
        cardCollider.isTrigger = true;
        if (templateCollider != null)
        {
            cardCollider.offset = templateCollider.offset;
            cardCollider.size = templateCollider.size;
        }
        else if (background.sprite != null)
        {
            cardCollider.size = background.sprite.bounds.size;
        }

        TowerCardDragHandle dragHandle = cardObject.AddComponent<TowerCardDragHandle>();
        dragHandle.SetCardView(cardView);
        return cardView;
    }

    private void PlaceCard(Transform cardTransform, int index, CardSlotPoint[] slots)
    {
        if (index < slots.Length && slots[index] != null)
        {
            cardTransform.position = slots[index].transform.position;
            return;
        }

        if (index == 0)
        {
            return;
        }

        cardTransform.localPosition = new Vector3(cardSpacing * index, 0f, 0f);
    }

    private void RemoveUnusedGeneratedCards(int cardCount)
    {
        CardView[] childCards = GetComponentsInChildren<CardView>(true);
        for (int i = 0; i < childCards.Length; i++)
        {
            CardView childCard = childCards[i];
            if (childCard == null || childCard.gameObject == gameObject)
            {
                continue;
            }

            int generatedIndex = ParseGeneratedCardIndex(childCard.name);
            if (generatedIndex < cardCount)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(childCard.gameObject);
            }
            else
            {
                DestroyImmediate(childCard.gameObject);
            }
        }
    }

    private static CardSlotPoint[] FindCardSlots()
    {
        return FindObjectsByType<CardSlotPoint>(FindObjectsSortMode.None)
            .OrderBy(slot => slot.SlotIndex)
            .ThenBy(slot => slot.name)
            .ToArray();
    }

    private static string GetGeneratedCardName(int zeroBasedIndex)
    {
        return $"Card_{zeroBasedIndex + 1}";
    }

    private static int ParseGeneratedCardIndex(string objectName)
    {
        const string Prefix = "Card_";
        if (!objectName.StartsWith(Prefix, StringComparison.Ordinal) ||
            !int.TryParse(objectName.Substring(Prefix.Length), out int oneBasedIndex))
        {
            return -1;
        }

        return oneBasedIndex - 1;
    }

    private static void ConfigureCard(CardView view, CardEntry entry)
    {
        view.Configure(entry.Image, entry.DisplayName, entry.TowerPrefab);
    }

    private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.sharedMaterial = source.sharedMaterial;
    }
}
