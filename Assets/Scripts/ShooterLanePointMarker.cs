using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class ShooterLanePointMarker : MonoBehaviour
{
    [SerializeField]
    private Color gizmoColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [SerializeField, Min(0.05f)]
    private float gizmoRadius = 0.18f;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 1.6f);

#if UNITY_EDITOR
        Handles.color = gizmoColor;
        Handles.Label(transform.position + Vector3.up * (gizmoRadius * 1.8f), name);
#endif
    }
}
