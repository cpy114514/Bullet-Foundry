using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GoblinEnemy))]
public sealed class PigLeaderSummoner : MonoBehaviour
{
    [SerializeField]
    private GoblinEnemy enemy;

    [SerializeField]
    private GameObject goblinPrefab;

    [SerializeField, Min(0.1f)]
    private float summonInterval = 7f;

    [SerializeField, Min(0f)]
    private float firstSummonDelay = 3f;

    [SerializeField, Min(0f)]
    private float spawnDelay = 0.55f;

    [SerializeField, Min(0.05f)]
    private float summonActionDuration = 1.1f;

    [SerializeField, Min(1)]
    private int maxActiveSummons = 3;

    [SerializeField]
    private Vector3 spawnOffset = new Vector3(0.75f, 0f, 0f);

    [SerializeField]
    private string summonStateName = "pigleader_summon";

    private readonly List<GameObject> activeSummons = new();
    private float nextSummonTime;
    private bool isSummoning;

    private void Awake()
    {
        ResolveEnemy();
    }

    private void OnEnable()
    {
        ResolveEnemy();
        isSummoning = false;
        nextSummonTime = Time.time + firstSummonDelay;
        activeSummons.Clear();
    }

    private void Update()
    {
        RemoveDestroyedSummons();
        if (enemy == null || enemy.IsActionBlocked || isSummoning ||
            goblinPrefab == null || activeSummons.Count >= maxActiveSummons ||
            Time.time < nextSummonTime)
        {
            return;
        }

        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        isSummoning = true;
        nextSummonTime = Time.time + summonInterval;
        enemy.PlayTemporaryAction(summonStateName, summonActionDuration);

        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        if (enemy != null && !enemy.IsActionBlocked && goblinPrefab != null)
        {
            float targetFootY = EnemySpawnAlignment.GetFootY(gameObject) + spawnOffset.y;
            GameObject summoned = EnemySpawnAlignment.InstantiateFootAligned(
                goblinPrefab,
                transform.position + spawnOffset,
                transform.rotation,
                transform.parent,
                targetFootY);
            activeSummons.Add(summoned);
        }

        float remainingDuration = Mathf.Max(0f, summonActionDuration - spawnDelay);
        if (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        isSummoning = false;
    }

    private void ResolveEnemy()
    {
        if (enemy == null)
        {
            enemy = GetComponent<GoblinEnemy>();
        }
    }

    private void RemoveDestroyedSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
            {
                activeSummons.RemoveAt(i);
            }
        }
    }
}
