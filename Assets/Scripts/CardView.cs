using System;
using System.Collections.Generic;
using System.Text;
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

    [SerializeField]
    private TextMesh priceTextMesh;

    [Header("Content")]
    [SerializeField]
    private Sprite iconSprite;

    [SerializeField]
    [TextArea(2, 5)]
    private string labelText = "Firetower";

    [SerializeField, Min(0)]
    private int price;

    [SerializeField]
    private GameObject towerPrefab;

    [Header("Text Auto Fit")]
    [SerializeField]
    private bool autoFitLabelText = true;

    [SerializeField, Min(4)]
    private int labelMaxCharactersPerLine = 9;

    [SerializeField, Range(0.45f, 1f)]
    private float labelMinimumScale = 0.68f;

    [SerializeField, Min(1)]
    private int labelMaximumLines = 2;

    [SerializeField]
    private bool autoFitPriceText = true;

    [SerializeField, Min(3)]
    private int priceMaxCharactersPerLine = 5;

    [SerializeField, Range(0.45f, 1f)]
    private float priceMinimumScale = 0.72f;

    [SerializeField, HideInInspector]
    private float baseLabelCharacterSize;

    [SerializeField, HideInInspector]
    private int baseLabelFontSize;

    [SerializeField, HideInInspector]
    private float basePriceCharacterSize;

    [SerializeField, HideInInspector]
    private int basePriceFontSize;

    public SpriteRenderer BackgroundRenderer => backgroundRenderer;

    public SpriteRenderer IconRenderer => iconRenderer;

    public TextMesh LabelTextMesh => labelTextMesh;

    public TextMesh PriceTextMesh => priceTextMesh;

    public GameObject TowerPrefab => towerPrefab;

    public int Price => Mathf.Max(0, price);

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

    public void Configure(GameObject prefab, string text, int cardPrice)
    {
        towerPrefab = prefab;
        labelText = text;
        price = Mathf.Max(0, cardPrice);

        SpriteRenderer towerRenderer = prefab != null
            ? prefab.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        iconSprite = towerRenderer != null ? towerRenderer.sprite : null;
        Apply();
    }

    public void SetReferences(
        SpriteRenderer background,
        SpriteRenderer icon,
        TextMesh label,
        TextMesh priceLabel = null)
    {
        backgroundRenderer = background;
        iconRenderer = icon;
        labelTextMesh = label;
        priceTextMesh = priceLabel;
    }

    public void Apply()
    {
        AutoFindReferences();

        if (iconRenderer != null)
        {
            iconRenderer.sprite = iconSprite;
        }

        if (labelTextMesh != null)
        {
            ApplyText(labelTextMesh, labelText, true);
        }

        if (priceTextMesh != null)
        {
            ApplyText(priceTextMesh, $"${price}", false);
        }
    }

    private void ApplyText(TextMesh textMesh, string value, bool isLabel)
    {
        CaptureBaseTextSize(textMesh, isLabel);

        if (isLabel && autoFitLabelText)
        {
            string fittedText = WrapText(value, labelMaxCharactersPerLine);
            textMesh.text = fittedText;
            ApplyTextScale(
                textMesh,
                fittedText,
                baseLabelCharacterSize,
                baseLabelFontSize,
                labelMaxCharactersPerLine,
                labelMaximumLines,
                labelMinimumScale);
            return;
        }

        if (!isLabel && autoFitPriceText)
        {
            textMesh.text = value;
            ApplyTextScale(
                textMesh,
                value,
                basePriceCharacterSize,
                basePriceFontSize,
                priceMaxCharactersPerLine,
                1,
                priceMinimumScale);
            return;
        }

        textMesh.text = value;
    }

    private void CaptureBaseTextSize(TextMesh textMesh, bool isLabel)
    {
        if (textMesh == null)
        {
            return;
        }

        if (isLabel)
        {
            if (baseLabelCharacterSize <= 0f)
            {
                baseLabelCharacterSize = textMesh.characterSize;
            }

            if (baseLabelFontSize <= 0)
            {
                baseLabelFontSize = textMesh.fontSize;
            }

            return;
        }

        if (basePriceCharacterSize <= 0f)
        {
            basePriceCharacterSize = textMesh.characterSize;
        }

        if (basePriceFontSize <= 0)
        {
            basePriceFontSize = textMesh.fontSize;
        }
    }

    private static void ApplyTextScale(
        TextMesh textMesh,
        string text,
        float baseCharacterSize,
        int baseFontSize,
        int maxCharactersPerLine,
        int maxLines,
        float minimumScale)
    {
        if (textMesh == null)
        {
            return;
        }

        string[] lines = (text ?? string.Empty).Split('\n');
        int longestLine = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            longestLine = Mathf.Max(longestLine, lines[i].Length);
        }

        float widthScale = longestLine > 0
            ? Mathf.Min(1f, maxCharactersPerLine / (float)longestLine)
            : 1f;
        float heightScale = lines.Length > maxLines
            ? maxLines / (float)lines.Length
            : 1f;
        float scale = Mathf.Clamp(
            Mathf.Min(widthScale, heightScale),
            minimumScale,
            1f);

        textMesh.characterSize = Mathf.Max(0.001f, baseCharacterSize * scale);
        textMesh.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseFontSize * scale));
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
    }

    private static string WrapText(string value, int maxCharactersPerLine)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        maxCharactersPerLine = Mathf.Max(1, maxCharactersPerLine);
        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        string[] paragraphs = normalized.Split('\n');
        StringBuilder result = new();

        for (int i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0)
            {
                result.Append('\n');
            }

            result.Append(WrapParagraph(paragraphs[i], maxCharactersPerLine));
        }

        return result.ToString();
    }

    private static string WrapParagraph(string paragraph, int maxCharactersPerLine)
    {
        string[] words = paragraph.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        List<string> lines = new();
        StringBuilder currentLine = new();

        for (int i = 0; i < words.Length; i++)
        {
            foreach (string piece in SplitLongWord(words[i], maxCharactersPerLine))
            {
                int nextLength = currentLine.Length == 0
                    ? piece.Length
                    : currentLine.Length + 1 + piece.Length;

                if (currentLine.Length > 0 && nextLength > maxCharactersPerLine)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                if (currentLine.Length > 0)
                {
                    currentLine.Append(' ');
                }

                currentLine.Append(piece);
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return string.Join("\n", lines);
    }

    private static IEnumerable<string> SplitLongWord(string word, int maxCharactersPerLine)
    {
        if (string.IsNullOrEmpty(word) || word.Length <= maxCharactersPerLine)
        {
            yield return word;
            yield break;
        }

        int start = 0;
        while (start < word.Length)
        {
            int remaining = word.Length - start;
            int length = Mathf.Min(maxCharactersPerLine, remaining);
            int split = FindCamelCaseSplit(word, start, length);

            if (split > start)
            {
                length = split - start;
            }

            yield return word.Substring(start, length);
            start += length;
        }
    }

    private static int FindCamelCaseSplit(string word, int start, int maxLength)
    {
        int end = Mathf.Min(word.Length, start + maxLength);
        for (int i = end - 1; i > start + 1; i--)
        {
            if (char.IsUpper(word[i]) && !char.IsUpper(word[i - 1]))
            {
                return i;
            }
        }

        return -1;
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
            TextMesh[] textMeshes = GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (!textMeshes[i].name.Contains("Price", System.StringComparison.OrdinalIgnoreCase))
                {
                    labelTextMesh = textMeshes[i];
                    break;
                }
            }
        }

        if (priceTextMesh == null)
        {
            TextMesh[] textMeshes = GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (textMeshes[i].name.Contains("Price", System.StringComparison.OrdinalIgnoreCase))
                {
                    priceTextMesh = textMeshes[i];
                    break;
                }
            }
        }

        if (iconSprite == null && iconRenderer != null)
        {
            iconSprite = iconRenderer.sprite;
        }
    }
}
