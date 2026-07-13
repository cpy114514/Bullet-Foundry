using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public sealed class SandboxEnemyEntry
{
    [SerializeField]
    private string displayName;

    [SerializeField]
    private GameObject enemyPrefab;

    public string DisplayName => !string.IsNullOrWhiteSpace(displayName)
        ? displayName
        : enemyPrefab != null ? enemyPrefab.name : string.Empty;

    public GameObject EnemyPrefab => enemyPrefab;
}

[DisallowMultipleComponent]
public sealed class SandboxEnemyList : MonoBehaviour
{
    private const string EnemySlotPrefix = "Enemy Button Slot";
    private const string LaneSlotPrefix = "Lane Button Slot";

    [SerializeField]
    private Transform buttonRoot;

    [SerializeField]
    private Transform enemyButtonSlotsRoot;

    [SerializeField]
    private Transform laneButtonSlotsRoot;

    [SerializeField]
    private Transform lanePointParent;

    [SerializeField]
    private string lanePointParentName = "Shooter Lane Points";

    [SerializeField]
    private List<SandboxEnemyEntry> enemies = new();

    [SerializeField, Min(1)]
    private int laneIndex = 3;

    [SerializeField]
    private float spawnX = 8.5f;

    [SerializeField]
    private float spawnZ = 0f;

    [SerializeField]
    private Vector3 firstButtonLocalPosition = Vector3.zero;

    [SerializeField, Min(0.15f)]
    private float buttonSpacing = 0.62f;

    [SerializeField, Min(0.01f)]
    private float labelCharacterSize = 0.11f;

    [SerializeField]
    private Vector2 buttonColliderSize = new(2.25f, 0.44f);

    [Header("Enemy Card Layout")]
    [SerializeField, Min(1)]
    private int cardsPerRow = 2;

    [SerializeField, Min(0.05f)]
    private float cardColumnSpacing = 1.15f;

    [SerializeField, Min(0.05f)]
    private float cardRowSpacing = 1.28f;

    [Header("Lane Selection")]
    [SerializeField]
    private bool buildLaneSelector = true;

    [SerializeField]
    private Vector3 firstLaneButtonLocalPosition = new(-1.05f, -0.35f, 0f);

    [SerializeField, Min(0.05f)]
    private float laneButtonSpacing = 0.52f;

    [SerializeField]
    private Vector2 laneButtonSize = new(0.42f, 0.38f);

    [SerializeField, Min(1)]
    private int fallbackLaneCount = 5;

    [SerializeField]
    private Color laneButtonTint = Color.white;

    [SerializeField]
    private Color selectedLaneButtonTint = new(0.42f, 0.42f, 0.42f, 1f);

    [Header("Card Visuals")]
    [SerializeField]
    private Sprite cardBackgroundSprite;

    [SerializeField]
    private Vector2 cardSize = new(1.05f, 1.25f);

    [SerializeField]
    private Vector2 enemyIconSize = new(0.75f, 0.65f);

    [SerializeField]
    private Vector3 iconLocalPosition = new(0f, 0.18f, 0f);

    [SerializeField]
    private Vector3 labelLocalPosition = new(0f, -0.43f, -0.05f);

    [SerializeField]
    private Color cardTint = Color.white;

    [SerializeField]
    private Color labelColor = Color.black;

    [SerializeField]
    private int backgroundSortingOrder = 68;

    [SerializeField]
    private int iconSortingOrder = 69;

    [SerializeField]
    private int labelSortingOrder = 70;

    private readonly List<ButtonBinding> enemyButtons = new();
    private readonly List<ButtonBinding> laneButtons = new();
    private Transform[] lanePoints = Array.Empty<Transform>();

    private void Awake()
    {
        ResolveReferences();
    }

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

        Vector2 pointerScreenPosition;
        bool released;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        pointerScreenPosition = mouse.position.ReadValue();
        released = mouse.leftButton.wasReleasedThisFrame;
#else
        pointerScreenPosition = Input.mousePosition;
        released = Input.GetMouseButtonUp(0);
#endif
        if (!released || !TryScreenToWorld(pointerScreenPosition, out Vector2 worldPosition))
        {
            return;
        }

        for (int i = 0; i < laneButtons.Count; i++)
        {
            ButtonBinding button = laneButtons[i];
            if (button.Collider == null || !button.Collider.OverlapPoint(worldPosition))
            {
                continue;
            }

            laneIndex = Mathf.Max(1, button.LaneIndex);
            RefreshLaneButtonVisuals();
            return;
        }

        for (int i = 0; i < enemyButtons.Count; i++)
        {
            ButtonBinding button = enemyButtons[i];
            if (button.Collider == null ||
                button.Entry == null ||
                button.Entry.EnemyPrefab == null ||
                !button.Collider.OverlapPoint(worldPosition))
            {
                continue;
            }

            SpawnEnemy(button.Entry.EnemyPrefab);
            return;
        }
    }

    public void RefreshNow()
    {
        if (!LevelSceneModeRequest.IsSandbox)
        {
            return;
        }

        ResolveReferences();
        BuildButtons();
    }

    private void ResolveReferences()
    {
        if (buttonRoot == null)
        {
            buttonRoot = transform;
        }

        if (enemyButtonSlotsRoot == null)
        {
            Transform found = FindChild(buttonRoot, "Enemy Button Slots");
            enemyButtonSlotsRoot = found != null ? found : buttonRoot;
        }

        if (laneButtonSlotsRoot == null)
        {
            Transform found = FindChild(buttonRoot, "Lane Button Slots");
            laneButtonSlotsRoot = found != null ? found : buttonRoot;
        }

        if (lanePointParent == null && !string.IsNullOrWhiteSpace(lanePointParentName))
        {
            GameObject laneParentObject = GameObject.Find(lanePointParentName);
            if (laneParentObject != null)
            {
                lanePointParent = laneParentObject.transform;
            }
        }

        if (lanePointParent != null && lanePointParent.childCount > 0)
        {
            lanePoints = new Transform[lanePointParent.childCount];
            for (int i = 0; i < lanePointParent.childCount; i++)
            {
                lanePoints[i] = lanePointParent.GetChild(i);
            }

            Array.Sort(lanePoints, (a, b) => a.position.y.CompareTo(b.position.y));
        }
    }

    private void BuildButtons()
    {
        ClearButtons();
        if (buttonRoot == null)
        {
            return;
        }

        BuildLaneButtons();
        BuildEnemyCards();
    }

    private void BuildEnemyCards()
    {
        Transform[] slots = GetSlots(enemyButtonSlotsRoot, EnemySlotPrefix);
        if (slots.Length > 0)
        {
            int slotIndex = 0;
            for (int i = 0; i < enemies.Count && slotIndex < slots.Length; i++)
            {
                SandboxEnemyEntry entry = enemies[i];
                if (entry == null || entry.EnemyPrefab == null)
                {
                    continue;
                }

                ConfigureEnemySlot(slots[slotIndex], entry);
                slotIndex++;
            }

            for (int i = slotIndex; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(false);
                }
            }

            return;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            SandboxEnemyEntry entry = enemies[i];
            if (entry == null || entry.EnemyPrefab == null)
            {
                continue;
            }

            GameObject button = new($"Generated Enemy Button - {entry.DisplayName}");
            button.transform.SetParent(buttonRoot, false);
            int buttonIndex = enemyButtons.Count;
            int row = buttonIndex / Mathf.Max(1, cardsPerRow);
            int column = buttonIndex % Mathf.Max(1, cardsPerRow);
            button.transform.localPosition = firstButtonLocalPosition
                + Vector3.right * cardColumnSpacing * column
                + Vector3.down * cardRowSpacing * row;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one;

            SpriteRenderer background = button.AddComponent<SpriteRenderer>();
            background.sprite = cardBackgroundSprite;
            background.color = cardTint;
            background.sortingOrder = backgroundSortingOrder;
            FitSpriteRenderer(background, cardSize);

            GameObject iconObject = new("Enemy Icon");
            iconObject.transform.SetParent(button.transform, false);
            iconObject.transform.localPosition = iconLocalPosition;
            iconObject.transform.localRotation = Quaternion.identity;

            SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = GetEnemyPreviewSprite(entry.EnemyPrefab);
            icon.color = Color.white;
            icon.sortingOrder = iconSortingOrder;
            FitSpriteRenderer(icon, enemyIconSize);

            GameObject labelObject = new("Enemy Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = labelLocalPosition;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = FormatCardLabel(entry.DisplayName);
            text.fontSize = 96;
            text.characterSize = labelCharacterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = labelColor;

            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = labelSortingOrder;
            }

            BoxCollider2D collider = button.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = buttonColliderSize.sqrMagnitude > 0.001f ? buttonColliderSize : cardSize;
            collider.offset = Vector2.zero;

            enemyButtons.Add(ButtonBinding.ForEnemy(entry, collider, background));
        }
    }

    private void BuildLaneButtons()
    {
        if (!buildLaneSelector)
        {
            return;
        }

        int laneCount = Mathf.Max(1, lanePoints.Length > 0 ? lanePoints.Length : fallbackLaneCount);
        laneIndex = Mathf.Clamp(laneIndex, 1, laneCount);
        Transform[] slots = GetSlots(laneButtonSlotsRoot, LaneSlotPrefix);
        if (slots.Length > 0)
        {
            int count = Mathf.Min(laneCount, slots.Length);
            for (int i = 0; i < count; i++)
            {
                ConfigureLaneSlot(slots[i], i + 1);
            }

            for (int i = count; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(false);
                }
            }

            return;
        }

        for (int i = 0; i < laneCount; i++)
        {
            int selectableLaneIndex = i + 1;
            GameObject button = new($"Generated Lane Button - {selectableLaneIndex}");
            button.transform.SetParent(buttonRoot, false);
            button.transform.localPosition = firstLaneButtonLocalPosition + Vector3.right * laneButtonSpacing * i;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one;

            SpriteRenderer background = button.AddComponent<SpriteRenderer>();
            background.sprite = cardBackgroundSprite;
            background.color = selectableLaneIndex == laneIndex ? selectedLaneButtonTint : laneButtonTint;
            background.sortingOrder = backgroundSortingOrder;
            FitSpriteRenderer(background, laneButtonSize);

            GameObject labelObject = new("Lane Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;

            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = selectableLaneIndex.ToString();
            text.fontSize = 96;
            text.characterSize = Mathf.Max(0.045f, labelCharacterSize * 0.9f);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = labelColor;

            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = labelSortingOrder;
            }

            BoxCollider2D collider = button.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = laneButtonSize;
            collider.offset = Vector2.zero;

            laneButtons.Add(ButtonBinding.ForLane(selectableLaneIndex, collider, background));
        }
    }

    private void ClearButtons()
    {
        for (int i = buttonRoot != null ? buttonRoot.childCount - 1 : -1; i >= 0; i--)
        {
            Transform child = buttonRoot.GetChild(i);
            if (child != null && child.name.StartsWith("Generated Enemy Button -", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (child != null && child.name.StartsWith("Generated Lane Button -", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        DeactivateSlots(enemyButtonSlotsRoot, EnemySlotPrefix);
        DeactivateSlots(laneButtonSlotsRoot, LaneSlotPrefix);

        enemyButtons.Clear();
        laneButtons.Clear();
    }

    private void ConfigureEnemySlot(Transform slot, SandboxEnemyEntry entry)
    {
        if (slot == null || entry == null)
        {
            return;
        }

        slot.gameObject.SetActive(true);
        SpriteRenderer background = EnsureSpriteRenderer(slot);
        background.sprite = cardBackgroundSprite;
        background.color = cardTint;
        background.sortingOrder = backgroundSortingOrder;
        FitSpriteRenderer(background, cardSize);

        Transform iconTransform = EnsureChild(slot, "Enemy Icon");
        iconTransform.localPosition = iconLocalPosition;
        SpriteRenderer icon = EnsureSpriteRenderer(iconTransform);
        icon.sprite = GetEnemyPreviewSprite(entry.EnemyPrefab);
        icon.color = Color.white;
        icon.sortingOrder = iconSortingOrder;
        FitSpriteRenderer(icon, enemyIconSize);

        Transform labelTransform = EnsureChild(slot, "Enemy Label");
        labelTransform.localPosition = labelLocalPosition;
        TextMesh text = EnsureTextMesh(labelTransform);
        text.text = FormatCardLabel(entry.DisplayName);
        text.fontSize = 96;
        text.characterSize = labelCharacterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = labelColor;
        SetTextSortingOrder(labelTransform, labelSortingOrder);

        BoxCollider2D collider = EnsureCollider(slot);
        collider.size = buttonColliderSize.sqrMagnitude > 0.001f ? buttonColliderSize : cardSize;
        collider.offset = Vector2.zero;
        enemyButtons.Add(ButtonBinding.ForEnemy(entry, collider, background));
    }

    private void ConfigureLaneSlot(Transform slot, int selectableLaneIndex)
    {
        if (slot == null)
        {
            return;
        }

        slot.gameObject.SetActive(true);
        SpriteRenderer background = EnsureSpriteRenderer(slot);
        background.sprite = cardBackgroundSprite;
        background.color = selectableLaneIndex == laneIndex ? selectedLaneButtonTint : laneButtonTint;
        background.sortingOrder = backgroundSortingOrder;
        FitSpriteRenderer(background, laneButtonSize);

        Transform labelTransform = EnsureChild(slot, "Lane Label");
        labelTransform.localPosition = new Vector3(0f, 0f, -0.05f);
        TextMesh text = EnsureTextMesh(labelTransform);
        text.text = selectableLaneIndex.ToString();
        text.fontSize = 96;
        text.characterSize = Mathf.Max(0.045f, labelCharacterSize * 0.9f);
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = labelColor;
        SetTextSortingOrder(labelTransform, labelSortingOrder);

        BoxCollider2D collider = EnsureCollider(slot);
        collider.size = laneButtonSize;
        collider.offset = Vector2.zero;
        laneButtons.Add(ButtonBinding.ForLane(selectableLaneIndex, collider, background));
    }

    private void RefreshLaneButtonVisuals()
    {
        for (int i = 0; i < laneButtons.Count; i++)
        {
            ButtonBinding button = laneButtons[i];
            if (button.Background == null)
            {
                continue;
            }

            button.Background.color = button.LaneIndex == laneIndex ? selectedLaneButtonTint : laneButtonTint;
        }
    }

    private void SpawnEnemy(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
        {
            return;
        }

        Vector3 position = new(spawnX, GetLaneY(), spawnZ);
        GameObject enemy = Instantiate(enemyPrefab, position, enemyPrefab.transform.rotation);
        enemy.name = enemyPrefab.name;
    }

    private static Sprite GetEnemyPreviewSprite(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = enemyPrefab.GetComponentsInChildren<SpriteRenderer>(true);
        Sprite bestSprite = null;
        float bestArea = -1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.sprite != null)
            {
                Rect rect = renderer.sprite.rect;
                float area = rect.width * rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestSprite = renderer.sprite;
                }
            }
        }

        return bestSprite;
    }

    private static string FormatCardLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        return trimmed.Length > 9 ? trimmed.Replace(" ", "\n") : trimmed;
    }

    private static void FitSpriteRenderer(SpriteRenderer renderer, Vector2 targetSize)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
        {
            return;
        }

        renderer.transform.localScale = new Vector3(
            targetSize.x / spriteSize.x,
            targetSize.y / spriteSize.y,
            1f);
    }

    private static Transform[] GetSlots(Transform root, string prefix)
    {
        if (root == null)
        {
            return Array.Empty<Transform>();
        }

        List<Transform> slots = new();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                slots.Add(child);
            }
        }

        return slots.ToArray();
    }

    private static void DeactivateSlots(Transform root, string prefix)
    {
        Transform[] slots = GetSlots(root, prefix);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static SpriteRenderer EnsureSpriteRenderer(Transform target)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = target.gameObject.AddComponent<SpriteRenderer>();
        }

        return renderer;
    }

    private static TextMesh EnsureTextMesh(Transform target)
    {
        TextMesh text = target.GetComponent<TextMesh>();
        if (text == null)
        {
            text = target.gameObject.AddComponent<TextMesh>();
        }

        return text;
    }

    private static BoxCollider2D EnsureCollider(Transform target)
    {
        BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = target.gameObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        return collider;
    }

    private static void SetTextSortingOrder(Transform target, int sortingOrder)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private float GetLaneY()
    {
        if (lanePoints.Length == 0)
        {
            ResolveReferences();
        }

        if (lanePoints.Length == 0)
        {
            return 0f;
        }

        int clampedLaneIndex = Mathf.Clamp(laneIndex - 1, 0, lanePoints.Length - 1);
        return lanePoints[clampedLaneIndex].position.y;
    }

    private static bool TryScreenToWorld(Vector2 screenPosition, out Vector2 worldPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            worldPosition = default;
            return false;
        }

        Vector3 screenPoint = new(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(camera.transform.position.z));
        Vector3 worldPoint = camera.ScreenToWorldPoint(screenPoint);
        worldPosition = new Vector2(worldPoint.x, worldPoint.y);
        return true;
    }

    private readonly struct ButtonBinding
    {
        private ButtonBinding(SandboxEnemyEntry entry, int laneIndex, Collider2D collider, SpriteRenderer background)
        {
            Entry = entry;
            LaneIndex = laneIndex;
            Collider = collider;
            Background = background;
        }

        public static ButtonBinding ForEnemy(SandboxEnemyEntry entry, Collider2D collider, SpriteRenderer background)
        {
            return new ButtonBinding(entry, 0, collider, background);
        }

        public static ButtonBinding ForLane(int laneIndex, Collider2D collider, SpriteRenderer background)
        {
            return new ButtonBinding(null, laneIndex, collider, background);
        }

        public SandboxEnemyEntry Entry { get; }

        public int LaneIndex { get; }

        public Collider2D Collider { get; }

        public SpriteRenderer Background { get; }
    }
}
