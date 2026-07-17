using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransitionController : MonoBehaviour
{
    private const float DefaultFadeDuration = 0.34f;

    private static SceneTransitionController instance;

    [SerializeField, Min(0.01f)]
    private float fadeDuration = DefaultFadeDuration;

    [SerializeField]
    private Color fadeColor = Color.black;

    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private GraphicRaycaster graphicRaycaster;
    private Coroutine activeRoutine;

    public static SceneTransitionController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SceneTransitionController>();
            }

            if (instance == null)
            {
                GameObject transitionObject = new("Scene Transition Controller");
                instance = transitionObject.AddComponent<SceneTransitionController>();
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
        DontDestroyOnLoad(gameObject);
        EnsureUi();
        SetFadeAlpha(0f);
        SetRaycastBlocking(false);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    public static void LoadScene(string sceneName)
    {
        Instance.StartLoadScene(sceneName);
    }

    public static void RunAfterFade(Action action)
    {
        Instance.StartRunAfterFade(action);
    }

    private void StartLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneTransitionController cannot load an empty scene name.");
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private void StartRunAfterFade(Action action)
    {
        if (action == null)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(RunAfterFadeRoutine(action));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        EnsureUi();
        yield return FadeRoutine(1f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        yield return FadeRoutine(0f);
        SetRaycastBlocking(false);
        activeRoutine = null;
    }

    private IEnumerator RunAfterFadeRoutine(Action action)
    {
        EnsureUi();
        yield return FadeRoutine(1f);
        action.Invoke();
        activeRoutine = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (activeRoutine == null && canvasGroup != null && canvasGroup.alpha <= 0.001f)
        {
            SetRaycastBlocking(false);
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        SetRaycastBlocking(true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
        SetRaycastBlocking(targetAlpha > 0.001f);
    }

    private void EnsureUi()
    {
        if (canvasGroup != null && fadeImage != null)
        {
            return;
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new("Scene Transition Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            graphicRaycaster = canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null)
            {
                graphicRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        canvasGroup = canvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
        }

        fadeImage = canvas.GetComponentInChildren<Image>();
        if (fadeImage == null)
        {
            GameObject imageObject = new("Fade Image");
            imageObject.transform.SetParent(canvas.transform, false);
            RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            fadeImage = imageObject.AddComponent<Image>();
        }

        fadeImage.color = fadeColor;
        SetRaycastBlocking(canvasGroup != null && canvasGroup.alpha > 0.001f);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetRaycastBlocking(bool shouldBlock)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = shouldBlock;
            canvasGroup.interactable = shouldBlock;
        }

        if (fadeImage != null)
        {
            fadeImage.raycastTarget = shouldBlock;
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = shouldBlock;
        }
    }
}
