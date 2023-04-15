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
    private Queue<GameObject> enemyPool;

    // Start is called before the first frame update
    void Start()
    {
        spawnIntervalTimer = 0;
        enemyPool = new Queue<GameObject>();
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
        var spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];

        if (spawnPoint != null)
        {
            GameObject spawnedEnemy;
            if (enemyPool.Count > 0)
            {
                spawnedEnemy = enemyPool.Dequeue();
                spawnedEnemy.SetActive(true);
            }
            else
            {
                spawnedEnemy = Instantiate(lstOfEnemies[UnityEngine.Random.Range(0, lstOfEnemies.Count)].gameObject, spawnPoint.position, spawnPoint.rotation);
            }

            spawnedEnemy.transform.position = spawnPoint.position;
            spawnedEnemy.transform.rotation = spawnPoint.rotation;
            spawnedEnemy.GetComponent<Enemy>().GotToTarget(endPoint);
            enemyCnt++;
        }
    }

    public void ReturnEnemyToPool(GameObject enemy)
    {
        enemy.SetActive(false);
        enemyPool.Enqueue(enemy);
        enemyCnt--;
    }
}
