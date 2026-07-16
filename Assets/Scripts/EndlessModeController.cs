using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EndlessModeController : MonoBehaviour
{
    private const string ProgressLayerKey = "BulletFoundry.Endless.NextLayer";

    [SerializeField]
    private LevelDefinition levelDefinition;

    [SerializeField]
    private LevelEnemySpawner normalSpawner;

    [SerializeField]
    private LevelResultPanelController resultPanel;

    [SerializeField]
    private Transform lanePointParent;

    [SerializeField]
    private Transform enemyParent;

    [SerializeField]
    private string lanePointParentName = "Shooter Lane Points";

    [SerializeField, Min(1)]
    private int baseEnemiesPerLayer = 3;

    [SerializeField, Min(0)]
    private int extraEnemiesPerLayer = 1;

    [SerializeField, Min(0.1f)]
    private float baseSpawnInterval = 1.5f;

    [SerializeField, Min(0.1f)]
    private float minimumSpawnInterval = 0.5f;

    [SerializeField, Min(0f)]
    private float spawnIntervalReductionPerLayer = 0.02f;

    [SerializeField, Min(0f)]
    private float initialPlacementDuration = 5f;

    [SerializeField]
    private float fallbackSpawnX = 8.5f;

    [SerializeField]
    private float spawnZ = 0f;

    [SerializeField, Min(0f)]
    private float healthGrowthPerLayer = 0.15f;

    [SerializeField, Min(0f)]
    private float speedGrowthPerLayer = 0.01f;

    [SerializeField, Min(1f)]
    private float maxSpeedMultiplier = 1.2f;

    private readonly List<GameObject> enemyPrefabs = new();
    private Transform[] lanes = new Transform[0];
    private int currentLayer = 1;
    private int enemiesToSpawn;
    private int enemiesSpawned;
    private float nextSpawnTime;
    private bool layerActive;
    private bool failureShown;
    private bool hasStartedRun;

    private void Awake()
    {
        if (!LevelSceneModeRequest.IsEndless)
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        CacheLanes();
        CacheEnemyPrefabs();
        if (normalSpawner != null)
        {
            normalSpawner.enabled = false;
        }
    }

    private void Start()
    {
        if (!LevelSceneModeRequest.IsEndless)
        {
            return;
        }

        currentLayer = Mathf.Max(1, PlayerPrefs.GetInt(ProgressLayerKey, 1));
        StartCoroutine(StartWhenCardsAreReady());
    }

    private void Update()
    {
        if (!LevelSceneModeRequest.IsEndless || !layerActive || CardSelectionMenu.IsOpen)
        {
            return;
        }

        if (IsFailureMet())
        {
            failureShown = true;
            layerActive = false;
            resultPanel?.ShowEndlessFailure();
            return;
        }

        if (enemiesSpawned < enemiesToSpawn && Time.time >= nextSpawnTime)
        {
            SpawnRandomEnemy();
            enemiesSpawned++;
            nextSpawnTime = Time.time + GetSpawnInterval();
        }

        if (enemiesSpawned >= enemiesToSpawn && !HasLivingEnemies())
        {
            CompleteLayer();
        }
    }

    private IEnumerator StartWhenCardsAreReady()
    {
        while (CardSelectionMenu.IsOpen)
        {
            yield return null;
        }

        yield return null;
        StartLayer();
    }

    private void StartLayer()
    {
        ResolveReferences();
        CacheLanes();
        CacheEnemyPrefabs();
        enemiesToSpawn = GetEnemyCountForLayer(currentLayer);
        enemiesSpawned = 0;
        nextSpawnTime = Time.time + (hasStartedRun ? 0f : initialPlacementDuration);
        hasStartedRun = true;
        failureShown = false;
        layerActive = enemyPrefabs.Count > 0 && lanes.Length > 0;
    }

    private void CompleteLayer()
    {
        layerActive = false;
        int clearedLayer = currentLayer;
        currentLayer++;
        PlayerPrefs.SetInt(ProgressLayerKey, currentLayer);
        PlayerPrefs.Save();
        resultPanel?.ShowEndlessLayerComplete(clearedLayer, StartLayer);
    }

    private int GetEnemyCountForLayer(int layer)
    {
        return Mathf.Max(1, baseEnemiesPerLayer + ((Mathf.Max(1, layer) - 1) * extraEnemiesPerLayer));
    }

    private float GetSpawnInterval()
    {
        float interval = baseSpawnInterval - ((Mathf.Max(1, currentLayer) - 1) * spawnIntervalReductionPerLayer);
        return Mathf.Max(minimumSpawnInterval, interval);
    }

    private float GetHealthMultiplier()
    {
        return 1f + ((Mathf.Max(1, currentLayer) - 1) * healthGrowthPerLayer);
    }

    private float GetSpeedMultiplier()
    {
        float multiplier = 1f + ((Mathf.Max(1, currentLayer) - 1) * speedGrowthPerLayer);
        return Mathf.Min(maxSpeedMultiplier, multiplier);
    }

    private void SpawnRandomEnemy()
    {
        if (enemyPrefabs.Count == 0 || lanes.Length == 0)
        {
            return;
        }

        IReadOnlyList<GameObject> availablePrefabs = GetEnemyPrefabsForLayer(currentLayer);
        if (availablePrefabs.Count == 0)
        {
            return;
        }

        GameObject enemyPrefab = availablePrefabs[Random.Range(0, availablePrefabs.Count)];
        int laneIndex = Random.Range(1, lanes.Length + 1);
        SpawnEnemy(enemyPrefab, laneIndex);
    }

    private void SpawnEnemy(GameObject enemyPrefab, int laneIndex)
    {
        if (enemyPrefab == null)
        {
            return;
        }

        int footLaneIndex = GetFootLaneIndex(enemyPrefab, laneIndex);
        float laneY = GetLaneY(laneIndex);
        float footLaneY = GetLaneY(footLaneIndex);
        float landBottomY = EnemySpawnAlignment.GetLandBottomYForLane(
            footLaneIndex,
            lanes,
            footLaneY);
        Vector3 position = new(fallbackSpawnX, laneY, spawnZ);
        GameObject enemy = EnemySpawnAlignment.InstantiateFootAligned(
            enemyPrefab,
            position,
            enemyPrefab.transform.rotation,
            enemyParent,
            landBottomY);
        enemy.name = enemyPrefab.name;

        GoblinEnemy goblin = enemy.GetComponent<GoblinEnemy>();
        if (goblin != null)
        {
            goblin.ApplyEndlessScaling(GetHealthMultiplier(), GetSpeedMultiplier());
        }
    }

    private bool IsFailureMet()
    {
        return !failureShown && HasEnemyBehindShooter();
    }

    private bool HasEnemyBehindShooter()
    {
        if (!TryGetShooterBounds(out Bounds shooterBounds))
        {
            return false;
        }

        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            GoblinEnemy enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            if (enemy.GetWorldBounds().max.x < shooterBounds.min.x)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLivingEnemies()
    {
        GoblinEnemy[] enemies = FindObjectsByType<GoblinEnemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetShooterBounds(out Bounds bounds)
    {
        GameObject shooter = GameObject.Find("Shooter");
        if (shooter == null)
        {
            bounds = default;
            return false;
        }

        TowerHealth health = shooter.GetComponent<TowerHealth>();
        if (health != null && !health.IsDestroyed)
        {
            bounds = health.GetWorldBounds();
            return true;
        }

        SpriteRenderer[] renderers = shooter.GetComponentsInChildren<SpriteRenderer>(true);
        bounds = renderers.Length > 0
            ? renderers[0].bounds
            : new Bounds(shooter.transform.position, Vector3.one);
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (levelDefinition == null)
        {
            levelDefinition = FindFirstObjectByType<LevelDefinition>();
        }

        if (normalSpawner == null)
        {
            normalSpawner = FindFirstObjectByType<LevelEnemySpawner>();
        }

        if (resultPanel == null)
        {
            resultPanel = FindFirstObjectByType<LevelResultPanelController>(FindObjectsInactive.Include);
        }

        if (lanePointParent == null && !string.IsNullOrWhiteSpace(lanePointParentName))
        {
            GameObject laneParentObject = GameObject.Find(lanePointParentName);
            if (laneParentObject != null)
            {
                lanePointParent = laneParentObject.transform;
            }
        }
    }

    private void CacheEnemyPrefabs()
    {
        enemyPrefabs.Clear();
        if (levelDefinition == null)
        {
            return;
        }

        IReadOnlyList<GameObject> catalog = levelDefinition.GetEnemyPrefabCatalog();
        HashSet<GameObject> unique = new();
        for (int i = 0; i < catalog.Count; i++)
        {
            GameObject prefab = catalog[i];
            if (prefab != null && unique.Add(prefab))
            {
                enemyPrefabs.Add(prefab);
            }
        }
    }

    private void CacheLanes()
    {
        ShooterLanePointMarker[] laneMarkers = lanePointParent != null
            ? lanePointParent.GetComponentsInChildren<ShooterLanePointMarker>(true)
            : FindObjectsByType<ShooterLanePointMarker>(FindObjectsSortMode.None);

        lanes = laneMarkers
            .Where(marker => marker != null)
            .Select(marker => marker.transform)
            .OrderBy(lane => lane.position.y)
            .ToArray();

        if (lanes.Length > 0)
        {
            return;
        }

        Debug.LogWarning("Endless Mode could not find any Shooter Lane Point Markers.", this);
    }

    private IReadOnlyList<GameObject> GetEnemyPrefabsForLayer(int layer)
    {
        int safeLayer = Mathf.Max(1, layer);
        List<GameObject> available = new();

        for (int i = 0; i < enemyPrefabs.Count; i++)
        {
            GameObject prefab = enemyPrefabs[i];
            if (prefab != null && IsEnemyUnlockedForLayer(prefab.name, safeLayer))
            {
                available.Add(prefab);
            }
        }

        // A custom catalog should still be playable even if it contains an unknown enemy name.
        return available.Count > 0 ? available : enemyPrefabs;
    }

    private static bool IsEnemyUnlockedForLayer(string enemyName, int layer)
    {
        return enemyName switch
        {
            "Goblin" => true,
            "SpeedGoblin" => layer >= 3,
            "Chicken" => layer >= 4,
            "Barbarian" => layer >= 6,
            "PigLeader" => layer >= 9,
            "FrogPrincess" => layer >= 10,
            "Giant" => layer >= 14,
            _ => layer >= 8
        };
    }

    private float GetLaneY(int targetLaneIndex)
    {
        if (lanes.Length == 0)
        {
            return 0f;
        }

        int laneIndex = Mathf.Clamp(targetLaneIndex - 1, 0, lanes.Length - 1);
        return lanes[laneIndex].position.y;
    }

    private int GetFootLaneIndex(GameObject enemyPrefab, int targetLaneIndex)
    {
        int laneCount = Mathf.Max(1, lanes.Length);
        int offset = 0;
        if (enemyPrefab != null && enemyPrefab.TryGetComponent(out GoblinEnemy enemy))
        {
            offset = enemy.SpawnFootLaneOffset;
        }

        return Mathf.Clamp(targetLaneIndex + offset, 1, laneCount);
    }

}
