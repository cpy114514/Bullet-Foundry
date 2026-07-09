using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelEditorController : MonoBehaviour
{
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
    private string outputFileName = "CustomLevel.json";

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
        "OldBaldGuy"
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
    private Text statusText;

    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;

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

    private string selectedEnemyId;
    private int selectedLane = 3;
    private bool deleteMode;
    private Canvas rootCanvas;
    private GameObject dragGhost;
    private bool pendingTimelineLayoutRefresh;
    private int nextSpawnId = 1;
    private int selectedSpawnId = -1;
    private bool draggingSpawnMarker;

    public float TimelineDuration => Mathf.Max(5f, timelineDuration);

    private void Awake()
    {
        NormalizeCatalogs();
        ResetTowerRules();
        selectedEnemyId = enemyIds.Count > 0 ? enemyIds[0] : "Goblin";
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
    }

    private void HandleKeyboardShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || IsInputFieldFocused())
        {
            return;
        }

        if ((keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) &&
            selectedSpawnId >= 0)
        {
            DeleteSelectedSpawn();
        }
    }

    public void DeleteSelectedSpawn()
    {
        if (selectedSpawnId >= 0)
        {
            RemoveSpawn(selectedSpawnId);
        }
    }

    public void DeselectSpawnMarker()
    {
        if (selectedSpawnId < 0)
        {
            return;
        }

        selectedSpawnId = -1;
        RebuildMarkers();
        SetStatus("Selection cleared.");
    }

    public void SelectSpawnMarker(int spawnId)
    {
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        if (spawn == null)
        {
            selectedSpawnId = -1;
            return;
        }

        selectedSpawnId = spawnId;
        deleteMode = false;
        UpdateDeleteModeVisual();
        RebuildMarkers();
        SetStatus($"Selected {PrettyName(spawn.Enemy)} at {spawn.Time:0.0}s on lane {spawn.Lane}. Drag it or press Delete.");
    }

    private void WireStaticUi()
    {
        AddInputListener(levelIdInput, value => levelId = Clean(value, "custom-level"));
        AddInputListener(displayNameInput, value => displayName = Clean(value, "Custom Level"));
        AddInputListener(outputFileInput, value => outputFileName = Clean(value, "CustomLevel.json"));
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

        AddButtonListener(saveButton, SaveJson);
        AddButtonListener(loadButton, LoadJson);
        AddButtonListener(testButton, TestPlay);
        AddButtonListener(backButton, () => SceneTransitionController.LoadScene(levelSelectSceneName));
        AddButtonListener(undoButton, UndoSpawn);
        AddButtonListener(clearButton, ClearSpawns);
        AddButtonListener(deleteModeButton, ToggleDeleteMode);

        LevelEditorTimelineClickArea clickArea = timelineArea != null
            ? timelineArea.GetComponent<LevelEditorTimelineClickArea>()
            : null;
        if (clickArea != null)
        {
            clickArea.SetController(this);
        }
    }

    private void PushDataToInputs()
    {
        SetInputValue(levelIdInput, levelId);
        SetInputValue(displayNameInput, displayName);
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
            SetButtonColor(button, isSelected ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white);
            button.onClick.AddListener(() =>
            {
                selectedEnemyId = enemyId;
                deleteMode = false;
                RebuildDynamicUi();
                SetStatus($"Selected enemy: {PrettyName(enemyId)}. Drag it onto the timeline.");
            });
        }
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
                $"{PrettyName(towerName)}  {(allowed ? "ALLOW" : "BLOCK")}",
                $"Tower Button - {towerName}");
            SetButtonColor(button, allowed ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f));
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
            Destroy(timelineGuideRoot.GetChild(i).gameObject);
        }

        float width = timelineArea.rect.width;
        float height = timelineArea.rect.height;
        CreateGuideImage("Header Background", timelineGuideRoot, new Color(0.82f, 0.82f, 0.78f, 1f), 0f, 0f, width, timelineHeaderHeight);

        for (int lane = 1; lane <= laneCount; lane++)
        {
            float top = timelineHeaderHeight + (lane - 1) * timelineLaneHeight;
            Color laneColor = lane % 2 == 0 ? new Color(0.9f, 0.9f, 0.86f, 1f) : new Color(0.84f, 0.84f, 0.8f, 1f);
            CreateGuideImage($"Lane {lane} Background", timelineGuideRoot, laneColor, 0f, top, width, timelineLaneHeight);
            CreateGuideText($"Lane {lane} Label", timelineGuideRoot, $"Lane {lane}", 14, TextAnchor.MiddleLeft, Color.black, 8f, top + 6f, 78f, 24f);
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
            if (major)
            {
                CreateGuideText($"Tick Label {second}s", timelineGuideRoot, $"{second}s", 14, TextAnchor.UpperLeft, Color.black, x + 4f, 6f, 58f, 28f);
            }
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
                float x = Mathf.Clamp(spawn.Time, 0f, TimelineDuration) * timelinePixelsPerSecond + 6f;
                float y = timelineHeaderHeight + (Mathf.Clamp(spawn.Lane, 1, laneCount) - 1) * timelineLaneHeight + 8f;
                SetTopLeft(rect, x, y, 154f, timelineLaneHeight - 16f);
            }

            Text label = markerObject.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.font = GetUiFont();
                label.text = $"{PrettyName(spawn.Enemy)}\n{spawn.Time:0.0}s  L{spawn.Lane}";
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8;
                label.resizeTextMaxSize = 18;
                label.color = spawn.Id == selectedSpawnId ? Color.black : Color.white;
            }

            Button button = markerObject.GetComponent<Button>();
            if (button != null)
            {
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
                image.color = spawn.Id == selectedSpawnId ? Color.white : Color.black;
            }

            Outline outline = markerObject.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = spawn.Id == selectedSpawnId ? Color.black : Color.white;
                outline.effectDistance = spawn.Id == selectedSpawnId ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
            }

            LevelEditorSpawnMarkerDrag drag = markerObject.GetComponent<LevelEditorSpawnMarkerDrag>();
            if (drag == null)
            {
                drag = markerObject.AddComponent<LevelEditorSpawnMarkerDrag>();
            }

            drag.Configure(this, spawnId);

            if (spawn.Id == selectedSpawnId)
            {
                markerObject.transform.SetAsLastSibling();
            }
        }
    }

    public void BeginSpawnMarkerDrag(int spawnId)
    {
        if (FindSpawn(spawnId) == null)
        {
            return;
        }

        selectedSpawnId = spawnId;
        draggingSpawnMarker = true;
        deleteMode = false;
        UpdateDeleteModeVisual();
        RebuildMarkers();
    }

    public void DragSpawnMarker(int spawnId, PointerEventData eventData)
    {
        if (!draggingSpawnMarker || eventData == null)
        {
            return;
        }

        if (TryGetTimelinePlacementFromScreenPoint(eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            MoveSpawn(spawnId, time, lane, false);
        }
    }

    public void EndSpawnMarkerDrag(int spawnId)
    {
        draggingSpawnMarker = false;
        LevelEditorSpawn spawn = FindSpawn(spawnId);
        if (spawn != null)
        {
            SetStatus($"Moved {PrettyName(spawn.Enemy)} to {spawn.Time:0.0}s on lane {spawn.Lane}.");
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
            RebuildMarkers();
        }
    }

    private void RemoveSpawn(int spawnId)
    {
        int index = spawns.FindIndex(spawn => spawn.Id == spawnId);
        if (index < 0)
        {
            return;
        }

        LevelEditorSpawn removed = spawns[index];
        spawns.RemoveAt(index);
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
    }

    public void EndEnemyCardDrag(string enemyId, PointerEventData eventData)
    {
        DestroyDragGhost();
        if (eventData == null)
        {
            return;
        }

        if (TryAddSpawnAtScreenPoint(enemyId, eventData.position, eventData.pressEventCamera, out float time, out int lane))
        {
            SetStatus($"Added {PrettyName(enemyId)} at {time:0.0}s on lane {lane}.");
        }
        else
        {
            SetStatus("Drop enemy cards onto the timeline lanes.");
        }
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

        LevelEditorSpawn spawn = new(nextSpawnId++, time, enemyId, lane);
        spawns.Add(spawn);
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
            eventData.Use();
        }
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

    private Text CreateGuideText(string objectName, RectTransform parent, string textValue, int fontSize, TextAnchor alignment, Color color, float left, float top, float width, float height)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        SetTopLeft(rect, left, top, width, height);
        Text text = textObject.AddComponent<Text>();
        text.font = GetUiFont();
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
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
        SetTopLeft(rect, 0f, 0f, 150f, 58f);
        Image image = dragGhost.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.78f);
        image.raycastTarget = false;
        Outline outline = dragGhost.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

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
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.raycastTarget = false;
    }

    private void DestroyDragGhost()
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost);
            dragGhost = null;
        }
    }

    private RectTransform GetRootCanvasRect()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = FindFirstObjectByType<Canvas>();
            }
        }

        return rootCanvas != null ? rootCanvas.transform as RectTransform : null;
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
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 20;
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
        text.fontSize = 18;
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

    private void SaveJson()
    {
        PullDataFromInputs();
        string path = GetOutputPath();
        try
        {
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
        string path = GetOutputPath();
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
            RebuildDynamicUi();
            SetStatus($"Loaded JSON: {path}");
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}");
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
        LevelLoadRequest.Set(json, displayName, 0);
        CardSelectionState.PrepareLevelLoad(playSceneName);
        SceneTransitionController.LoadScene(playSceneName);
    }

    private void UndoSpawn()
    {
        if (spawns.Count <= 0)
        {
            SetStatus("No spawn to undo.");
            return;
        }

        spawns.RemoveAt(spawns.Count - 1);
        RebuildMarkers();
        SetStatus("Removed last spawn.");
    }

    private void ClearSpawns()
    {
        spawns.Clear();
        selectedSpawnId = -1;
        RebuildMarkers();
        SetStatus("Timeline cleared.");
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
            id = Clean(levelId, "custom-level"),
            displayName = Clean(displayName, "Custom Level"),
            startingCoins = Mathf.Max(0, startingCoins),
            showCardSelectionOnStart = true,
            waitForCardSelectionBeforeLoadingCards = true,
            cardRules = BuildCardRules(),
            enemySpawns = spawns
                .OrderBy(spawn => spawn.Time)
                .Select(spawn => new LevelEnemySpawnJson
                {
                    time = Mathf.Max(0f, spawn.Time),
                    enemy = spawn.Enemy,
                    lane = Mathf.Clamp(spawn.Lane, 1, laneCount),
                    spawnX = spawnX,
                    offset = new LevelVector3Json()
                })
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
        levelId = data.id;
        displayName = data.displayName;
        startingCoins = data.startingCoins;
        spawns.Clear();
        selectedSpawnId = -1;
        for (int i = 0; i < data.enemySpawns.Count; i++)
        {
            LevelEnemySpawnJson spawn = data.enemySpawns[i];
            spawns.Add(new LevelEditorSpawn(nextSpawnId++, spawn.time, spawn.enemy, spawn.lane));
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
        levelId = Clean(levelIdInput != null ? levelIdInput.text : levelId, "custom-level");
        displayName = Clean(displayNameInput != null ? displayNameInput.text : displayName, "Custom Level");
        outputFileName = Clean(outputFileInput != null ? outputFileInput.text : outputFileName, "CustomLevel.json");
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
        towerNames = towerNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
        string fileName = Clean(outputFileName, "CustomLevel.json");
        return Path.Combine(Application.persistentDataPath, fileName);
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
        for (int i = markerRoot != null ? markerRoot.childCount - 1 : -1; i >= 0; i--)
        {
            DestroyGeneratedObject(markerRoot.GetChild(i).gameObject);
        }
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target != null)
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
        public LevelEditorSpawn(int id, float time, string enemy, int lane)
        {
            Id = id;
            Time = Mathf.Max(0f, time);
            Enemy = Clean(enemy, "Goblin");
            Lane = Mathf.Max(1, lane);
        }

        public int Id { get; }

        public float Time { get; set; }

        public string Enemy { get; }

        public int Lane { get; set; }
    }
}
