using UnityEngine;

[DefaultExecutionOrder(-25)]
[DisallowMultipleComponent]
public sealed class SandboxModeController : MonoBehaviour
{
    [SerializeField]
    private CardRuntimeLoader cardRuntimeLoader;

    [SerializeField]
    private SandboxCardRowScroller cardRowScroller;

    [SerializeField]
    private SandboxEnemyList enemyList;

    private void Awake()
    {
        ApplySandboxState();
    }

    private void OnEnable()
    {
        ApplySandboxState();
    }

    private void Start()
    {
        ApplySandboxState();
    }

    private void ApplySandboxState()
    {
        bool isSandbox = LevelSceneModeRequest.IsSandbox;
        if (!isSandbox)
        {
            return;
        }

        CardSelectionState.ClearAll();
        CardSelectionMenu.HideAll();

        if (cardRuntimeLoader == null)
        {
            cardRuntimeLoader = FindFirstObjectByType<CardRuntimeLoader>();
        }

        if (cardRuntimeLoader != null)
        {
            cardRuntimeLoader.LoadCards();
        }

        if (cardRowScroller == null)
        {
            SandboxCardRowScroller[] scrollers = FindObjectsByType<SandboxCardRowScroller>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            cardRowScroller = scrollers.Length > 0 ? scrollers[0] : null;
        }

        if (cardRowScroller != null)
        {
            cardRowScroller.RefreshNow();
        }

        if (enemyList == null)
        {
            SandboxEnemyList[] enemyLists = FindObjectsByType<SandboxEnemyList>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            enemyList = enemyLists.Length > 0 ? enemyLists[0] : null;
        }

        if (enemyList != null)
        {
            enemyList.RefreshNow();
        }
    }
}
