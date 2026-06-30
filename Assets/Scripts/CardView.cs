using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private SpriteRenderer backgroundRenderer;

    [SerializeField]
    private SpriteRenderer iconRenderer;

    [SerializeField]
    private TextMesh labelTextMesh;

    [Header("Content")]
    [SerializeField]
    private Sprite iconSprite;

    [SerializeField]
    [TextArea(2, 5)]
    private string labelText = "Firetower";

    private GameObject towerPrefab;

    public SpriteRenderer BackgroundRenderer => backgroundRenderer;

    public SpriteRenderer IconRenderer => iconRenderer;

    public TextMesh LabelTextMesh => labelTextMesh;

    public GameObject TowerPrefab => towerPrefab;

    public Sprite IconSprite
    {
        get => iconSprite;
        set
        {
            iconSprite = value;
            Apply();
        }
    }

    public string LabelText
    {
        get => labelText;
        set
        {
            labelText = value;
            Apply();
        }
    }

    private void Reset()
    {
        AutoFindReferences();
        Apply();
    }

    private void OnEnable()
    {
        AutoFindReferences();
        Apply();
    }

    private void OnValidate()
    {
        AutoFindReferences();
        Apply();
    }

    public void Configure(Sprite sprite, string text)
    {
        Configure(sprite, text, null);
    }

    public void Configure(Sprite sprite, string text, GameObject prefab)
    {
        iconSprite = sprite;
        labelText = text;
        towerPrefab = prefab;
        Apply();
    }

    public void SetReferences(
        SpriteRenderer background,
        SpriteRenderer icon,
        TextMesh label)
    {
        backgroundRenderer = background;
        iconRenderer = icon;
        labelTextMesh = label;
    }

    public void Apply()
    {
        AutoFindReferences();

        if (iconRenderer != null)
        {
            if (iconSprite != null)
            {
                iconRenderer.sprite = iconSprite;
            }
        }

        if (labelTextMesh != null)
        {
            labelTextMesh.text = labelText;
        }
    }

    private void AutoFindReferences()
    {
        if (backgroundRenderer == null)
        {
            backgroundRenderer = GetComponent<SpriteRenderer>();
        }

        if (iconRenderer == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != backgroundRenderer)
                {
                    iconRenderer = renderers[i];
                    break;
                }
            }
        }

        if (labelTextMesh == null)
        {
            labelTextMesh = GetComponentInChildren<TextMesh>(true);
        }

        if (iconSprite == null && iconRenderer != null)
        {
            iconSprite = iconRenderer.sprite;
        }
    }
}
