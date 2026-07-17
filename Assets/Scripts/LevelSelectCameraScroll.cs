using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class LevelSelectCameraScroll : MonoBehaviour
{
    [SerializeField]
    private float minX = -7f;

    [SerializeField]
    private float maxX = 9.5f;

    [SerializeField, Min(0f)]
    private float keyScrollSpeed = 8f;

    [SerializeField, Min(0f)]
    private float wheelScrollSpeed = 2.4f;

    [SerializeField, Min(0f)]
    private float dragScrollSpeed = 1f;

    [SerializeField, Min(0.01f)]
    private float smoothTime = 0.14f;

    [SerializeField]
    private bool invertDrag = true;

    private bool isDragging;
    private Vector2 lastPointerPosition;
    private float targetX;
    private float smoothVelocity;

    public void Configure(float minimumX, float maximumX)
    {
        minX = Mathf.Min(minimumX, maximumX);
        maxX = Mathf.Max(minimumX, maximumX);
        targetX = Mathf.Clamp(transform.position.x, minX, maxX);
        SnapCameraPositionToTarget();
    }

    private void Awake()
    {
        // The level-select scene must never inherit the paused state from a result panel.
        Time.timeScale = 1f;
        ResetScrollState();
    }

    private void OnEnable()
    {
        ResetScrollState();
    }

    private void Update()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings != null && settings.IsOpen)
        {
            isDragging = false;
            return;
        }

        HandleKeyboardScroll();
        HandleWheelScroll();
        HandleDragScroll();
        SmoothCameraPosition();
    }

    private void HandleKeyboardScroll()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float direction = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            direction -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            direction += 1f;
        }

        if (!Mathf.Approximately(direction, 0f))
        {
            MoveTarget(direction * keyScrollSpeed * Time.deltaTime);
        }
    }

    private void HandleWheelScroll()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float wheelY = mouse.scroll.ReadValue().y;
        if (!Mathf.Approximately(wheelY, 0f))
        {
            float wheelSteps = Mathf.Abs(wheelY) > 10f ? wheelY / 120f : wheelY;
            MoveTarget(wheelSteps * wheelScrollSpeed);
        }
    }

    private void HandleDragScroll()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            isDragging = false;
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            isDragging = !PointerHitsClickableObject();
            lastPointerPosition = mouse.position.ReadValue();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || !mouse.leftButton.isPressed)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        float deltaX = pointerPosition.x - lastPointerPosition.x;
        float direction = invertDrag ? -deltaX : deltaX;
        MoveTarget(direction * GetWorldUnitsPerScreenPixel() * dragScrollSpeed);
        lastPointerPosition = pointerPosition;
    }

    private bool PointerHitsClickableObject()
    {
        Camera worldCamera = GetComponent<Camera>();
        Mouse mouse = Mouse.current;
        if (worldCamera == null || mouse == null)
        {
            return false;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        Vector3 screenPoint = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(worldCamera.transform.position.z));
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        Collider2D hit = Physics2D.OverlapPoint(worldPosition);
        return hit != null && (
            hit.GetComponent<LevelSelectNode>() != null ||
            hit.GetComponent<LevelSelectReturnButton>() != null ||
            hit.GetComponent<LevelSelectSettingsButton>() != null ||
            hit.GetComponent<CommunityLevelButton>() != null);
    }

    private float GetWorldUnitsPerScreenPixel()
    {
        Camera worldCamera = GetComponent<Camera>();
        if (worldCamera == null || Screen.width <= 0)
        {
            return 0.01f;
        }

        if (worldCamera.orthographic)
        {
            return worldCamera.orthographicSize * 2f * worldCamera.aspect / Screen.width;
        }

        float distanceFromCamera = Mathf.Abs(worldCamera.transform.position.z);
        float frustumWidth = 2f * distanceFromCamera *
            Mathf.Tan(worldCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) *
            worldCamera.aspect;
        return frustumWidth / Screen.width;
    }

    private void MoveTarget(float deltaX)
    {
        targetX = Mathf.Clamp(targetX + deltaX, minX, maxX);
    }

    private void SmoothCameraPosition()
    {
        Vector3 position = transform.position;
        position.x = Mathf.SmoothDamp(
            position.x,
            targetX,
            ref smoothVelocity,
            smoothTime);

        if (Mathf.Abs(position.x - targetX) < 0.001f)
        {
            position.x = targetX;
            smoothVelocity = 0f;
        }

        transform.position = position;
    }

    private void SnapCameraPositionToTarget()
    {
        Vector3 position = transform.position;
        position.x = targetX;
        transform.position = position;
        smoothVelocity = 0f;
    }

    private void ResetScrollState()
    {
        isDragging = false;
        targetX = Mathf.Clamp(transform.position.x, minX, maxX);
        SnapCameraPositionToTarget();
    }
}
