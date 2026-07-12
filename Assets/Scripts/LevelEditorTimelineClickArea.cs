using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class LevelEditorTimelineClickArea : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField]
    private LevelEditorController controller;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        if (controller == null)
        {
            controller = GetComponentInParent<LevelEditorController>();
        }
    }

    public void SetController(LevelEditorController target)
    {
        controller = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.HandleTimelineClick(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        controller?.BeginTimelineBoxSelection(eventData);
        controller?.BeginTimelinePan(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.UpdateTimelineBoxSelection(eventData);
        controller?.UpdateTimelinePan(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndTimelineBoxSelection(eventData);
        controller?.EndTimelinePan(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        controller?.HandleTimelineScroll(eventData);
    }
}
