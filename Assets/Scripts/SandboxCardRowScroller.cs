using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SandboxCardRowScroller : MonoBehaviour
{
    [SerializeField]
    private CardRuntimeLoader cardRuntimeLoader;

    [SerializeField]
    private Transform cardsRoot;

    [SerializeField]
    private Vector3 rowStartPosition = new(-7.15f, 4.05f, 0f);

    [SerializeField, Min(0.5f)]
    private float cardSpacing = 1.55f;

    [SerializeField, Min(1f)]
    private float visibleWidth = 13.6f;

    [SerializeField, Min(0.05f)]
    private float scrollUnitsPerStep = 0.9f;

    [SerializeField, Min(0.01f)]
    private float smoothTime = 0.08f;

    private CardCatalog catalog;
    private int arrangedCardCount = -1;
    private float currentOffset;
    private float targetOffset;
    private float velocity;
    private float minOffset;
    private bool rowStartCaptured;

    private void OnEnable()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (!LevelSceneModeRequest.IsSandbox)
        {
            return;
        }

        EnsureCatalog();
        if (catalog == null)
        {
            return;
        }

        CaptureRowStartPosition();
        if (catalog.ActiveCards.Count != arrangedCardCount)
        {
            ArrangeCards();
        }

        float rawScrollDelta = GetScrollDelta();
        if (!Mathf.Approximately(rawScrollDelta, 0f))
        {
            float scrollSteps = Mathf.Abs(rawScrollDelta) > 10f
                ? rawScrollDelta / 120f
                : rawScrollDelta;
            targetOffset = Mathf.Clamp(
                targetOffset + scrollSteps * scrollUnitsPerStep,
                minOffset,
                0f);
        }

        currentOffset = Mathf.SmoothDamp(
            currentOffset,
            targetOffset,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        ApplyOffset();
    }

    public void RefreshNow()
    {
        if (!LevelSceneModeRequest.IsSandbox)
        {
            return;
        }

        EnsureCatalog();
        CaptureRowStartPosition();
        ArrangeCards();
        ApplyOffset();
    }

    private void EnsureCatalog()
    {
        if (cardRuntimeLoader == null)
        {
            cardRuntimeLoader = FindFirstObjectByType<CardRuntimeLoader>();
        }

        if (cardRuntimeLoader != null)
        {
            catalog = cardRuntimeLoader.LoadedCatalog;
        }

        if (catalog == null)
        {
            catalog = FindFirstObjectByType<CardCatalog>();
        }

        if (catalog != null)
        {
            if (cardsRoot == null)
            {
                cardsRoot = catalog.transform;
            }
        }
    }

    private void CaptureRowStartPosition()
    {
        if (rowStartCaptured)
        {
            return;
        }

        Transform root = cardsRoot != null ? cardsRoot : catalog != null ? catalog.transform : null;
        if (root == null)
        {
            return;
        }

        rowStartPosition = root.position;
        rowStartCaptured = true;
    }

    private void ArrangeCards()
    {
        if (catalog == null)
        {
            arrangedCardCount = -1;
            return;
        }

        IReadOnlyList<CardView> cards = catalog.ActiveCards;
        arrangedCardCount = cards.Count;
        if (cards.Count == 0)
        {
            minOffset = 0f;
            targetOffset = 0f;
            currentOffset = 0f;
            return;
        }

        Transform root = catalog.transform;
        Transform scrollRoot = cardsRoot != null ? cardsRoot : root;
        if (scrollRoot != root && root.parent != scrollRoot)
        {
            root.SetParent(scrollRoot, false);
        }

        if (scrollRoot == root)
        {
            cardsRoot = root;
            root.position = rowStartPosition + new Vector3(currentOffset, 0f, 0f);
        }
        else
        {
            scrollRoot.position = rowStartPosition + new Vector3(currentOffset, 0f, 0f);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            CardView card = cards[i];
            if (card == null)
            {
                continue;
            }

            Transform cardTransform = card.transform;
            if (cardTransform != root)
            {
                cardTransform.SetParent(root, false);
                cardTransform.localPosition = new Vector3(cardSpacing * i, 0f, 0f);
                cardTransform.localRotation = Quaternion.identity;
            }

            SetCardVisible(card, true);
        }

        float contentWidth = Mathf.Max(0f, (cards.Count - 1) * cardSpacing);
        minOffset = Mathf.Min(0f, visibleWidth - contentWidth - cardSpacing);
        targetOffset = Mathf.Clamp(targetOffset, minOffset, 0f);
        currentOffset = Mathf.Clamp(currentOffset, minOffset, 0f);
    }

    private void ApplyOffset()
    {
        if (cardsRoot != null)
        {
            cardsRoot.position = rowStartPosition + new Vector3(currentOffset, 0f, 0f);
        }
    }

    private static void SetCardVisible(CardView card, bool isVisible)
    {
        SpriteRenderer[] sprites = card.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].GetComponentInParent<CardView>() == card)
            {
                sprites[i].enabled = isVisible;
            }
        }

        MeshRenderer[] meshes = card.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] != null && meshes[i].GetComponentInParent<CardView>() == card)
            {
                meshes[i].enabled = isVisible;
            }
        }

        Collider2D[] colliders = card.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].GetComponentInParent<CardView>() == card)
            {
                colliders[i].enabled = isVisible;
            }
        }
    }

    private static float GetScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }
}
