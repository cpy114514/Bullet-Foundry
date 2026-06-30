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
        serializedObject.ApplyModifiedProperties();
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

        list.index = newIndex;
        serializedObject.ApplyModifiedProperties();
    }
}
