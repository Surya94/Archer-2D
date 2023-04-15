using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Enemy : MonoBehaviour
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

    public void TakeDamage(int damage,Vector2 dir, float val)
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
        Vector3 tragetPos = new Vector3(transform.position.x, target.position.y,transform.position.z);
        transform.DOMove(tragetPos, movementSpeed).OnComplete(()=> { DestroyEnemy(); Debug.Log("Ballon reached Destination"); });
    }

    private IEnumerator DestroyEnemyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DestroyEnemy();
    }

    private void DestroyEnemy()
    {
        animator.SetTrigger("Reset");
        EnemySpawner.Instance.ReturnEnemyToPool(this.gameObject);
    }
}
