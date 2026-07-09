using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class LevelEditorEnemyCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private LevelEditorController controller;
    private string enemyId;

    public void Configure(LevelEditorController targetController, string targetEnemyId)
    {
        controller = targetController;
        enemyId = targetEnemyId;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        controller?.BeginEnemyCardDrag(enemyId, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.UpdateEnemyCardDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndEnemyCardDrag(enemyId, eventData);
    }
}
