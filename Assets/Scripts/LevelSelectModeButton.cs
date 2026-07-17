using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class LevelSelectModeButton : MonoBehaviour
{
    [SerializeField]
    private LevelSceneMode mode = LevelSceneMode.Sandbox;

    [SerializeField]
    private string targetSceneName = "Levels";

    [Header("Animation")]
    [SerializeField, Range(1f, 1.2f)]
    private float hoverScale = 1.08f;

    [SerializeField, Range(0.8f, 1f)]
    private float pressedScale = 0.94f;

    [SerializeField, Min(1f)]
    private float scaleSpeed = 14f;

    private Vector3 baseScale;
    private bool pointerIsOverButton;
    private bool pointerIsPressingButton;

#if ENABLE_INPUT_SYSTEM
    private bool pointerPressedOnThisButton;
#endif

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        pointerIsOverButton = false;
        pointerIsPressingButton = false;
    }

    public void Configure(LevelSceneMode buttonMode, string sceneName)
    {
        mode = buttonMode;
        targetSceneName = sceneName;
    }

#if ENABLE_INPUT_SYSTEM
    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            pointerPressedOnThisButton = false;
            pointerIsOverButton = false;
            pointerIsPressingButton = false;
            UpdateVisualScale();
            return;
        }

        pointerIsOverButton = PointerIsOverThisButton(pointer.position.ReadValue());

        if (pointer.press.wasPressedThisFrame)
        {
            pointerPressedOnThisButton = pointerIsOverButton;
            pointerIsPressingButton = pointerPressedOnThisButton;
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            bool shouldPress = pointerPressedOnThisButton && pointerIsOverButton;
            pointerPressedOnThisButton = false;
            pointerIsPressingButton = false;

            if (shouldPress)
            {
                LoadMode();
            }
        }

        if (!pointer.press.isPressed)
        {
            pointerIsPressingButton = false;
        }

        UpdateVisualScale();
    }
#else
    private void Update()
    {
        UpdateVisualScale();
    }

    private void OnMouseUpAsButton()
    {
        LoadMode();
    }
#endif

    private void LoadMode()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings != null && settings.IsOpen)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"{name} has no target scene assigned.");
            return;
        }

        LevelSceneModeRequest.Set(mode);
        if (mode == LevelSceneMode.Sandbox)
        {
            LevelLoadRequest.Set(BuildSandboxJson(), "Sandbox", 0);
        }
        else if (mode == LevelSceneMode.Endless)
        {
            LevelLoadRequest.Set(BuildEndlessJson(), "Endless", 0);
        }
        else if (mode == LevelSceneMode.LevelEditor)
        {
            LevelLoadRequest.Clear();
            targetSceneName = string.IsNullOrWhiteSpace(targetSceneName) || targetSceneName == "Levels"
                ? "LevelEditor"
                : targetSceneName;
        }

        CardSelectionState.PrepareLevelLoad(targetSceneName);
        SceneTransitionController.LoadScene(targetSceneName);
    }

    private static string BuildSandboxJson()
    {
        return "{" +
            "\"schemaVersion\":1," +
            "\"id\":\"sandbox\"," +
            "\"displayName\":\"Sandbox\"," +
            "\"startingCoins\":99999," +
            "\"showCardSelectionOnStart\":false," +
            "\"waitForCardSelectionBeforeLoadingCards\":false," +
            "\"cardRules\":{\"restrictAvailableCards\":false,\"allowedCards\":[],\"bannedCards\":[]}," +
            "\"enemySpawns\":[]" +
            "}";
    }

    private static string BuildEndlessJson()
    {
        return "{" +
            "\"schemaVersion\":1," +
            "\"id\":\"endless\"," +
            "\"displayName\":\"Endless Mode\"," +
            "\"startingCoins\":1000," +
            "\"showCardSelectionOnStart\":true," +
            "\"waitForCardSelectionBeforeLoadingCards\":true," +
            "\"cardRules\":{\"restrictAvailableCards\":false,\"allowedCards\":[],\"bannedCards\":[]}," +
            "\"enemySpawns\":[]" +
            "}";
    }

    private void UpdateVisualScale()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        float scaleMultiplier = pointerIsPressingButton
            ? pressedScale
            : pointerIsOverButton
                ? hoverScale
                : 1f;
        Vector3 targetScale = baseScale * scaleMultiplier;
        float t = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }

#if ENABLE_INPUT_SYSTEM
    private bool PointerIsOverThisButton(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || !TryGetComponent(out Collider2D buttonCollider))
        {
            return false;
        }

        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distanceFromCamera);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        return buttonCollider.OverlapPoint(worldPosition);
    }
#endif
}
