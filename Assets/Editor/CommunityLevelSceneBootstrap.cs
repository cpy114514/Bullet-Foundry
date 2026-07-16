using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CommunityLevelSceneBootstrap
{
    private const string ScenePath = "Assets/Scenes/LevelSelect.unity";

    [MenuItem("Tools/Bullet Foundry/Setup Community Button")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before setting up the community button.");
            return;
        }

        Scene originalScene = EditorSceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject endlessButton = GameObject.Find("Endless Button");
        if (endlessButton == null || endlessButton.transform.parent == null)
        {
            Debug.LogError("Could not create the Community button: Endless Button is missing.");
            return;
        }

        Transform parent = endlessButton.transform.parent;
        Transform existing = parent.Find("Community Button");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject communityButton = Object.Instantiate(endlessButton, parent);
        communityButton.name = "Community Button";
        communityButton.transform.localPosition = endlessButton.transform.localPosition + new Vector3(3f, 0f, 0f);

        LevelSelectModeButton modeButton = communityButton.GetComponent<LevelSelectModeButton>();
        if (modeButton != null)
        {
            Object.DestroyImmediate(modeButton);
        }

        TextMesh label = communityButton.GetComponent<TextMesh>();
        if (label != null)
        {
            label.text = "COMMUNITY";
        }

        communityButton.AddComponent<CommunityLevelButton>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!string.IsNullOrWhiteSpace(originalScene.path) && originalScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(originalScene.path, OpenSceneMode.Single);
        }
    }
}
