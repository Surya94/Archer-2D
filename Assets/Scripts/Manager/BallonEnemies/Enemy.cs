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

    /// <summary>
    /// Balloons currently spawned and not yet burst. Bonus effects (bomb, lightning) pick their
    /// targets from here rather than searching the scene. Only pool-spawned balloons register,
    /// so the inactive template instances EnemySpawner keeps in the scene never appear.
    /// </summary>
    public static readonly List<Enemy> Alive = new List<Enemy>();

    public bool IsAlive => !isBurst && gameObject.activeInHierarchy;

    private bool isBurst;
    private Collider2D hitCollider;

    void Start()
    {
        curHealth = maxHealth;
    }

    // Pooled balloons are reused, and Start() only ever runs once per instance - so without
    // this a recycled balloon came back with curHealth 0 and popped in a single hit.
    public override void OnObjectSpawn()
    {
        base.OnObjectSpawn();
        curHealth = maxHealth;
        isBurst = false;

        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
            hitCollider.enabled = true;

        if (!Alive.Contains(this))
            Alive.Add(this);
    }

    public void TakeDamage(int damage)
    {
        // The popped balloon lingers for destoryTime while its burst animation plays; without
        // this guard an arrow hitting the remains burst it again and paid out a second time.
        if (isBurst) return;

        curHealth -= damage;
        if (curHealth <= 0)
        {
            curHealth = 0;
            Burst();
        }
    }

    /// <summary>
    /// Pop this balloon: plays the burst, awards its points, and returns it to the pool after
    /// destoryTime. Safe to call more than once - only the first call does anything. Bonus
    /// effects call this directly to pop balloons without an arrow.
    /// </summary>
    public void Burst()
    {
        if (isBurst) return;
        isBurst = true;
        Alive.Remove(this);

        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
            hitCollider.enabled = false;

        transform.DOKill();
        animator.SetTrigger("Burst");
        SignalManager.Instance.DispatchSignal(
            new OnBalloonBurst()
            {
                position = transform.position,
                pointsToGive = expToGive
            });
        OnBurst();
        StartCoroutine(DestroyEnemyAfterDelay(destoryTime));
    }

    /// <summary>Hook for subclasses that do something extra when popped.</summary>
    protected virtual void OnBurst()
    {
    }

    /// <summary>
    /// True when the balloon's centre is inside the camera view. The margin keeps balloons that
    /// are only just poking in from below out of "everything on screen" effects.
    /// </summary>
    public bool IsOnScreen(Camera cam, float margin = 0.02f)
    {
        if (cam == null) return false;

        Vector3 viewport = cam.WorldToViewportPoint(transform.position);
        return viewport.x > margin && viewport.x < 1f - margin
            && viewport.y > margin && viewport.y < 1f - margin;
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
        Alive.Remove(this);
        transform.DOKill();
    }

    protected virtual void OnDestroy()
    {
        Alive.Remove(this);
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
