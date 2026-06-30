using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FireTowerPlacementBootstrap
{
    static FireTowerPlacementBootstrap()
    {
        EditorApplication.delayCall += EnsurePlacementSystem;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Tower Card Placement")]
    public static void EnsurePlacementSystem()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Camera mainCamera = FindMainCamera();
        if (mainCamera == null)
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
        changed |= SetObjectReference(serializedSystem.FindProperty("worldCamera"), mainCamera);

        if (changed)
        {
            serializedSystem.ApplyModifiedProperties();
            EditorUtility.SetDirty(placementSystem);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
        }
    }

    private static bool SetObjectReference(SerializedProperty property, UnityEngine.Object value)
    {
        if (property == null || property.objectReferenceValue == value)
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

}
