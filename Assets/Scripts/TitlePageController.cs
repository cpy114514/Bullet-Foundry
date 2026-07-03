using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public sealed class TitlePageController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "LevelSelect";

    [SerializeField]
    private SettingsMenuController settingsMenu;

    private bool isStartingGame;

    private void Awake()
    {
        if (settingsMenu == null)
        {
            settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }
    }

    public void StartGame()
    {
        if (IsSettingsOpen())
        {
            return;
        }

        if (isStartingGame)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogWarning("TitlePageController cannot start because gameSceneName is empty.");
            return;
        }

        isStartingGame = true;
        SceneTransitionController.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        if (IsSettingsOpen())
        {
            return;
        }

        SceneTransitionController.RunAfterFade(QuitAfterTransition);
    }

    public void OpenSettings()
    {
        ResolveSettingsMenu();
        if (settingsMenu != null)
        {
            settingsMenu.OpenSettings();
        }
    }

    public void CloseSettings()
    {
        ResolveSettingsMenu();
        if (settingsMenu != null)
        {
            settingsMenu.CloseSettings();
        }
    }

    public void ToggleSettings()
    {
        ResolveSettingsMenu();
        if (settingsMenu != null)
        {
            settingsMenu.ToggleSettings();
        }
    }

    public void SetGameSceneName(string sceneName)
    {
        gameSceneName = sceneName;
    }

    public bool IsSettingsOpen()
    {
        ResolveSettingsMenu();
        return settingsMenu != null && settingsMenu.IsOpen;
    }

    private void ResolveSettingsMenu()
    {
        if (settingsMenu == null)
        {
            settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }
    }

    private static void QuitAfterTransition()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        QuitWebPage();
#elif UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void BulletFoundryQuitWebPage();

    private static void QuitWebPage()
    {
        BulletFoundryQuitWebPage();
    }
#endif
}
