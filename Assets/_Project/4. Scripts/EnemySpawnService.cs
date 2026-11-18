using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The one place an enemy gets put into the world. The garrison, the Director
// and the outpost all go through SpawnAt, so place-before-configure exists
// once instead of three times.
//
// Replaces the old EnemySpawner, which picked a random NavMesh vertex - any
// walkable point on the map, including two metres behind the player.
public class EnemySpawnService : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] List<SpawnZone> zones = new List<SpawnZone>();

    [Tooltip("Reinforcements never appear closer than this. Wants to be past the alerted FOV radius so they don't arrive already hunting.")]
    [SerializeField] float minDistanceFromPlayer = 25f;
    [SerializeField] int defaultPoolSize = 10;

    Camera cam;

    readonly List<SpawnZone> ideal = new List<SpawnZone>();
    readonly List<SpawnZone> acceptable = new List<SpawnZone>();

    public Transform Player => player;

    void Start()
    {
        if (player == null)
            Debug.LogError($"{name}: no player assigned - nothing will spawn.");
    }

    // For reinforcements. Picks a zone that's far enough away, preferring ones
    // off screen. If there's nowhere legitimate it spawns NOTHING rather than
    // something badly placed.
    public Enemy SpawnInZones(EnemyScriptableObject config)
    {
        SpawnZone zone = PickZone();
        if (zone == null)
        {
            Debug.LogWarning($"{name}: no valid spawn zone - all too close to the player, or none assigned.");
            return null;
        }

        if (!zone.TryGetPoint(out Vector3 point))
        {
            Debug.LogWarning($"{name}: zone '{zone.name}' has no NavMesh under it.");
            return null;
        }

        return SpawnAt(config, point);
    }

    SpawnZone PickZone()
    {
        if (player == null) return null;
        if (cam == null) cam = Camera.main;

        ideal.Clear();
        acceptable.Clear();

        foreach (SpawnZone z in zones)
        {
            if (z == null) continue;

            switch (z.Evaluate(player.position, minDistanceFromPlayer, cam))
            {
                case SpawnZone.Validity.Ideal:      ideal.Add(z); break;
                case SpawnZone.Validity.Acceptable: acceptable.Add(z); break;
            }
        }

        if (ideal.Count > 0)      return ideal[Random.Range(0, ideal.Count)];
        if (acceptable.Count > 0) return acceptable[Random.Range(0, acceptable.Count)];
        return null;
    }

    // Everything that puts an enemy in the world ends up here.
    public Enemy SpawnAt(EnemyScriptableObject config, Vector3 position, int poolSize = 0)
    {
        if (config == null || player == null) return null;

        // Cheap - CreateInstance is a lookup that rebuilds a stale pool.
        ObjectPool pool = ObjectPool.CreateInstance(config.Prefab, poolSize > 0 ? poolSize : defaultPoolSize);
        if (pool == null) return null;

        PoolableObject obj = pool.GetObject();
        if (obj == null)
        {
            Debug.LogWarning($"{name}: pool exhausted for {config.name}.");
            return null;
        }

        Enemy enemy = obj.GetComponent<Enemy>();
        if (enemy == null)
        {
            obj.gameObject.SetActive(false);
            return null;
        }

        // Place first, configure second. ResetForSpawn skips every NavMesh call
        // when the agent isn't on the mesh, so configuring first silently does
        // nothing and the enemy keeps its last life's path.
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            Debug.LogError($"{name}: no NavMesh near {position}.");
            obj.gameObject.SetActive(false);
            return null;
        }

        enemy.Agent.enabled = true;
        enemy.Agent.Warp(hit.position);

        config.SetupEnemy(enemy, player.gameObject);

        return enemy;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        GizmoDraw.FlatCircle(player.position, minDistanceFromPlayer);
    }
}