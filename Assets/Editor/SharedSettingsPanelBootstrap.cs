using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes the actual TitlePage settings canvas the source of truth, then places
/// that same Prefab in scenes that need Settings.
/// </summary>
public static class SharedSettingsPanelBootstrap
{
    private const string TitleScenePath = "Assets/Scenes/TitlePage.unity";
    private const string PrefabPath = "Assets/Prefabs/UI/SharedSettingsPanel.prefab";
    private const string CanvasName = "Settings UI Canvas";

    public static GameObject RefreshPrefabFromTitlePage()
    {
        Scene titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        Transform canvasTransform = FindRoot(titleScene, CanvasName);
        if (canvasTransform == null)
        {
            Debug.LogError("Cannot create shared Settings prefab: TitlePage has no Settings UI Canvas.");
            return null;
        }

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        GameObject source = canvasTransform.gameObject;
        GameObject prefab;
        if (PrefabUtility.IsPartOfPrefabInstance(source))
        {
            PrefabUtility.ApplyPrefabInstance(source, InteractionMode.AutomatedAction);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        else
        {
            prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(source, PrefabPath, InteractionMode.AutomatedAction);
        }

        EditorSceneManager.MarkSceneDirty(titleScene);
        EditorSceneManager.SaveScene(titleScene);
        return prefab;
    }

    public static void InstallIntoScene(Scene scene, GameObject prefab, int sortingOrder)
    {
        if (!scene.IsValid() || !scene.isLoaded || prefab == null)
        {
            return;
        }

        Transform existingCanvas = FindRoot(scene, CanvasName);
        if (existingCanvas != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCanvas.gameObject);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        instance.name = CanvasName;
        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }

        SettingsMenuController settingsMenu = FindSettingsMenu(scene);
        if (settingsMenu == null)
        {
            GameObject controllerRoot = new("Settings Menu Controller");
            SceneManager.MoveGameObjectToScene(controllerRoot, scene);
            settingsMenu = controllerRoot.AddComponent<SettingsMenuController>();
        }

        BindSettingsMenu(settingsMenu, instance.transform);
        EditorUtility.SetDirty(settingsMenu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BindSettingsMenu(SettingsMenuController settingsMenu, Transform canvasRoot)
    {
        SerializedObject serialized = new(settingsMenu);
        SetBool(serialized.FindProperty("buildDefaultUiIfMissing"), false);
        SetBool(serialized.FindProperty("startClosed"), true);
        SetObject(serialized.FindProperty("settingsPanel"), FindDeep(canvasRoot, "Settings Panel"));
        SetObject(serialized.FindProperty("closeSettingsButton"), FindDeep<Button>(canvasRoot, "Close Settings Button"));
        SetObject(serialized.FindProperty("resolutionDropdown"), FindDeep<Dropdown>(canvasRoot, "RESOLUTION Dropdown"));
        SetObject(serialized.FindProperty("fullscreenToggle"), FindDeep<Toggle>(canvasRoot, "FULLSCREEN Toggle"));
        SetObject(serialized.FindProperty("masterVolumeToggle"), FindDeep<Toggle>(canvasRoot, "VOLUME Toggle"));
        SetObject(serialized.FindProperty("masterVolumeSlider"), FindDeep<Slider>(canvasRoot, "VOLUME Slider"));
        SetObject(serialized.FindProperty("masterVolumeValueText"), FindDeep<Text>(canvasRoot, "VOLUME Value"));
        SetObject(serialized.FindProperty("musicToggle"), FindDeep<Toggle>(canvasRoot, "MUSIC Toggle"));
        SetObject(serialized.FindProperty("musicVolumeSlider"), FindDeep<Slider>(canvasRoot, "MUSIC Slider"));
        SetObject(serialized.FindProperty("musicVolumeValueText"), FindDeep<Text>(canvasRoot, "MUSIC Value"));
        SetObject(serialized.FindProperty("soundEffectsToggle"), FindDeep<Toggle>(canvasRoot, "SOUND EFFECTS Toggle"));
        SetObject(serialized.FindProperty("soundEffectsVolumeSlider"), FindDeep<Slider>(canvasRoot, "SOUND EFFECTS Slider"));
        SetObject(serialized.FindProperty("soundEffectsVolumeValueText"), FindDeep<Text>(canvasRoot, "SOUND EFFECTS Value"));
        SetObject(serialized.FindProperty("clickEffectToggle"), FindDeep<Toggle>(canvasRoot, "CLICK EFFECT Toggle"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SettingsMenuController FindSettingsMenu(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<SettingsMenuController>(true))
            .FirstOrDefault();
    }

    private static Transform FindRoot(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .Select(root => root.transform)
            .FirstOrDefault(transform => string.Equals(transform.name, objectName, StringComparison.Ordinal));
    }

    private static GameObject FindDeep(Transform root, string objectName)
    {
        return FindDeep<Transform>(root, objectName)?.gameObject;
    }

    private static T FindDeep<T>(Transform root, string objectName) where T : Component
    {
        return root.GetComponentsInChildren<T>(true)
            .FirstOrDefault(component => string.Equals(component.name, objectName, StringComparison.Ordinal));
    }

    private static void SetObject(SerializedProperty property, UnityEngine.Object value)
    {
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBool(SerializedProperty property, bool value)
    {
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
