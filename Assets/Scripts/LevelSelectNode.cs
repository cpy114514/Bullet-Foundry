using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class LevelSelectNode : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int levelNumber = 1;

    [SerializeField]
    private bool bossLevel;

    [Header("Scene")]
    [SerializeField]
    private UnityEngine.Object targetSceneAsset;

    [SerializeField]
    private string targetSceneName = "Levels";

    [Header("Level JSON")]
    [SerializeField]
    private TextAsset levelJson;

    [SerializeField]
    private string externalJsonLocation;

    [SerializeField]
    private bool preferExternalJson;

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
    private bool loadingLevel;

#if ENABLE_INPUT_SYSTEM
    private bool pointerPressedOnThisNode;
#endif

    public int LevelNumber => levelNumber;
    public bool BossLevel => bossLevel;
    public string TargetSceneName => targetSceneName;
    public TextAsset LevelJson => levelJson;
    public string ExternalJsonLocation => externalJsonLocation;

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
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (CardSelectionMenu.IsOpen || (settings != null && settings.IsOpen))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning($"{name} has no target scene assigned.");
            return;
        }

        if (loadingLevel)
        {
            return;
        }

        LevelSceneModeRequest.Clear();

        if (preferExternalJson && !string.IsNullOrWhiteSpace(externalJsonLocation))
        {
            LoadExternalJson();
            return;
        }

        if (levelJson != null)
        {
            TryStartLevel(levelJson.text, levelJson.name);
            return;
        }

        if (!string.IsNullOrWhiteSpace(externalJsonLocation))
        {
            LoadExternalJson();
            return;
        }

        Debug.LogWarning($"{name} has no level JSON assigned.", this);
    }

    private void LoadExternalJson()
    {
        if (LooksLikeUrl(externalJsonLocation) || Application.platform == RuntimePlatform.WebGLPlayer)
        {
            StartCoroutine(LoadExternalJsonRoutine(externalJsonLocation));
            return;
        }

        if (!LevelJsonUtility.TryReadExternal(externalJsonLocation, out string json, out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        TryStartLevel(json, externalJsonLocation);
    }

    private IEnumerator LoadExternalJsonRoutine(string location)
    {
        loadingLevel = true;
        string url = location;
        if (!LooksLikeUrl(url))
        {
            url = LevelJsonUtility.ResolveExternalPath(location);
        }

        using UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();
        loadingLevel = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Could not import level JSON from '{url}': {request.error}", this);
            yield break;
        }

        TryStartLevel(request.downloadHandler.text, url);
    }

    private void TryStartLevel(string json, string source)
    {
        if (!LevelJsonUtility.TryParse(json, out _, out string error))
        {
            Debug.LogError($"Invalid level JSON on {name}: {error}", this);
            return;
        }

        loadingLevel = true;
        LevelLoadRequest.Set(json, source, levelNumber);
        CardSelectionState.PrepareLevelLoad(targetSceneName);
        SceneTransitionController.LoadScene(targetSceneName);
    }

    private static bool LooksLikeUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps ||
             uri.Scheme == Uri.UriSchemeFile);
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        levelNumber = Mathf.Max(1, levelNumber);

        if (targetSceneAsset == null)
        {
            return;
        }

        string scenePath = AssetDatabase.GetAssetPath(targetSceneAsset);
        if (string.IsNullOrWhiteSpace(scenePath) ||
            !string.Equals(Path.GetExtension(scenePath), ".unity", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        targetSceneName = Path.GetFileNameWithoutExtension(scenePath);
    }
#endif
}
