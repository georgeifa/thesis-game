using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    public Transform Player;
    public int NumberOfEnemiesToSpawn = 5;
    public float SpawnDelay = 1f;
    public List<EnemyScriptableObject> Enemies = new List<EnemyScriptableObject>();
    private NavMeshTriangulation triangulation;
    private Dictionary<int,ObjectPool> EnemyObjectPools = new Dictionary<int, ObjectPool>();

    [Header("Debug")]
    [SerializeField] private bool enableRespawnEnemies = true;
    [SerializeField] private KeyCode RespawnEnemiesKey = KeyCode.F1;


    void Awake()
    {
        for(int i=0; i<Enemies.Count; i++)
        {
            EnemyObjectPools.Add(i, ObjectPool.CreateInstance(Enemies[i].Prefab, NumberOfEnemiesToSpawn));
        }
    }

    void Start()
    {
        triangulation  = NavMesh.CalculateTriangulation();
        StartCoroutine(SpawnEnemies());
    }

    void Update()
    {
        if (enableRespawnEnemies && Input.GetKeyDown(RespawnEnemiesKey))
            StartCoroutine(SpawnEnemies());
    }

    private IEnumerator SpawnEnemies()
    {
        WaitForSeconds wait = new WaitForSeconds(SpawnDelay);

        int SpawnedEnemies = 0;

        while(SpawnedEnemies < NumberOfEnemiesToSpawn)
        {
            SpawnEnemy();
            SpawnedEnemies++;

            yield return wait;
        } 
    }

    private void SpawnEnemy()
    {
        int index = Random.Range(0, Enemies.Count);
        PoolableObject poolableObject = EnemyObjectPools[index].GetObject();

        if (poolableObject == null)
        {
            Debug.LogError($"Unable to fetch enemy of type {index} from the pool. Out of objects?");
            return;
        }

        Enemy enemy = poolableObject.GetComponent<Enemy>();

        // ── 1. PLACE FIRST ───────────────────────────────────────────────────
        // Configuration has to happen at the FINAL position: ResetForSpawn skips
        // every NavMesh call when the agent isn't on the mesh, and a pooled enemy
        // sits underground where the sink left it until it's warped.
        int vertexIndex = Random.Range(0, triangulation.vertices.Length);

        if (!NavMesh.SamplePosition(triangulation.vertices[vertexIndex], out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Debug.LogError($"Unable to place NavMeshAgent on NavMesh at {triangulation.vertices[vertexIndex]}");
            poolableObject.gameObject.SetActive(false);   // give it back rather than leaking it
            return;
        }

        enemy.Agent.enabled = true;      // Warp on a disabled agent silently fails
        enemy.Agent.Warp(hit.position);

        // ── 2. THEN CONFIGURE ────────────────────────────────────────────────
        Enemies[index].SetupEnemy(enemy, Player.gameObject);
    }
}
