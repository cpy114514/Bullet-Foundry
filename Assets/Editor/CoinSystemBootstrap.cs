using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoinSystemBootstrap
{
    private const string CardPrefabPath = "Assets/Prefab/Cards.prefab";
    private const string CoinIconName = "UI_7";
    private const string CoinCountName = "Coin Count";

    static CoinSystemBootstrap()
    {
        EditorApplication.delayCall += EnsureCoinDisplay;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Coin Display")]
    public static void EnsureCoinDisplay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        CoinWallet wallet = Resources
            .FindObjectsOfTypeAll<CoinWallet>()
            .FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.IsValid());
        Transform coinIcon = Resources
            .FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                string.Equals(candidate.name, CoinIconName, StringComparison.Ordinal));
        if (coinIcon == null)
        {
            SpriteRenderer iconRenderer = Resources
                .FindObjectsOfTypeAll<SpriteRenderer>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    Vector2.Distance(
                        candidate.transform.position,
                        new Vector2(-8.78f, 3.98f)) <= 0.35f);
            coinIcon = iconRenderer != null ? iconRenderer.transform : null;
        }

        if (wallet == null || coinIcon == null)
        {
            return;
        }

        Transform countTransform = coinIcon.Find(CoinCountName);
        bool changed = false;
        if (countTransform == null)
        {
            TextMesh template = GetTextTemplate();
            if (template == null)
            {
                return;
            }

            GameObject countObject = UnityEngine.Object.Instantiate(
                template.gameObject,
                coinIcon,
                false);
            countObject.name = CoinCountName;
            countTransform = countObject.transform;
            countTransform.localPosition = new Vector3(0.6f, 0f, -0.05f);
            countTransform.localRotation = Quaternion.identity;
            changed = true;
        }

        TextMesh valueText = countTransform.GetComponent<TextMesh>();
        if (valueText == null)
        {
            return;
        }

        SerializedObject serializedWallet = new SerializedObject(wallet);
        int startingCoins = serializedWallet.FindProperty("startingCoins").intValue;
        valueText.text = Mathf.Max(0, startingCoins).ToString();

        CoinCounterDisplay display = countTransform.GetComponent<CoinCounterDisplay>();
        if (display == null)
        {
            display = countTransform.gameObject.AddComponent<CoinCounterDisplay>();
            changed = true;
        }

        SerializedObject serializedDisplay = new SerializedObject(display);
        SerializedProperty valueTextProperty = serializedDisplay.FindProperty("valueText");
        if (valueTextProperty.objectReferenceValue != valueText)
        {
            valueTextProperty.objectReferenceValue = valueText;
            serializedDisplay.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(countTransform.gameObject);
            EditorSceneManager.MarkSceneDirty(wallet.gameObject.scene);
            EditorSceneManager.SaveScene(wallet.gameObject.scene);
        }

    }

    private static TextMesh GetTextTemplate()
    {
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab == null)
        {
            return null;
        }

        return cardPrefab
            .GetComponentsInChildren<TextMesh>(true)
            .FirstOrDefault(text =>
                !text.name.Contains("Price", StringComparison.OrdinalIgnoreCase));
    }
}
