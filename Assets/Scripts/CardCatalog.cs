using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CardEntry
{
    [SerializeField]
    private Sprite image;

    [SerializeField]
    [TextArea(2, 5)]
    private string displayName;

    public Sprite Image => image;

    public string DisplayName => displayName;
}

[DisallowMultipleComponent]
public sealed class CardCatalog : MonoBehaviour
{
    [SerializeField]
    private List<CardEntry> cards = new List<CardEntry>();

    public IReadOnlyList<CardEntry> Cards => cards;
}
