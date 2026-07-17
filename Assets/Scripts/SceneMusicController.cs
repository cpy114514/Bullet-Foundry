using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class SceneMusicController : MonoBehaviour
{
    [SerializeField]
    private string levelsSceneName = "Levels";

    [SerializeField]
    private AudioClip defaultMusic;

    [SerializeField]
    private AudioClip levelsMusic;

    private AudioSource musicSource;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();
        ConfigureSource();
        ConnectToSettings();
    }

    private void OnEnable()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        ConfigureSource();
    }

    private void ConfigureSource()
    {
        if (musicSource == null)
        {
            return;
        }

        AudioClip targetClip = GetMusicForCurrentScene();
        if (musicSource.clip != targetClip)
        {
            musicSource.clip = targetClip;
        }

        musicSource.playOnAwake = true;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.mute = !GameSettings.MusicEnabled;
        musicSource.volume = GameSettings.MusicVolume;

        if (targetClip != null && Application.isPlaying && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private AudioClip GetMusicForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return string.Equals(sceneName, levelsSceneName, System.StringComparison.Ordinal)
            ? levelsMusic
            : defaultMusic;
    }

    private void ConnectToSettings()
    {
        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings != null)
        {
            settings.SetMusicSources(new[] { musicSource });
        }
        else
        {
            GameSettings.ApplyAudio(new[] { musicSource }, null);
        }
    }
}
