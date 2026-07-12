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
    private float resumeTimeScale = 1f;
    private bool isPaused;

    private void Awake()
    {
        BuildIfNeeded();
        SetPaused(false);
    }

    private void Update()
    {
        if (!WasEscapePressed()) return;
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
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
        window.SetActive(false);
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
        if (value == isPaused && overlay != null) return;
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

        pauseButton = CreateButton(canvas.transform, "Pause Button", "II");
        SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-138f, -88f), new Vector2(94f, 64f));
        pauseButton.onClick.AddListener(Pause);

        overlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one; overlayRect.offsetMin = Vector2.zero; overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, .62f);

        window = new GameObject("Pause Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(overlay.transform, false);
        Image background = window.GetComponent<Image>(); background.color = new Color(.96f, .96f, .93f, 1f);
        window.AddComponent<Outline>().effectColor = Color.black;
        SetRect(window.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-250f, -220f), new Vector2(500f, 440f));
        CreateLabel(window.transform, "PAUSED", new Vector2(0f, 120f), 54);
        Button resume = CreateButton(window.transform, "Resume", "RESUME"); SetRect(resume.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-175f, -55f), new Vector2(350f, 64f)); resume.onClick.AddListener(Resume);
        Button settings = CreateButton(window.transform, "Settings", "SETTINGS"); SetRect(settings.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-175f, -130f), new Vector2(350f, 64f)); settings.onClick.AddListener(OpenSettings);
        Button exit = CreateButton(window.transform, "Exit", "EXIT"); SetRect(exit.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(-175f, -205f), new Vector2(350f, 64f)); exit.onClick.AddListener(ExitToLevelSelect);
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Color.white; go.AddComponent<Outline>().effectColor = Color.black;
        CreateLabel(go.transform, label, Vector2.zero, 28);
        return go.GetComponent<Button>();
    }
    private static void CreateLabel(Transform parent, string value, Vector2 position, int size)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(460f, 70f);
        Text text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.black; text.raycastTarget = false;
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
