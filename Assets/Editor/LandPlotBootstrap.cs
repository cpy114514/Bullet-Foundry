using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LandPlotBootstrap
{
    private static readonly Vector3 TargetScale = new(1.5f, 1.5f, 1f);

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

        GameObject[] lands = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform =>
                transform.name.ToLowerInvariant().StartsWith("land") &&
                transform.GetComponent<SpriteRenderer>() != null)
            .Select(transform => transform.gameObject)
            .ToArray();

        bool changed = false;

        foreach (GameObject land in lands)
        {
            if (land.transform.localScale != TargetScale)
            {
                Undo.RecordObject(land.transform, "Resize land plot");
                land.transform.localScale = TargetScale;
                changed = true;
            }

            if (land.GetComponent<LandPlot>() == null)
            {
                Undo.AddComponent<LandPlot>(land);
                changed = true;
            }
        }

        if (changed && lands.Length > 0)
        {
            EditorSceneManager.MarkSceneDirty(lands[0].scene);
            EditorSceneManager.SaveScene(lands[0].scene);
        }
    }
}
