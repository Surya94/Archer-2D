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

    [Header("Bonus balloons")]
    [Tooltip("Prefab assets, one per bonus type. Each needs its own PoolableTypes asset.")]
    public List<BonusBalloon> bonusBalloons;
    [Tooltip("Seconds between bonus balloons, picked at random from this range each time.")]
    public Vector2 bonusIntervalRange = new Vector2(25f, 35f);

    private int enemyCnt;
    private float bonusTimer;
    private BonusBalloon activeBonus;
    private BonusBalloon lastBonusPrefab;
    private float spawnIntervalTimer;
    //private Queue<GameObject> enemyPool;

    // Start is called before the first frame update
    void Start()
    {
        spawnIntervalTimer = 0;
        //enemyPool = new Queue<GameObject>();
        RollBonusTimer();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateBonus();

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
    
    /// <summary>
    /// Time-based rather than a per-spawn chance, so bonuses arrive at a steady pace. Only one
    /// bonus is ever alive; the countdown holds at zero until the current one is popped or has
    /// floated away. Bonuses don't count toward maxEnemy.
    /// </summary>
    private void UpdateBonus()
    {
        if (bonusBalloons == null || bonusBalloons.Count == 0) return;

        if (bonusTimer > 0f)
        {
            bonusTimer -= Time.deltaTime;
            return;
        }

        if (activeBonus != null && activeBonus.IsAlive) return;

        if (SpawnBonus())
            RollBonusTimer();
    }

    private void RollBonusTimer()
    {
        bonusTimer = Random.Range(bonusIntervalRange.x, bonusIntervalRange.y);
    }

    private bool SpawnBonus()
    {
        if (spawnPoints == null || spawnPoints.Count == 0 || endPoint == null) return false;

        var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        if (spawnPoint == null) return false;

        // Avoid the same bonus twice in a row when there's a choice.
        var prefab = bonusBalloons[Random.Range(0, bonusBalloons.Count)];
        if (prefab == lastBonusPrefab && bonusBalloons.Count > 1)
            prefab = bonusBalloons[(bonusBalloons.IndexOf(prefab) + Random.Range(1, bonusBalloons.Count)) % bonusBalloons.Count];
        if (prefab == null) return false;

        var bonus = ObjectPoolManager.Instance.SpawnObject(prefab, spawnPoint.position, spawnPoint.rotation);
        if (bonus == null) return false;

        bonus.GotToTarget(endPoint);
        activeBonus = bonus;
        lastBonusPrefab = prefab;
        return true;
    }

    public void ReturnEnemyToPool(Enemy enemy)
    {
        ObjectPoolManager.Instance.DespawnObject(enemy);
    }
}
