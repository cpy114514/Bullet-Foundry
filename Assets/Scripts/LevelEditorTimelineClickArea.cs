using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class LevelEditorTimelineClickArea : MonoBehaviour, IPointerClickHandler, IScrollHandler
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
        controller?.DeselectSpawnMarker();
    }

    public void OnScroll(PointerEventData eventData)
    {
        controller?.HandleTimelineScroll(eventData);
    }
}
