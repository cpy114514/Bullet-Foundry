using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LandGridBootstrap
{
    private const string LanePointParentName = "Shooter Lane Points";
    private const string SessionKey = "BulletFoundry.LandGridBootstrap.Completed";
    private const float PositionTolerance = 0.05f;

    static LandGridBootstrap()
    {
        EditorApplication.delayCall += EnsureLandGrid;
    }

    private static void EnsureLandGrid()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        Transform lanePointParent = GameObject.Find(LanePointParentName)?.transform;
        if (lanePointParent == null || lanePointParent.childCount == 0)
        {
            return;
        }

        Transform[] lanePoints = GetLanePoints(lanePointParent);
        List<GameObject> lands = FindLandObjects();
        if (lands.Count == 0)
        {
            return;
        }

        List<GameObject> templateRow = GetLargestLandRow(lands);
        if (templateRow.Count == 0)
        {
            return;
        }

        int createdCount = 0;

        for (int laneIndex = 0; laneIndex < lanePoints.Length; laneIndex++)
        {
            float laneY = lanePoints[laneIndex].position.y;

            for (int columnIndex = 0; columnIndex < templateRow.Count; columnIndex++)
            {
                GameObject templateLand = templateRow[columnIndex];
                Vector3 targetPosition = templateLand.transform.position;
                targetPosition.y = laneY;

                if (HasLandAt(lands, targetPosition))
                {
                    continue;
                }

                GameObject land = Object.Instantiate(templateLand);
                Undo.RegisterCreatedObjectUndo(land, "Create land grid");

                land.name = $"land r{laneIndex + 1} c{columnIndex + 1}";
                land.transform.position = targetPosition;
                EnsureLandPlot(land);

                lands.Add(land);
                createdCount++;
            }
        }

        if (createdCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(templateRow[0].scene);
            LandHierarchyBootstrap.OrganizeLandHierarchy();
        }

        SessionState.SetBool(SessionKey, true);
    }

    private static Transform[] GetLanePoints(Transform lanePointParent)
    {
        Transform[] lanePoints = new Transform[lanePointParent.childCount];
        for (int i = 0; i < lanePointParent.childCount; i++)
        {
            lanePoints[i] = lanePointParent.GetChild(i);
        }

        return lanePoints
            .OrderBy(lanePoint => lanePoint.position.y)
            .ToArray();
    }

    private static List<GameObject> FindLandObjects()
    {
        return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform =>
                transform.name.ToLowerInvariant().StartsWith("land") &&
                transform.GetComponent<SpriteRenderer>() != null)
            .Select(transform => transform.gameObject)
            .OrderBy(land => land.transform.position.x)
            .ToList();
    }

    private static List<GameObject> GetLargestLandRow(List<GameObject> lands)
    {
        return lands
            .GroupBy(land => Mathf.RoundToInt(land.transform.position.y * 100f))
            .OrderByDescending(group => group.Count())
            .First()
            .OrderBy(land => land.transform.position.x)
            .ToList();
    }

    private static bool HasLandAt(List<GameObject> lands, Vector3 position)
    {
        return lands.Any(land =>
            Mathf.Abs(land.transform.position.x - position.x) <= PositionTolerance &&
            Mathf.Abs(land.transform.position.y - position.y) <= PositionTolerance);
    }

    private static void EnsureLandPlot(GameObject land)
    {
        if (land.GetComponent<LandPlot>() == null)
        {
            land.AddComponent<LandPlot>();
        }
    }
}
