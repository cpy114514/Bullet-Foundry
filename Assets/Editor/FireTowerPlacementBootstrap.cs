using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FireTowerPlacementBootstrap
{
    private const string CardName = "Card_fire";
    private const string TowerPrefabPath = "Assets/Prefab/tower_fire.prefab";

    static FireTowerPlacementBootstrap()
    {
        EditorApplication.delayCall += EnsurePlacementSystem;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Fire Tower Placement")]
    public static void EnsurePlacementSystem()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Transform card = FindSceneTransform(CardName);
        Camera mainCamera = FindMainCamera();
        GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPrefabPath);

        if (card == null || mainCamera == null || towerPrefab == null)
        {
            return;
        }

        SpriteRenderer cardRenderer = card.GetComponent<SpriteRenderer>();
        if (cardRenderer == null)
        {
            return;
        }

        FireTowerPlacementSystem placementSystem =
            mainCamera.GetComponent<FireTowerPlacementSystem>();

        if (placementSystem == null)
        {
            placementSystem = Undo.AddComponent<FireTowerPlacementSystem>(mainCamera.gameObject);
        }

        SerializedObject serializedSystem = new SerializedObject(placementSystem);
        bool changed = false;

        changed |= SetObjectReference(
            serializedSystem.FindProperty("worldCamera"),
            mainCamera);
        changed |= SetObjectReference(
            serializedSystem.FindProperty("fireTowerCardRenderer"),
            cardRenderer);
        changed |= SetObjectReference(
            serializedSystem.FindProperty("fireTowerPrefab"),
            towerPrefab);

        if (changed)
        {
            serializedSystem.ApplyModifiedProperties();
            EditorUtility.SetDirty(placementSystem);
            EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
        }
    }

    private static bool SetObjectReference(SerializedProperty property, UnityEngine.Object value)
    {
        if (property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static Camera FindMainCamera()
    {
        return UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
    }

    private static Transform FindSceneTransform(string objectName)
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(transform =>
                transform.gameObject.scene.IsValid() &&
                string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase));
    }
}
