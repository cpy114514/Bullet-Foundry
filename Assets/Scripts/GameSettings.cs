using UnityEngine;

public static class GameSettings
{
    public const string ResolutionWidthKey = "settings.resolution.width";
    public const string ResolutionHeightKey = "settings.resolution.height";
    public const string FullscreenKey = "settings.fullscreen";
    public const string MasterVolumeEnabledKey = "settings.volume.master.enabled";
    public const string MasterVolumeKey = "settings.volume.master";
    public const string MusicEnabledKey = "settings.music.enabled";
    public const string MusicVolumeKey = "settings.music.volume";
    public const string SoundEffectsEnabledKey = "settings.sfx.enabled";
    public const string SoundEffectsVolumeKey = "settings.sfx.volume";
    public const string ClickEffectEnabledKey = "settings.click_effect.enabled";

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
    }

    public static bool MasterVolumeEnabled
    {
        get => PlayerPrefs.GetInt(MasterVolumeEnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(MasterVolumeEnabledKey, value ? 1 : 0);
    }

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(MusicEnabledKey, value ? 1 : 0);
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }

    public static bool SoundEffectsEnabled
    {
        get => PlayerPrefs.GetInt(SoundEffectsEnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(SoundEffectsEnabledKey, value ? 1 : 0);
    }

    public static float SoundEffectsVolume
    {
        get => PlayerPrefs.GetFloat(SoundEffectsVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(SoundEffectsVolumeKey, Mathf.Clamp01(value));
    }

    public static bool ClickEffectEnabled
    {
        get => PlayerPrefs.GetInt(ClickEffectEnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(ClickEffectEnabledKey, value ? 1 : 0);
    }

    public static int ResolutionWidth
    {
        get => PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        set => PlayerPrefs.SetInt(ResolutionWidthKey, Mathf.Max(1, value));
    }

    public static int ResolutionHeight
    {
        get => PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
        set => PlayerPrefs.SetInt(ResolutionHeightKey, Mathf.Max(1, value));
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    public static void ApplyAudio(AudioSource[] musicSources, AudioSource[] soundEffectSources)
    {
        AudioListener.volume = MasterVolumeEnabled ? MasterVolume : 0f;
        ApplySources(musicSources, MusicEnabled, MusicVolume);
        ApplySources(soundEffectSources, SoundEffectsEnabled, SoundEffectsVolume);
    }

    private static void ApplySources(AudioSource[] sources, bool enabled, float volume)
    {
        if (sources == null)
        {
            return;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
            {
                continue;
            }

            source.mute = !enabled;
            source.volume = volume;
        }
    }
}
