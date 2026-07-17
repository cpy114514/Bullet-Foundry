using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelResultPanelController : MonoBehaviour
{
    private const int ResultCanvasSortingOrder = 9000;

    [SerializeField]
    private string levelSelectSceneName = "LevelSelect";

    [SerializeField]
    private Canvas canvas;

    [SerializeField]
    private GameObject root;

    [SerializeField]
    private GameObject successPanel;

    [SerializeField]
    private GameObject failurePanel;

    [SerializeField]
    private Button successRetryButton;

    [SerializeField]
    private Button successLevelSelectButton;

    [SerializeField]
    private Button failureRetryButton;

    [SerializeField]
    private Button failureLevelSelectButton;

    [SerializeField]
    private LevelEnemySpawner spawner;

    [SerializeField]
    private TowerHealth shooterHealth;

    [SerializeField]
    private Transform shooterTransform;

    private bool resultShown;
    private float resumeTimeScale = 1f;
    private UnityAction endlessContinueAction;

    private void Awake()
    {
        ResolveReferences();
        NormalizeCanvas();
        BindButtons();
        HidePanels();
    }

    private void Update()
    {
        if (resultShown ||
            LevelSceneModeRequest.ActiveMode != LevelSceneMode.Normal ||
            CardSelectionMenu.IsOpen)
        {
            return;
        }

        ResolveRuntimeReferences();

        if (IsFailureMet())
        {
            ShowResult(false);
            return;
        }

        if (IsSuccessMet())
        {
            ShowResult(true);
        }
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        resultShown = false;
        SceneTransitionController.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitToLevelSelect()
    {
        Time.timeScale = 1f;
        resultShown = false;
        SceneTransitionController.LoadScene(levelSelectSceneName);
    }

    public void ShowEndlessLayerComplete(int layer, UnityAction continueAction)
    {
        endlessContinueAction = continueAction;
        BindButton(successRetryButton, ContinueEndless);
        BindButton(successLevelSelectButton, ExitToLevelSelect);
        SetButtonLabel(successRetryButton, "CONTINUE");
        SetButtonLabel(successLevelSelectButton, "EXIT");
        SetPanelTitle(successPanel, $"LAYER {Mathf.Max(1, layer)} CLEARED");
        ShowResult(true);
    }

    public void ShowEndlessFailure()
    {
        BindButton(failureRetryButton, RetryLevel);
        BindButton(failureLevelSelectButton, ExitToLevelSelect);
        SetButtonLabel(failureRetryButton, "RETRY");
        SetButtonLabel(failureLevelSelectButton, "EXIT");
        ShowResult(false);
    }

    private void ContinueEndless()
    {
        UnityAction action = endlessContinueAction;
        endlessContinueAction = null;
        HidePanels();
        action?.Invoke();
    }

    private void ShowResult(bool success)
    {
        resultShown = true;
        resumeTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        NormalizeCanvas();

        SettingsMenuController settings = FindFirstObjectByType<SettingsMenuController>();
        if (settings != null && settings.IsOpen)
        {
            settings.CloseSettings();
        }

        if (root != null)
        {
            root.SetActive(true);
        }

        if (successPanel != null)
        {
            successPanel.SetActive(success);
        }

        if (failurePanel != null)
        {
            failurePanel.SetActive(!success);
        }
    }

    private void HidePanels()
    {
        resultShown = false;
        Time.timeScale = resumeTimeScale;

        if (root != null)
        {
            root.SetActive(false);
        }

        if (successPanel != null)
        {
            successPanel.SetActive(false);
        }

        if (failurePanel != null)
        {
            failurePanel.SetActive(false);
        }
    }

    private bool IsFailureMet()
    {
        return HasEnemyBehindShooter();
    }

    private bool IsSuccessMet()
    {
        return spawner != null &&
            spawner.IsSpawnQueueComplete &&
            !HasLivingEnemies();
    }

    private static bool HasLivingEnemies()
    {
        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasEnemyBehindShooter()
    {
        if (!TryGetShooterBounds(out Bounds shooterBounds))
        {
            return false;
        }

        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            GoblinEnemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            Bounds enemyBounds = enemy.GetWorldBounds();
            if (enemyBounds.max.x < shooterBounds.min.x)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetShooterBounds(out Bounds bounds)
    {
        if (shooterHealth != null &&
            !shooterHealth.IsDestroyed &&
            shooterHealth.gameObject.activeInHierarchy)
        {
            bounds = shooterHealth.GetWorldBounds();
            return true;
        }

        if (shooterTransform == null)
        {
            GameObject shooter = GameObject.Find("Shooter");
            shooterTransform = shooter != null ? shooter.transform : null;
        }

        if (shooterTransform == null)
        {
            bounds = default;
            return false;
        }

        SpriteRenderer[] renderers = shooterTransform.GetComponentsInChildren<SpriteRenderer>(true);
        bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(shooterTransform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        Transform searchRoot = canvas != null ? canvas.transform : transform;
        if (root == null)
        {
            Transform found = FindChild(searchRoot, "Result Overlay");
            root = found != null ? found.gameObject : null;
        }

        if (successPanel == null)
        {
            Transform found = FindChild(searchRoot, "Success Panel");
            successPanel = found != null ? found.gameObject : null;
        }

        if (failurePanel == null)
        {
            Transform found = FindChild(searchRoot, "Failure Panel");
            failurePanel = found != null ? found.gameObject : null;
        }

        if (successRetryButton == null)
        {
            successRetryButton = FindButton(searchRoot, "Success Retry");
        }

        if (successLevelSelectButton == null)
        {
            successLevelSelectButton = FindButton(searchRoot, "Success Level Select");
        }

        if (failureRetryButton == null)
        {
            failureRetryButton = FindButton(searchRoot, "Failure Retry");
        }

        if (failureLevelSelectButton == null)
        {
            failureLevelSelectButton = FindButton(searchRoot, "Failure Level Select");
        }

        ResolveRuntimeReferences();
    }

    private void NormalizeCanvas()
    {
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ResultCanvasSortingOrder;
    }

    private void ResolveRuntimeReferences()
    {
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<LevelEnemySpawner>();
        }

        if (shooterHealth == null)
        {
            GameObject shooter = GameObject.Find("Shooter");
            shooterHealth = shooter != null ? shooter.GetComponent<TowerHealth>() : null;
            shooterTransform = shooter != null ? shooter.transform : shooterTransform;
        }

        if (shooterTransform == null)
        {
            GameObject shooter = GameObject.Find("Shooter");
            shooterTransform = shooter != null ? shooter.transform : null;
        }
    }

    private void BindButtons()
    {
        BindButton(successRetryButton, RetryLevel);
        BindButton(successLevelSelectButton, ExitToLevelSelect);
        BindButton(failureRetryButton, RetryLevel);
        BindButton(failureLevelSelectButton, ExitToLevelSelect);
    }

    private static Button FindButton(Transform root, string objectName)
    {
        Transform found = FindChild(root, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (text != null)
        {
            text.text = label;
        }
    }

    private static void SetPanelTitle(GameObject panel, string title)
    {
        if (panel == null)
        {
            return;
        }

        Text[] texts = panel.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.GetComponentInParent<Button>(true) != null)
            {
                continue;
            }

            text.text = title;
            return;
        }
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
