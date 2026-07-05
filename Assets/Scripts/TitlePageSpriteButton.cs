using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public sealed class TitlePageSpriteButton : MonoBehaviour
{
    public enum ButtonAction
    {
        StartGame,
        QuitGame,
        OpenSettings,
        CloseSettings,
        ToggleSettings
    }

    [SerializeField]
    private TitlePageController controller;

    [SerializeField]
    private ButtonAction action = ButtonAction.StartGame;

    [Header("Animation")]
    [SerializeField, Range(1f, 1.2f)]
    private float hoverScale = 1.06f;

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

    private void Reset()
    {
        AutoFindController();
        FitColliderToSprite();
    }

    public void Configure(TitlePageController newController, ButtonAction newAction)
    {
        controller = newController;
        action = newAction;
        FitColliderToSprite();
    }

    private void OnValidate()
    {
        if (controller == null)
        {
            AutoFindController();
        }

        FitColliderToSprite();
    }

    private void Awake()
    {
        if (controller == null)
        {
            AutoFindController();
        }

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

        pointerIsOverButton = !ShouldBlockForOpenSettings() &&
            PointerIsOverThisButton(pointer.position.ReadValue());

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
                Press();
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
        Press();
    }
#endif

    public void Press()
    {
        if (controller == null)
        {
            AutoFindController();
            if (controller == null)
            {
                Debug.LogWarning($"{name} cannot run title action because TitlePageController is missing.");
                return;
            }
        }

        if (ShouldBlockForOpenSettings())
        {
            return;
        }

        switch (action)
        {
            case ButtonAction.StartGame:
                controller.StartGame();
                break;
            case ButtonAction.QuitGame:
                controller.QuitGame();
                break;
            case ButtonAction.OpenSettings:
                controller.OpenSettings();
                break;
            case ButtonAction.CloseSettings:
                controller.CloseSettings();
                break;
            case ButtonAction.ToggleSettings:
                controller.ToggleSettings();
                break;
        }
    }

    private bool ShouldBlockForOpenSettings()
    {
        if (action == ButtonAction.CloseSettings || action == ButtonAction.ToggleSettings)
        {
            return false;
        }

        if (controller != null && controller.IsSettingsOpen())
        {
            return true;
        }

        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        return settings != null && settings.IsOpen;
    }

    private void UpdateVisualScale()
    {
        if (baseScale == Vector3.zero)
        {
            baseScale = transform.localScale;
        }

        float scaleMultiplier = 1f;
        if (pointerIsPressingButton)
        {
            scaleMultiplier = pressedScale;
        }
        else if (pointerIsOverButton)
        {
            scaleMultiplier = hoverScale;
        }

        Vector3 targetScale = baseScale * scaleMultiplier;
        float t = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }

#if ENABLE_INPUT_SYSTEM
    private bool PointerIsOverThisButton(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return false;
        }

        if (!TryGetComponent(out Collider2D buttonCollider))
        {
            return false;
        }

        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distanceFromCamera);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        return buttonCollider.OverlapPoint(worldPosition);
    }
#endif

    private void AutoFindController()
    {
        controller = FindFirstObjectByType<TitlePageController>();
    }

    private void FitColliderToSprite()
    {
        if (!TryGetComponent(out BoxCollider2D boxCollider))
        {
            return;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        boxCollider.isTrigger = true;
        boxCollider.size = spriteRenderer.sprite.bounds.size;
        boxCollider.offset = spriteRenderer.sprite.bounds.center;
    }
}
