using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class LevelSelectNode : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int levelNumber = 1;

    [SerializeField]
    private bool bossLevel;

    [SerializeField]
    private string targetSceneName = "Level_test";

    [Header("Animation")]
    [SerializeField, Range(1f, 1.25f)]
    private float hoverScale = 1.1f;

    [SerializeField, Range(0.8f, 1f)]
    private float pressedScale = 0.94f;

    [SerializeField, Min(1f)]
    private float scaleSpeed = 14f;

    private Vector3 baseScale;
    private bool pointerIsOverNode;
    private bool pointerIsPressingNode;

#if ENABLE_INPUT_SYSTEM
    private bool pointerPressedOnThisNode;
#endif

    public int LevelNumber => levelNumber;
    public bool BossLevel => bossLevel;
    public string TargetSceneName => targetSceneName;

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

        pointerIsOverNode = false;
        pointerIsPressingNode = false;
    }

    public void Configure(int number, bool isBoss, string sceneName)
    {
        levelNumber = Mathf.Max(1, number);
        bossLevel = isBoss;
        targetSceneName = sceneName;
    }

#if ENABLE_INPUT_SYSTEM
    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            pointerPressedOnThisNode = false;
            pointerIsOverNode = false;
            pointerIsPressingNode = false;
            UpdateVisualScale();
            return;
        }

        pointerIsOverNode = PointerIsOverThisNode(pointer.position.ReadValue());

        if (pointer.press.wasPressedThisFrame)
        {
            pointerPressedOnThisNode = pointerIsOverNode;
            pointerIsPressingNode = pointerPressedOnThisNode;
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            bool shouldPress = pointerPressedOnThisNode &&
                pointerIsOverNode;
            pointerPressedOnThisNode = false;
            pointerIsPressingNode = false;

            if (shouldPress)
            {
                LoadLevel();
            }
        }

        if (!pointer.press.isPressed)
        {
            pointerIsPressingNode = false;
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
        LoadLevel();
    }
#endif

    private void LoadLevel()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"{name} has no target scene assigned.");
            return;
        }

        SceneTransitionController.LoadScene(targetSceneName);
    }

    private void UpdateVisualScale()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        float scaleMultiplier = pointerIsPressingNode
            ? pressedScale
            : pointerIsOverNode
                ? hoverScale
                : 1f;
        Vector3 targetScale = baseScale * scaleMultiplier;
        float t = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }

#if ENABLE_INPUT_SYSTEM
    private bool PointerIsOverThisNode(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || !TryGetComponent(out Collider2D nodeCollider))
        {
            return false;
        }

        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distanceFromCamera);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        return nodeCollider.OverlapPoint(worldPosition);
    }
#endif
}
