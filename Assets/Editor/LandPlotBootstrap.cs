using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LandPlotBootstrap
{
    private const string SessionKey = "BulletFoundry.LandPlotBootstrap.Completed";

    static LandPlotBootstrap()
    {
        EditorApplication.delayCall += EnsureLandPlots;
    }

    private static void EnsureLandPlots()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        GameObject[] lands = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform =>
                transform.name.ToLowerInvariant().StartsWith("land") &&
                transform.GetComponent<SpriteRenderer>() != null)
            .Select(transform => transform.gameObject)
            .ToArray();

        bool changed = false;

        foreach (GameObject land in lands)
        {
            if (land.GetComponent<LandPlot>() != null)
            {
                continue;
            }

            Undo.AddComponent<LandPlot>(land);
            changed = true;
        }

        if (changed && lands.Length > 0)
        {
            EditorSceneManager.MarkSceneDirty(lands[0].scene);
        }

        SessionState.SetBool(SessionKey, true);
    }
}
