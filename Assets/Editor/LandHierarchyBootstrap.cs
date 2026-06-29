using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LandHierarchyBootstrap
{
    private const string LandParentName = "Land Plots";
    private const string RowNamePrefix = "Plot Row";
    private const string LanePointParentName = "Shooter Lane Points";

    static LandHierarchyBootstrap()
    {
        EditorApplication.delayCall += OrganizeLandHierarchy;
    }

    [MenuItem("Tools/Bullet Foundry/Organize Land Plots")]
    public static void OrganizeLandHierarchy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        List<Transform> lands = FindLandTransforms();
        if (lands.Count == 0)
        {
            return;
        }

        bool changed = false;
        GameObject landParent = GameObject.Find(LandParentName);
        if (landParent == null)
        {
            landParent = new GameObject(LandParentName);
            Undo.RegisterCreatedObjectUndo(landParent, "Create land hierarchy");
            changed = true;
        }

        float[] rowYPositions = GetRowYPositions(lands);
        Transform[] rowParents = EnsureRowParents(landParent.transform, rowYPositions.Length, ref changed);

        Dictionary<int, List<Transform>> rows = new Dictionary<int, List<Transform>>();
        for (int i = 0; i < rowYPositions.Length; i++)
        {
            rows[i] = new List<Transform>();
        }

        foreach (Transform land in lands)
        {
            int rowIndex = FindNearestRowIndex(land.position.y, rowYPositions);
            rows[rowIndex].Add(land);
        }

        for (int rowIndex = 0; rowIndex < rowParents.Length; rowIndex++)
        {
            List<Transform> rowLands = rows[rowIndex]
                .OrderBy(land => land.position.x)
                .ToList();

            for (int columnIndex = 0; columnIndex < rowLands.Count; columnIndex++)
            {
                Transform land = rowLands[columnIndex];
                Vector3 worldPosition = land.position;
                Quaternion worldRotation = land.rotation;
                Vector3 worldScale = land.lossyScale;
                string expectedName = $"land r{rowIndex + 1} c{columnIndex + 1}";

                if (land.parent != rowParents[rowIndex])
                {
                    Undo.RecordObject(land, "Organize land hierarchy");
                    land.SetParent(rowParents[rowIndex], true);
                    land.position = worldPosition;
                    land.rotation = worldRotation;
                    SetWorldScale(land, worldScale);
                    changed = true;
                }

                if (land.name != expectedName)
                {
                    Undo.RecordObject(land, "Rename land plot");
                    land.name = expectedName;
                    changed = true;
                }

                if (land.GetSiblingIndex() != columnIndex)
                {
                    Undo.RecordObject(land, "Sort land plot");
                    land.SetSiblingIndex(columnIndex);
                    changed = true;
                }

                if (land.GetComponent<LandPlot>() == null)
                {
                    Undo.AddComponent<LandPlot>(land.gameObject);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(lands[0].gameObject.scene);
        }
    }

    private static List<Transform> FindLandTransforms()
    {
        return Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform =>
                transform.name.ToLowerInvariant().StartsWith("land") &&
                transform.GetComponent<SpriteRenderer>() != null)
            .OrderBy(transform => transform.position.y)
            .ThenBy(transform => transform.position.x)
            .ToList();
    }

    private static float[] GetRowYPositions(List<Transform> lands)
    {
        Transform lanePointParent = GameObject.Find(LanePointParentName)?.transform;
        if (lanePointParent != null && lanePointParent.childCount > 0)
        {
            float[] lanePointYs = new float[lanePointParent.childCount];
            for (int i = 0; i < lanePointParent.childCount; i++)
            {
                lanePointYs[i] = lanePointParent.GetChild(i).position.y;
            }

            return lanePointYs
                .OrderBy(y => y)
                .ToArray();
        }

        return lands
            .Select(land => Mathf.Round(land.position.y * 100f) / 100f)
            .Distinct()
            .OrderBy(y => y)
            .ToArray();
    }

    private static Transform[] EnsureRowParents(Transform landParent, int rowCount, ref bool changed)
    {
        Transform[] rowParents = new Transform[rowCount];

        for (int i = 0; i < rowCount; i++)
        {
            string rowName = $"{RowNamePrefix} {i + 1}";
            Transform rowParent = landParent.Find(rowName);

            if (rowParent == null)
            {
                GameObject row = new GameObject(rowName);
                Undo.RegisterCreatedObjectUndo(row, "Create land row");
                row.transform.SetParent(landParent, false);
                rowParent = row.transform;
                changed = true;
            }

            if (rowParent.GetSiblingIndex() != i)
            {
                Undo.RecordObject(rowParent, "Sort land row");
                rowParent.SetSiblingIndex(i);
                changed = true;
            }

            rowParents[i] = rowParent;
        }

        return rowParents;
    }

    private static int FindNearestRowIndex(float yPosition, float[] rowYPositions)
    {
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(yPosition - rowYPositions[0]);

        for (int i = 1; i < rowYPositions.Length; i++)
        {
            float distance = Mathf.Abs(yPosition - rowYPositions[i]);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private static void SetWorldScale(Transform transform, Vector3 worldScale)
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            transform.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        transform.localScale = new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);
    }
}
