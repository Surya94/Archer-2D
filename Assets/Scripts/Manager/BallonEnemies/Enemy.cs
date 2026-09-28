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
    public bool IsFrozen => isFrozen;

    private bool isBurst;
    private bool isFrozen;
    private Collider2D hitCollider;
    private SpriteRenderer body;
    private SpriteRenderer iceShell;
    private Tween thawWobble;

    private const float IceShellAlpha = 0.55f;

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
        ClearFreeze();

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

        // A frozen balloon's animator is stopped - restart it so the pop actually plays.
        ClearFreeze();
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
    /// Time bonus freeze. Pauses this balloon's rise (the DOMove) and its idle animation and
    /// ices it over; it can still be shot. Only the balloon stops - Time.timeScale is untouched,
    /// so the bow and arrows keep full speed.
    /// </summary>
    public virtual void SetFrozen(bool frozen, Color iceColour)
    {
        if (isBurst || frozen == isFrozen) return;
        isFrozen = frozen;

        if (body == null) body = GetComponent<SpriteRenderer>();

        if (frozen)
        {
            transform.DOPause();
            if (animator != null) animator.speed = 0f;
            if (body != null)
            {
                // A tint alone can only darken (red went maroon), so the body gets a light cool
                // tint and a pale copy of its own sprite on top frosts it over.
                body.DOKill();
                body.DOColor(Color.Lerp(Color.white, iceColour, 0.5f), 0.25f).SetLink(gameObject);

                SpriteRenderer shell = GetIceShell();
                shell.sprite = body.sprite;
                shell.flipX = body.flipX;
                shell.DOKill();
                shell.gameObject.SetActive(true);
                Color c = Color.Lerp(Color.white, iceColour, 0.35f);
                c.a = 0f;
                shell.color = c;
                shell.DOFade(IceShellAlpha, 0.25f).SetLink(gameObject);
            }
            return;
        }

        if (animator != null) animator.speed = 1f;
        if (body != null)
        {
            body.DOKill();
            body.DOColor(Color.white, 0.3f).SetLink(gameObject);
        }
        if (iceShell != null)
        {
            SpriteRenderer shell = iceShell;
            shell.DOKill();
            shell.DOFade(0f, 0.25f).SetLink(gameObject)
                .OnComplete(() => shell.gameObject.SetActive(false));
        }

        // Thaw: a short shiver, then carry on rising. Rotation, not position, so it can't fight
        // the paused DOMove that is about to resume.
        thawWobble?.Kill(true);
        thawWobble = transform.DOPunchRotation(new Vector3(0f, 0f, 12f), 0.3f, 12, 0.6f)
            .SetLink(gameObject)
            .OnComplete(() => transform.DOPlay());
    }

    /// <summary>
    /// Drop any freeze state instantly - for pooling and for popping a frozen balloon. Callers
    /// kill the transform's tweens right after, so this must run first: completing the wobble
    /// (Kill(true)) snaps the rotation back, where a plain DOKill would leave it mid-tilt.
    /// </summary>
    private void ClearFreeze()
    {
        thawWobble?.Kill(true);
        thawWobble = null;
        isFrozen = false;
        if (animator != null) animator.speed = 1f;
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (body != null)
        {
            body.DOKill();
            body.color = Color.white;
        }
        if (iceShell != null)
        {
            iceShell.DOKill();
            iceShell.gameObject.SetActive(false);
        }
    }

    /// <summary>The frost coat drawn over a frozen balloon; created the first time it's needed.</summary>
    private SpriteRenderer GetIceShell()
    {
        if (iceShell != null) return iceShell;

        GameObject go = new GameObject("IceShell");
        go.transform.SetParent(transform, false);
        iceShell = go.AddComponent<SpriteRenderer>();
        iceShell.sortingLayerID = body.sortingLayerID;
        iceShell.sortingOrder = body.sortingOrder + 1;
        go.SetActive(false);
        return iceShell;
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
        ClearFreeze();
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
