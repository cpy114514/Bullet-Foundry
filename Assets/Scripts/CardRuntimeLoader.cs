using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CardRuntimeLoader : MonoBehaviour
{
    private const string CardsResourcePath = "Cards";

    [SerializeField]
    private GameObject cardsPrefab;

    [SerializeField]
    private FireTowerPlacementSystem placementSystem;

    [SerializeField]
    private Transform cardSlotsRoot;

    private CardCatalog loadedCatalog;

    public CardCatalog LoadedCatalog => loadedCatalog;

    private void Awake()
    {
        LoadCards();
    }

    public void AdoptLoadedCatalog(CardCatalog catalog)
    {
        if (catalog == null)
        {
            LoadCards();
            return;
        }

        if (loadedCatalog != null && loadedCatalog != catalog)
        {
            Destroy(loadedCatalog.gameObject);
        }

        loadedCatalog = catalog;
        loadedCatalog.gameObject.name = cardsPrefab != null ? cardsPrefab.name : "Cards";
        LoadCards();
    }

    public void LoadCards()
    {
        LevelDefinition levelDefinition = FindFirstObjectByType<LevelDefinition>();
        if (levelDefinition != null && levelDefinition.ShouldDelayCardRuntimeLoad())
        {
            return;
        }

        ResolveCardSlotsRoot();

        if (loadedCatalog == null)
        {
            loadedCatalog = FindSceneGameplayCatalog();
        }

        if (loadedCatalog == null)
        {
            if (cardsPrefab == null)
            {
                cardsPrefab = Resources.Load<GameObject>(CardsResourcePath);
            }

            if (cardsPrefab == null)
            {
                return;
            }

            GameObject cardsObject = Instantiate(cardsPrefab);
            cardsObject.name = cardsPrefab.name;
            loadedCatalog = cardsObject.GetComponent<CardCatalog>();
        }

        if (loadedCatalog == null)
        {
            return;
        }

        loadedCatalog.SetCardSlotsRoot(cardSlotsRoot, cardSlotsRoot == null);

        IReadOnlyCollection<string> selectedTowerNames = CardSelectionState.SelectionConfirmed
            ? CardSelectionState.SelectedTowerNames
            : null;

        if (selectedTowerNames != null)
        {
            List<string> orderedTowerNames = new List<string>(CardSelectionState.SelectedTowerNames);
            if (levelDefinition != null && levelDefinition.HasCardRules())
            {
                HashSet<string> availableTowerNames = new HashSet<string>(
                    levelDefinition.GetAvailableTowerNames(
                        loadedCatalog.Cards,
                        selectedTowerNames));
                orderedTowerNames.RemoveAll(towerName => !availableTowerNames.Contains(towerName));
            }

            loadedCatalog.BuildCardsInOrder(orderedTowerNames);
        }
        else if (levelDefinition != null && levelDefinition.HasCardRules())
        {
            loadedCatalog.BuildCards(levelDefinition.GetAvailableTowerNames(
                loadedCatalog.Cards,
                null),
                false);
        }
        else
        {
            loadedCatalog.BuildCards();
        }

        if (placementSystem == null)
        {
            placementSystem = FindFirstObjectByType<FireTowerPlacementSystem>();
        }

        if (placementSystem != null)
        {
            placementSystem.SetCardCatalog(loadedCatalog);
        }
    }

    private void ResolveCardSlotsRoot()
    {
        if (cardSlotsRoot != null && cardSlotsRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        CardSlotPoint[] slots = FindObjectsByType<CardSlotPoint>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < slots.Length; i++)
        {
            CardSlotPoint slot = slots[i];
            if (slot == null || slot.GetComponentInParent<CardSelectionMenu>(true) != null)
            {
                continue;
            }

            cardSlotsRoot = slot.transform.parent;
            return;
        }
    }

    private CardCatalog FindSceneGameplayCatalog()
    {
        CardCatalog[] catalogs = FindObjectsByType<CardCatalog>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < catalogs.Length; i++)
        {
            CardCatalog catalog = catalogs[i];
            if (catalog == null ||
                catalog.GetComponentInParent<CardSelectionMenu>(true) != null ||
                IsTransientSelectionDock(catalog))
            {
                continue;
            }

            if (catalog.gameObject.name == "Cards")
            {
                return catalog;
            }
        }

        for (int i = 0; i < catalogs.Length; i++)
        {
            CardCatalog catalog = catalogs[i];
            if (catalog == null ||
                catalog.GetComponentInParent<CardSelectionMenu>(true) != null ||
                IsTransientSelectionDock(catalog))
            {
                continue;
            }

            return catalog;
        }

        return null;
    }

    private static bool IsTransientSelectionDock(CardCatalog catalog)
    {
        return catalog != null &&
            catalog.gameObject.name.StartsWith("Selected Cards Dock", System.StringComparison.Ordinal);
    }
}
