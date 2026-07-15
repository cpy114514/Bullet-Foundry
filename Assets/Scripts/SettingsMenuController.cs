using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class SettingsMenuController : MonoBehaviour
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;
    private const float DefaultFontScale = 1.75f;

    [Header("Build")]
    [SerializeField]
    private bool buildDefaultUiIfMissing = true;

    [SerializeField]
    private bool startClosed = true;

    [SerializeField, Range(1f, 2.5f)]
    private float fontScale = DefaultFontScale;

    [Header("Audio Sources")]
    [SerializeField]
    private AudioSource[] musicSources = System.Array.Empty<AudioSource>();

    [SerializeField]
    private AudioSource[] soundEffectSources = System.Array.Empty<AudioSource>();

    [Header("UI")]
    [SerializeField]
    private GameObject settingsPanel;

    [Header("Animation")]
    [SerializeField, Min(0.01f)]
    private float panelAnimationDuration = 0.18f;

    [SerializeField, Range(0.7f, 1f)]
    private float panelClosedScale = 0.9f;

    [SerializeField]
    private Button openSettingsButton;

    [SerializeField]
    private Button closeSettingsButton;

    [SerializeField]
    private Dropdown resolutionDropdown;

    [SerializeField]
    private Toggle fullscreenToggle;

    [SerializeField]
    private Toggle masterVolumeToggle;

    [SerializeField]
    private Slider masterVolumeSlider;

    [SerializeField]
    private Text masterVolumeValueText;

    [SerializeField]
    private Toggle musicToggle;

    [SerializeField]
    private Slider musicVolumeSlider;

    [SerializeField]
    private Text musicVolumeValueText;

    [SerializeField]
    private Toggle soundEffectsToggle;

    [SerializeField]
    private Slider soundEffectsVolumeSlider;

    [SerializeField]
    private Text soundEffectsVolumeValueText;

    [SerializeField]
    private Toggle clickEffectToggle;

    private readonly List<Vector2Int> resolutionOptions = new();
    private bool isApplyingUiState;
    private bool settingsIsOpen;
    private Coroutine settingsAnimationRoutine;
    private CanvasGroup settingsCanvasGroup;
    private Vector3 panelOpenScale = Vector3.one;

    public bool IsOpen => settingsIsOpen;

    private void Awake()
    {
        ResolveSceneUiReferences();

        if (buildDefaultUiIfMissing && settingsPanel == null)
        {
            BuildDefaultUi();
        }

        EnsureEventSystem();
        PopulateResolutionDropdown();
        BindUiEvents();
        LoadSettingsIntoUi();
        ApplySettings(false);
        CacheAnimationState();

        if (startClosed)
        {
            CloseSettingsImmediate();
        }
        else
        {
            OpenSettingsImmediate();
        }
    }

    // A settings canvas is deliberately stored in each scene so its layout can
    // be edited in Unity.  These name-based fallbacks keep that editable UI
    // connected even after a designer rearranges the hierarchy.
    private void ResolveSceneUiReferences()
    {
        Transform panel = settingsPanel != null
            ? settingsPanel.transform
            : FindNamed(transform, "Settings Panel");
        if (panel == null)
        {
            return;
        }

        settingsPanel = panel.gameObject;
        closeSettingsButton ??= FindComponent<Button>(panel, "Close Settings Button");
        resolutionDropdown ??= FindComponent<Dropdown>(panel, "RESOLUTION Dropdown");
        fullscreenToggle ??= FindComponent<Toggle>(panel, "FULLSCREEN Toggle");
        masterVolumeToggle ??= FindComponent<Toggle>(panel, "VOLUME Toggle");
        masterVolumeSlider ??= FindComponent<Slider>(panel, "VOLUME Slider");
        masterVolumeValueText ??= FindComponent<Text>(panel, "VOLUME Value");
        musicToggle ??= FindComponent<Toggle>(panel, "MUSIC Toggle");
        musicVolumeSlider ??= FindComponent<Slider>(panel, "MUSIC Slider");
        musicVolumeValueText ??= FindComponent<Text>(panel, "MUSIC Value");
        soundEffectsToggle ??= FindComponent<Toggle>(panel, "SOUND EFFECTS Toggle");
        soundEffectsVolumeSlider ??= FindComponent<Slider>(panel, "SOUND EFFECTS Slider");
        soundEffectsVolumeValueText ??= FindComponent<Text>(panel, "SOUND EFFECTS Value");
        clickEffectToggle ??= FindComponent<Toggle>(panel, "CLICK EFFECT Toggle");
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform found = FindNamed(root, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static Transform FindNamed(Transform root, string objectName)
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
            Transform found = FindNamed(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        LoadSettingsIntoUi();
        ApplySettings(false);
        StartSettingsAnimation(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        StartSettingsAnimation(false);
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        if (settingsIsOpen)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void ApplySettings()
    {
        ApplySettings(true);
    }

    private void CacheAnimationState()
    {
        if (settingsPanel == null)
        {
            return;
        }

        panelOpenScale = settingsPanel.transform.localScale;
        if (panelOpenScale == Vector3.zero)
        {
            panelOpenScale = Vector3.one;
        }

        settingsCanvasGroup = GetSettingsRoot().GetComponent<CanvasGroup>();
        if (settingsCanvasGroup == null)
        {
            settingsCanvasGroup = GetSettingsRoot().AddComponent<CanvasGroup>();
        }
    }

    private void StartSettingsAnimation(bool open)
    {
        CacheAnimationState();

        if (settingsAnimationRoutine != null)
        {
            StopCoroutine(settingsAnimationRoutine);
        }

        settingsAnimationRoutine = StartCoroutine(SettingsAnimationRoutine(open));
    }

    private IEnumerator SettingsAnimationRoutine(bool open)
    {
        GameObject root = GetSettingsRoot();
        settingsIsOpen = open;
        root.SetActive(true);
        settingsPanel.SetActive(true);

        float startAlpha = settingsCanvasGroup != null ? settingsCanvasGroup.alpha : (open ? 0f : 1f);
        float targetAlpha = open ? 1f : 0f;
        Vector3 closedScale = panelOpenScale * panelClosedScale;
        Vector3 startScale = settingsPanel.transform.localScale;
        Vector3 targetScale = open ? panelOpenScale : closedScale;
        float duration = Mathf.Max(0.01f, panelAnimationDuration);
        float elapsed = 0f;

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.blocksRaycasts = true;
            settingsCanvasGroup.interactable = open;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = SmoothStep(t);

            if (settingsCanvasGroup != null)
            {
                settingsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            }

            settingsPanel.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = targetAlpha;
            settingsCanvasGroup.blocksRaycasts = open;
            settingsCanvasGroup.interactable = open;
        }

        settingsPanel.transform.localScale = targetScale;

        if (!open)
        {
            settingsPanel.SetActive(false);
        }

        settingsAnimationRoutine = null;
    }

    private void OpenSettingsImmediate()
    {
        if (settingsPanel == null)
        {
            return;
        }

        CacheAnimationState();
        GameObject root = GetSettingsRoot();
        settingsIsOpen = true;
        root.SetActive(true);
        settingsPanel.SetActive(true);
        settingsPanel.transform.localScale = panelOpenScale;

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 1f;
            settingsCanvasGroup.blocksRaycasts = true;
            settingsCanvasGroup.interactable = true;
        }
    }

    private void CloseSettingsImmediate()
    {
        if (settingsPanel == null)
        {
            return;
        }

        CacheAnimationState();
        settingsIsOpen = false;
        settingsPanel.transform.localScale = panelOpenScale * panelClosedScale;
        settingsPanel.SetActive(false);

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.blocksRaycasts = false;
            settingsCanvasGroup.interactable = false;
        }

    }

    private GameObject GetSettingsRoot()
    {
        if (settingsPanel == null || settingsPanel.transform.parent == null)
        {
            return settingsPanel;
        }

        Transform parent = settingsPanel.transform.parent;
        return parent.GetComponent<Canvas>() != null ? parent.gameObject : settingsPanel;
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    public void SetMusicSources(AudioSource[] sources)
    {
        musicSources = sources ?? System.Array.Empty<AudioSource>();
        ApplySettings(false);
    }

    public void SetSoundEffectSources(AudioSource[] sources)
    {
        soundEffectSources = sources ?? System.Array.Empty<AudioSource>();
        ApplySettings(false);
    }

    private void BindUiEvents()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveListener(OpenSettings);
            openSettingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        AddToggleListener(masterVolumeToggle, OnMasterVolumeToggleChanged);
        AddSliderListener(masterVolumeSlider, OnMasterVolumeChanged);
        AddToggleListener(musicToggle, OnMusicToggleChanged);
        AddSliderListener(musicVolumeSlider, OnMusicVolumeChanged);
        AddToggleListener(soundEffectsToggle, OnSoundEffectsToggleChanged);
        AddSliderListener(soundEffectsVolumeSlider, OnSoundEffectsVolumeChanged);
        AddToggleListener(clickEffectToggle, OnClickEffectToggleChanged);
    }

    private static void AddSliderListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    private static void AddToggleListener(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(action);
        toggle.onValueChanged.AddListener(action);
    }

    private void PopulateResolutionDropdown()
    {
        resolutionOptions.Clear();
        AddResolutionOption(DefaultWidth, DefaultHeight);
        AddResolutionOption(1366, 768);
        AddResolutionOption(1600, 900);
        AddResolutionOption(1920, 1080);

        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            AddResolutionOption(resolutions[i].width, resolutions[i].height);
        }

        AddResolutionOption(Screen.currentResolution.width, Screen.currentResolution.height);
        resolutionOptions.Sort((left, right) =>
        {
            int widthCompare = left.x.CompareTo(right.x);
            return widthCompare != 0 ? widthCompare : left.y.CompareTo(right.y);
        });

        if (resolutionDropdown == null)
        {
            return;
        }

        List<string> labels = new();
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            Vector2Int option = resolutionOptions[i];
            labels.Add($"{option.x} x {option.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }

    private void AddResolutionOption(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Vector2Int option = new(width, height);
        if (!resolutionOptions.Contains(option))
        {
            resolutionOptions.Add(option);
        }
    }

    private void LoadSettingsIntoUi()
    {
        isApplyingUiState = true;

        SetDropdownValueWithoutNotify(resolutionDropdown, FindResolutionIndex(
            GameSettings.ResolutionWidth,
            GameSettings.ResolutionHeight));
        SetToggleValueWithoutNotify(fullscreenToggle, GameSettings.Fullscreen);
        SetToggleValueWithoutNotify(masterVolumeToggle, GameSettings.MasterVolumeEnabled);
        SetSliderValueWithoutNotify(masterVolumeSlider, GameSettings.MasterVolume);
        SetToggleValueWithoutNotify(musicToggle, GameSettings.MusicEnabled);
        SetSliderValueWithoutNotify(musicVolumeSlider, GameSettings.MusicVolume);
        SetToggleValueWithoutNotify(soundEffectsToggle, GameSettings.SoundEffectsEnabled);
        SetSliderValueWithoutNotify(soundEffectsVolumeSlider, GameSettings.SoundEffectsVolume);
        SetToggleValueWithoutNotify(clickEffectToggle, GameSettings.ClickEffectEnabled);
        RefreshValueTexts();
        RefreshControlInteractable();

        isApplyingUiState = false;
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            Vector2Int option = resolutionOptions[i];
            if (option.x == width && option.y == height)
            {
                return i;
            }
        }

        Vector2Int current = new(Screen.currentResolution.width, Screen.currentResolution.height);
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i] == current)
            {
                return i;
            }
        }

        return 0;
    }

    private static void SetDropdownValueWithoutNotify(Dropdown dropdown, int value)
    {
        if (dropdown != null)
        {
            dropdown.SetValueWithoutNotify(value);
            dropdown.RefreshShownValue();
        }
    }

    private static void SetToggleValueWithoutNotify(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private static void SetSliderValueWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }

    private void OnResolutionChanged(int _)
    {
        ApplySettings();
    }

    private void OnFullscreenChanged(bool _)
    {
        ApplySettings();
    }

    private void OnMasterVolumeToggleChanged(bool _)
    {
        ApplySettings();
    }

    private void OnMasterVolumeChanged(float _)
    {
        ApplySettings();
    }

    private void OnMusicToggleChanged(bool _)
    {
        ApplySettings();
    }

    private void OnMusicVolumeChanged(float _)
    {
        ApplySettings();
    }

    private void OnSoundEffectsToggleChanged(bool _)
    {
        ApplySettings();
    }

    private void OnSoundEffectsVolumeChanged(float _)
    {
        ApplySettings();
    }

    private void OnClickEffectToggleChanged(bool _)
    {
        ApplySettings();
    }

    private void ApplySettings(bool save)
    {
        if (isApplyingUiState)
        {
            return;
        }

        if (resolutionOptions.Count > 0 && (resolutionDropdown != null || fullscreenToggle != null))
        {
            int resolutionIndex = resolutionDropdown != null
                ? resolutionDropdown.value
                : FindResolutionIndex(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            resolutionIndex = Mathf.Clamp(resolutionIndex, 0, resolutionOptions.Count - 1);
            Vector2Int selectedResolution = resolutionOptions[resolutionIndex];
            bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : GameSettings.Fullscreen;
            GameSettings.ResolutionWidth = selectedResolution.x;
            GameSettings.ResolutionHeight = selectedResolution.y;
            GameSettings.Fullscreen = fullscreen;
            Screen.SetResolution(selectedResolution.x, selectedResolution.y, fullscreen);
        }

        if (masterVolumeToggle != null)
        {
            GameSettings.MasterVolumeEnabled = masterVolumeToggle.isOn;
        }

        if (masterVolumeSlider != null)
        {
            GameSettings.MasterVolume = masterVolumeSlider.value;
        }

        if (musicToggle != null)
        {
            GameSettings.MusicEnabled = musicToggle.isOn;
        }

        if (musicVolumeSlider != null)
        {
            GameSettings.MusicVolume = musicVolumeSlider.value;
        }

        if (soundEffectsToggle != null)
        {
            GameSettings.SoundEffectsEnabled = soundEffectsToggle.isOn;
        }

        if (soundEffectsVolumeSlider != null)
        {
            GameSettings.SoundEffectsVolume = soundEffectsVolumeSlider.value;
        }

        if (clickEffectToggle != null)
        {
            GameSettings.ClickEffectEnabled = clickEffectToggle.isOn;
        }

        GameSettings.ApplyAudio(musicSources, soundEffectSources);
        RefreshValueTexts();
        RefreshControlInteractable();

        if (save)
        {
            GameSettings.Save();
        }
    }

    private void RefreshValueTexts()
    {
        SetPercentText(masterVolumeValueText, masterVolumeSlider);
        SetPercentText(musicVolumeValueText, musicVolumeSlider);
        SetPercentText(soundEffectsVolumeValueText, soundEffectsVolumeSlider);
    }

    private void RefreshControlInteractable()
    {
        bool masterEnabled = masterVolumeToggle == null || masterVolumeToggle.isOn;
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.interactable = masterEnabled;
        }

        bool musicEnabled = musicToggle == null || musicToggle.isOn;
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.interactable = musicEnabled;
        }

        bool soundEffectsEnabled = soundEffectsToggle == null || soundEffectsToggle.isOn;
        if (soundEffectsVolumeSlider != null)
        {
            soundEffectsVolumeSlider.interactable = soundEffectsEnabled;
        }
    }

    private static void SetPercentText(Text text, Slider slider)
    {
        if (text != null && slider != null)
        {
            text.text = $"{Mathf.RoundToInt(slider.value * 100f)}%";
        }
    }

    private void BuildDefaultUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new("Settings Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform canvasTransform = canvas.transform;
        settingsPanel = CreatePanel(canvasTransform).gameObject;
        RectTransform panelRect = settingsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 700f);

        VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 32, 32);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        Text title = CreateText(settingsPanel.transform, "Settings Title", "SETTINGS", 34, TextAnchor.MiddleCenter);
        SetLayoutHeight(title.gameObject, 70f);

        resolutionDropdown = CreateDropdownRow("RESOLUTION");
        fullscreenToggle = CreateToggleRow("FULLSCREEN");
        CreateToggleSliderRow("VOLUME", out masterVolumeToggle, out masterVolumeSlider, out masterVolumeValueText);
        CreateToggleSliderRow("MUSIC", out musicToggle, out musicVolumeSlider, out musicVolumeValueText);
        CreateToggleSliderRow(
            "SOUND EFFECTS",
            out soundEffectsToggle,
            out soundEffectsVolumeSlider,
            out soundEffectsVolumeValueText);
        clickEffectToggle = CreateToggleRow("CLICK EFFECT");

        closeSettingsButton = CreateButton(settingsPanel.transform, "Close Settings Button", "BACK");
        SetLayoutHeight(closeSettingsButton.gameObject, 48f);
    }

    private RectTransform CreatePanel(Transform parent)
    {
        GameObject panelObject = new("Settings Panel");
        panelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = panelObject.AddComponent<RectTransform>();
        Image image = panelObject.AddComponent<Image>();
        image.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        return rectTransform;
    }

    private Dropdown CreateDropdownRow(string label)
    {
        GameObject row = CreateRow(label);
        Dropdown dropdown = CreateDropdown(row.transform, $"{label} Dropdown");
        SetLayoutWidth(dropdown.gameObject, 320f);
        return dropdown;
    }

    private Toggle CreateToggleRow(string label)
    {
        GameObject row = CreateRow(label);
        Toggle toggle = CreateToggle(row.transform, $"{label} Toggle");
        SetLayoutWidth(toggle.gameObject, 120f);
        return toggle;
    }

    private Slider CreateSliderRow(string label, out Text valueText)
    {
        GameObject row = CreateRow(label);
        Slider slider = CreateSlider(row.transform, $"{label} Slider");
        SetLayoutWidth(slider.gameObject, 240f);
        valueText = CreateText(row.transform, $"{label} Value", "100%", 20, TextAnchor.MiddleRight);
        SetLayoutWidth(valueText.gameObject, 120f);
        return slider;
    }

    private void CreateToggleSliderRow(
        string label,
        out Toggle toggle,
        out Slider slider,
        out Text valueText)
    {
        GameObject row = CreateRow(label);
        toggle = CreateToggle(row.transform, $"{label} Toggle");
        SetLayoutWidth(toggle.gameObject, 90f);
        slider = CreateSlider(row.transform, $"{label} Slider");
        SetLayoutWidth(slider.gameObject, 240f);
        valueText = CreateText(row.transform, $"{label} Value", "100%", 20, TextAnchor.MiddleRight);
        SetLayoutWidth(valueText.gameObject, 100f);
    }

    private GameObject CreateRow(string label)
    {
        GameObject row = new($"{label} Row");
        row.transform.SetParent(settingsPanel.transform, false);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 22f;
        SetLayoutHeight(row, 52f);

        Text labelText = CreateText(row.transform, $"{label} Label", label, 22, TextAnchor.MiddleLeft);
        SetLayoutWidth(labelText.gameObject, 360f);
        return row;
    }

    private Button CreateButton(Transform parent, string objectName, string label)
    {
        GameObject buttonObject = new(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(180f, 48f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Text", label, 22, TextAnchor.MiddleCenter);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = Mathf.RoundToInt(24f * Mathf.Max(1f, fontScale));
        text.raycastTarget = false;
        text.transform.SetAsLastSibling();
        return button;
    }

    private Dropdown CreateDropdown(Transform parent, string objectName)
    {
        GameObject dropdownObject = new(objectName);
        dropdownObject.transform.SetParent(parent, false);
        RectTransform rectTransform = dropdownObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(320f, 44f);

        Image image = dropdownObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 1f);

        Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
        Text label = CreateText(dropdownObject.transform, "Label", "", 18, TextAnchor.MiddleLeft);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        ConfigureDropdownText(label, 14, 18);
        dropdown.captionText = label;

        Text itemText = CreateDropdownTemplate(dropdownObject.transform, dropdown);
        dropdown.itemText = itemText;
        return dropdown;
    }

    private Text CreateDropdownTemplate(Transform parent, Dropdown dropdown)
    {
        GameObject templateObject = new("Template");
        templateObject.SetActive(false);
        templateObject.transform.SetParent(parent, false);

        RectTransform templateRect = templateObject.AddComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, 220f);
        Image templateImage = templateObject.AddComponent<Image>();
        templateImage.color = new Color(0.08f, 0.08f, 0.08f, 0.98f);
        ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        dropdown.template = templateRect;

        GameObject viewportObject = new("Viewport");
        viewportObject.transform.SetParent(templateObject.transform, false);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportRect;

        GameObject contentObject = new("Content");
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 48f);
        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        GameObject itemObject = new("Item");
        itemObject.transform.SetParent(contentObject.transform, false);
        RectTransform itemRect = itemObject.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0f, 48f);
        Toggle itemToggle = itemObject.AddComponent<Toggle>();
        Image itemBackground = itemObject.AddComponent<Image>();
        itemBackground.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        itemToggle.targetGraphic = itemBackground;

        Text itemLabel = CreateText(itemObject.transform, "Item Label", "Option", 18, TextAnchor.MiddleLeft);
        RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(12f, 0f);
        itemLabelRect.offsetMax = new Vector2(-12f, 0f);
        ConfigureDropdownText(itemLabel, 14, 18);
        return itemLabel;
    }

    private static void ConfigureDropdownText(Text text, int minSize, int maxSize)
    {
        if (text == null)
        {
            return;
        }

        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private Toggle CreateToggle(Transform parent, string objectName)
    {
        GameObject toggleObject = new(objectName);
        toggleObject.transform.SetParent(parent, false);
        RectTransform rectTransform = toggleObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(120f, 44f);

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        GameObject backgroundObject = new("Background");
        backgroundObject.transform.SetParent(toggleObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(44f, 44f);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.14f, 0.14f, 0.14f, 1f);

        GameObject checkmarkObject = new("Checkmark");
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(28f, 28f);
        Image checkmark = checkmarkObject.AddComponent<Image>();
        checkmark.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        return toggle;
    }

    private Slider CreateSlider(Transform parent, string objectName)
    {
        GameObject sliderObject = new(objectName);
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(240f, 44f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        RectTransform background = CreateSliderImage(sliderObject.transform, "Background", new Color(0.14f, 0.14f, 0.14f, 1f));
        background.anchorMin = new Vector2(0f, 0.35f);
        background.anchorMax = new Vector2(1f, 0.65f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        RectTransform fillArea = CreateRect(sliderObject.transform, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreateSliderImage(fillArea, "Fill", new Color(0.75f, 0.75f, 0.75f, 1f));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        RectTransform handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.9f, 0.9f, 0.9f, 1f));
        handle.sizeDelta = new Vector2(22f, 36f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private static RectTransform CreateRect(Transform parent, string objectName)
    {
        GameObject gameObject = new(objectName);
        gameObject.transform.SetParent(parent, false);
        return gameObject.AddComponent<RectTransform>();
    }

    private static RectTransform CreateSliderImage(Transform parent, string objectName, Color color)
    {
        RectTransform rectTransform = CreateRect(parent, objectName);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(Transform parent, string objectName, string text, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100f, 36f);

        Text textComponent = textObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.fontSize = Mathf.RoundToInt(fontSize * Mathf.Max(1f, fontScale));
        textComponent.alignment = alignment;
        textComponent.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        textComponent.font = GetDefaultFont();
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void SetLayoutHeight(GameObject gameObject, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = gameObject.AddComponent<LayoutElement>();
        }

        element.preferredHeight = height;
    }

    private static void SetLayoutWidth(GameObject gameObject, float width)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = gameObject.AddComponent<LayoutElement>();
        }

        element.preferredWidth = width;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
