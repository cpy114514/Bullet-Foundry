using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class LevelEditorController : MonoBehaviour
{
    private static readonly Color CardNormalColor = new(0.98f, 0.98f, 0.96f, 1f);
    private static readonly Color CardSelectedColor = new(0.82f, 0.82f, 0.8f, 1f);
    private static readonly Color CardDisabledColor = new(0.62f, 0.62f, 0.6f, 1f);
    private const float TimelineMarkerBaseWidth = 124f;
    private const string DefaultLevelName = "Custom Level";

    [Header("Data")]
    [SerializeField]
    private string levelId = "custom-level";

    [SerializeField]
    private string displayName = "Custom Level";

    [SerializeField, Min(0)]
    private int startingCoins = 75;

    [SerializeField, Min(5f)]
    private float timelineDuration = 60f;

    [SerializeField, Min(1)]
    private int laneCount = 5;

    [SerializeField]
    private float spawnX = 8.5f;

    [SerializeField]
    private string outputFileName = "Custom Level.json";

    [SerializeField]
    private string playSceneName = "Levels";

    [SerializeField]
    private string levelSelectSceneName = "LevelSelect";

    [Header("Catalog")]
    [SerializeField]
    private List<string> enemyIds = new()
    {
        "Goblin",
        "SpeedGoblin",
        "Barbarian",
        "PigLeader",
        "FrogPrincess",
        "Chicken",
        "Giant"
    };

    [SerializeField]
    private List<string> towerNames = new()
    {
        "FireTower",
        "IceTower",
        "CoinTower",
        "DoubleHeadCoinTower",
        "SplitTower",
        "TriwayTower",
        "TransitTower",
        "OrbitTower",
        "ElectricTower",
        "RandomBulletTower",
        "RocketTower",
        "ShieldTower",
        "HomingTower"
    };

    [Header("Scene UI References")]
    [SerializeField]
    private Font uiFont;

    [SerializeField]
    private InputField levelIdInput;

    [SerializeField]
    private InputField displayNameInput;

    [SerializeField]
    private InputField startingCoinsInput;

    [SerializeField]
    private InputField timelineDurationInput;

    [SerializeField]
    private InputField outputFileInput;

    [SerializeField]
    private RectTransform enemyListRoot;

    [SerializeField]
    private RectTransform towerListRoot;

    [SerializeField]
    private RectTransform laneButtonRoot;

    [SerializeField]
    private RectTransform timelineArea;

    [SerializeField]
    private RectTransform timelineViewport;

    [SerializeField]
    private ScrollRect timelineScrollRect;

    [SerializeField]
    private RectTransform timelineGuideRoot;

    [SerializeField]
    private RectTransform markerRoot;

    [SerializeField]
    private RectTransform timelineOverviewDotRoot;

    [SerializeField]
    private RectTransform timelineOverviewViewportIndicator;

    [SerializeField]
    private Text statusText;

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;

    [SerializeField]
    private Button exportButton;

    [SerializeField]
    private Button testButton;

    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button undoButton;

    [SerializeField]
    private Button clearButton;

    [SerializeField]
    private Button deleteModeButton;

    [SerializeField]
    private GameObject buttonPrefab;

    [SerializeField]
    private GameObject markerPrefab;

    [Header("Timeline Layout")]
    [SerializeField, Min(20f)]
    private float timelinePixelsPerSecond = 90f;

    [SerializeField, Min(40f)]
    private float timelineLaneHeight = 96f;

    [SerializeField, Min(20f)]
    private float timelineHeaderHeight = 42f;

    private readonly List<LevelEditorSpawn> spawns = new();
    private readonly Dictionary<string, bool> towerAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameObject> generatedButtons = new();
    private readonly List<GameObject> generatedMarkers = new();
    private readonly Dictionary<int, RectTransform> generatedMarkerRects = new();
    private readonly List<GameObject> generatedOverviewDots = new();
    private readonly Stack<List<LevelEditorSpawn>> undoHistory = new();
    private readonly Stack<List<LevelEditorSpawn>> redoHistory = new();
    private readonly HashSet<int> selectedSpawnIds = new();
    private readonly Dictionary<int, Vector2> dragStartPositions = new();

    private string selectedEnemyId;
    private int selectedLane = 3;
    private bool deleteMode;
    private Canvas rootCanvas;
    private GameObject dragGhost;
    private GameObject timelinePreview;
    private bool pendingTimelineLayoutRefresh;
    private int nextSpawnId = 1;
    private int selectedSpawnId = -1;
    private bool draggingSpawnMarker;
    private List<LevelEditorSpawn> dragStartSnapshot;
    private RectTransform boxSelectionRect;
    private Vector2 boxSelectionStart;
    private bool boxSelecting;
    private bool timelinePanning;
    private Vector2 timelinePanStartPointer;
    private float timelinePanStartPosition;
    private GameObject markerContextMenu;
    private InputField markerContextCountInput;
    private int markerContextSpawnId = -1;
    private GameObject levelFilePanel;
    private ScrollRect localLevelScrollRect;
    private RectTransform localLevelListRoot;
    private Text localLevelEmptyText;

    public float TimelineDuration => Mathf.Max(5f, timelineDuration);

    private void Awake()
    {
        NormalizeCatalogs();
        ResetTowerRules();
        // Opening the editor should not arm a placement card. The player must
        // explicitly click or drag an enemy card before placing anything.
        selectedEnemyId = null;
        selectedLane = Mathf.Clamp(selectedLane, 1, laneCount);
    }

    private void Start()
    {
        WireStaticUi();
        PushDataToInputs();
        RebuildDynamicUi();
        pendingTimelineLayoutRefresh = true;
        SetStatus("Drag an enemy card onto the timeline track.");
    }

    private void LateUpdate()
    {
        if (!pendingTimelineLayoutRefresh)
        {
            return;
        }

        pendingTimelineLayoutRefresh = false;
        RebuildTimeline();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
        HandleRightClickFallback();
    }

    private void HandleKeyboardShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || IsInputFieldFocused())
        {
            return;
        }

        if ((keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) &&
            selectedSpawnIds.Count > 0)
        {
            DeleteSelectedSpawns();
        }

        if (IsCtrlHeld() && keyboard.zKey.wasPressedThisFrame)
        {
            UndoLastChange();
            return;
        }

        if (IsCtrlHeld() && keyboard.yKey.wasPressedThisFrame)
        {
            RedoLastChange();
        }
    }

    private void HandleRightClickFallback()
    {
        if (IsInputFieldFocused() ||
            !WasRightMousePressedThisFrame(out Vector2 screenPosition))
        {
            return;
        }

        Camera eventCamera = GetCanvasEventCamera();
        if (IsPointerInsideContextMenu(screenPosition, eventCamera))
        {
            return;
        }

        if (TryGetSpawnMarkerAtScreenPoint(screenPosition, eventCamera, out int spawnId))
        {
            OpenMarkerContextMenu(spawnId, screenPosition, eventCamera);
            return;
        }

        if (timelineViewport != null &&
            RectTransformUtility.RectangleContainsScreenPoint(timelineViewport, screenPosition, eventCamera))
        {
            OpenTimelineContextMenu(screenPosition, eventCamera);
        }
    }

    private static bool WasRightMousePressedThisFrame(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    public void DeleteSelectedSpawn()
    {
        DeleteSelectedSpawns();
    }

    private void DeleteSelectedSpawns()
    {
        if (selectedSpawnIds.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        int removedCount = spawns.RemoveAll(spawn => selectedSpawnIds.Contains(spawn.Id));
        selectedSpawnIds.Clear();
        selectedSpawnId = -1;
        RebuildMarkers();
        SetStatus($"Removed {removedCount} marker{(removedCount == 1 ? string.Empty : "s")}.");
    }

    public void DeselectSpawnMarker()
    {
        if (selectedSpawnIds.Count == 0)
        {
            return;
        }

        selectedSpawnIds.Clear();
        selectedSpawnId = -1;
        RebuildMarkers();
        SetStatus("Selection cleared.");
    }

    public void SelectSpawnMarker(int spawnId)
    {
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        if (spawn == null)
        {
            selectedSpawnIds.Clear();
            selectedSpawnId = -1;
            return;
        }

        selectedSpawnIds.Clear();
        selectedSpawnIds.Add(spawnId);
        selectedSpawnId = spawnId;
        deleteMode = false;
        UpdateDeleteModeVisual();
        RebuildMarkers();
        SetStatus($"Selected {PrettyName(spawn.Enemy)} at {spawn.Time:0.0}s on lane {spawn.Lane}. Drag it or press Delete.");
    }

    public void OpenMarkerContextMenu(int spawnId, PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        OpenMarkerContextMenu(spawnId, eventData.position, eventData.pressEventCamera);
    }

    private void OpenMarkerContextMenu(int spawnId, Vector2 screenPosition, Camera eventCamera)
    {
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        RectTransform canvasRect = GetRootCanvasRect();
        if (spawn == null || canvasRect == null)
        {
            return;
        }

        if (!selectedSpawnIds.Contains(spawnId))
        {
            SelectSpawnMarker(spawnId);
        }

        CloseMarkerContextMenu();
        markerContextSpawnId = spawnId;

        const float menuWidth = 220f;
        const float menuHeight = 104f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            eventCamera,
            out Vector2 localPoint);

        GameObject menu = new("Marker Context Menu", typeof(RectTransform), typeof(Image));
        menu.transform.SetParent(canvasRect, false);
        markerContextMenu = menu;
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.SetAsLastSibling();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0f, 1f);
        menuRect.sizeDelta = new Vector2(menuWidth, menuHeight);
        menuRect.anchoredPosition = new Vector2(
            Mathf.Clamp(localPoint.x + 12f, canvasRect.rect.xMin, canvasRect.rect.xMax - menuWidth),
            Mathf.Clamp(localPoint.y - 12f, canvasRect.rect.yMin + menuHeight, canvasRect.rect.yMax));

        Image menuImage = menu.GetComponent<Image>();
        menuImage.color = new Color(0.98f, 0.98f, 0.96f, 1f);
        Outline menuOutline = menu.AddComponent<Outline>();
        menuOutline.effectColor = Color.black;
        menuOutline.effectDistance = new Vector2(2f, -2f);

        CreateContextText(menuRect, "COUNT", 24, TextAnchor.MiddleLeft, 12f, 6f, 64f, 36f);
        markerContextCountInput = CreateContextCountInput(menuRect, spawn.Count, 80f, 6f, 128f, 36f);
        markerContextCountInput.onEndEdit.AddListener(_ => ApplyMarkerContextCount());
        CreateContextButton(menuRect, "DELETE SELECTED", 22, 12f, 54f, 196f, 40f, DeleteSelectedMarkersFromContextMenu);
        RefreshContextMenuText(menu);
        SetStatus($"Set count for {selectedSpawnIds.Count} selected marker{(selectedSpawnIds.Count == 1 ? string.Empty : "s")}.");
    }

    private void ApplyMarkerContextCount()
    {
        if (markerContextSpawnId < 0 || markerContextCountInput == null ||
            !int.TryParse(markerContextCountInput.text, out int parsed))
        {
            return;
        }

        int count = Mathf.Clamp(parsed, 1, 999);
        List<LevelEditorSpawn> targets = spawns
            .Where(spawn => selectedSpawnIds.Contains(spawn.Id))
            .ToList();
        if (targets.Count == 0)
        {
            LevelEditorSpawn contextSpawn = FindSpawn(markerContextSpawnId);
            if (contextSpawn != null)
            {
                targets.Add(contextSpawn);
            }
        }

        if (targets.Count == 0 || targets.All(spawn => spawn.Count == count))
        {
            return;
        }

        PushUndoSnapshot();
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].Count = count;
        }

        RebuildMarkers();
        SetStatus($"Set {targets.Count} card{(targets.Count == 1 ? string.Empty : "s")} to {(count == 1 ? "1 enemy" : $"{count} enemies")}.");
    }

    private void DeleteSelectedMarkersFromContextMenu()
    {
        CloseMarkerContextMenu();
        DeleteSelectedSpawns();
    }

    private void OpenTimelineContextMenu(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        OpenTimelineContextMenu(eventData.position, eventData.pressEventCamera);
    }

    private void OpenTimelineContextMenu(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform canvasRect = GetRootCanvasRect();
        if (canvasRect == null)
        {
            return;
        }

        CloseMarkerContextMenu();
        const float menuWidth = 190f;
        const float menuHeight = 104f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            eventCamera,
            out Vector2 localPoint);

        GameObject menu = new("Timeline Context Menu", typeof(RectTransform), typeof(Image));
        menu.transform.SetParent(canvasRect, false);
        markerContextMenu = menu;
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.SetAsLastSibling();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0f, 1f);
        menuRect.sizeDelta = new Vector2(menuWidth, menuHeight);
        menuRect.anchoredPosition = new Vector2(
            Mathf.Clamp(localPoint.x + 12f, canvasRect.rect.xMin, canvasRect.rect.xMax - menuWidth),
            Mathf.Clamp(localPoint.y - 12f, canvasRect.rect.yMin + menuHeight, canvasRect.rect.yMax));

        Image menuImage = menu.GetComponent<Image>();
        menuImage.color = new Color(0.98f, 0.98f, 0.96f, 1f);
        Outline menuOutline = menu.AddComponent<Outline>();
        menuOutline.effectColor = Color.black;
        menuOutline.effectDistance = new Vector2(2f, -2f);
        CreateContextButton(menuRect, "UNDO", 22, 10f, 8f, 170f, 40f, UndoFromContextMenu);
        CreateContextButton(menuRect, "REDO", 22, 10f, 56f, 170f, 40f, RedoFromContextMenu);
        RefreshContextMenuText(menu);
    }

    private void UndoFromContextMenu()
    {
        CloseMarkerContextMenu();
        UndoLastChange();
    }

    private void RedoFromContextMenu()
    {
        CloseMarkerContextMenu();
        RedoLastChange();
    }

    private void CloseMarkerContextMenu()
    {
        markerContextCountInput = null;
        markerContextSpawnId = -1;
        if (markerContextMenu != null)
        {
            DestroyGeneratedObject(markerContextMenu);
            markerContextMenu = null;
        }
    }

    private Text CreateContextText(RectTransform parent, string value, int fontSize, TextAnchor alignment, float left, float top, float width, float height)
    {
        GameObject textObject = new("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        SetTopLeft(textRect, left, top, width, height);
        Text text = textObject.GetComponent<Text>();
        text.font = GetUiFont();
        // Runtime-created legacy Text defaults to the generic UI material in
        // this project, which does not render the hand-drawn font glyphs.
        // Use the font material explicitly so context-menu labels are visible.
        if (text.font != null && text.font.material != null)
        {
            text.material = text.font.material;
        }
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.black;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = Mathf.Max(10, fontSize);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    // Context menus are spawned during an input event. Mark their labels dirty
    // so Unity rebuilds them on the next UI pass without re-entering PlayerLoop.
    private static void RefreshContextMenuText(GameObject menu)
    {
        if (menu == null)
        {
            return;
        }

        Text[] labels = menu.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            label.SetAllDirty();
        }

    }

    private InputField CreateContextCountInput(RectTransform parent, int value, float left, float top, float width, float height)
    {
        GameObject inputObject = new("Count Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        SetTopLeft(inputRect, left, top, width, height);
        Image background = inputObject.GetComponent<Image>();
        background.color = Color.white;
        Outline outline = inputObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        Text text = CreateContextText(inputRect, value.ToString(), 28, TextAnchor.MiddleCenter, 6f, 2f, width - 12f, height - 4f);
        text.raycastTarget = false;
        InputField input = inputObject.GetComponent<InputField>();
        input.textComponent = text;
        input.text = value.ToString();
        input.contentType = InputField.ContentType.IntegerNumber;
        return input;
    }

    private Button CreateContextButton(RectTransform parent, string label, int fontSize, float left, float top, float width, float height, Action action)
    {
        GameObject buttonObject = new(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetTopLeft(buttonRect, left, top, width, height);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.88f, 1f);
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.88f, 1f);
        colors.highlightedColor = new Color(0.78f, 0.78f, 0.76f, 1f);
        colors.pressedColor = new Color(0.62f, 0.62f, 0.6f, 1f);
        button.colors = colors;
        CreateContextText(buttonRect, label, fontSize, TextAnchor.MiddleCenter, 4f, 1f, width - 8f, height - 2f);
        button.onClick.AddListener(() => action.Invoke());
        return button;
    }

    private void WireStaticUi()
    {
        MergeLevelNameUi();
        AddInputListener(levelIdInput, value =>
        {
            SetMergedLevelName(value);
            PushDataToInputs();
        });
        AddInputListener(displayNameInput, value =>
        {
            SetMergedLevelName(value);
            PushDataToInputs();
        });
        AddInputListener(outputFileInput, _ => PushDataToInputs());
        AddInputListener(startingCoinsInput, value =>
        {
            if (int.TryParse(value, out int parsed))
            {
                startingCoins = Mathf.Max(0, parsed);
            }

            PushDataToInputs();
        });
        AddInputListener(timelineDurationInput, value =>
        {
            if (float.TryParse(value, out float parsed))
            {
                timelineDuration = Mathf.Max(5f, parsed);
            }

            PushDataToInputs();
            RebuildTimeline();
            pendingTimelineLayoutRefresh = true;
        });

        EnsureExportButton();
        AddButtonListener(saveButton, SaveJson);
        AddButtonListener(loadButton, OpenLevelFilePanel);
        AddButtonListener(exportButton, ExportJson);
        AddButtonListener(testButton, TestPlay);
        AddButtonListener(backButton, () => SceneTransitionController.LoadScene(levelSelectSceneName));
        AddButtonListener(clearButton, ClearSpawns);

        LevelEditorTimelineClickArea clickArea = timelineArea != null
            ? timelineArea.GetComponent<LevelEditorTimelineClickArea>()
            : null;
        if (clickArea != null)
        {
            clickArea.SetController(this);
        }

        if (timelineScrollRect != null)
        {
            timelineScrollRect.onValueChanged.AddListener(_ => UpdateTimelineOverviewViewportIndicator());
        }
    }

    private void MergeLevelNameUi()
    {
        SetMergedLevelName(!string.IsNullOrWhiteSpace(displayName) ? displayName : levelId);
        RenameInputLabel(levelIdInput, "LEVEL NAME");
        SetInputAndLabelActive(displayNameInput, false);
    }

    private void SetMergedLevelName(string value)
    {
        string levelName = Clean(value, DefaultLevelName);
        levelId = levelName;
        displayName = levelName;
        string fileName = levelName;
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            fileName = fileName.Replace(invalidCharacters[i], '-');
        }

        fileName = fileName.Trim();
        outputFileName = EnsureJsonExtension(string.IsNullOrWhiteSpace(fileName) ? DefaultLevelName : fileName);
    }

    private static void RenameInputLabel(InputField input, string label)
    {
        if (input == null || input.transform.parent == null)
        {
            return;
        }

        string expectedName = input.gameObject.name.Replace("Input", "Input Label");
        Transform parent = input.transform.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!string.Equals(child.name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Text text = child.GetComponent<Text>();
            if (text != null)
            {
                text.text = label;
            }

            return;
        }
    }

    private static void SetInputAndLabelActive(InputField input, bool active)
    {
        if (input == null)
        {
            return;
        }

        input.gameObject.SetActive(active);
        if (input.transform.parent == null)
        {
            return;
        }

        string expectedName = input.gameObject.name.Replace("Input", "Input Label");
        Transform parent = input.transform.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                child.gameObject.SetActive(active);
                return;
            }
        }
    }

    private void PushDataToInputs()
    {
        SetInputValue(levelIdInput, levelId);
        SetInputValue(displayNameInput, levelId);
        SetInputValue(startingCoinsInput, startingCoins.ToString());
        SetInputValue(timelineDurationInput, Mathf.RoundToInt(TimelineDuration).ToString());
        SetInputValue(outputFileInput, outputFileName);
    }

    private void RebuildDynamicUi()
    {
        RebuildEnemyButtons();
        RebuildTowerButtons();
        RebuildTimeline();
        UpdateDeleteModeVisual();
    }

    private void RebuildEnemyButtons()
    {
        ClearGeneratedButtonsUnder(enemyListRoot);
        if (enemyListRoot == null)
        {
            return;
        }

        for (int i = 0; i < enemyIds.Count; i++)
        {
            string enemyId = enemyIds[i];
            Button button = CreateUiButton(enemyListRoot, PrettyName(enemyId), $"Enemy Button - {enemyId}");
            LevelEditorEnemyCardDrag drag = button.gameObject.GetComponent<LevelEditorEnemyCardDrag>();
            if (drag == null)
            {
                drag = button.gameObject.AddComponent<LevelEditorEnemyCardDrag>();
            }

            drag.Configure(this, enemyId);
            bool isSelected = string.Equals(enemyId, selectedEnemyId, StringComparison.OrdinalIgnoreCase);
            SetButtonColor(button, isSelected ? CardSelectedColor : CardNormalColor);
            SetButtonTextColor(button, Color.black);
            SetButtonTextSize(button, 24, 12, 26);
            button.onClick.AddListener(() =>
            {
                selectedEnemyId = enemyId;
                deleteMode = false;
                RebuildDynamicUi();
                SetStatus($"Selected enemy: {PrettyName(enemyId)}. Click the timeline to place it, or drag the card onto a lane.");
            });
        }
    }

    private void ClearEnemySelection()
    {
        if (string.IsNullOrWhiteSpace(selectedEnemyId))
        {
            return;
        }

        selectedEnemyId = null;
        RebuildEnemyButtons();
    }

    private void RebuildLaneButtons()
    {
        ClearGeneratedButtonsUnder(laneButtonRoot);
        if (laneButtonRoot == null)
        {
            return;
        }

        for (int lane = 1; lane <= laneCount; lane++)
        {
            int laneValue = lane;
            Button button = CreateUiButton(laneButtonRoot, laneValue.ToString(), $"Lane Button - {laneValue}");
            SetButtonColor(button, selectedLane == laneValue ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white);
            button.onClick.AddListener(() =>
            {
                selectedLane = laneValue;
                RebuildLaneButtons();
                SetStatus($"Selected lane: {laneValue}.");
            });
        }
    }

    private void RebuildTowerButtons()
    {
        ClearGeneratedButtonsUnder(towerListRoot);
        if (towerListRoot == null)
        {
            return;
        }

        for (int i = 0; i < towerNames.Count; i++)
        {
            string towerName = towerNames[i];
            bool allowed = IsTowerAllowed(towerName);
            Button button = CreateUiButton(
                towerListRoot,
                $"{(allowed ? "[✓]" : "[ ]")} {PrettyName(towerName)}",
                $"Tower Button - {towerName}");
            SetButtonColor(button, allowed ? CardNormalColor : CardDisabledColor);
            SetButtonTextColor(button, Color.black);
            SetButtonTextSize(button, 20, 10, 22);
            button.onClick.AddListener(() =>
            {
                towerAllowed[towerName] = !IsTowerAllowed(towerName);
                RebuildTowerButtons();
                SetStatus($"{PrettyName(towerName)} is now {(IsTowerAllowed(towerName) ? "allowed" : "blocked")}.");
            });
        }
    }

    private void RebuildTimeline()
    {
        ResizeTimelineContent();
        RebuildTimelineGuides();
        RebuildMarkers();
    }

    private void RebuildTimelineOverview()
    {
        ClearGeneratedOverviewDots();
        UpdateTimelineOverviewViewportIndicator();
        if (timelineOverviewDotRoot == null || spawns.Count == 0)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float width = timelineOverviewDotRoot.rect.width;
        float height = timelineOverviewDotRoot.rect.height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        const float dotSize = 9f;
        float leftPadding = dotSize * 0.5f;
        float usableWidth = Mathf.Max(0f, width - dotSize);
        float usableHeight = Mathf.Max(0f, height - dotSize);
        for (int i = 0; i < spawns.Count; i++)
        {
            LevelEditorSpawn spawn = spawns[i];
            GameObject dot = new("Timeline Overview Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(timelineOverviewDotRoot, false);
            generatedOverviewDots.Add(dot);

            RectTransform rect = dot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float timePercent = Mathf.Clamp01(spawn.Time / TimelineDuration);
            float lanePercent = (Mathf.Clamp(spawn.Lane, 1, laneCount) - 0.5f) / Mathf.Max(1, laneCount);
            float stackOffset = ((spawn.Id % 3) - 1) * 2f;
            rect.anchoredPosition = new Vector2(
                leftPadding + usableWidth * timePercent,
                -(leftPadding + usableHeight * lanePercent + stackOffset));
            rect.sizeDelta = new Vector2(dotSize, dotSize);

            Image image = dot.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }
    }

    private void UpdateTimelineOverviewViewportIndicator()
    {
        if (timelineOverviewViewportIndicator == null || timelineOverviewDotRoot == null ||
            timelineArea == null || timelineViewport == null || timelineScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float overviewWidth = timelineOverviewDotRoot.rect.width;
        float overviewHeight = timelineOverviewDotRoot.rect.height;
        float contentWidth = timelineArea.rect.width;
        float visibleWidth = timelineViewport.rect.width;
        if (overviewWidth <= 0f || overviewHeight <= 0f || contentWidth <= 0f || visibleWidth <= 0f)
        {
            return;
        }

        float visibleFraction = Mathf.Clamp01(visibleWidth / contentWidth);
        float indicatorWidth = Mathf.Clamp(overviewWidth * visibleFraction, 16f, overviewWidth);
        float horizontalRange = Mathf.Max(0f, overviewWidth - indicatorWidth);
        float left = horizontalRange * Mathf.Clamp01(timelineScrollRect.horizontalNormalizedPosition);
        SetTopLeft(timelineOverviewViewportIndicator, 8f + left, 4f, indicatorWidth, overviewHeight);
    }

    private void ResizeTimelineContent()
    {
        if (timelineArea == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float viewportWidth = timelineViewport != null ? timelineViewport.rect.width : 0f;
        float viewportHeight = timelineViewport != null ? timelineViewport.rect.height : 0f;
        float width = Mathf.Max(viewportWidth, TimelineDuration * timelinePixelsPerSecond);
        if (viewportHeight > timelineHeaderHeight && laneCount > 0)
        {
            timelineLaneHeight = Mathf.Max(timelineLaneHeight, (viewportHeight - timelineHeaderHeight) / laneCount);
        }

        float height = Mathf.Max(viewportHeight, timelineHeaderHeight + laneCount * timelineLaneHeight);
        timelineArea.anchorMin = new Vector2(0f, 1f);
        timelineArea.anchorMax = new Vector2(0f, 1f);
        timelineArea.pivot = new Vector2(0f, 1f);
        timelineArea.sizeDelta = new Vector2(width, height);

        if (markerRoot != null)
        {
            markerRoot.anchorMin = Vector2.zero;
            markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = Vector2.zero;
            markerRoot.offsetMax = Vector2.zero;
        }

        if (timelineGuideRoot != null)
        {
            timelineGuideRoot.anchorMin = Vector2.zero;
            timelineGuideRoot.anchorMax = Vector2.one;
            timelineGuideRoot.offsetMin = Vector2.zero;
            timelineGuideRoot.offsetMax = Vector2.zero;
        }

        if (timelineScrollRect != null)
        {
            timelineScrollRect.horizontal = true;
            timelineScrollRect.vertical = false;
        }
    }

    private void RebuildTimelineGuides()
    {
        if (timelineGuideRoot == null || timelineArea == null)
        {
            return;
        }

        for (int i = timelineGuideRoot.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(timelineGuideRoot.GetChild(i).gameObject);
        }

        float width = timelineArea.rect.width;
        float height = timelineArea.rect.height;
        CreateGuideImage("Header Background", timelineGuideRoot, new Color(0.82f, 0.82f, 0.78f, 1f), 0f, 0f, width, timelineHeaderHeight);

        for (int lane = 1; lane <= laneCount; lane++)
        {
            float top = timelineHeaderHeight + (lane - 1) * timelineLaneHeight;
            Color laneColor = lane % 2 == 0 ? new Color(0.9f, 0.9f, 0.86f, 1f) : new Color(0.84f, 0.84f, 0.8f, 1f);
            CreateGuideImage($"Lane {lane} Background", timelineGuideRoot, laneColor, 0f, top, width, timelineLaneHeight);
            CreateGuideImage($"Lane {lane} Bottom Line", timelineGuideRoot, new Color(0f, 0f, 0f, 0.28f), 0f, top + timelineLaneHeight - 1f, width, 1.5f);
        }

        int majorStep = TimelineDuration <= 45f ? 5 : 10;
        for (int second = 0; second <= Mathf.CeilToInt(TimelineDuration); second++)
        {
            bool major = second % majorStep == 0;
            bool minor = second % 1 == 0;
            if (!major && !minor)
            {
                continue;
            }

            float x = second * timelinePixelsPerSecond;
            if (x > width)
            {
                continue;
            }

            CreateGuideImage($"Tick {second}s", timelineGuideRoot, major ? new Color(0f, 0f, 0f, 0.36f) : new Color(0f, 0f, 0f, 0.12f), x, 0f, major ? 2f : 1f, height);
        }
    }

    private void RebuildMarkers()
    {
        ClearGeneratedMarkers();
        if (markerRoot == null || timelineArea == null)
        {
            return;
        }

        spawns.Sort((a, b) => a.Time.CompareTo(b.Time));
        for (int i = 0; i < spawns.Count; i++)
        {
            LevelEditorSpawn spawn = spawns[i];
            int spawnId = spawn.Id;
            GameObject markerObject = Instantiate(markerPrefab != null ? markerPrefab : buttonPrefab, markerRoot);
            markerObject.name = $"Spawn Marker - {spawn.Enemy} {spawn.Time:0.0}s L{spawn.Lane}";
            markerObject.SetActive(true);
            generatedMarkers.Add(markerObject);

            RectTransform rect = markerObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                ApplyMarkerLayout(rect, spawn);
                generatedMarkerRects[spawnId] = rect;
            }

            Text label = markerObject.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                ConfigureMarkerLabel(label, spawn);
            }

            Button button = markerObject.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = selectedSpawnIds.Contains(spawn.Id) ? CardSelectedColor : CardNormalColor;
                colors.highlightedColor = new Color(0.9f, 0.9f, 0.88f, 1f);
                colors.pressedColor = new Color(0.72f, 0.72f, 0.7f, 1f);
                colors.selectedColor = CardSelectedColor;
                colors.disabledColor = CardDisabledColor;
                button.colors = colors;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (deleteMode)
                    {
                        RemoveSpawn(spawnId);
                    }
                    else
                    {
                        SelectSpawnMarker(spawnId);
                    }
                });
            }

            Image image = markerObject.GetComponent<Image>();
            if (image != null)
            {
                image.color = selectedSpawnIds.Contains(spawn.Id) ? CardSelectedColor : CardNormalColor;
            }

            Outline outline = markerObject.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = Color.black;
                outline.effectDistance = selectedSpawnIds.Contains(spawn.Id) ? new Vector2(4f, -4f) : new Vector2(1.5f, -1.5f);
            }

            LevelEditorSpawnMarkerDrag drag = markerObject.GetComponent<LevelEditorSpawnMarkerDrag>();
            if (drag == null)
            {
                drag = markerObject.AddComponent<LevelEditorSpawnMarkerDrag>();
            }

            drag.Configure(this, spawnId);

            if (selectedSpawnIds.Contains(spawn.Id))
            {
                markerObject.transform.SetAsLastSibling();
            }
        }

        RebuildTimelineOverview();
    }

    public void BeginSpawnMarkerDrag(int spawnId)
    {
        if (FindSpawn(spawnId) == null)
        {
            return;
        }

        if (!selectedSpawnIds.Contains(spawnId))
        {
            SelectSpawnMarker(spawnId);
            SetStatus("Marker selected. Drag it again to move it.");
            return;
        }

        draggingSpawnMarker = true;
        deleteMode = false;
        UpdateDeleteModeVisual();
        dragStartSnapshot = CaptureSpawnSnapshot();
        dragStartPositions.Clear();
        foreach (int selectedId in selectedSpawnIds)
        {
            LevelEditorSpawn selected = FindSpawn(selectedId);
            if (selected != null)
            {
                dragStartPositions[selectedId] = new Vector2(selected.Time, selected.Lane);
            }
        }
    }

    public void DragSpawnMarker(int spawnId, PointerEventData eventData)
    {
        if (!draggingSpawnMarker || eventData == null)
        {
            return;
        }

        if (TryGetTimelinePlacementFromScreenPoint(eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            MoveSelectedSpawns(spawnId, time, lane);
        }
    }

    public void EndSpawnMarkerDrag(int spawnId)
    {
        bool wasDragging = draggingSpawnMarker;
        draggingSpawnMarker = false;
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        if (spawn != null)
        {
            if (wasDragging)
            {
                if (dragStartSnapshot != null && !SpawnSnapshotsMatch(dragStartSnapshot, spawns))
                {
                    PushUndoSnapshot(dragStartSnapshot);
                }

                dragStartSnapshot = null;
                dragStartPositions.Clear();
                RebuildMarkers();
                SetStatus($"Moved {selectedSpawnIds.Count} selected marker{(selectedSpawnIds.Count == 1 ? string.Empty : "s")}.");
            }
            else
            {
                SetStatus($"Selected {PrettyName(spawn.Enemy)}. Drag again to move it.");
            }
        }
    }

    private void MoveSpawn(int spawnId, float time, int lane, bool rebuild)
    {
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        if (spawn == null)
        {
            return;
        }

        spawn.Time = Mathf.Clamp(time, 0f, TimelineDuration);
        spawn.Lane = Mathf.Clamp(lane, 1, laneCount);
        selectedSpawnId = spawnId;
        if (rebuild)
        {
            RebuildMarkers();
        }
        else
        {
            UpdateMarkerVisual(spawn);
        }
    }

    private void MoveSelectedSpawns(int anchorSpawnId, float time, int lane)
    {
        if (!dragStartPositions.TryGetValue(anchorSpawnId, out Vector2 anchorStart))
        {
            MoveSpawn(anchorSpawnId, time, lane, false);
            return;
        }

        float timeDelta = time - anchorStart.x;
        int laneDelta = lane - Mathf.RoundToInt(anchorStart.y);
        foreach (KeyValuePair<int, Vector2> pair in dragStartPositions)
        {
            LevelEditorSpawn spawn = FindSpawn(pair.Key);
            if (spawn == null)
            {
                continue;
            }

            spawn.Time = Mathf.Clamp(pair.Value.x + timeDelta, 0f, TimelineDuration);
            spawn.Lane = Mathf.Clamp(Mathf.RoundToInt(pair.Value.y) + laneDelta, 1, laneCount);
        }

        selectedSpawnId = anchorSpawnId;
        UpdateMarkerVisual(FindSpawn(anchorSpawnId));
    }

    private void UpdateMarkerVisual(LevelEditorSpawn spawn)
    {
        if (spawn == null)
        {
            return;
        }

        spawns.Sort((a, b) => a.Time.CompareTo(b.Time));
        for (int i = 0; i < spawns.Count; i++)
        {
            LevelEditorSpawn current = spawns[i];
            if (!generatedMarkerRects.TryGetValue(current.Id, out RectTransform rect) || rect == null)
            {
                RebuildMarkers();
                return;
            }

            ApplyMarkerLayout(rect, current);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                ConfigureMarkerLabel(label, current);
            }
        }

        RebuildTimelineOverview();
    }

    private void ApplyMarkerLayout(RectTransform rect, LevelEditorSpawn spawn)
    {
        GetMarkerStackInfo(spawn, out int stackIndex, out int stackCount);
        GetMarkerLayout(spawn.Time, spawn.Lane, stackIndex, stackCount, out float x, out float y, out float width, out float height);
        SetTopLeft(rect, x, y, width, height);
    }

    private void GetPreviewMarkerLayout(float time, int lane, out float x, out float y, out float width, out float height)
    {
        int normalizedLane = Mathf.Clamp(lane, 1, laneCount);
        int timeCell = Mathf.FloorToInt(Mathf.Clamp(time, 0f, TimelineDuration));
        int existingCount = 0;
        for (int i = 0; i < spawns.Count; i++)
        {
            LevelEditorSpawn candidate = spawns[i];
            if (Mathf.Clamp(candidate.Lane, 1, laneCount) == normalizedLane &&
                Mathf.FloorToInt(Mathf.Clamp(candidate.Time, 0f, TimelineDuration)) == timeCell)
            {
                existingCount++;
            }
        }

        // The preview is the next card in this stack, which is exactly where
        // the card will end up after the player releases the pointer.
        GetMarkerLayout(time, normalizedLane, existingCount, existingCount + 1, out x, out y, out width, out height);
    }

    private void GetMarkerLayout(float time, int lane, int stackIndex, int stackCount, out float x, out float y, out float width, out float height)
    {
        x = Mathf.Clamp(time, 0f, TimelineDuration) * timelinePixelsPerSecond + 6f;
        y = timelineHeaderHeight + (Mathf.Clamp(lane, 1, laneCount) - 1) * timelineLaneHeight + 8f;
        width = TimelineMarkerBaseWidth;
        height = timelineLaneHeight - 16f;
        if (stackCount > 1)
        {
            int shrinkSteps = Mathf.Min(stackCount - 1, 4);
            width = Mathf.Max(82f, TimelineMarkerBaseWidth - shrinkSteps * 10f);
            height = Mathf.Max(38f, height - shrinkSteps * 5f);
            x += stackIndex * 8f;
            y += stackIndex * 5f;
        }

        height = Mathf.Max(38f, height);
    }

    private void GetMarkerStackInfo(LevelEditorSpawn target, out int stackIndex, out int stackCount)
    {
        stackIndex = 0;
        stackCount = 0;
        int lane = Mathf.Clamp(target.Lane, 1, laneCount);
        int timeCell = Mathf.FloorToInt(Mathf.Clamp(target.Time, 0f, TimelineDuration));
        for (int i = 0; i < spawns.Count; i++)
        {
            LevelEditorSpawn candidate = spawns[i];
            if (Mathf.Clamp(candidate.Lane, 1, laneCount) != lane ||
                Mathf.FloorToInt(Mathf.Clamp(candidate.Time, 0f, TimelineDuration)) != timeCell)
            {
                continue;
            }

            if (candidate.Id == target.Id)
            {
                stackIndex = stackCount;
            }

            stackCount++;
        }
    }

    private void ConfigureMarkerLabel(Text label, LevelEditorSpawn spawn)
    {
        label.font = GetUiFont();
        string countSuffix = spawn.Count > 1 ? $"  x{spawn.Count}" : string.Empty;
        label.text = $"{PrettyName(spawn.Enemy)}{countSuffix}\n{spawn.Time:0.0}s  L{spawn.Lane}";
        ConfigureTextToFit(label, 10, 28, 24);
        label.color = Color.black;
    }

    private void RemoveSpawn(int spawnId)
    {
        int index = spawns.FindIndex(spawn => spawn.Id == spawnId);
        if (index < 0)
        {
            return;
        }

        LevelEditorSpawn removed = spawns[index];
        PushUndoSnapshot();
        spawns.RemoveAt(index);
        selectedSpawnIds.Remove(spawnId);
        if (selectedSpawnId == spawnId)
        {
            selectedSpawnId = -1;
        }

        RebuildMarkers();
        SetStatus($"Removed {PrettyName(removed.Enemy)}.");
    }

    private LevelEditorSpawn FindSpawn(int spawnId)
    {
        return spawns.FirstOrDefault(spawn => spawn.Id == spawnId);
    }

    public void BeginEnemyCardDrag(string enemyId, PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            return;
        }

        selectedEnemyId = enemyId;
        deleteMode = false;
        UpdateDeleteModeVisual();
        CreateDragGhost(enemyId);
        UpdateEnemyCardDrag(eventData);
        SetStatus($"Dragging {PrettyName(enemyId)}. Drop it on a timeline lane.");
    }

    public void UpdateEnemyCardDrag(PointerEventData eventData)
    {
        if (dragGhost == null || eventData == null)
        {
            return;
        }

        RectTransform ghostRect = dragGhost.transform as RectTransform;
        RectTransform canvasRect = GetRootCanvasRect();
        if (ghostRect == null || canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);
        ghostRect.anchoredPosition = localPoint + new Vector2(18f, -18f);

        UpdateTimelinePreview(selectedEnemyId, eventData);
    }

    public void EndEnemyCardDrag(string enemyId, PointerEventData eventData)
    {
        DestroyDragGhost();
        DestroyTimelinePreview();
        if (eventData == null)
        {
            return;
        }

        if (TryAddSpawnAtScreenPoint(enemyId, eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            ClearEnemySelection();
            SetStatus($"Added {PrettyName(enemyId)} at {time:0.0}s on lane {lane}.");
        }
        else
        {
            SetStatus("Drop enemy cards onto the timeline lanes.");
        }
    }

    public void HandleTimelineClick(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenTimelineContextMenu(eventData);
            eventData.Use();
            return;
        }

        CloseMarkerContextMenu();

        if (!deleteMode && !string.IsNullOrWhiteSpace(selectedEnemyId) &&
            TryAddSpawnAtScreenPoint(selectedEnemyId, eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            string addedEnemyId = selectedEnemyId;
            ClearEnemySelection();
            SetStatus($"Added {PrettyName(addedEnemyId)} at {time:0.0}s on lane {lane}.");
            eventData.Use();
            return;
        }

        DeselectSpawnMarker();
    }

    public void BeginTimelineBoxSelection(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
            !string.IsNullOrWhiteSpace(selectedEnemyId) || timelineViewport == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(timelineViewport, eventData.position, eventData.pressEventCamera) ||
            !TryGetTimelineTopLeftPoint(eventData.position, eventData.pressEventCamera, out boxSelectionStart))
        {
            return;
        }

        boxSelecting = true;
        EnsureBoxSelectionRect();
        UpdateBoxSelectionRect(boxSelectionStart);
        boxSelectionRect.gameObject.SetActive(true);
    }

    public void UpdateTimelineBoxSelection(PointerEventData eventData)
    {
        if (!boxSelecting || eventData == null ||
            !TryGetTimelineTopLeftPoint(eventData.position, eventData.pressEventCamera, out Vector2 currentPoint))
        {
            return;
        }

        UpdateBoxSelectionRect(currentPoint);
    }

    public void EndTimelineBoxSelection(PointerEventData eventData)
    {
        if (!boxSelecting)
        {
            return;
        }

        boxSelecting = false;
        if (boxSelectionRect != null)
        {
            boxSelectionRect.gameObject.SetActive(false);
        }

        if (eventData == null ||
            !TryGetTimelineTopLeftPoint(eventData.position, eventData.pressEventCamera, out Vector2 endPoint))
        {
            return;
        }

        Rect selection = MakeTopLeftRect(boxSelectionStart, endPoint);
        if (selection.width < 8f && selection.height < 8f)
        {
            return;
        }

        selectedSpawnIds.Clear();
        foreach (KeyValuePair<int, RectTransform> pair in generatedMarkerRects)
        {
            RectTransform marker = pair.Value;
            if (marker == null)
            {
                continue;
            }

            float left = marker.anchoredPosition.x;
            float top = -marker.anchoredPosition.y;
            Rect markerRect = new(left, top, marker.rect.width, marker.rect.height);
            if (selection.Overlaps(markerRect, true))
            {
                selectedSpawnIds.Add(pair.Key);
            }
        }

        selectedSpawnId = selectedSpawnIds.Count > 0 ? selectedSpawnIds.First() : -1;
        RebuildMarkers();
        SetStatus(selectedSpawnIds.Count > 0
            ? $"Selected {selectedSpawnIds.Count} marker{(selectedSpawnIds.Count == 1 ? string.Empty : "s")}. Drag one to move the group."
            : "No markers inside selection box.");
    }

    private void EnsureBoxSelectionRect()
    {
        if (boxSelectionRect != null || timelineArea == null)
        {
            return;
        }

        GameObject box = new("Timeline Box Selection", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(timelineArea, false);
        boxSelectionRect = box.GetComponent<RectTransform>();
        boxSelectionRect.anchorMin = new Vector2(0f, 1f);
        boxSelectionRect.anchorMax = new Vector2(0f, 1f);
        boxSelectionRect.pivot = new Vector2(0f, 1f);
        if (markerRoot != null)
        {
            boxSelectionRect.SetSiblingIndex(markerRoot.GetSiblingIndex());
        }

        Image image = box.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.08f);
        image.raycastTarget = false;
        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        box.SetActive(false);
    }

    private void UpdateBoxSelectionRect(Vector2 currentPoint)
    {
        if (boxSelectionRect == null)
        {
            return;
        }

        Rect selection = MakeTopLeftRect(boxSelectionStart, currentPoint);
        SetTopLeft(boxSelectionRect, selection.xMin, selection.yMin, selection.width, selection.height);
    }

    private static Rect MakeTopLeftRect(Vector2 first, Vector2 second)
    {
        return Rect.MinMaxRect(
            Mathf.Min(first.x, second.x),
            Mathf.Min(first.y, second.y),
            Mathf.Max(first.x, second.x),
            Mathf.Max(first.y, second.y));
    }

    private bool TryGetTimelineTopLeftPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 point)
    {
        point = Vector2.zero;
        if (timelineArea == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineArea, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect areaRect = timelineArea.rect;
        point = new Vector2(
            Mathf.Clamp(localPoint.x - areaRect.xMin, 0f, areaRect.width),
            Mathf.Clamp(areaRect.yMax - localPoint.y, 0f, areaRect.height));
        return true;
    }

    private bool TryAddSpawnAtScreenPoint(string enemyId, Vector2 screenPoint, Camera eventCamera, out float time, out int lane)
    {
        time = 0f;
        lane = 1;
        if (string.IsNullOrWhiteSpace(enemyId) ||
            !TryGetTimelinePlacementFromScreenPoint(screenPoint, eventCamera, out time, out lane))
        {
            return false;
        }

        PushUndoSnapshot();
        LevelEditorSpawn spawn = new(nextSpawnId++, time, enemyId, lane);
        spawns.Add(spawn);
        selectedSpawnIds.Clear();
        selectedSpawnIds.Add(spawn.Id);
        selectedSpawnId = spawn.Id;
        RebuildMarkers();
        return true;
    }

    private bool TryGetTimelinePlacementFromScreenPoint(Vector2 screenPoint, Camera eventCamera, out float time, out int lane)
    {
        time = 0f;
        lane = 1;
        if (timelineViewport == null || timelineArea == null)
        {
            return false;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(timelineViewport, screenPoint, eventCamera))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineArea, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = timelineArea.rect;
        float x = localPoint.x - rect.xMin;
        float yFromTop = rect.yMax - localPoint.y;
        if (x < 0f || x > rect.width || yFromTop < timelineHeaderHeight || yFromTop > rect.height)
        {
            return false;
        }

        time = Mathf.Round(Mathf.Clamp(x / timelinePixelsPerSecond, 0f, TimelineDuration) * 2f) * 0.5f;
        lane = Mathf.Clamp(Mathf.FloorToInt((yFromTop - timelineHeaderHeight) / timelineLaneHeight) + 1, 1, laneCount);
        return true;
    }

    public void HandleTimelineScroll(PointerEventData eventData)
    {
        if (eventData == null || timelineScrollRect == null)
        {
            return;
        }

        if (IsCtrlHeld())
        {
            ZoomTimelineAtScreenPoint(eventData.position, eventData.scrollDelta.y, eventData.pressEventCamera);
            eventData.Use();
            return;
        }

        if (Mathf.Abs(eventData.scrollDelta.y) > 0.001f)
        {
            timelineScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                timelineScrollRect.horizontalNormalizedPosition - eventData.scrollDelta.y * 0.035f);
            UpdateTimelineOverviewViewportIndicator();
            eventData.Use();
        }
    }

    public void BeginTimelinePan(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Middle ||
            timelineScrollRect == null || timelineArea == null || timelineViewport == null)
        {
            return;
        }

        timelinePanning = true;
        timelinePanStartPointer = eventData.position;
        timelinePanStartPosition = timelineScrollRect.horizontalNormalizedPosition;
    }

    public void UpdateTimelinePan(PointerEventData eventData)
    {
        if (!timelinePanning || eventData == null || timelineScrollRect == null ||
            timelineArea == null || timelineViewport == null)
        {
            return;
        }

        float scrollableWidth = Mathf.Max(1f, timelineArea.rect.width - timelineViewport.rect.width);
        float horizontalDelta = eventData.position.x - timelinePanStartPointer.x;
        timelineScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
            timelinePanStartPosition - horizontalDelta / scrollableWidth);
        UpdateTimelineOverviewViewportIndicator();
    }

    public void EndTimelinePan(PointerEventData eventData)
    {
        timelinePanning = false;
    }

    public void ZoomTimelineAtScreenPoint(Vector2 screenPoint, float scrollDelta, Camera eventCamera)
    {
        if (Mathf.Abs(scrollDelta) < 0.001f || timelineArea == null)
        {
            return;
        }

        float oldPixelsPerSecond = timelinePixelsPerSecond;
        float timeAtPointer = 0f;
        bool hasPointerTime = TryGetTimelineTimeFromScreenPoint(screenPoint, eventCamera, out timeAtPointer);
        float oldNormalized = timelineScrollRect != null ? timelineScrollRect.horizontalNormalizedPosition : 0f;

        float zoomFactor = scrollDelta > 0f ? 1.12f : 1f / 1.12f;
        timelinePixelsPerSecond = Mathf.Clamp(timelinePixelsPerSecond * zoomFactor, 30f, 260f);
        if (Mathf.Approximately(oldPixelsPerSecond, timelinePixelsPerSecond))
        {
            return;
        }

        RebuildTimeline();
        Canvas.ForceUpdateCanvases();

        if (timelineScrollRect == null)
        {
            return;
        }

        if (hasPointerTime && timelineViewport != null && timelineArea != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineViewport, screenPoint, eventCamera, out Vector2 viewportLocalPoint);
            float viewportX = viewportLocalPoint.x - timelineViewport.rect.xMin;
            float targetContentX = -(timeAtPointer * timelinePixelsPerSecond - viewportX);
            float minX = Mathf.Min(0f, timelineViewport.rect.width - timelineArea.rect.width);
            targetContentX = Mathf.Clamp(targetContentX, minX, 0f);
            timelineArea.anchoredPosition = new Vector2(targetContentX, timelineArea.anchoredPosition.y);
        }
        else
        {
            timelineScrollRect.horizontalNormalizedPosition = oldNormalized;
        }

        SetStatus($"Timeline zoom: {timelinePixelsPerSecond:0}px/s. Ctrl + mouse wheel to zoom.");
    }

    private bool TryGetTimelineTimeFromScreenPoint(Vector2 screenPoint, Camera eventCamera, out float time)
    {
        time = 0f;
        if (timelineViewport == null || timelineArea == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(timelineViewport, screenPoint, eventCamera) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineArea, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = timelineArea.rect;
        float x = Mathf.Clamp(localPoint.x - rect.xMin, 0f, rect.width);
        time = Mathf.Clamp(x / timelinePixelsPerSecond, 0f, TimelineDuration);
        return true;
    }

    private Image CreateGuideImage(string objectName, RectTransform parent, Color color, float left, float top, float width, float height)
    {
        GameObject imageObject = new(objectName);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        SetTopLeft(rect, left, top, width, height);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void CreateDragGhost(string enemyId)
    {
        DestroyDragGhost();
        RectTransform canvasRect = GetRootCanvasRect();
        if (canvasRect == null)
        {
            return;
        }

        dragGhost = new GameObject($"Dragging Enemy - {enemyId}");
        dragGhost.transform.SetParent(canvasRect, false);
        RectTransform rect = dragGhost.AddComponent<RectTransform>();
        SetTopLeft(rect, 0f, 0f, 180f, 70f);
        Image image = dragGhost.AddComponent<Image>();
        image.color = new Color(0.98f, 0.98f, 0.96f, 0.96f);
        image.raycastTarget = false;
        Outline outline = dragGhost.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject textObject = new("Text");
        textObject.transform.SetParent(dragGhost.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        Text text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.text = PrettyName(enemyId);
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.raycastTarget = false;
        ConfigureTextToFit(text, 12, 30, 26);

        dragGhost.transform.SetAsLastSibling();
    }

    private void DestroyDragGhost()
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }
    }

    private void UpdateTimelinePreview(string enemyId, PointerEventData eventData)
    {
        if (eventData == null || markerRoot == null ||
            !TryGetTimelinePlacementFromScreenPoint(eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            HideTimelinePreview();
            return;
        }

        EnsureTimelinePreview(enemyId);
        if (timelinePreview == null)
        {
            return;
        }

        timelinePreview.SetActive(true);
        RectTransform rect = timelinePreview.transform as RectTransform;
        if (rect != null)
        {
            GetPreviewMarkerLayout(time, lane, out float x, out float y, out float width, out float height);
            SetTopLeft(rect, x, y, width, height);
        }

        Text text = timelinePreview.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            ConfigureMarkerLabel(text, new LevelEditorSpawn(-1, time, enemyId, lane));
        }

        timelinePreview.transform.SetAsLastSibling();
    }

    private void EnsureTimelinePreview(string enemyId)
    {
        if (timelinePreview != null)
        {
            return;
        }

        timelinePreview = new GameObject("Timeline Enemy Preview");
        timelinePreview.transform.SetParent(markerRoot, false);
        RectTransform rect = timelinePreview.AddComponent<RectTransform>();
        SetTopLeft(rect, 0f, 0f, TimelineMarkerBaseWidth, Mathf.Max(38f, timelineLaneHeight - 16f));
        Image image = timelinePreview.AddComponent<Image>();
        image.color = new Color(0.98f, 0.98f, 0.96f, 0.72f);
        image.raycastTarget = false;
        Outline outline = timelinePreview.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        GameObject textObject = new("Text");
        textObject.transform.SetParent(timelinePreview.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);
        Text text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.text = PrettyName(enemyId);
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.raycastTarget = false;
        ConfigureTextToFit(text, 10, 28, 24);
    }

    private void HideTimelinePreview()
    {
        if (timelinePreview != null)
        {
            timelinePreview.SetActive(false);
        }
    }

    private void DestroyTimelinePreview()
    {
        if (timelinePreview != null)
        {
            Destroy(timelinePreview);
            timelinePreview = null;
        }
    }

    private RectTransform GetRootCanvasRect()
    {
        if (rootCanvas == null || rootCanvas.gameObject.scene != gameObject.scene)
        {
            rootCanvas = ResolveEditorCanvas();
        }

        return rootCanvas != null ? rootCanvas.transform as RectTransform : null;
    }

    private Canvas ResolveEditorCanvas()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (IsSceneCanvas(parentCanvas))
        {
            return parentCanvas;
        }

        if (timelineArea != null)
        {
            Canvas timelineCanvas = timelineArea.GetComponentInParent<Canvas>();
            if (IsSceneCanvas(timelineCanvas))
            {
                return timelineCanvas;
            }
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas firstSceneCanvas = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!IsSceneCanvas(canvas))
            {
                continue;
            }

            if (string.Equals(canvas.name, "Level Editor Canvas", StringComparison.OrdinalIgnoreCase))
            {
                return canvas;
            }

            firstSceneCanvas ??= canvas;
        }

        return firstSceneCanvas;
    }

    private bool IsSceneCanvas(Canvas canvas)
    {
        return canvas != null && canvas.gameObject.scene == gameObject.scene;
    }

    private Camera GetCanvasEventCamera()
    {
        if (rootCanvas == null)
        {
            GetRootCanvasRect();
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
    }

    private bool IsPointerInsideContextMenu(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform menuRect = markerContextMenu != null
            ? markerContextMenu.transform as RectTransform
            : null;
        return menuRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(menuRect, screenPosition, eventCamera);
    }

    private bool TryGetSpawnMarkerAtScreenPoint(Vector2 screenPosition, Camera eventCamera, out int spawnId)
    {
        spawnId = -1;
        RectTransform bestRect = null;
        int bestSiblingIndex = int.MinValue;

        foreach (KeyValuePair<int, RectTransform> pair in generatedMarkerRects)
        {
            RectTransform markerRect = pair.Value;
            if (markerRect == null ||
                !markerRect.gameObject.activeInHierarchy ||
                !RectTransformUtility.RectangleContainsScreenPoint(markerRect, screenPosition, eventCamera))
            {
                continue;
            }

            int siblingIndex = markerRect.GetSiblingIndex();
            if (bestRect != null && siblingIndex < bestSiblingIndex)
            {
                continue;
            }

            bestRect = markerRect;
            bestSiblingIndex = siblingIndex;
            spawnId = pair.Key;
        }

        return bestRect != null;
    }

    private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private Button CreateUiButton(RectTransform parent, string label, string objectName)
    {
        GameObject source = buttonPrefab != null ? buttonPrefab : CreateFallbackButtonPrefab();
        GameObject buttonObject = Instantiate(source, parent);
        buttonObject.name = objectName;
        buttonObject.SetActive(true);
        generatedButtons.Add(buttonObject);

        Text text = buttonObject.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.font = GetUiFont();
            text.text = label;
            ConfigureTextToFit(text, 10, 30, 24);
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        button.onClick.RemoveAllListeners();
        return button;
    }

    private GameObject CreateFallbackButtonPrefab()
    {
        GameObject buttonObject = new("Generated Button Template");
        buttonObject.SetActive(false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 44f);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;
        buttonObject.AddComponent<Button>();

        GameObject labelObject = new("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        Text text = labelObject.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.font = GetUiFont();
        text.fontSize = 30;
        ConfigureTextToFit(text, 10, 30, 24);
        buttonPrefab = buttonObject;
        return buttonObject;
    }

    private Font GetUiFont()
    {
        if (uiFont != null)
        {
            return uiFont;
        }

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallback == null)
        {
            fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return fallback;
    }

    private void EnsureExportButton()
    {
        if (exportButton != null || loadButton == null)
        {
            return;
        }

        GameObject exportObject = Instantiate(loadButton.gameObject, loadButton.transform.parent);
        exportObject.name = "Export Button";
        exportObject.SetActive(true);
        exportButton = exportObject.GetComponent<Button>();
        if (exportButton == null)
        {
            exportButton = exportObject.AddComponent<Button>();
        }

        exportButton.onClick.RemoveAllListeners();
        Text label = exportObject.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "Export";
            label.font = GetUiFont();
            ConfigureTextToFit(label, 10, 30, 24);
        }
    }

    private void OpenLevelFilePanel()
    {
        PullDataFromInputs();
        EnsureLevelFilePanel();
        if (levelFilePanel == null)
        {
            SetStatus("Load panel could not be created.");
            return;
        }

        PopulateLocalLevelList();
        levelFilePanel.SetActive(true);
        levelFilePanel.transform.SetAsLastSibling();
    }

    private void CloseLevelFilePanel()
    {
        if (levelFilePanel != null)
        {
            levelFilePanel.SetActive(false);
        }
    }

    private void EnsureLevelFilePanel()
    {
        if (levelFilePanel != null)
        {
            return;
        }

        RectTransform canvasRect = GetRootCanvasRect();
        if (canvasRect == null)
        {
            return;
        }

        levelFilePanel = new GameObject("Level File Panel", typeof(RectTransform), typeof(Image));
        levelFilePanel.transform.SetParent(canvasRect, false);
        RectTransform panelRect = levelFilePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image overlay = levelFilePanel.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject windowObject = new("Window", typeof(RectTransform), typeof(Image));
        windowObject.transform.SetParent(levelFilePanel.transform, false);
        RectTransform windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(780f, 600f);
        Image windowImage = windowObject.GetComponent<Image>();
        windowImage.color = new Color(0.96f, 0.96f, 0.93f, 1f);
        Outline outline = windowObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        CreateContextText(windowRect, "Load Level", 40, TextAnchor.MiddleLeft, 30f, 16f, 420f, 52f);
        CreateContextButton(windowRect, "X", 26, 724f, 20f, 38f, 38f, CloseLevelFilePanel);

        CreateContextText(windowRect, "Local Saves", 28, TextAnchor.MiddleLeft, 32f, 84f, 280f, 38f);
        CreateContextText(windowRect, Application.persistentDataPath, 16, TextAnchor.UpperLeft, 32f, 122f, 710f, 40f);

        GameObject scrollObject = new("Local Level Scroll View", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        scrollObject.transform.SetParent(windowRect, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetTopLeft(scrollRectTransform, 32f, 170f, 716f, 340f);
        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.01f);
        scrollBackground.raycastTarget = true;

        GameObject listObject = new("Local Level List", typeof(RectTransform));
        listObject.transform.SetParent(scrollObject.transform, false);
        localLevelListRoot = listObject.GetComponent<RectTransform>();
        localLevelListRoot.anchorMin = new Vector2(0f, 1f);
        localLevelListRoot.anchorMax = new Vector2(1f, 1f);
        localLevelListRoot.pivot = new Vector2(0f, 1f);
        localLevelListRoot.anchoredPosition = Vector2.zero;
        localLevelListRoot.sizeDelta = new Vector2(0f, 340f);
        localLevelListRoot.localScale = Vector3.one;

        localLevelScrollRect = scrollObject.GetComponent<ScrollRect>();
        localLevelScrollRect.viewport = scrollRectTransform;
        localLevelScrollRect.content = localLevelListRoot;
        localLevelScrollRect.horizontal = false;
        localLevelScrollRect.vertical = true;
        localLevelScrollRect.movementType = ScrollRect.MovementType.Clamped;
        localLevelScrollRect.scrollSensitivity = 28f;

        CreateContextButton(windowRect, "Import", 24, 32f, 528f, 180f, 48f, ImportExternalJson);
        CreateContextButton(windowRect, "Refresh", 24, 232f, 528f, 160f, 48f, PopulateLocalLevelList);

        levelFilePanel.SetActive(false);
    }

    private void PopulateLocalLevelList()
    {
        if (localLevelListRoot == null)
        {
            return;
        }

        for (int i = localLevelListRoot.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(localLevelListRoot.GetChild(i).gameObject);
        }

        string[] files = GetLocalLevelFiles();
        float contentHeight = Mathf.Max(340f, files.Length > 0 ? files.Length * 54f : 48f);
        localLevelListRoot.sizeDelta = new Vector2(localLevelListRoot.sizeDelta.x, contentHeight);
        localLevelListRoot.anchoredPosition = Vector2.zero;
        if (localLevelScrollRect != null)
        {
            localLevelScrollRect.verticalNormalizedPosition = 1f;
        }

        if (files.Length == 0)
        {
            localLevelEmptyText = CreateContextText(
                localLevelListRoot,
                "No local .json saves yet.",
                22,
                TextAnchor.UpperLeft,
                0f,
                0f,
                500f,
                48f);
            return;
        }

        localLevelEmptyText = null;
        int visibleCount = files.Length;
        for (int i = 0; i < visibleCount; i++)
        {
            string filePath = files[i];
            string label = Path.GetFileName(filePath);
            Button loadLevelButton = CreateContextButton(
                localLevelListRoot,
                label,
                20,
                0f,
                i * 54f,
                580f,
                46f,
                () => LoadJsonFromPath(filePath, true));
            SetButtonTextColor(loadLevelButton, Color.black);

            Button deleteLevelButton = CreateContextButton(
                localLevelListRoot,
                "Delete",
                20,
                596f,
                i * 54f,
                120f,
                46f,
                () => DeleteLocalLevelFile(filePath));
            SetButtonTextColor(deleteLevelButton, Color.black);
        }
    }

    private void DeleteLocalLevelFile(string path)
    {
        try
        {
            string root = Path.GetFullPath(Application.persistentDataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                SetStatus("Delete failed: local level file was not found.");
                PopulateLocalLevelList();
                return;
            }

            File.Delete(fullPath);
            SetStatus($"Deleted level: {Path.GetFileName(fullPath)}");
            PopulateLocalLevelList();
        }
        catch (Exception exception)
        {
            SetStatus($"Delete failed: {exception.Message}");
        }
    }

    private static string[] GetLocalLevelFiles()
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            return Directory.GetFiles(Application.persistentDataPath, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void SaveJson()
    {
        PullDataFromInputs();
        string path = GetOutputPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildJson());
            SetStatus($"Saved JSON: {path}");
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
        }
    }

    private void LoadJson()
    {
        PullDataFromInputs();
        LoadJsonFromPath(GetOutputPath(), false);
    }

    private void LoadJsonFromPath(string path, bool closePanel)
    {
        path = NormalizeJsonPath(path);
        if (!File.Exists(path))
        {
            SetStatus($"No JSON found at {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (!LevelJsonUtility.TryParse(json, out LevelJsonData data, out string error))
            {
                SetStatus($"Load failed: {error}");
                return;
            }

            ApplyData(data);
            PushDataToInputs();
            undoHistory.Clear();
            redoHistory.Clear();
            RebuildDynamicUi();
            SetStatus($"Loaded JSON: {path}");
            if (closePanel)
            {
                CloseLevelFilePanel();
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
        }
    }

    private void ImportExternalJson()
    {
        if (TryPickImportPath(out string path))
        {
            LoadJsonFromPath(path, true);
            return;
        }

        SetStatus("Import canceled.");
    }

    private void ExportJson()
    {
        PullDataFromInputs();
        if (TryPickExportPath(Clean(outputFileName, "CustomLevel.json"), out string pickedPath))
        {
            ExportJsonToPath(pickedPath);
            return;
        }

        ExportJsonToPath(GetDefaultExportPath());
    }

    private void ExportJsonToPath(string path)
    {
        path = NormalizeJsonPath(path);
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, BuildJson());
            SetStatus($"Exported JSON: {path}");
        }
        catch (Exception exception)
        {
            SetStatus($"Export failed: {exception.Message}");
        }
    }

    private void TestPlay()
    {
        PullDataFromInputs();
        string json = BuildJson();
        if (!LevelJsonUtility.TryParse(json, out _, out string error))
        {
            SetStatus($"Cannot test: {error}");
            return;
        }

        LevelSceneModeRequest.Set(LevelSceneMode.Normal);
        LevelLoadRequest.Set(json, levelId, 0);
        CardSelectionState.PrepareLevelLoad(playSceneName);
        SceneTransitionController.LoadScene(playSceneName);
    }

    private void UndoLastChange()
    {
        if (undoHistory.Count == 0)
        {
            SetStatus("Nothing to undo.");
            return;
        }

        redoHistory.Push(CaptureSpawnSnapshot());
        RestoreSpawnSnapshot(undoHistory.Pop());
        SetStatus("Undid last timeline change.");
    }

    private void RedoLastChange()
    {
        if (redoHistory.Count == 0)
        {
            SetStatus("Nothing to redo.");
            return;
        }

        undoHistory.Push(CaptureSpawnSnapshot());
        RestoreSpawnSnapshot(redoHistory.Pop());
        SetStatus("Redid timeline change.");
    }

    private void ClearSpawns()
    {
        if (spawns.Count == 0)
        {
            SetStatus("Timeline is already clear.");
            return;
        }

        PushUndoSnapshot();
        spawns.Clear();
        selectedSpawnIds.Clear();
        selectedSpawnId = -1;
        RebuildMarkers();
        SetStatus("Timeline cleared.");
    }

    private void PushUndoSnapshot()
    {
        PushUndoSnapshot(CaptureSpawnSnapshot());
    }

    private void PushUndoSnapshot(List<LevelEditorSpawn> snapshot)
    {
        undoHistory.Push(CloneSpawnSnapshot(snapshot));
        redoHistory.Clear();
    }

    private List<LevelEditorSpawn> CaptureSpawnSnapshot()
    {
        return CloneSpawnSnapshot(spawns);
    }

    private static List<LevelEditorSpawn> CloneSpawnSnapshot(IEnumerable<LevelEditorSpawn> source)
    {
        return source
            .Select(spawn => new LevelEditorSpawn(spawn.Id, spawn.Time, spawn.Enemy, spawn.Lane, spawn.Count))
            .ToList();
    }

    private void RestoreSpawnSnapshot(List<LevelEditorSpawn> snapshot)
    {
        spawns.Clear();
        spawns.AddRange(CloneSpawnSnapshot(snapshot));
        selectedSpawnIds.Clear();
        selectedSpawnId = -1;
        nextSpawnId = Mathf.Max(nextSpawnId, spawns.Count == 0 ? 1 : spawns.Max(spawn => spawn.Id) + 1);
        RebuildMarkers();
    }

    private static bool SpawnSnapshotsMatch(IReadOnlyList<LevelEditorSpawn> left, IReadOnlyList<LevelEditorSpawn> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            LevelEditorSpawn a = left[i];
            LevelEditorSpawn b = right[i];
            if (a.Id != b.Id || !Mathf.Approximately(a.Time, b.Time) || a.Lane != b.Lane || a.Count != b.Count ||
                !string.Equals(a.Enemy, b.Enemy, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void ToggleDeleteMode()
    {
        deleteMode = !deleteMode;
        UpdateDeleteModeVisual();
        SetStatus(deleteMode ? "Delete mode on. Click a marker to remove it." : "Delete mode off.");
    }

    private void UpdateDeleteModeVisual()
    {
        Text label = deleteModeButton != null ? deleteModeButton.GetComponentInChildren<Text>(true) : null;
        if (label != null)
        {
            label.text = deleteMode ? "DELETE: ON" : "DELETE: OFF";
        }

        SetButtonColor(deleteModeButton, deleteMode ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white);
    }

    private string BuildJson()
    {
        LevelJsonData data = new()
        {
            schemaVersion = LevelJsonUtility.CurrentSchemaVersion,
            id = Clean(levelId, DefaultLevelName),
            displayName = Clean(levelId, DefaultLevelName),
            startingCoins = Mathf.Max(0, startingCoins),
            timelineDuration = TimelineDuration,
            showCardSelectionOnStart = true,
            waitForCardSelectionBeforeLoadingCards = true,
            cardRules = BuildCardRules(),
            enemySpawns = spawns
                .OrderBy(spawn => spawn.Time)
                .SelectMany(spawn => Enumerable.Range(0, Mathf.Clamp(spawn.Count, 1, 999))
                    .Select(_ => new LevelEnemySpawnJson
                {
                    time = Mathf.Max(0f, spawn.Time),
                    enemy = spawn.Enemy,
                    lane = Mathf.Clamp(spawn.Lane, 1, laneCount),
                    spawnX = spawnX,
                    offset = new LevelVector3Json()
                }))
                .ToList()
        };

        return JsonUtility.ToJson(data, true);
    }

    private LevelCardRulesJson BuildCardRules()
    {
        List<string> allowed = towerNames.Where(IsTowerAllowed).ToList();
        List<string> banned = towerNames.Where(name => !IsTowerAllowed(name)).ToList();
        return new LevelCardRulesJson
        {
            restrictAvailableCards = banned.Count > 0 || allowed.Count < towerNames.Count,
            allowedCards = allowed,
            bannedCards = banned
        };
    }

    private void ApplyData(LevelJsonData data)
    {
        SetMergedLevelName(!string.IsNullOrWhiteSpace(data.displayName) ? data.displayName : data.id);
        startingCoins = data.startingCoins;
        timelineDuration = Mathf.Max(5f, data.timelineDuration <= 0f ? 60f : data.timelineDuration);
        spawns.Clear();
        selectedSpawnIds.Clear();
        selectedSpawnId = -1;
        foreach (IGrouping<string, LevelEnemySpawnJson> group in data.enemySpawns.GroupBy(spawn =>
                     $"{spawn.enemy}|{spawn.time:0.###}|{spawn.lane}|{spawn.spawnX:0.###}"))
        {
            LevelEnemySpawnJson spawn = group.First();
            spawns.Add(new LevelEditorSpawn(nextSpawnId++, spawn.time, spawn.enemy, spawn.lane, group.Count()));
        }

        ResetTowerRules();
        if (data.cardRules != null && data.cardRules.restrictAvailableCards)
        {
            for (int i = 0; i < towerNames.Count; i++)
            {
                towerAllowed[towerNames[i]] = data.cardRules.allowedCards.Contains(towerNames[i]);
            }
        }
    }

    private void PullDataFromInputs()
    {
        string levelName = levelIdInput != null ? levelIdInput.text : levelId;
        if (string.IsNullOrWhiteSpace(levelName) && displayNameInput != null)
        {
            levelName = displayNameInput.text;
        }

        SetMergedLevelName(levelName);
        if (startingCoinsInput != null && int.TryParse(startingCoinsInput.text, out int parsedCoins))
        {
            startingCoins = Mathf.Max(0, parsedCoins);
        }

        if (timelineDurationInput != null && float.TryParse(timelineDurationInput.text, out float parsedDuration))
        {
            timelineDuration = Mathf.Max(5f, parsedDuration);
        }
    }

    private void NormalizeCatalogs()
    {
        enemyIds = enemyIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        EnsureCatalogEntry(enemyIds, "Giant");
        towerNames = towerNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void EnsureCatalogEntry(List<string> catalog, string value)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!catalog.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            catalog.Add(value);
        }
    }

    private void ResetTowerRules()
    {
        towerAllowed.Clear();
        for (int i = 0; i < towerNames.Count; i++)
        {
            towerAllowed[towerNames[i]] = true;
        }
    }

    private bool IsTowerAllowed(string towerName)
    {
        return !towerAllowed.TryGetValue(towerName, out bool allowed) || allowed;
    }

    private static bool IsCtrlHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
               ((keyboard.leftCtrlKey != null && keyboard.leftCtrlKey.isPressed) ||
                (keyboard.rightCtrlKey != null && keyboard.rightCtrlKey.isPressed));
    }

    private static bool IsInputFieldFocused()
    {
        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject == null)
        {
            return false;
        }

        InputField input = selectedObject.GetComponent<InputField>();
        return input != null && input.isFocused;
    }

    private string GetOutputPath()
    {
        string fileName = EnsureJsonExtension(Clean(outputFileName, "CustomLevel.json"));
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private string GetDefaultExportPath()
    {
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Application.persistentDataPath;
        }

        return Path.Combine(directory, EnsureJsonExtension(Clean(outputFileName, "CustomLevel.json")));
    }

    private static string NormalizeJsonPath(string path)
    {
        path = Clean(path, "CustomLevel.json");
        if (Directory.Exists(path))
        {
            return Path.Combine(path, "CustomLevel.json");
        }

        return EnsureJsonExtension(path);
    }

    private static string EnsureJsonExtension(string path)
    {
        return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + ".json";
    }

    private static bool TryPickImportPath(out string path)
    {
#if UNITY_EDITOR
        path = EditorUtility.OpenFilePanel("Import Level JSON", Application.persistentDataPath, "json");
        return !string.IsNullOrWhiteSpace(path);
#else
        path = string.Empty;
        return false;
#endif
    }

    private string GetExportDirectory()
    {
        string defaultPath = GetDefaultExportPath();
        string directory = Path.GetDirectoryName(defaultPath);
        return string.IsNullOrWhiteSpace(directory) ? Application.persistentDataPath : directory;
    }

    private bool TryPickExportPath(string defaultName, out string path)
    {
#if UNITY_EDITOR
        path = EditorUtility.SaveFilePanel(
            "Export Level JSON",
            GetExportDirectory(),
            EnsureJsonExtension(defaultName),
            "json");
        return !string.IsNullOrWhiteSpace(path);
#else
        path = string.Empty;
        return false;
#endif
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static void AddInputListener(InputField input, Action<string> action)
    {
        if (input == null || action == null)
        {
            return;
        }

        input.onEndEdit.RemoveAllListeners();
        input.onEndEdit.AddListener(value => action.Invoke(value));
    }

    private static void AddButtonListener(Button button, Action action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action.Invoke());
    }

    private static void SetInputValue(InputField input, string value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(value);
        }
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }
    }

    private static void SetButtonTextColor(Button button, Color color)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (text != null)
        {
            text.color = color;
        }
    }

    private static void SetButtonTextSize(Button button, int maxSize, int minSize, int fallbackSize)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (text == null)
        {
            return;
        }

        ConfigureTextToFit(text, minSize, maxSize, fallbackSize);
    }

    // Legacy UI.Text does not clip overflowing glyphs by default. Every dynamic
    // editor card shares this configuration so it stays readable at any layout
    // size without spilling into a neighbouring card or timeline lane.
    private static void ConfigureTextToFit(Text text, int minSize, int maxSize, int fallbackSize)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = Mathf.Clamp(fallbackSize, minSize, maxSize);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(1, minSize);
        text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, maxSize);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;
    }

    private void ClearGeneratedButtonsUnder(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(root.GetChild(i).gameObject);
        }
    }

    private void ClearGeneratedMarkers()
    {
        generatedMarkerRects.Clear();
        timelinePreview = null;
        for (int i = markerRoot != null ? markerRoot.childCount - 1 : -1; i >= 0; i--)
        {
            DestroyGeneratedObject(markerRoot.GetChild(i).gameObject);
        }
    }

    private void ClearGeneratedOverviewDots()
    {
        generatedOverviewDots.Clear();
        for (int i = timelineOverviewDotRoot != null ? timelineOverviewDotRoot.childCount - 1 : -1; i >= 0; i--)
        {
            DestroyGeneratedObject(timelineOverviewDotRoot.GetChild(i).gameObject);
        }
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static string Clean(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string PrettyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        List<char> chars = new();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && value[i - 1] != ' ')
            {
                chars.Add(' ');
            }

            chars.Add(c);
        }

        return new string(chars.ToArray());
    }

    [Serializable]
    private sealed class LevelEditorSpawn
    {
        public LevelEditorSpawn(int id, float time, string enemy, int lane, int count = 1)
        {
            Id = id;
            Time = Mathf.Max(0f, time);
            Enemy = Clean(enemy, "Goblin");
            Lane = Mathf.Max(1, lane);
            Count = Mathf.Max(1, count);
        }

        public int Id { get; }

        public float Time { get; set; }

        public string Enemy { get; }

        public int Lane { get; set; }

        public int Count { get; set; }
    }
}
