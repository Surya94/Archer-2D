using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SawSwingTrap : MonoBehaviour
{
    public int damage = 100;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.CompareTag("Enemy"))
        {
            collision.transform.GetComponent<Enemy>().TakeDamage(damage);
            SignalManager.Instance.DispatchSignal(new OnUpdateStreak() { isStreakMaintained = true });
            SoundManger.Instance.PlayHitSound();

        }
    }
}
