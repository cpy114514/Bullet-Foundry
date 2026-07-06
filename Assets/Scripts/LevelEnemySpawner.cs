using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelEnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private LevelDefinition levelDefinition;

    [SerializeField]
    private Transform lanePointParent;

    [SerializeField]
    private Transform enemyParent;

    [Header("Fallback")]
    [SerializeField]
    private string lanePointParentName = "Shooter Lane Points";

    [SerializeField]
    private float fallbackSpawnX = 8.5f;

    [SerializeField]
    private float spawnZ = 0f;

    private readonly List<LevelEnemySpawn> spawnQueue = new();
    private Transform[] lanes = new Transform[0];
    private float elapsedTime;
    private int nextSpawnIndex;

    private void Awake()
    {
        ResolveReferences();
        CacheSpawnQueue();
        CacheLanes();
    }

    private void OnEnable()
    {
        elapsedTime = 0f;
        nextSpawnIndex = 0;
    }

    private void Update()
    {
        if (spawnQueue.Count == 0 || nextSpawnIndex >= spawnQueue.Count)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        while (nextSpawnIndex < spawnQueue.Count &&
            elapsedTime >= spawnQueue[nextSpawnIndex].SpawnTime)
        {
            Spawn(spawnQueue[nextSpawnIndex]);
            nextSpawnIndex++;
        }
    }

    private void ResolveReferences()
    {
        if (levelDefinition == null)
        {
            levelDefinition = GetComponent<LevelDefinition>();
        }

        if (levelDefinition == null)
        {
            levelDefinition = FindFirstObjectByType<LevelDefinition>();
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

    private void CacheSpawnQueue()
    {
        spawnQueue.Clear();
        if (levelDefinition == null || levelDefinition.EnemySpawns == null)
        {
            return;
        }

        spawnQueue.AddRange(levelDefinition.EnemySpawns
            .Where(spawn => spawn != null && spawn.EnemyPrefab != null)
            .OrderBy(spawn => spawn.SpawnTime));
    }

    private void CacheLanes()
    {
        if (lanePointParent != null && lanePointParent.childCount > 0)
        {
            lanes = new Transform[lanePointParent.childCount];
            for (int i = 0; i < lanePointParent.childCount; i++)
            {
                lanes[i] = lanePointParent.GetChild(i);
            }

            lanes = lanes
                .Where(lane => lane != null)
                .OrderBy(lane => lane.position.y)
                .ToArray();
            return;
        }

        lanes = FindObjectsByType<ShooterLanePointMarker>(FindObjectsSortMode.None)
            .Select(marker => marker.transform)
            .Where(transform => transform != null)
            .OrderBy(transform => transform.position.y)
            .ToArray();
    }

    private void Spawn(LevelEnemySpawn spawn)
    {
        if (spawn == null || spawn.EnemyPrefab == null)
        {
            return;
        }

        Vector3 position = GetLaneSpawnPosition(spawn);
        GameObject enemy = Instantiate(
            spawn.EnemyPrefab,
            position,
            spawn.EnemyPrefab.transform.rotation,
            enemyParent);

        enemy.name = spawn.EnemyPrefab.name;
    }

    private Vector3 GetLaneSpawnPosition(LevelEnemySpawn spawn)
    {
        float laneY = 0f;
        if (lanes.Length > 0)
        {
            int laneIndex = Mathf.Clamp(spawn.LaneIndex - 1, 0, lanes.Length - 1);
            laneY = lanes[laneIndex].position.y;
        }

        float spawnX = Mathf.Approximately(spawn.SpawnX, 0f)
            ? fallbackSpawnX
            : spawn.SpawnX;

        Vector3 position = new(spawnX, laneY, spawnZ);
        position += spawn.Offset;
        return position;
    }
}
