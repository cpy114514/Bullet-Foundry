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
            EditorGUI.LabelField(rect, "卡片数据（按 + 添加）");

        cardList.elementHeight =
            (EditorGUIUtility.singleLineHeight * (TextAreaLines + 2f)) +
            (RowSpacing * 4f);

        cardList.drawElementCallback = DrawCardEntry;
        cardList.onAddCallback = AddCardEntry;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        cardList.DoLayoutList();
        bool changed = serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            ResolveTowerPrefabs((CardCatalog)target);
        }

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
            string normalizedName = NormalizeName(displayName);

            GameObject match = towerPrefabs.FirstOrDefault(prefab =>
                NormalizeName(prefab.name) == normalizedName);

            if (towerPrefab.objectReferenceValue != match)
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
        SerializedProperty image = entry.FindPropertyRelative("image");
        SerializedProperty displayName = entry.FindPropertyRelative("displayName");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        rect.y += RowSpacing;
        rect.height = lineHeight;

        EditorGUI.PropertyField(rect, image, new GUIContent("图片"));
        rect.y += lineHeight + RowSpacing;

        EditorGUI.LabelField(rect, "名称（可换行）");
        rect.y += lineHeight + RowSpacing;
        rect.height = lineHeight * TextAreaLines;
        displayName.stringValue = EditorGUI.TextArea(rect, displayName.stringValue);
    }

    private void AddCardEntry(ReorderableList list)
    {
        int newIndex = cardsProperty.arraySize;
        cardsProperty.InsertArrayElementAtIndex(newIndex);

        SerializedProperty newEntry = cardsProperty.GetArrayElementAtIndex(newIndex);
        newEntry.FindPropertyRelative("image").objectReferenceValue = null;
        newEntry.FindPropertyRelative("displayName").stringValue = string.Empty;
        newEntry.FindPropertyRelative("towerPrefab").objectReferenceValue = null;

        list.index = newIndex;
        serializedObject.ApplyModifiedProperties();
        ResolveTowerPrefabs((CardCatalog)target);
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
                    $"找不到与“{entry.DisplayName}”同名的塔楼 Prefab。",
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
