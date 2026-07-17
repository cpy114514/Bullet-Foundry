using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CommunityPostGridLayout : MonoBehaviour
{
    [SerializeField, Min(1)] private int columnCount = 2;
    [SerializeField, Min(0f)] private float horizontalSpacing = 14f;
    [SerializeField, Min(0f)] private float verticalSpacing = 14f;
    [SerializeField, Min(0f)] private float horizontalPadding = 14f;
    [SerializeField, Min(0f)] private float verticalPadding = 14f;
    [SerializeField, Min(1f)] private float defaultCardHeight = 560f;

    public void Rebuild()
    {
        RectTransform content = transform as RectTransform;
        if (content == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        int columns = Mathf.Max(1, columnCount);
        int childCount = content.childCount;
        float availableWidth = Mathf.Max(1f, content.rect.width - (horizontalPadding * 2f) - (horizontalSpacing * (columns - 1)));
        float cardWidth = availableWidth / columns;
        float cursorY = verticalPadding;

        for (int rowStart = 0; rowStart < childCount; rowStart += columns)
        {
            int rowCount = Mathf.Min(columns, childCount - rowStart);
            float rowHeight = 0f;
            for (int index = 0; index < rowCount; index++)
            {
                RectTransform card = content.GetChild(rowStart + index) as RectTransform;
                if (card == null || !card.gameObject.activeSelf)
                {
                    continue;
                }

                LayoutElement layout = card.GetComponent<LayoutElement>();
                rowHeight = Mathf.Max(rowHeight, layout != null && layout.preferredHeight > 0f ? layout.preferredHeight : defaultCardHeight);
            }

            if (rowHeight <= 0f)
            {
                continue;
            }

            for (int index = 0; index < rowCount; index++)
            {
                RectTransform card = content.GetChild(rowStart + index) as RectTransform;
                if (card == null || !card.gameObject.activeSelf)
                {
                    continue;
                }

                LayoutElement layout = card.GetComponent<LayoutElement>();
                float cardHeight = layout != null && layout.preferredHeight > 0f ? layout.preferredHeight : defaultCardHeight;
                card.anchorMin = new Vector2(0f, 1f);
                card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(0f, 1f);
                card.sizeDelta = new Vector2(cardWidth, cardHeight);
                card.anchoredPosition = new Vector2(horizontalPadding + (index * (cardWidth + horizontalSpacing)), -cursorY);
            }

            cursorY += rowHeight + verticalSpacing;
        }

        float contentHeight = childCount == 0 ? 0f : cursorY - verticalSpacing + verticalPadding;
        content.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, content.parent is RectTransform parent ? parent.rect.height : 0f));
    }
}
