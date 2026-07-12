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
    [Header("Title-page visual style")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite pauseIconSprite;
    [SerializeField] private Font uiFont;

    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject window;
    [SerializeField] private Button pauseButton;
    private float resumeTimeScale = 1f;
    private bool isPaused;

    private void Awake()
    {
        BuildIfNeeded();
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

    private void BuildIfNeeded()
    {
        if (canvas != null) return;
        GameObject canvasObject = new("Pause UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3400;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        pauseButton = CreatePauseIconButton(canvas.transform);
        // This sits directly beside the shortened gameplay card dock.
        SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-116f, -86f), new Vector2(72f, 70f));
        pauseButton.onClick.AddListener(Pause);

        overlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one; overlayRect.offsetMin = Vector2.zero; overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, .48f);

        window = new GameObject("Pause Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(overlay.transform, false);
        Image background = window.GetComponent<Image>();
        ApplySprite(background, panelSprite, new Color(.98f, .98f, .96f, 1f));
        SetRect(window.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-270f, -260f), new Vector2(540f, 520f));
        CreateLabel(window.transform, "PAUSED", new Vector2(0f, 155f), 72);
        Button resume = CreateButton(window.transform, "Resume", "RESUME", 42); SetRect(resume.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-185f, 30f), new Vector2(370f, 78f)); resume.onClick.AddListener(Resume);
        Button settings = CreateButton(window.transform, "Settings", "SETTINGS", 42); SetRect(settings.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-185f, -72f), new Vector2(370f, 78f)); settings.onClick.AddListener(OpenSettings);
        Button exit = CreateButton(window.transform, "Exit", "EXIT", 42); SetRect(exit.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-185f, -174f), new Vector2(370f, 78f)); exit.onClick.AddListener(ExitToLevelSelect);
    }

    private Button CreateButton(Transform parent, string name, string label, int fontSize)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        ApplySprite(go.GetComponent<Image>(), buttonSprite, Color.white);
        CreateLabel(go.transform, label, Vector2.zero, fontSize);
        return go.GetComponent<Button>();
    }

    private Button CreatePauseIconButton(Transform parent)
    {
        GameObject go = new("Pause Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ApplySprite(go.GetComponent<Image>(), pauseIconSprite != null ? pauseIconSprite : buttonSprite, Color.white);
        CreatePauseBar(go.transform, "Left Bar", -10f);
        CreatePauseBar(go.transform, "Right Bar", 10f);
        return go.GetComponent<Button>();
    }

    private static void CreatePauseBar(Transform parent, string name, float x)
    {
        GameObject bar = new(name, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(7f, 28f);
        Image image = bar.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    private void CreateLabel(Transform parent, string value, Vector2 position, int size)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(460f, 70f);
        Text text = go.GetComponent<Text>(); text.text = value; text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.black; text.raycastTarget = false;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static void ApplySprite(Image image, Sprite sprite, Color fallbackColor)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = fallbackColor;
        if (sprite == null) image.gameObject.AddComponent<Outline>().effectColor = Color.black;
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
