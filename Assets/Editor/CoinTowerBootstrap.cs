using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoinTowerBootstrap
{
    private const string CoinPickupPrefabPath = "Assets/Prefab/CoinPickup.prefab";

    static CoinTowerBootstrap()
    {
        EditorApplication.delayCall += EnsureCoinTowerSetup;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Coin Tower")]
    public static void EnsureCoinTowerSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Transform[] coinTowers = UnityEngine.Object
            .FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform =>
                transform.gameObject.scene.IsValid() &&
                transform.GetComponent<SpriteRenderer>() != null &&
                transform.name.StartsWith("cointower", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (coinTowers.Length == 0)
        {
            return;
        }

        Camera mainCamera = UnityEngine.Object
            .FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));

        if (mainCamera == null)
        {
            return;
        }

        bool changed = false;
        if (mainCamera.GetComponent<CoinWallet>() == null)
        {
            Undo.AddComponent<CoinWallet>(mainCamera.gameObject);
            changed = true;
        }

        for (int i = 0; i < coinTowers.Length; i++)
        {
            CoinTower coinTower = coinTowers[i].GetComponent<CoinTower>();
            if (coinTower == null)
            {
                coinTower = Undo.AddComponent<CoinTower>(coinTowers[i].gameObject);
                changed = true;
            }

            if (coinTower != null)
            {
                changed |= ConfigureCoinTower(coinTower);
            }

            if (coinTowers[i].GetComponent<TowerHealth>() == null)
            {
                Undo.AddComponent<TowerHealth>(coinTowers[i].gameObject);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
        }
    }

    private static bool ConfigureCoinTower(CoinTower coinTower)
    {
        SerializedObject serialized = new(coinTower);
        bool changed = false;

        SerializedProperty coinValue = serialized.FindProperty("coinValue");
        if (coinValue.intValue != 2)
        {
            coinValue.intValue = 2;
            changed = true;
        }

        GameObject coinPickupObject = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPickupPrefabPath);
        CoinPickup coinPickup = coinPickupObject != null
            ? coinPickupObject.GetComponent<CoinPickup>()
            : null;
        if (coinPickup != null)
        {
            SerializedProperty pickupPrefab = serialized.FindProperty("coinPickupPrefab");
            if (pickupPrefab.objectReferenceValue != coinPickup)
            {
                pickupPrefab.objectReferenceValue = coinPickup;
                changed = true;
            }

            SpriteRenderer coinRenderer = coinPickup.GetComponent<SpriteRenderer>();
            if (coinRenderer != null)
            {
                SerializedProperty pickupSprite = serialized.FindProperty("coinPickupSprite");
                if (pickupSprite.objectReferenceValue != coinRenderer.sprite)
                {
                    pickupSprite.objectReferenceValue = coinRenderer.sprite;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coinTower);
        }

        return changed;
    }
}
