using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private string levelSelectSceneName = "LevelSelect";

    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject window;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    private float resumeTimeScale = 1f;
    private bool isPaused;

    private void Awake()
    {
        ResolveSceneReferences();
        ValidateSceneBuiltPauseUi();
        NormalizeSceneLayout();
        BindButtons();
        SetPaused(false);
    }

    private void Update()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();

        // Settings needs to appear above the paused game. Once it closes,
        // restore the pause window without unpausing the level.
        if (isPaused && settings != null && !settings.IsOpen && overlay != null && !overlay.activeSelf)
        {
            overlay.SetActive(true);
            if (window != null) window.SetActive(true);
        }

        if (!WasEscapePressed()) return;
        if (settings != null && settings.IsOpen)
        {
            settings.CloseSettings();
            return;
        }
        if (isPaused) Resume(); else Pause();
    }

    public void Pause() => SetPaused(true);
    public void Resume() => SetPaused(false);

    public void OpenSettings()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings == null) return;
        // The shared settings canvas is below this pause canvas. Disable the
        // whole overlay instead of only its window so it remains clickable.
        if (overlay != null) overlay.SetActive(false);
        if (window != null) window.SetActive(false);
        settings.OpenSettings();
    }

    public void ExitToLevelSelect()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneTransitionController.LoadScene(levelSelectSceneName);
    }

    private void SetPaused(bool value)
    {
        // A newly built overlay starts active. Do not early-out until its
        // visible state actually matches the requested pause state.
        if (value == isPaused && overlay != null && overlay.activeSelf == value) return;
        if (value)
        {
            resumeTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else if (isPaused)
        {
            Time.timeScale = resumeTimeScale;
        }
        isPaused = value;
        if (overlay != null) overlay.SetActive(value);
        if (pauseButton != null) pauseButton.gameObject.SetActive(!value);
        if (!value && window != null) window.SetActive(true);
    }

    private void ValidateSceneBuiltPauseUi()
    {
        if (canvas != null && pauseButton != null)
        {
            return;
        }

        Debug.LogWarning(
            "PauseMenuController requires scene-built Pause UI and Pause Button. No runtime pause UI was generated.",
            this);
    }

    private void ResolveSceneReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvas == null)
        {
            GameObject pauseCanvas = GameObject.Find("Pause UI");
            canvas = pauseCanvas != null ? pauseCanvas.GetComponent<Canvas>() : null;
        }

        Transform root = canvas != null ? canvas.transform : transform;
        if (overlay == null)
        {
            Transform found = FindChild(root, "Pause Overlay");
            overlay = found != null ? found.gameObject : null;
        }

        if (window == null)
        {
            Transform found = FindChild(root, "Pause Window");
            window = found != null ? found.gameObject : null;
        }

        if (pauseButton == null)
        {
            pauseButton = FindComponent<Button>(root, "Pause Button");
        }

        if (resumeButton == null)
        {
            resumeButton = FindComponent<Button>(root, "Resume");
        }

        if (settingsButton == null)
        {
            settingsButton = FindComponent<Button>(root, "Settings");
        }

        if (exitButton == null)
        {
            exitButton = FindComponent<Button>(root, "Exit");
        }
    }

    private void NormalizeSceneLayout()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3400;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }
        }

        if (overlay != null)
        {
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.pivot = new Vector2(0.5f, 0.5f);
                overlayRect.anchoredPosition = Vector2.zero;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.localScale = Vector3.one;
            }
        }

        if (window != null)
        {
            RectTransform windowRect = window.GetComponent<RectTransform>();
            if (windowRect != null)
            {
                SetRect(windowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 520f));
                windowRect.localScale = Vector3.one;
            }
        }
    }

    private void BindButtons()
    {
        BindButton(pauseButton, Pause);
        BindButton(resumeButton, Resume);
        BindButton(settingsButton, OpenSettings);
        BindButton(exitButton, ExitToLevelSelect);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform found = FindChild(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
