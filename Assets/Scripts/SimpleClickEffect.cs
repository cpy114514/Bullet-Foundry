using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class SimpleClickEffect : MonoBehaviour
{
    [SerializeField]
    private Canvas targetCanvas;

    [SerializeField, Min(0.01f)]
    private float duration = 0.22f;

    [SerializeField, Min(1f)]
    private float startSize = 18f;

    [SerializeField, Min(1f)]
    private float endSize = 54f;

    [SerializeField]
    private Color effectColor = new Color(0.85f, 0.85f, 0.85f, 0.55f);

    private Sprite effectSprite;

    private void Awake()
    {
        EnsureCanvas();
        effectSprite = CreateCircleSprite();
    }

    private void Update()
    {
        if (TryGetClickPosition(out Vector2 screenPosition))
        {
            SfxManager.Play(SfxManager.ButtonClickKey);

            if (GameSettings.ClickEffectEnabled)
            {
                Spawn(screenPosition);
            }
        }
    }

    private bool TryGetClickPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = Vector2.zero;
        return false;
    }

    private void Spawn(Vector2 screenPosition)
    {
        EnsureCanvas();
        if (targetCanvas == null)
        {
            return;
        }

        GameObject effectObject = new("Click Effect");
        effectObject.transform.SetParent(targetCanvas.transform, false);

        RectTransform rectTransform = effectObject.AddComponent<RectTransform>();
        rectTransform.position = screenPosition;
        rectTransform.sizeDelta = Vector2.one * startSize;

        Image image = effectObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = effectSprite;
        image.color = effectColor;

        StartCoroutine(AnimateEffect(rectTransform, image));
    }

    private IEnumerator AnimateEffect(RectTransform rectTransform, Image image)
    {
        float elapsed = 0f;
        while (elapsed < duration && rectTransform != null && image != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float size = Mathf.Lerp(startSize, endSize, t);
            rectTransform.sizeDelta = Vector2.one * size;

            Color color = effectColor;
            color.a *= 1f - t;
            image.color = color;
            yield return null;
        }

        if (rectTransform != null)
        {
            Destroy(rectTransform.gameObject);
        }
    }

    private void EnsureCanvas()
    {
        if (targetCanvas != null)
        {
            return;
        }

        targetCanvas = FindFirstObjectByType<Canvas>();
    }

    private static Sprite CreateCircleSprite()
    {
        const int Size = 32;
        Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false);
        Vector2 center = new((Size - 1) * 0.5f, (Size - 1) * 0.5f);
        float radius = Size * 0.45f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, Size, Size),
            new Vector2(0.5f, 0.5f),
            Size);
    }
}
