using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Linq;

public static class LevelPauseMenuBootstrap
{
    private const string LevelsScenePath = "Assets/Scenes/Levels.unity";
    private const string LevelSelectScenePath = "Assets/Scenes/LevelSelect.unity";
    private const string UiPath = "Assets/Image/UI.png";
    private const string Ui2Path = "Assets/Image/UI2.png";

    [MenuItem("Bullet Foundry/Setup Level Pause Menu")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before setting up the level pause menu.");
            return;
        }

        Scene previous = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(LevelsScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find("Pause Menu Controller");
        if (root == null)
        {
            root = new GameObject("Pause Menu Controller");
        }

        if (root.GetComponent<PauseMenuController>() == null)
        {
            root.AddComponent<PauseMenuController>();
        }

        PauseMenuController pauseMenu = root.GetComponent<PauseMenuController>();
        SerializedObject serializedPauseMenu = new(pauseMenu);
        serializedPauseMenu.FindProperty("panelSprite").objectReferenceValue = LoadSprite(UiPath, "UI_1");
        serializedPauseMenu.FindProperty("buttonSprite").objectReferenceValue = LoadSprite(Ui2Path, "UI2_8");
        serializedPauseMenu.ApplyModifiedPropertiesWithoutUndo();

        GameObject sharedSettingsPrefab = SharedSettingsPanelBootstrap.RefreshPrefabFromTitlePage();
        if (sharedSettingsPrefab == null)
        {
            return;
        }

        // RefreshPrefabFromTitlePage opens TitlePage, so reopen the level scene
        // before adding the shared instance.
        scene = EditorSceneManager.OpenScene(LevelsScenePath, OpenSceneMode.Single);
        root = GameObject.Find("Pause Menu Controller");
        pauseMenu = root != null ? root.GetComponent<PauseMenuController>() : null;
        if (pauseMenu != null)
        {
            serializedPauseMenu = new SerializedObject(pauseMenu);
            serializedPauseMenu.FindProperty("panelSprite").objectReferenceValue = LoadSprite(UiPath, "UI_1");
            serializedPauseMenu.FindProperty("buttonSprite").objectReferenceValue = LoadSprite(Ui2Path, "UI2_8");
            serializedPauseMenu.ApplyModifiedPropertiesWithoutUndo();
            pauseMenu.RebuildSceneUi();
        }

        SharedSettingsPanelBootstrap.InstallIntoScene(scene, sharedSettingsPrefab, 3300);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Scene levelSelectScene = EditorSceneManager.OpenScene(LevelSelectScenePath, OpenSceneMode.Single);
        SharedSettingsPanelBootstrap.InstallIntoScene(levelSelectScene, sharedSettingsPrefab, 100);
        EditorSceneManager.MarkSceneDirty(levelSelectScene);
        EditorSceneManager.SaveScene(levelSelectScene);

        if (previous.IsValid() && !string.IsNullOrWhiteSpace(previous.path) && previous.path != LevelSelectScenePath)
        {
            EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        Debug.Log("Level pause menu added to Levels.");
    }

    private static Sprite LoadSprite(string path, string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => string.Equals(sprite.name, spriteName, StringComparison.Ordinal));
    }
}
