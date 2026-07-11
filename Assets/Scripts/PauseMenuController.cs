using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene-local pause menu for playable levels.  It builds its own Canvas so it
/// does not need to rearrange the player's existing HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField]
    private string exitSceneName = "LevelSelect";

    [SerializeField]
    private Sprite panelSprite;

    [SerializeField]
    private Sprite buttonSprite;

    [Header("Scene UI References")]
    [SerializeField]
    private Canvas pauseCanvas;

    [SerializeField]
    private GameObject overlay;

    [SerializeField]
    private GameObject pauseWindow;

    [SerializeField]
    private Button pauseButton;
    private SettingsMenuController settingsMenu;
    private bool isPaused;
    private float timeScaleBeforePause = 1f;
    private bool preserveExistingPauseState;

    private void Awake()
    {
        ResolveSceneUiReferences();
        BuildUi();
        SetPaused(false, true);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Builds the editable pause UI into the open scene.  Runtime still has a
    /// fallback BuildUi call for old scenes, but Levels stores this UI now.
    /// </summary>
    public void RebuildSceneUi()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Canvas existingCanvas = pauseCanvas != null
            ? pauseCanvas
            : GetComponentInChildren<Canvas>(true);
        if (existingCanvas != null)
        {
            DestroyImmediate(existingCanvas.gameObject);
        }

        pauseCanvas = null;
        overlay = null;
        pauseWindow = null;
        pauseButton = null;
        BuildUi();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void Update()
    {
        if (isPaused && pauseWindow != null && !pauseWindow.activeSelf &&
            (settingsMenu == null || !settingsMenu.IsOpen))
        {
            pauseWindow.SetActive(true);
        }

        if (!isPaused && WasPauseButtonPressed())
        {
            Pause();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (settingsMenu != null && settingsMenu.IsOpen)
        {
            settingsMenu.CloseSettings();
            return;
        }

        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        SetPaused(true, false);
    }

    public void Resume()
    {
        if (settingsMenu != null && settingsMenu.IsOpen)
        {
            settingsMenu.CloseSettings();
        }

        SetPaused(false, false);
    }

    public void OpenSettings()
    {
        EnsureSettingsMenu();
        if (settingsMenu == null)
        {
            return;
        }

        if (pauseWindow != null)
        {
            pauseWindow.SetActive(false);
        }

        settingsMenu?.OpenSettings();
    }

    public void ExitToLevelSelect()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SceneTransitionController.LoadScene(exitSceneName);
    }

    private void SetPaused(bool shouldPause, bool immediate)
    {
        if (shouldPause == isPaused && !immediate)
        {
            return;
        }

        if (shouldPause)
        {
            timeScaleBeforePause = Time.timeScale;
            preserveExistingPauseState = timeScaleBeforePause <= 0.001f || CardSelectionMenu.IsOpen;
            Time.timeScale = 0f;
            isPaused = true;
        }
        else
        {
            if (isPaused)
            {
                Time.timeScale = preserveExistingPauseState
                    ? 0f
                    : Mathf.Max(0.01f, timeScaleBeforePause);
            }

            preserveExistingPauseState = false;
            isPaused = false;
        }

        if (overlay != null)
        {
            overlay.SetActive(shouldPause);
        }

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(!shouldPause);
        }

        if (!shouldPause && pauseWindow != null)
        {
            pauseWindow.SetActive(true);
        }
    }

    private void EnsureSettingsMenu()
    {
        if (settingsMenu == null)
        {
            settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }

        if (settingsMenu != null)
        {
            return;
        }

        Debug.LogWarning("Pause menu could not find the shared Settings Menu Controller.");
    }

    private bool WasPauseButtonPressed()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || pauseButton == null ||
            !pauseButton.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform buttonRect = pauseButton.transform as RectTransform;
        return buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(
            buttonRect,
            mouse.position.ReadValue(),
            null);
    }

    private void BuildUi()
    {
        ResolveSceneUiReferences();
        if (pauseCanvas != null && overlay != null && pauseWindow != null && pauseButton != null)
        {
            return;
        }

        GameObject canvasObject = new("Pause Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        pauseCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // This remains above world-space card selection UI and other gameplay HUD.
        canvas.sortingOrder = 3200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRoot = canvas.GetComponent<RectTransform>();
        pauseButton = CreateButton(canvasRoot, "Pause Button", "II", 32);
        SetAnchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-132f, -88f), new Vector2(96f, 64f));

        overlay = new GameObject("Pause Panel", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvasRoot, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        SetAnchor(overlayRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

        pauseWindow = new GameObject("Pause Window", typeof(RectTransform), typeof(Image), typeof(Outline));
        pauseWindow.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = pauseWindow.GetComponent<RectTransform>();
        SetAnchor(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-270f, -250f), new Vector2(270f, 250f));
        Image panelImage = pauseWindow.GetComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = panelSprite != null && panelSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        panelImage.color = new Color(0.96f, 0.96f, 0.93f, 1f);
        Outline outline = pauseWindow.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        Text title = CreateText(panelRect, "Title", "PAUSED", 58, TextAnchor.MiddleCenter);
        SetAnchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -112f), new Vector2(-28f, -40f));

        Button resumeButton = CreateButton(panelRect, "Resume Button", "RESUME", 30);
        SetAnchor(resumeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -212f), new Vector2(190f, -142f));
        resumeButton.onClick.AddListener(Resume);

        Button settingsButton = CreateButton(panelRect, "Settings Button", "SETTINGS", 30);
        SetAnchor(settingsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -302f), new Vector2(190f, -232f));
        settingsButton.onClick.AddListener(OpenSettings);

        Button exitButton = CreateButton(panelRect, "Exit Button", "EXIT", 30);
        SetAnchor(exitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -392f), new Vector2(190f, -322f));
        exitButton.onClick.AddListener(ExitToLevelSelect);
    }

    private void ResolveSceneUiReferences()
    {
        if (pauseCanvas == null)
        {
            pauseCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (pauseCanvas == null)
        {
            return;
        }

        Transform canvasTransform = pauseCanvas.transform;
        if (overlay == null)
        {
            Transform overlayTransform = canvasTransform.Find("Pause Panel");
            overlay = overlayTransform != null ? overlayTransform.gameObject : null;
        }

        if (pauseWindow == null && overlay != null)
        {
            Transform windowTransform = overlay.transform.Find("Pause Window");
            pauseWindow = windowTransform != null ? windowTransform.gameObject : null;
        }

        if (pauseButton == null)
        {
            Transform buttonTransform = canvasTransform.Find("Pause Button");
            pauseButton = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        }
    }

    private Button CreateButton(Transform parent, string objectName, string label, int fontSize)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = buttonSprite;
        image.type = buttonSprite != null && buttonSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, 1f);
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.82f, 0.8f, 1f);
        colors.pressedColor = new Color(0.62f, 0.62f, 0.6f, 1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Text", label, fontSize, TextAnchor.MiddleCenter);
        SetAnchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        text.raycastTarget = false;
        return button;
    }

    private static Text CreateText(Transform parent, string objectName, string value, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.black;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
