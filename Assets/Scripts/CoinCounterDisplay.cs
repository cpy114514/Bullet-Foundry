using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinCounterDisplay : MonoBehaviour
{
    [SerializeField]
    private TextMesh valueText;

    private CoinWallet wallet;

    private void OnEnable()
    {
        ResolveReferences();
        if (wallet != null)
        {
            wallet.CoinsChanged += UpdateDisplay;
            UpdateDisplay(wallet.CurrentCoins);
        }
    }

    private void OnDisable()
    {
        if (wallet != null)
        {
            wallet.CoinsChanged -= UpdateDisplay;
        }
    }

    private void ResolveReferences()
    {
        if (valueText == null)
        {
            valueText = GetComponent<TextMesh>();
        }

        wallet = CoinWallet.Instance;
        if (wallet == null)
        {
            wallet = FindFirstObjectByType<CoinWallet>();
        }
    }

    private void UpdateDisplay(int coins)
    {
        if (valueText != null)
        {
            valueText.text = Mathf.Max(0, coins).ToString();
        }
    }
}
