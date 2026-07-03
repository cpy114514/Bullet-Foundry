using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TriwayTowerBootstrap
{
    private const string PrefabPath = "Assets/Prefab/TriwayTower.prefab";

    static TriwayTowerBootstrap()
    {
        EditorApplication.delayCall += EnsureTriwayTowerPrefab;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Triway Tower")]
    public static void EnsureTriwayTowerPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Transform sceneTower = UnityEngine.Object
            .FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(transform =>
                transform.gameObject.scene.IsValid() &&
                NormalizeName(transform.name) == "triwaytower");

        if (sceneTower == null)
        {
            ConfigureExistingPrefab();
            return;
        }

        string connectedPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
            sceneTower.gameObject);
        if (string.Equals(connectedPrefabPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
        {
            ConfigureExistingPrefab();
            return;
        }

        ConfigureTower(sceneTower.gameObject);
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            sceneTower.gameObject,
            PrefabPath,
            InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(sceneTower.gameObject);
        EditorSceneManager.MarkSceneDirty(sceneTower.gameObject.scene);
        EditorSceneManager.SaveScene(sceneTower.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("TriwayTower prefab created and connected to the scene object.");
    }

    private static void ConfigureExistingPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureTower(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureTower(GameObject tower)
    {
        if (tower.GetComponent<TriwayTower>() == null)
        {
            tower.AddComponent<TriwayTower>();
        }

        if (tower.GetComponent<TowerHealth>() == null)
        {
            tower.AddComponent<TowerHealth>();
        }

        BoxCollider2D collider = tower.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = tower.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        FitCollider(tower.transform, collider);
    }

    private static void FitCollider(Transform root, BoxCollider2D collider)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        collider.offset = root.InverseTransformPoint(bounds.center);
        Vector3 scale = root.lossyScale;
        collider.size = new Vector2(
            scale.x != 0f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            scale.y != 0f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y);
    }

    private static string NormalizeName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
