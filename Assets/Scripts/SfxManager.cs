using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight singleton SFX player. Auto-creates a hidden GameObject on
/// first access and survives scene loads. Looks up clips by key from
/// <c>Resources/Audio/SFX/</c>; pass a plain filename (with or without .wav).
/// </summary>
[DisallowMultipleComponent]
public sealed class SfxManager : MonoBehaviour
{
    public const string ButtonClickKey = "ui_button_click";
    public const string CoinPickupKey = "coin_pickup";

    private const string ResourceFolder = "Audio/SFX";

    private static SfxManager instance;

    private readonly Dictionary<string, AudioClip> clips = new();
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExistsEarly()
    {
        // Force the singleton to materialize before the first scene loads so
        // gameplay code can call Play() from Awake/OnEnable without ordering bugs.
        _ = Instance;
    }

    public static SfxManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            SfxManager existing = FindFirstObjectByType<SfxManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject go = new GameObject("[SfxManager]");
            instance = go.AddComponent<SfxManager>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // 2D — UI and gameplay sfx
    }

    /// <summary>Play a clip by key (filename without extension). Safe to call when no clip is registered.</summary>
    public static void Play(string key, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!GameSettings.SoundEffectsEnabled)
        {
            return;
        }

        SfxManager manager = Instance;
        if (manager == null)
        {
            return;
        }

        manager.PlayInternal(key, volumeScale);
    }

    /// <summary>Play a clip directly (bypasses the registry).</summary>
    public static void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return;
        }

        if (!GameSettings.SoundEffectsEnabled)
        {
            return;
        }

        SfxManager manager = Instance;
        if (manager == null || manager.source == null)
        {
            return;
        }

        manager.source.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * GameSettings.SoundEffectsVolume);
    }

    private void PlayInternal(string key, float volumeScale)
    {
        AudioClip clip = GetClip(key);
        if (clip == null || source == null)
        {
            return;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * GameSettings.SoundEffectsVolume);
    }

    private AudioClip GetClip(string key)
    {
        if (clips.TryGetValue(key, out AudioClip cached))
        {
            return cached;
        }

        string normalized = key.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
            ? key
            : key + ".wav";

        AudioClip loaded = Resources.Load<AudioClip>($"{ResourceFolder}/{normalized.Replace(".wav", string.Empty)}");
        if (loaded == null)
        {
            Debug.LogWarning($"[SfxManager] No clip found at Resources/{ResourceFolder}/{key}");
            return null;
        }

        clips[key] = loaded;
        return loaded;
    }
}
