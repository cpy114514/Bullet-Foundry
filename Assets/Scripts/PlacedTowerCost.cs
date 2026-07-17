using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlacedTowerCost : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int originalPrice;

    public int OriginalPrice => Mathf.Max(0, originalPrice);

    public void SetOriginalPrice(int price)
    {
        originalPrice = Mathf.Max(0, price);
    }
}
