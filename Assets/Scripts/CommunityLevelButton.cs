using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CommunityLevelButton : MonoBehaviour
{
    [SerializeField, Tooltip("Example: http://hackclub.app:12345")]
    private string apiBaseUrl;

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

    public void Configure(string serverUrl)
    {
        apiBaseUrl = serverUrl;
    }

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
            bool shouldOpen = pointerPressedOnThisButton && pointerIsOverButton;
            pointerPressedOnThisButton = false;
            pointerIsPressingButton = false;
            if (shouldOpen)
            {
                OpenCommunityLevels();
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
        OpenCommunityLevels();
    }
#endif

    private void OpenCommunityLevels()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings != null && settings.IsOpen)
        {
            return;
        }

        CommunityLevelBrowser.Show(apiBaseUrl);
    }

    private void UpdateVisualScale()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        float multiplier = pointerIsPressingButton
            ? pressedScale
            : pointerIsOverButton ? hoverScale : 1f;
        float t = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * multiplier, t);
    }

#if ENABLE_INPUT_SYSTEM
    private bool PointerIsOverThisButton(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || !TryGetComponent(out Collider2D buttonCollider))
        {
            return false;
        }

        float distance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 point = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
        return buttonCollider.OverlapPoint(point);
    }
#endif
}

public static class CommunityLevelButtonRuntime
{
    private const string LevelSelectSceneName = "LevelSelect";
    private const string EndlessButtonName = "Endless Button";
    private const string CommunityButtonName = "Community Button";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateFallbackButton()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != LevelSelectSceneName || GameObject.Find(CommunityButtonName) != null)
        {
            return;
        }

        GameObject endlessButton = GameObject.Find(EndlessButtonName);
        if (endlessButton == null || endlessButton.transform.parent == null)
        {
            return;
        }

        GameObject communityButton = Object.Instantiate(endlessButton, endlessButton.transform.parent);
        communityButton.name = CommunityButtonName;
        communityButton.transform.localPosition = endlessButton.transform.localPosition + new Vector3(3f, 0f, 0f);

        LevelSelectModeButton modeButton = communityButton.GetComponent<LevelSelectModeButton>();
        if (modeButton != null)
        {
            modeButton.enabled = false;
            Object.Destroy(modeButton);
        }

        TextMesh label = communityButton.GetComponent<TextMesh>();
        if (label != null)
        {
            label.text = "COMMUNITY";
        }

        communityButton.AddComponent<CommunityLevelButton>();
    }
}
