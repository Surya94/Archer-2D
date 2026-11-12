using UnityEngine;

namespace Archer.Scripts.Manager.Feel
{
    /// <summary>
    /// The burst of rubber scraps, ring and flash that plays where a balloon pops. Pooled through
    /// ObjectPoolManager; it tints itself to the balloon and hands itself back when finished.
    /// Particles and the return timer both run on scaled time, so a burst freezes mid-air while
    /// the game is paused instead of finishing behind the pause menu.
    /// </summary>
    public class PopBurstFX : PoolableObject
    {
        public ParticleSystem shards;
        public ParticleSystem ring;
        public ParticleSystem flash;
        [Tooltip("Seconds before the effect returns to the pool - longer than the longest particle.")]
        public float lifetime = 0.9f;

        public void Play(Color colour, float scale)
        {
            transform.localScale = Vector3.one * scale;
            colour.a = 1f;

            if (shards != null)
            {
                ParticleSystem.MainModule main = shards.main;
                // Two tones of the balloon's own colour, so the scraps read as its rubber.
                main.startColor = new ParticleSystem.MinMaxGradient(
                    Color.Lerp(colour, Color.black, 0.12f), Color.Lerp(colour, Color.white, 0.4f));
            }

            if (ring != null)
            {
                ParticleSystem.MainModule main = ring.main;
                main.startColor = Color.Lerp(colour, Color.white, 0.65f);
            }

            ParticleSystem root = shards != null ? shards : GetComponent<ParticleSystem>();
            if (root != null)
            {
                root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                root.Play(true);
            }

            CancelInvoke(nameof(ReturnToPool));
            Invoke(nameof(ReturnToPool), lifetime);
        }

        public override void OnObjectDespawn()
        {
            base.OnObjectDespawn();
            CancelInvoke(nameof(ReturnToPool));
        }

        private void ReturnToPool()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && this != null)
                pool.DespawnObject(this);
        }
    }
}
