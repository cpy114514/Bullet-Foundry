using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CardSlotBootstrap
{
    private const string ScenePath = "Assets/Scenes/Level_test.unity";
    private const string CardPrefabPath = "Assets/Prefab/Cards.prefab";
    private const string RootName = "Card Slots";
    private const int SlotCount = 6;
    private const float FirstSlotX = -7.4f;
    private const float SlotSpacing = 1.6f;
    private const float SlotY = 4f;

    static CardSlotBootstrap()
    {
        EditorApplication.delayCall += EnsureCardSlots;
    }

    [MenuItem("Tools/Bullet Foundry/Setup Card Slots")]
    public static void EnsureCardSlots()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        Transform root = scene.GetRootGameObjects()
            .Select(gameObject => gameObject.transform)
            .FirstOrDefault(transform =>
                string.Equals(transform.name, RootName, StringComparison.Ordinal));

        bool changed = false;
        if (root == null)
        {
            GameObject rootObject = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            root = rootObject.transform;
            changed = true;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            string slotName = $"Card Slot {i + 1}";
            Transform slot = root.Find(slotName);
            if (slot == null)
            {
                GameObject slotObject = new GameObject(slotName);
                slot = slotObject.transform;
                slot.SetParent(root, false);
                slot.position = new Vector3(FirstSlotX + SlotSpacing * i, SlotY, 0f);
                changed = true;
            }

            CardSlotPoint point = slot.GetComponent<CardSlotPoint>();
            if (point == null)
            {
                point = slot.gameObject.AddComponent<CardSlotPoint>();
                changed = true;
            }

            if (point.SlotIndex != i)
            {
                point.SetSlotIndex(i);
                EditorUtility.SetDirty(point);
                changed = true;
            }
        }

        CardCatalog[] sceneCatalogs = Resources.FindObjectsOfTypeAll<CardCatalog>()
            .Where(candidate =>
                candidate != null && candidate.gameObject.scene == scene)
            .ToArray();
        for (int i = 0; i < sceneCatalogs.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(sceneCatalogs[i].gameObject);
            changed = true;
        }

        FireTowerPlacementSystem placementSystem =
            Resources.FindObjectsOfTypeAll<FireTowerPlacementSystem>()
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.gameObject.scene == scene);
        if (placementSystem != null)
        {
            CardRuntimeLoader loader = placementSystem.GetComponent<CardRuntimeLoader>();
            if (loader == null)
            {
                loader = placementSystem.gameObject.AddComponent<CardRuntimeLoader>();
                changed = true;
            }

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            SerializedObject serializedLoader = new SerializedObject(loader);
            SerializedProperty prefabProperty =
                serializedLoader.FindProperty("cardsPrefab");
            SerializedProperty placementProperty =
                serializedLoader.FindProperty("placementSystem");
            if (prefabProperty.objectReferenceValue != cardPrefab ||
                placementProperty.objectReferenceValue != placementSystem)
            {
                prefabProperty.objectReferenceValue = cardPrefab;
                placementProperty.objectReferenceValue = placementSystem;
                serializedLoader.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loader);
                changed = true;
            }

            SerializedObject serializedPlacement = new SerializedObject(placementSystem);
            SerializedProperty catalogProperty =
                serializedPlacement.FindProperty("cardCatalog");
            if (catalogProperty != null && catalogProperty.objectReferenceValue != null)
            {
                catalogProperty.objectReferenceValue = null;
                serializedPlacement.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(placementSystem);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
