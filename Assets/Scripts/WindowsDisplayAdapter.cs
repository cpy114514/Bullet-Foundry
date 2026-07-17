using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the 16:9 game layout visible when a Windows player uses a smaller
/// aspect ratio, while allowing additional horizontal space on wider displays.
/// </summary>
public sealed class WindowsDisplayAdapter : MonoBehaviour
{
    private const float ReferenceAspect = 16f / 9f;
    private const float SizeTolerance = 0.001f;

    private sealed class CameraState
    {
        public float baseOrthographicSize;
        public float lastAppliedSize;
    }

    private readonly Dictionary<Camera, CameraState> cameras = new();
    private int lastScreenWidth;
    private int lastScreenHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<WindowsDisplayAdapter>() != null)
        {
            return;
        }

        GameObject host = new("Windows Display Adapter");
        DontDestroyOnLoad(host);
        host.AddComponent<WindowsDisplayAdapter>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyLayout(force: true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyLayout(force: false);
        }
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        cameras.Clear();
        ApplyLayout(force: true);
    }

    private void ApplyLayout(bool force)
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        float currentAspect = (float)width / height;
        float multiplier = Mathf.Max(1f, ReferenceAspect / currentAspect);

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (camera == null || !camera.orthographic)
            {
                continue;
            }

            if (!cameras.TryGetValue(camera, out CameraState state))
            {
                state = new CameraState
                {
                    baseOrthographicSize = camera.orthographicSize,
                    lastAppliedSize = camera.orthographicSize
                };
                cameras.Add(camera, state);
            }

            // Preserve zoom changes made by gameplay controls between resizes.
            if (!force && Mathf.Abs(camera.orthographicSize - state.lastAppliedSize) > SizeTolerance)
            {
                float previousAspect = lastScreenHeight > 0
                    ? (float)lastScreenWidth / lastScreenHeight
                    : ReferenceAspect;
                float previousMultiplier = Mathf.Max(1f, ReferenceAspect / previousAspect);
                state.baseOrthographicSize = camera.orthographicSize / previousMultiplier;
            }

            float adaptedSize = state.baseOrthographicSize * multiplier;
            camera.orthographicSize = adaptedSize;
            state.lastAppliedSize = adaptedSize;
        }

        ConfigureCanvasScalers();
        lastScreenWidth = width;
        lastScreenHeight = height;
    }

    private static void ConfigureCanvasScalers()
    {
        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                continue;
            }

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
