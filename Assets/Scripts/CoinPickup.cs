using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinPickup : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int value = 1;

    [SerializeField, Min(0.01f)]
    private float collectMouseRadius = 1.2f;

    [SerializeField, Min(1f)]
    private float collectScreenRadius = 90f;

    [SerializeField, Min(0f)]
    private float bobAmplitude = 0.08f;

    [SerializeField, Min(0f)]
    private float bobSpeed = 5f;

    [SerializeField, Min(0f)]
    private float lifetime = 8f;

    [SerializeField, Min(0f)]
    private float scatterDuration = 0.18f;

    private Vector3 spawnPosition;
    private Vector3 scatterStartPosition;
    private float spawnTime;
    private float scatterEndTime;
    private bool collected;
    private SpriteRenderer spriteRenderer;

    public void SetValue(int newValue)
    {
        value = Mathf.Max(1, newValue);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        EnsureCollider();
        spawnPosition = transform.position;
        scatterStartPosition = spawnPosition;
        spawnTime = Time.time;
        scatterEndTime = Time.time;
    }

    public void ScatterTo(Vector3 targetPosition)
    {
        scatterStartPosition = transform.position;
        spawnPosition = targetPosition;
        scatterEndTime = Time.time + scatterDuration;
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        if (Time.time < scatterEndTime)
        {
            float duration = Mathf.Max(0.01f, scatterDuration);
            float t = Mathf.Clamp01(1f - ((scatterEndTime - Time.time) / duration));
            transform.position = Vector3.Lerp(scatterStartPosition, spawnPosition, EaseOut(t));
        }
        else if (bobAmplitude > 0f && bobSpeed > 0f)
        {
            Vector3 position = spawnPosition;
            position.y += Mathf.Sin((Time.time - spawnTime) * bobSpeed) * bobAmplitude;
            transform.position = position;
        }

        if (lifetime > 0f && Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        TryCollectFromMouse();
    }

    private void OnMouseEnter()
    {
        Collect();
    }

    private void OnMouseDown()
    {
        Collect();
    }

    private void TryCollectFromMouse()
    {
        if (IsMouseCloseInScreenSpace() || IsMouseCloseInWorldSpace())
        {
            Collect();
        }
    }

    private bool IsMouseCloseInScreenSpace()
    {
        Vector2 mousePosition = GetMouseScreenPosition();
        if (!IsFinite(mousePosition))
        {
            return false;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 coinScreen = camera.WorldToScreenPoint(transform.position);
            if (coinScreen.z < 0f)
            {
                continue;
            }

            if (Vector2.Distance(mousePosition, coinScreen) <= collectScreenRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMouseCloseInWorldSpace()
    {
        Camera camera = GetCollectionCamera();
        if (camera == null)
        {
            return false;
        }

        Vector2 mousePosition = GetMouseScreenPosition();
        if (!IsFinite(mousePosition))
        {
            return false;
        }

        float zDistance = camera.orthographic
            ? Mathf.Abs(camera.transform.position.z - transform.position.z)
            : Mathf.Abs(transform.position.z - camera.transform.position.z);
        Vector3 mouseScreen = new(mousePosition.x, mousePosition.y, zDistance);
        Vector3 mouseWorld = camera.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = transform.position.z;
        return Vector2.Distance(transform.position, mouseWorld) <= collectMouseRadius;
    }

    private void Collect()
    {
        if (collected)
        {
            return;
        }

        collected = true;
        CoinWallet wallet = CoinWallet.Instance != null
            ? CoinWallet.Instance
            : FindFirstObjectByType<CoinWallet>();

        if (wallet != null)
        {
            wallet.AddCoins(value);
        }

        Destroy(gameObject);
    }

    private void EnsureCollider()
    {
        if (TryGetComponent(out Collider2D collider2D))
        {
            collider2D.isTrigger = true;
            if (collider2D is CircleCollider2D circleCollider2D)
            {
                circleCollider2D.radius = collectMouseRadius;
            }

            return;
        }

        CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
        circleCollider.isTrigger = true;
        circleCollider.radius = collectMouseRadius;
    }

    private static Camera GetCollectionCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        return null;
    }

    private static Vector2 GetMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return new Vector2(float.NaN, float.NaN);
#endif
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y);
    }

    private static float EaseOut(float t)
    {
        return 1f - ((1f - t) * (1f - t));
    }
}
