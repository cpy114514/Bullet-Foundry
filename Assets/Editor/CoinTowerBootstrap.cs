using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CoinTowerBootstrap
{
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
            if (coinTowers[i].GetComponent<CoinTower>() == null)
            {
                Undo.AddComponent<CoinTower>(coinTowers[i].gameObject);
                changed = true;
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
}
