using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ShooterLanePointBootstrap
{
    private const int LanePointCount = 5;
    private const string ParentName = "Shooter Lane Points";
    private const string ShooterName = "Shooter";
    private const string RailwayName = "railway";
    private const string SessionKey = "BulletFoundry.ShooterLanePointBootstrap.Created";

    static ShooterLanePointBootstrap()
    {
        EditorApplication.delayCall += EnsureLanePoints;
    }

    private static void EnsureLanePoints()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        ShooterMovement shooterMovement = Object.FindFirstObjectByType<ShooterMovement>();
        if (shooterMovement == null)
        {
            return;
        }

        if (GameObject.Find(ParentName) != null)
        {
            AssignExistingLanePoints(shooterMovement);
            SessionState.SetBool(SessionKey, true);
            return;
        }

        GameObject shooter = GameObject.Find(ShooterName);
        Vector3 shooterPosition = shooter != null
            ? shooter.transform.position
            : shooterMovement.transform.position;

        GameObject parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create shooter lane points");

        Transform[] lanePointTransforms = new Transform[LanePointCount];
        Vector3[] lanePositions = BuildInitialLanePositions(shooterPosition);

        for (int i = 0; i < LanePointCount; i++)
        {
            GameObject lanePoint = new GameObject($"Lane Point {i + 1}");
            Undo.RegisterCreatedObjectUndo(lanePoint, "Create shooter lane point");
            lanePoint.transform.SetParent(parent.transform, true);
            lanePoint.transform.position = lanePositions[i];
            lanePoint.AddComponent<ShooterLanePointMarker>();
            lanePointTransforms[i] = lanePoint.transform;
        }

        AssignLanePoints(shooterMovement, lanePointTransforms);

        EditorSceneManager.MarkSceneDirty(shooterMovement.gameObject.scene);
        SessionState.SetBool(SessionKey, true);
    }

    private static Vector3[] BuildInitialLanePositions(Vector3 shooterPosition)
    {
        Vector3[] positions = new Vector3[LanePointCount];

        GameObject railway = GameObject.Find(RailwayName);
        SpriteRenderer railwayRenderer = railway != null
            ? railway.GetComponent<SpriteRenderer>()
            : null;

        if (railwayRenderer != null)
        {
            Bounds railwayBounds = railwayRenderer.bounds;
            float laneStep = railwayBounds.size.y / (LanePointCount + 1);

            for (int i = 0; i < LanePointCount; i++)
            {
                positions[i] = new Vector3(
                    shooterPosition.x,
                    railwayBounds.min.y + laneStep * (i + 1),
                    shooterPosition.z);
            }

            return positions;
        }

        const float fallbackSpacing = 1.25f;
        float firstLaneY = shooterPosition.y - fallbackSpacing * (LanePointCount - 1) * 0.5f;

        for (int i = 0; i < LanePointCount; i++)
        {
            positions[i] = new Vector3(
                shooterPosition.x,
                firstLaneY + fallbackSpacing * i,
                shooterPosition.z);
        }

        return positions;
    }

    private static void AssignExistingLanePoints(ShooterMovement shooterMovement)
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null || parent.transform.childCount == 0)
        {
            return;
        }

        Transform[] lanePointTransforms = new Transform[parent.transform.childCount];
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            Transform lanePointTransform = parent.transform.GetChild(i);
            if (lanePointTransform.GetComponent<ShooterLanePointMarker>() == null)
            {
                lanePointTransform.gameObject.AddComponent<ShooterLanePointMarker>();
            }

            lanePointTransforms[i] = lanePointTransform;
        }

        AssignLanePoints(shooterMovement, lanePointTransforms);
        EditorSceneManager.MarkSceneDirty(shooterMovement.gameObject.scene);
    }

    private static void AssignLanePoints(ShooterMovement shooterMovement, Transform[] lanePointTransforms)
    {
        SerializedObject serializedShooterMovement = new SerializedObject(shooterMovement);
        SerializedProperty lanePointsProperty = serializedShooterMovement.FindProperty("lanePoints");

        lanePointsProperty.arraySize = lanePointTransforms.Length;
        for (int i = 0; i < lanePointTransforms.Length; i++)
        {
            lanePointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = lanePointTransforms[i];
        }

        serializedShooterMovement.ApplyModifiedProperties();
    }
}
