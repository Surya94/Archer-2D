using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Enemy : PoolableObject
{
    public int curHealth;
    public int maxHealth;
    public int movementSpeed;
    public int expToGive;
    public Rigidbody2D rb;
    public Animator animator;
    public float destoryTime = 5f;
    void Start()
    {
        curHealth = maxHealth;       
    }

    public void TakeDamage(int damage)
    {
        curHealth -= damage;
        if (curHealth <= 0)
        {
            curHealth = 0;
            transform.DOKill();
            animator.SetTrigger("Burst");
            SignalManager.Instance.DispatchSignal(
                new OnBalloonBurst()
                {
                    position = transform.position,
                    pointsToGive = expToGive
                });
            StartCoroutine(DestroyEnemyAfterDelay(destoryTime));
        }
    }

    public void GotToTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("[Enemy] GotToTarget called with a null/destroyed target; not moving.");
            return;
        }

        Vector3 tragetPos = new Vector3(transform.position.x, target.position.y,transform.position.z);
        transform.DOMove(tragetPos, movementSpeed).OnComplete(()=> { DestroyEnemy(); Debug.Log("Ballon reached Destination"); });
    }

    // A DOMove started here outlives both the pooled despawn and the scene unload: the tween
    // keeps writing to transform.position and its OnComplete closure still holds this enemy,
    // so after a scene change it throws inside DOTween and then calls back into a destroyed
    // animator and spawner. Kill it on both exits.
    public override void OnObjectDespawn()
    {
        base.OnObjectDespawn();
        transform.DOKill();
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }

    private IEnumerator DestroyEnemyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DestroyEnemy();
    }

    private void DestroyEnemy()
    {
        // Reachable from a tween/coroutine callback, so this object or the spawner may already
        // be gone - e.g. when the scene was unloaded mid-flight.
        if (this == null) return;

        if (animator != null)
            animator.SetTrigger("Reset");

        EnemySpawner spawner = EnemySpawner.Instance;

        if (spawner != null)
            spawner.ReturnEnemyToPool(this);
    }
}
