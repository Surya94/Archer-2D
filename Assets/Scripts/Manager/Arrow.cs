using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public class Arrow : PoolableObject
{
    public Rigidbody2D rb;
    public bool isFired;
    public GameObject endPoint;
    public TrailRenderer trailRenderer;
    public int damage = 10;
    public int pushForce = 5;
    public float destoryTime = 1f;
    public Collider2D myCollider;
    public string ID;
    private float arrowForce;
    private bool isHit;
    private void Start()
    {
        InitArrow();
    }

    private void InitArrow()
    {
        ID = Guid.NewGuid().ToString();
        myCollider.enabled = false;
        trailRenderer.emitting = false;
        isHit = false;
    }

    void Update()
    {
        if (isFired)
        {
            trailRenderer.emitting = true;
            float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    public void SetFireData(float launchForce)
    {
        myCollider.enabled = true;
        arrowForce = launchForce;
    }

    public void DisableArrow()
    {
        if (!isHit)
            SignalManager.Instance.DispatchSignal(new OnUpdateStreak() { isStreakMaintained = false });
        InitArrow();
        arrowForce = 0;
        ObjectPoolManager.Instance.DespawnObject(this);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.CompareTag("Enemy"))
        {
            collision.transform.GetComponent<Enemy>().TakeDamage(damage);
            isHit = true;
            SignalManager.Instance.DispatchSignal(new OnUpdateStreak() { isStreakMaintained=true});
            SoundManger.Instance.PlayHitSound();

        }
    }
}
