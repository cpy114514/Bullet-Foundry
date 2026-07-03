using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CardCatalog))]
public sealed class CardCatalogEditor : Editor
{
    private const float RowSpacing = 2f;
    private const int TextAreaLines = 3;

    private ReorderableList cardList;
    private SerializedProperty cardsProperty;

    private void OnEnable()
    {
        cardsProperty = serializedObject.FindProperty("cards");
        cardList = new ReorderableList(
            serializedObject,
            cardsProperty,
            true,
            true,
            true,
            true);

        cardList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "卡牌数据（按 + 添加）");

        cardList.elementHeight =
            (EditorGUIUtility.singleLineHeight * (TextAreaLines + 4f)) +
            (RowSpacing * 6f);

        cardList.drawElementCallback = DrawCardEntry;
        cardList.onAddCallback = AddCardEntry;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        cardList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();

        DrawResolutionWarnings();
    }

    public static bool ResolveTowerPrefabs(CardCatalog catalog)
    {
        if (catalog == null)
        {
            return false;
        }

        GameObject[] towerPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab.GetComponent<TowerHealth>() != null)
            .ToArray();

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty entries = serializedCatalog.FindProperty("cards");
        bool changed = false;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string displayName = entry.FindPropertyRelative("displayName").stringValue;
            SerializedProperty towerPrefab = entry.FindPropertyRelative("towerPrefab");
            if (towerPrefab.objectReferenceValue != null)
            {
                continue;
            }

            string normalizedName = NormalizeName(displayName);

            GameObject match = towerPrefabs.FirstOrDefault(prefab =>
                NormalizeName(prefab.name) == normalizedName);

            if (match != null)
            {
                towerPrefab.objectReferenceValue = match;
                changed = true;
            }
        }

        if (changed)
        {
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        return changed;
    }

    private void DrawCardEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = cardsProperty.GetArrayElementAtIndex(index);
        SerializedProperty towerPrefab = entry.FindPropertyRelative("towerPrefab");
        SerializedProperty displayName = entry.FindPropertyRelative("displayName");
        SerializedProperty price = entry.FindPropertyRelative("price");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        rect.y += RowSpacing;
        rect.height = lineHeight;

        EditorGUI.PropertyField(rect, towerPrefab, new GUIContent("塔楼 Prefab"));
        rect.y += lineHeight + RowSpacing;

        EditorGUI.LabelField(rect, "名称（可换行）");
        rect.y += lineHeight + RowSpacing;
        rect.height = lineHeight * TextAreaLines;
        displayName.stringValue = EditorGUI.TextArea(rect, displayName.stringValue);
        rect.y += rect.height + RowSpacing;
        rect.height = lineHeight;
        EditorGUI.PropertyField(rect, price, new GUIContent("价格"));
    }

    private void AddCardEntry(ReorderableList list)
    {
        int newIndex = cardsProperty.arraySize;
        cardsProperty.InsertArrayElementAtIndex(newIndex);

        SerializedProperty newEntry = cardsProperty.GetArrayElementAtIndex(newIndex);
        newEntry.FindPropertyRelative("displayName").stringValue = string.Empty;
        newEntry.FindPropertyRelative("towerPrefab").objectReferenceValue = null;
        newEntry.FindPropertyRelative("price").intValue = 25;

        list.index = newIndex;
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawResolutionWarnings()
    {
        CardCatalog catalog = (CardCatalog)target;
        for (int i = 0; i < catalog.Cards.Count; i++)
        {
            CardEntry entry = catalog.Cards[i];
            if (!string.IsNullOrWhiteSpace(entry.DisplayName) && entry.TowerPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    $"卡牌“{entry.DisplayName}”还没有指定塔楼 Prefab。",
                    MessageType.Warning);
            }
        }
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
