using System.Collections.Generic;
using UnityEngine;

public static class CardSelectionState
{
    private static readonly List<string> selectedTowerNames = new();
    private static bool selectionConfirmed;

    public static string TargetSceneName { get; private set; } = "Level_test";

    public static IReadOnlyList<string> SelectedTowerNames => selectedTowerNames;

    public static bool HasSelection => selectedTowerNames.Count > 0;

    public static bool SelectionConfirmed => selectionConfirmed;

    public static bool IsSelectionConfirmedForScene(string sceneName)
    {
        return selectionConfirmed &&
            !string.IsNullOrWhiteSpace(sceneName) &&
            string.Equals(TargetSceneName, sceneName, System.StringComparison.Ordinal);
    }

    public static void PrepareLevelLoad(string targetSceneName)
    {
        selectedTowerNames.Clear();
        selectionConfirmed = false;
        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            TargetSceneName = targetSceneName;
        }
    }

    public static void BeginSelection(string targetSceneName)
    {
        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            TargetSceneName = targetSceneName;
        }

        selectionConfirmed = false;
    }

    public static void SetSelection(IEnumerable<CardView> selectedCards)
    {
        selectedTowerNames.Clear();
        if (selectedCards == null)
        {
            return;
        }

        foreach (CardView card in selectedCards)
        {
            GameObject towerPrefab = card != null ? card.TowerPrefab : null;
            if (towerPrefab == null)
            {
                continue;
            }

            string towerName = towerPrefab.name;
            if (!string.IsNullOrWhiteSpace(towerName) &&
                !selectedTowerNames.Contains(towerName))
            {
                selectedTowerNames.Add(towerName);
            }
        }
    }

    public static void ConfirmSelection(IEnumerable<CardView> selectedCards)
    {
        SetSelection(selectedCards);
        selectionConfirmed = true;
    }

    public static bool ContainsTower(GameObject towerPrefab)
    {
        return towerPrefab != null && selectedTowerNames.Contains(towerPrefab.name);
    }

    public static void ClearSelection()
    {
        selectedTowerNames.Clear();
    }

    public static void ClearAll()
    {
        selectedTowerNames.Clear();
        selectionConfirmed = false;
    }
}
