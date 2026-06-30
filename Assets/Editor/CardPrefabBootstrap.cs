using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CardPrefabBootstrap
{
    private const string CardName = "Card_fire";
    private const string LabelText = "Firetower";
    private const string PrefabPath = "Assets/Prefab/Card_fire.prefab";

    static CardPrefabBootstrap()
    {
        EditorApplication.delayCall += EnsureCardPrefabIfNeeded;
    }

    [MenuItem("Tools/Bullet Foundry/Create Fire Card Prefab")]
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
    }

    private static void EnsureCardPrefabIfNeeded()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        EnsureCardPrefab();
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
