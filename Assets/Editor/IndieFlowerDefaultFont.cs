using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class IndieFlowerDefaultFont
{
    private const string FontPath = "Assets/Fonts/IndieFlower-Regular.ttf";

    private static Font defaultFont;

    static IndieFlowerDefaultFont()
    {
        ObjectFactory.componentWasAdded += OnComponentWasAdded;
        EditorApplication.hierarchyChanged += ApplyToDefaultTextObjects;
        EditorApplication.delayCall += ApplyToDefaultTextObjects;
    }

    [MenuItem("Tools/Bullet Foundry/Apply Indie Flower Font")]
    private static void ApplyToAllOpenSceneText()
    {
        var changed = ApplyToTextObjects(replaceCustomFonts: true, recordUndo: true);
        Debug.Log($"Applied Indie Flower font to {changed} text object(s).");
    }

    private static void OnComponentWasAdded(Component component)
    {
        if (component is Text uiText)
        {
            ApplyToText(uiText, replaceCustomFonts: false, recordUndo: false);
        }
        else if (component is TextMesh textMesh)
        {
            ApplyToTextMesh(textMesh, replaceCustomFonts: false, recordUndo: false);
        }
    }

    private static void ApplyToDefaultTextObjects()
    {
        ApplyToTextObjects(replaceCustomFonts: false, recordUndo: false);
    }

    private static int ApplyToTextObjects(bool replaceCustomFonts, bool recordUndo)
    {
        var changed = 0;

        foreach (var uiText in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (ApplyToText(uiText, replaceCustomFonts, recordUndo))
            {
                changed++;
            }
        }

        foreach (var textMesh in Resources.FindObjectsOfTypeAll<TextMesh>())
        {
            if (ApplyToTextMesh(textMesh, replaceCustomFonts, recordUndo))
            {
                changed++;
            }
        }

        return changed;
    }

    private static bool ApplyToText(Text text, bool replaceCustomFonts, bool recordUndo)
    {
        if (!ShouldProcess(text) || !ShouldReplace(text.font, replaceCustomFonts))
        {
            return false;
        }

        var font = GetDefaultFont();
        if (font == null)
        {
            return false;
        }

        if (recordUndo)
        {
            Undo.RecordObject(text, "Apply Indie Flower Font");
        }

        text.font = font;
        MarkDirty(text);
        return true;
    }

    private static bool ApplyToTextMesh(TextMesh textMesh, bool replaceCustomFonts, bool recordUndo)
    {
        if (!ShouldProcess(textMesh) || !ShouldReplace(textMesh.font, replaceCustomFonts))
        {
            return false;
        }

        var font = GetDefaultFont();
        if (font == null)
        {
            return false;
        }

        if (recordUndo)
        {
            Undo.RecordObject(textMesh, "Apply Indie Flower Font");
        }

        textMesh.font = font;
        var meshRenderer = textMesh.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            if (recordUndo)
            {
                Undo.RecordObject(meshRenderer, "Apply Indie Flower Font Material");
            }

            meshRenderer.sharedMaterial = font.material;
            MarkDirty(meshRenderer);
        }

        MarkDirty(textMesh);
        return true;
    }

    private static bool ShouldProcess(Component component)
    {
        if (component == null || component.gameObject == null)
        {
            return false;
        }

        if ((component.hideFlags & HideFlags.NotEditable) != 0)
        {
            return false;
        }

        if (EditorUtility.IsPersistent(component))
        {
            return AssetDatabase.GetAssetPath(component).StartsWith("Assets/");
        }

        return component.gameObject.scene.IsValid();
    }

    private static bool ShouldReplace(Font font, bool replaceCustomFonts)
    {
        if (replaceCustomFonts || font == null)
        {
            return true;
        }

        return font.name == "Arial" || font.name == "LegacyRuntime";
    }

    private static Font GetDefaultFont()
    {
        if (defaultFont != null)
        {
            return defaultFont;
        }

        defaultFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (defaultFont == null)
        {
            Debug.LogWarning($"Could not load default font at {FontPath}.");
        }

        return defaultFont;
    }

    private static void MarkDirty(Component component)
    {
        EditorUtility.SetDirty(component);

        if (!EditorUtility.IsPersistent(component) && component.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }
}
