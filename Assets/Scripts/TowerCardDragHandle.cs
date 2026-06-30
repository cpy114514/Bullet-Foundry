using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CardView))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TowerCardDragHandle : MonoBehaviour
{
    [SerializeField]
    private CardView cardView;

    public void SetCardView(CardView view)
    {
        cardView = view;
    }

    private void Awake()
    {
        if (cardView == null)
        {
            cardView = GetComponent<CardView>();
        }
    }
}
