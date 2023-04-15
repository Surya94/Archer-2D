using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
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
    private int hitCount;
    private void Start()
    {
        InitArrow();
    }

    private void InitArrow()
    {
        ID = Guid.NewGuid().ToString();
        myCollider.enabled = false;
        trailRenderer.emitting = false;
        hitCount = 0;
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
        Invoke("DisableArrow", destoryTime);
        //Destroy(gameObject, destoryTime);
    }

    private void DisableArrow()
    {
        InitArrow();
        arrowForce = 0;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        //rb.velocity = Vector2.zero;
        //rb.isKinematic = true;
        //myCollider.enabled = false;
        if (collision.transform.CompareTag("Enemy"))
        {
            collision.transform.GetComponent<Enemy>().TakeDamage(damage, transform.right.normalized, pushForce * arrowForce);
            hitCount++;
            if (hitCount >= 2)
                SignalManager.Instance.DispatchSignal(new OnAddArrows(1));
            SoundManger.Instance.BowSoundManager.PlayHitSound();

        }
    }
}
