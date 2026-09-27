using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : Singleton<EnemySpawner>
{
    public List<Transform> spawnPoints;
    public List<Enemy> lstOfEnemies;
    public int spawnInterval;
    public int maxEnemy;
    public Transform endPoint;
    public bool infiniteEnemies;

    private int enemyCnt;
    private float spawnIntervalTimer;
    //private Queue<GameObject> enemyPool;

    // Start is called before the first frame update
    void Start()
    {
        spawnIntervalTimer = 0;
        //enemyPool = new Queue<GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!infiniteEnemies && (maxEnemy != 0 && enemyCnt > maxEnemy))
            return;

        if (spawnIntervalTimer <= 0)
        {
            spawnIntervalTimer = spawnInterval;
            SpawnEnemy();
        }
        else
        {
            spawnIntervalTimer -= Time.deltaTime;
        }
    }

    private void SpawnEnemy()
    {
        // Guard the Inspector-assigned data. This component is a scene object whose references
        // are all scene objects; if a bare instance is ever auto-created by the lazy Singleton
        // getter it will have none of them, and endPoint in particular was dereferenced
        // unguarded inside Enemy.GotToTarget.
        if (spawnPoints == null || spawnPoints.Count == 0) return;
        if (lstOfEnemies == null || lstOfEnemies.Count == 0) return;
        if (endPoint == null) return;

        var spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];

        if (spawnPoint != null)
        {
            var enemyPrefab = lstOfEnemies[Random.Range(0, lstOfEnemies.Count)];

            if (enemyPrefab == null) return;

            var spawnedEnemy = ObjectPoolManager.Instance.SpawnObject(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            if (spawnedEnemy != null)
            {
                spawnedEnemy.transform.position = spawnPoint.position;
                spawnedEnemy.transform.rotation = spawnPoint.rotation;
                spawnedEnemy.GotToTarget(endPoint);
                enemyCnt++;
            }
        }
    }
    
    public void ReturnEnemyToPool(Enemy enemy)
    {
        ObjectPoolManager.Instance.DespawnObject(enemy);
    }
}
