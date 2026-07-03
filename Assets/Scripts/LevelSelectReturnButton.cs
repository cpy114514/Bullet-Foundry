using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class LevelSelectReturnButton : MonoBehaviour
{
    [SerializeField]
    private string titleSceneName = "TitlePage";

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

    public void Configure(string sceneName)
    {
        titleSceneName = sceneName;
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
            bool shouldPress = pointerPressedOnThisButton &&
                pointerIsOverButton;
            pointerPressedOnThisButton = false;
            pointerIsPressingButton = false;

            if (shouldPress)
            {
                ReturnToTitle();
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
        ReturnToTitle();
    }
#endif

    private void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning($"{name} has no title scene assigned.");
            return;
        }

        SceneTransitionController.LoadScene(titleSceneName);
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
