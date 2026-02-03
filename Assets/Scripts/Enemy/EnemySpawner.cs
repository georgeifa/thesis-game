using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using Unity.Behavior;
using Unity.VisualScripting;
using UnityEditor.Analytics;
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
        int RandomEnemyIndex = Random.Range(0, Enemies.Count);

        PoolableObject poolableObject = EnemyObjectPools[RandomEnemyIndex].GetObject();

        if(poolableObject != null)
        {
            Enemy enemy = poolableObject.GetComponent<Enemy>();
            Enemies[RandomEnemyIndex].SetupEnemy(enemy,Player.gameObject);

            int VertexIndex = Random.Range(0,triangulation.vertices.Length);

            NavMeshHit hit;
            if(NavMesh.SamplePosition(triangulation.vertices[VertexIndex],out hit, 2f, 1))
            {
                enemy.Agent.Warp(hit.position);

                enemy.behavior.SetVariableValue("Target",Player.gameObject);
                enemy.Agent.enabled = true;
                enemy.behavior.SetVariableValue("Current State",State.Idle);
            }
            else
            {
                Debug.LogError($"Unable to place NavMeshAgent on NavMesh, Tried to use {triangulation.vertices[VertexIndex]}");
            }
        }
        else
        {
            Debug.LogError($"Unable to fetch enemy of type {RandomEnemyIndex} from object pool. Out of objects?");
        }
    } 
}
