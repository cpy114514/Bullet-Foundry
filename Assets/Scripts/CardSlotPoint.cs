using UnityEngine;

[DisallowMultipleComponent]
public sealed class CardSlotPoint : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int slotIndex;

    [SerializeField]
    private Vector2 cardSize = new(1.4f, 1.85f);

    public int SlotIndex => slotIndex;

    public void SetSlotIndex(int index)
    {
        slotIndex = Mathf.Max(0, index);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.65f, 0.65f, 0.65f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(cardSize.x, cardSize.y, 0f));
        Gizmos.DrawLine(transform.position + Vector3.left * 0.12f, transform.position + Vector3.right * 0.12f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.12f, transform.position + Vector3.up * 0.12f);
    }
}
