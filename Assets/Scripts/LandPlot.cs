using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LandPlot : MonoBehaviour
{
    [SerializeField]
    private bool isOccupied;

    [SerializeField]
    private bool treatChildrenAsTowers = true;

    [SerializeField, Range(0f, 1f)]
    private float emptyAlpha = 0.35f;

    [SerializeField, Range(0f, 1f)]
    private float occupiedAlpha = 1f;

    private SpriteRenderer spriteRenderer;

    public bool IsOccupied => isOccupied || HasTowerChild();

    private void Awake()
    {
        CacheSpriteRenderer();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        CacheSpriteRenderer();
        ApplyVisualState();
    }

    private void OnValidate()
    {
        CacheSpriteRenderer();
        ApplyVisualState();
    }

    private void OnTransformChildrenChanged()
    {
        ApplyVisualState();
    }

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
        ApplyVisualState();
    }

    public void SetTower(Transform tower)
    {
        isOccupied = tower != null;

        if (tower != null)
        {
            tower.SetParent(transform, true);
        }

        ApplyVisualState();
    }

    private void CacheSpriteRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplyVisualState()
    {
        CacheSpriteRenderer();
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = IsOccupied ? occupiedAlpha : emptyAlpha;
        spriteRenderer.color = color;
    }

    private bool HasTowerChild()
    {
        return treatChildrenAsTowers && transform.childCount > 0;
    }
}
