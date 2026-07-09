using UnityEngine;

[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public sealed class LevelSceneModeController : MonoBehaviour
{
    [SerializeField]
    private GameObject levelEditorRoot;

    [SerializeField]
    private GameObject sandboxRoot;

    [SerializeField]
    private bool hideLevelEditorInNormalAndSandbox = true;

    private void Awake()
    {
        ResolveReferences();
        ApplyMode(LevelSceneModeRequest.ConsumeRequestOrDefault());
    }

    private void ResolveReferences()
    {
        if (levelEditorRoot == null)
        {
            GameObject found = GameObject.Find("Level Editor");
            if (found != null)
            {
                levelEditorRoot = found;
            }
        }

        if (sandboxRoot == null)
        {
            GameObject found = GameObject.Find("Sandbox Mode UI");
            if (found != null)
            {
                sandboxRoot = found;
            }
        }
    }

    private void ApplyMode(LevelSceneMode mode)
    {
        if (levelEditorRoot != null && !levelEditorRoot.activeSelf)
        {
            levelEditorRoot.SetActive(true);
        }

        if (sandboxRoot != null)
        {
            sandboxRoot.SetActive(mode == LevelSceneMode.Sandbox);
        }
    }
}
