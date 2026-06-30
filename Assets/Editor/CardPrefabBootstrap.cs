using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CardPrefabBootstrap
{
    private const string CardName = "Card_fire";
    private const string LabelText = "Firetower";
    private const string PrefabPath = "Assets/Prefab/Cards.prefab";

    static CardPrefabBootstrap()
    {
        EditorApplication.delayCall += EnsureCardPrefabIfNeeded;
    }

    [MenuItem("Tools/Bullet Foundry/Create Tower Card Prefab")]
    public static void EnsureCardPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Transform card = FindSceneTransform(CardName);
        if (card == null)
        {
            return;
        }

        CardView cardView = EnsureCardView(card);
        cardView.Apply();

        PrefabUtility.SaveAsPrefabAssetAndConnect(
            card.gameObject,
            PrefabPath,
            InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
        AssetDatabase.SaveAssets();
        EnsurePrefabInteraction();
    }

    private static void EnsureCardPrefabIfNeeded()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            bool changed = EnsurePrefabInteraction();
            CardCatalog catalog = AssetDatabase
                .LoadAssetAtPath<GameObject>(PrefabPath)
                .GetComponent<CardCatalog>();
            changed |= CardCatalogEditor.ResolveTowerPrefabs(catalog);
            if (changed)
            {
                AssetDatabase.SaveAssets();
            }

            return;
        }

        EnsureCardPrefab();
    }

    private static bool EnsurePrefabInteraction()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            return false;
        }

        bool changed = false;
        try
        {
            CardView cardView = prefabRoot.GetComponent<CardView>();
            SpriteRenderer background = prefabRoot.GetComponent<SpriteRenderer>();
            if (cardView == null || background == null)
            {
                return false;
            }

            BoxCollider2D collider = prefabRoot.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = prefabRoot.AddComponent<BoxCollider2D>();
                changed = true;
            }

            Vector2 expectedSize = background.sprite != null
                ? background.sprite.bounds.size
                : background.size;
            if (collider.size != expectedSize)
            {
                collider.size = expectedSize;
                changed = true;
            }

            if (!collider.isTrigger)
            {
                collider.isTrigger = true;
                changed = true;
            }

            TowerCardDragHandle dragHandle =
                prefabRoot.GetComponent<TowerCardDragHandle>();
            if (dragHandle == null)
            {
                dragHandle = prefabRoot.AddComponent<TowerCardDragHandle>();
                changed = true;
            }

            SerializedObject serializedHandle = new SerializedObject(dragHandle);
            SerializedProperty cardViewProperty = serializedHandle.FindProperty("cardView");
            if (cardViewProperty.objectReferenceValue != cardView)
            {
                cardViewProperty.objectReferenceValue = cardView;
                serializedHandle.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return changed;
    }

    private static CardView EnsureCardView(Transform card)
    {
        CardView cardView = card.GetComponent<CardView>();
        if (cardView == null)
        {
            cardView = Undo.AddComponent<CardView>(card.gameObject);
        }

        SpriteRenderer backgroundRenderer = card.GetComponent<SpriteRenderer>();
        SpriteRenderer iconRenderer = FindIconRenderer(card, backgroundRenderer);
        TextMesh labelTextMesh = card.GetComponentInChildren<TextMesh>(true);

        SerializedObject serializedCardView = new SerializedObject(cardView);
        serializedCardView.FindProperty("backgroundRenderer").objectReferenceValue = backgroundRenderer;
        serializedCardView.FindProperty("iconRenderer").objectReferenceValue = iconRenderer;
        serializedCardView.FindProperty("labelTextMesh").objectReferenceValue = labelTextMesh;
        serializedCardView.FindProperty("iconSprite").objectReferenceValue = iconRenderer != null
            ? iconRenderer.sprite
            : null;
        serializedCardView.FindProperty("labelText").stringValue = labelTextMesh != null
            ? labelTextMesh.text
            : LabelText;
        serializedCardView.ApplyModifiedProperties();

        return cardView;
    }

    private static SpriteRenderer FindIconRenderer(Transform card, SpriteRenderer backgroundRenderer)
    {
        return card.GetComponentsInChildren<SpriteRenderer>(true)
            .FirstOrDefault(renderer => renderer != backgroundRenderer);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(transform =>
                transform.gameObject.scene.IsValid() &&
                string.Equals(transform.name, objectName, System.StringComparison.OrdinalIgnoreCase));
    }
}
