using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinWallet : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int startingCoins;

    [SerializeField, Min(0)]
    private int currentCoins;

    public static CoinWallet Instance { get; private set; }

    public int CurrentCoins => currentCoins;

    public event Action<int> CoinsChanged;

    private void Awake()
    {
        Instance = this;
        currentCoins = Mathf.Max(0, startingCoins);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentCoins += amount;
        CoinsChanged?.Invoke(currentCoins);
    }
}
