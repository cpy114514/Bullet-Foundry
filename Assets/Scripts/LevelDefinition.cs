using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class LevelEnemySpawn
{
    [SerializeField, Min(0f)]
    private float spawnTime;

    [SerializeField]
    private GameObject enemyPrefab;

    [SerializeField, Min(1)]
    private int laneIndex = 3;

    [SerializeField]
    private float spawnX = 8.5f;

    [SerializeField]
    private Vector3 offset;

    public float SpawnTime => Mathf.Max(0f, spawnTime);

    public GameObject EnemyPrefab => enemyPrefab;

    public int LaneIndex => Mathf.Max(1, laneIndex);

    public float SpawnX => spawnX;

    public Vector3 Offset => offset;

    public LevelEnemySpawn(
        float time,
        GameObject prefab,
        int lane,
        float x,
        Vector3 spawnOffset)
    {
        spawnTime = Mathf.Max(0f, time);
        enemyPrefab = prefab;
        laneIndex = Mathf.Max(1, lane);
        spawnX = x;
        offset = spawnOffset;
    }
}

[Serializable]
public sealed class LevelEnemyPrefabEntry
{
    [SerializeField]
    private string id;

    [SerializeField]
    private GameObject prefab;

    public string Id => !string.IsNullOrWhiteSpace(id)
        ? id.Trim()
        : prefab != null ? prefab.name : string.Empty;

    public GameObject Prefab => prefab;
}

[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class LevelDefinition : MonoBehaviour
{
    [Header("JSON")]
    [SerializeField]
    private TextAsset defaultLevelJson;

    [Header("Level")]
    [SerializeField]
    private string levelName = "Level 1";

    [SerializeField, Min(0)]
    private int startingCoins = 75;

    [Header("Cards")]
    [SerializeField]
    private bool showCardSelectionOnStart = true;

    [SerializeField]
    private bool waitForCardSelectionBeforeLoadingCards = true;

    [SerializeField]
    private bool restrictAvailableCards;

    [SerializeField]
    private List<GameObject> allowedCardPrefabs = new();

    [SerializeField]
    private List<GameObject> bannedCardPrefabs = new();

    [Header("Enemy Timeline")]
    [SerializeField]
    private List<LevelEnemySpawn> enemySpawns = new();

    [SerializeField]
    private List<LevelEnemyPrefabEntry> enemyPrefabCatalog = new();

    [Header("Runtime")]
    [SerializeField]
    private CoinWallet wallet;

    private readonly List<string> jsonAllowedCardNames = new();
    private readonly List<string> jsonBannedCardNames = new();
    private bool useJsonCardRules;

    public string LevelName => levelName;

    public int StartingCoins => Mathf.Max(0, startingCoins);

    public bool RestrictAvailableCards => restrictAvailableCards;

    public bool ShowCardSelectionOnStart => showCardSelectionOnStart;

    public IReadOnlyList<LevelEnemySpawn> EnemySpawns => enemySpawns;

    public string LoadedJsonSource { get; private set; }

    private void Awake()
    {
        string json = LevelLoadRequest.HasJson
            ? LevelLoadRequest.Json
            : defaultLevelJson != null ? defaultLevelJson.text : null;
        string source = LevelLoadRequest.HasJson
            ? LevelLoadRequest.Source
            : defaultLevelJson != null ? defaultLevelJson.name : string.Empty;

        if (!string.IsNullOrWhiteSpace(json) && !TryApplyJson(json, source, out string error))
        {
            Debug.LogError($"Could not load level JSON from {source}: {error}", this);
        }
    }

    private void Start()
    {
        ApplyStartingCoins();
        TryShowCardSelection();
    }

    public void ApplyStartingCoins()
    {
        CoinWallet targetWallet = wallet != null
            ? wallet
            : CoinWallet.Instance != null
                ? CoinWallet.Instance
                : FindFirstObjectByType<CoinWallet>();

        if (targetWallet != null)
        {
            targetWallet.SetStartingCoins(StartingCoins);
        }
    }

    public IReadOnlyCollection<string> GetAvailableTowerNames(
        IReadOnlyList<CardEntry> catalogCards,
        IReadOnlyCollection<string> selectedTowerNames)
    {
        HashSet<string> towerNames = BuildInitialTowerNameSet(catalogCards, selectedTowerNames);

        if (restrictAvailableCards)
        {
            HashSet<string> allowedNames = useJsonCardRules
                ? new HashSet<string>(jsonAllowedCardNames)
                : BuildPrefabNameSet(allowedCardPrefabs);
            towerNames.IntersectWith(allowedNames);
        }

        HashSet<string> bannedNames = useJsonCardRules
            ? new HashSet<string>(jsonBannedCardNames)
            : BuildPrefabNameSet(bannedCardPrefabs);
        towerNames.ExceptWith(bannedNames);
        return towerNames.ToList();
    }

    public bool HasCardRules()
    {
        if (useJsonCardRules)
        {
            return restrictAvailableCards ||
                jsonAllowedCardNames.Count > 0 ||
                jsonBannedCardNames.Count > 0;
        }

        return restrictAvailableCards ||
            ContainsValidPrefab(bannedCardPrefabs) ||
            ContainsValidPrefab(allowedCardPrefabs);
    }

    public bool ShouldDelayCardRuntimeLoad()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return showCardSelectionOnStart &&
            waitForCardSelectionBeforeLoadingCards &&
            !CardSelectionState.IsSelectionConfirmedForScene(sceneName);
    }

    private void TryShowCardSelection()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!showCardSelectionOnStart ||
            CardSelectionState.IsSelectionConfirmedForScene(sceneName))
        {
            return;
        }

        CardSelectionMenu.Show(sceneName);
    }

    public bool TryApplyJson(string json, string source, out string error)
    {
        if (!LevelJsonUtility.TryParse(json, out LevelJsonData data, out error))
        {
            return false;
        }

        Dictionary<string, GameObject> enemyLookup = BuildEnemyPrefabLookup();
        List<LevelEnemySpawn> parsedSpawns = new();
        for (int i = 0; i < data.enemySpawns.Count; i++)
        {
            LevelEnemySpawnJson spawn = data.enemySpawns[i];
            if (!enemyLookup.TryGetValue(spawn.enemy, out GameObject enemyPrefab) || enemyPrefab == null)
            {
                Debug.LogWarning(
                    $"Level '{data.displayName}' skipped unknown enemy '{spawn.enemy}'. " +
                    "Add it to LevelDefinition > Enemy Prefab Catalog.",
                    this);
                continue;
            }

            parsedSpawns.Add(new LevelEnemySpawn(
                spawn.time,
                enemyPrefab,
                spawn.lane,
                spawn.spawnX,
                spawn.offset.ToVector3()));
        }

        levelName = data.displayName;
        startingCoins = data.startingCoins;
        showCardSelectionOnStart = data.showCardSelectionOnStart;
        waitForCardSelectionBeforeLoadingCards = data.waitForCardSelectionBeforeLoadingCards;
        restrictAvailableCards = data.cardRules.restrictAvailableCards;
        enemySpawns = parsedSpawns;

        jsonAllowedCardNames.Clear();
        jsonAllowedCardNames.AddRange(data.cardRules.allowedCards);
        jsonBannedCardNames.Clear();
        jsonBannedCardNames.AddRange(data.cardRules.bannedCards);
        useJsonCardRules = true;
        LoadedJsonSource = source;
        error = string.Empty;
        return true;
    }

    private Dictionary<string, GameObject> BuildEnemyPrefabLookup()
    {
        Dictionary<string, GameObject> lookup = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < enemyPrefabCatalog.Count; i++)
        {
            LevelEnemyPrefabEntry entry = enemyPrefabCatalog[i];
            if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            lookup[entry.Id] = entry.Prefab;
            lookup[entry.Prefab.name] = entry.Prefab;
        }

        for (int i = 0; i < enemySpawns.Count; i++)
        {
            GameObject prefab = enemySpawns[i]?.EnemyPrefab;
            if (prefab != null)
            {
                lookup[prefab.name] = prefab;
            }
        }

        return lookup;
    }

    private static HashSet<string> BuildInitialTowerNameSet(
        IReadOnlyList<CardEntry> catalogCards,
        IReadOnlyCollection<string> selectedTowerNames)
    {
        if (selectedTowerNames != null)
        {
            return new HashSet<string>(selectedTowerNames);
        }

        HashSet<string> towerNames = new();
        if (catalogCards == null)
        {
            return towerNames;
        }

        for (int i = 0; i < catalogCards.Count; i++)
        {
            GameObject towerPrefab = catalogCards[i]?.TowerPrefab;
            if (towerPrefab != null)
            {
                towerNames.Add(towerPrefab.name);
            }
        }

        return towerNames;
    }

    private static HashSet<string> BuildPrefabNameSet(IReadOnlyList<GameObject> prefabs)
    {
        HashSet<string> names = new();
        if (prefabs == null)
        {
            return names;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
            {
                names.Add(prefabs[i].name);
            }
        }

        return names;
    }

    private static bool ContainsValidPrefab(IReadOnlyList<GameObject> prefabs)
    {
        if (prefabs == null)
        {
            return false;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        startingCoins = Mathf.Max(0, startingCoins);
    }
}
