using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class CardRuntimeLoader : MonoBehaviour
{
    [SerializeField]
    private GameObject cardsPrefab;

    [SerializeField]
    private FireTowerPlacementSystem placementSystem;

    private CardCatalog loadedCatalog;

    public CardCatalog LoadedCatalog => loadedCatalog;

    private void Awake()
    {
        LoadCards();
    }

    public void LoadCards()
    {
        if (loadedCatalog != null)
        {
            return;
        }

        CardCatalog existingCatalog = FindFirstObjectByType<CardCatalog>();
        if (existingCatalog != null)
        {
            loadedCatalog = existingCatalog;
        }
        else if (cardsPrefab != null)
        {
            GameObject cardsObject = Instantiate(cardsPrefab);
            cardsObject.name = cardsPrefab.name;
            loadedCatalog = cardsObject.GetComponent<CardCatalog>();
        }

        if (loadedCatalog == null)
        {
            return;
        }

        loadedCatalog.BuildCards();

        if (placementSystem == null)
        {
            placementSystem = FindFirstObjectByType<FireTowerPlacementSystem>();
        }

        if (placementSystem != null)
        {
            placementSystem.SetCardCatalog(loadedCatalog);
        }
    }
}
