using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class LevelEditorSpawnMarkerDrag : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private LevelEditorController controller;
    private int spawnId;

    public void Configure(LevelEditorController targetController, int targetSpawnId)
    {
        controller = targetController;
        spawnId = targetSpawnId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.SelectSpawnMarker(spawnId);
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        controller?.BeginSpawnMarkerDrag(spawnId);
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.DragSpawnMarker(spawnId, eventData);
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndSpawnMarkerDrag(spawnId);
        eventData.Use();
    }
}
