using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CoinWallet : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int startingCoins = 15;

    [SerializeField, Min(0)]
    private int currentCoins;

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugCoinKey = true;

    [SerializeField, Min(1)]
    private int debugCoinsPerPress = 100;

    public static CoinWallet Instance { get; private set; }

    public int CurrentCoins => currentCoins;

    public event Action<int> CoinsChanged;

    private void Awake()
    {
        Instance = this;
        currentCoins = Mathf.Max(0, startingCoins);
        CoinsChanged?.Invoke(currentCoins);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!enableDebugCoinKey)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
        {
            AddCoins(debugCoinsPerPress);
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

    public bool CanAfford(int amount)
    {
        return amount <= 0 || currentCoins >= amount;
    }

    public bool TrySpendCoins(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (!CanAfford(amount))
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        currentCoins -= amount;
        CoinsChanged?.Invoke(currentCoins);
        return true;
    }

    private void OnValidate()
    {
        startingCoins = Mathf.Max(0, startingCoins);
        currentCoins = Mathf.Max(0, currentCoins);
        debugCoinsPerPress = Mathf.Max(1, debugCoinsPerPress);
    }
}
