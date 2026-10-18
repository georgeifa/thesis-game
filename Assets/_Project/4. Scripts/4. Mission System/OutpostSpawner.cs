using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Feeds enemies into an objective's zone while that objective is active.
// Spawns a group up front, then tops up on a trickle as they die.
//
// This is NOT the director and doesn't try to be - it spawns at authored
// points and keeps a count. Week 3 either drives it from the director or
// replaces it. It exists because "clear the outpost" can't complete without
// something putting enemies in the outpost.
public class OutpostSpawner : MonoBehaviour
{
    [Header("Links")]
    [Tooltip("Spawns only while this is Active. Falls back to one on this object.")]
    [SerializeField] MissionObjective objective;
    [SerializeField] Transform player;

    [Header("What to spawn")]
    [SerializeField] List<EnemyScriptableObject> enemies = new List<EnemyScriptableObject>();
    [SerializeField] List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] int poolSize = 10;

    [Header("Pacing")]
    [Tooltip("Spawned all at once the moment the objective activates.")]
    [SerializeField] int initialGroup = 5;
    [Tooltip("Stops topping up above this many alive at once.")]
    [SerializeField] int maxAlive = 6;
    [SerializeField] float minInterval = 3f;
    [SerializeField] float maxInterval = 5f;

    readonly List<Enemy> spawned = new List<Enemy>();

    bool wasActive;
    float nextSpawnIn;

    public int Alive => spawned.Count;

    void Awake()
    {
        if (objective == null) objective = GetComponent<MissionObjective>();
    }

    void Start()
    {
        if (objective == null)
            Debug.LogError($"{name}: no objective - this spawner will never run.");

        if (spawnPoints.Count == 0)
            Debug.LogError($"{name}: no spawn points assigned.");
    }

    void Update()
    {
        if (objective == null || player == null) return;

        bool active = objective.State == ObjectiveState.Active;

        // Polled rather than subscribed, same as everything else here.
        if (active && !wasActive)
        {
            for (int i = 0; i < initialGroup; i++) Spawn();
            nextSpawnIn = Random.Range(minInterval, maxInterval);
        }
        wasActive = active;

        if (!active) return;

        PruneDead();

        if (spawned.Count >= maxAlive) return;

        nextSpawnIn -= Time.deltaTime;
        if (nextSpawnIn > 0f) return;

        Spawn();
        nextSpawnIn = Random.Range(minInterval, maxInterval);
    }

    // A pooled enemy deactivates itself on death, so "still active" is the
    // whole liveness test. No subscription to OnDeath, nothing to unhook.
    void PruneDead()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            Enemy e = spawned[i];
            if (e == null || !e.gameObject.activeSelf) spawned.RemoveAt(i);
        }
    }

    void Spawn()
    {
        if (enemies.Count == 0 || spawnPoints.Count == 0) return;

        EnemyScriptableObject config = enemies[Random.Range(0, enemies.Count)];

        // Cheap now that CreateInstance is a dictionary lookup that validates
        // and rebuilds a stale pool - no reason to cache the handle.
        ObjectPool pool = ObjectPool.CreateInstance(config.Prefab, poolSize);
        if (pool == null) return;

        PoolableObject obj = pool.GetObject();
        if (obj == null)
        {
            Debug.LogWarning($"{name}: pool exhausted for {config.name}.");
            return;
        }

        Enemy enemy = obj.GetComponent<Enemy>();
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];

        // Place first, configure second. ResetForSpawn skips every NavMesh call
        // when the agent isn't on the mesh, and a pooled enemy sits wherever it
        // sank until it's warped - so configuring first silently does nothing.
        if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogError($"{name}: spawn point '{point.name}' isn't near the NavMesh.");
            obj.gameObject.SetActive(false);
            return;
        }

        enemy.Agent.enabled = true;
        enemy.Agent.Warp(hit.position);

        config.SetupEnemy(enemy, player.gameObject);

        spawned.Add(enemy);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        foreach (Transform p in spawnPoints)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.position, 0.6f);
            Gizmos.DrawLine(p.position, p.position + Vector3.up * 2f);
        }
    }
}