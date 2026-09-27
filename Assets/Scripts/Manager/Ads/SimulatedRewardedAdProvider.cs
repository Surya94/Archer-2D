using System;
using System.Collections;
using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Stand-in used whenever the real ad network is unavailable: the APPLOVIN_MAX scripting
    /// define is absent, or AdsConfig has no rewarded ad unit id yet. Always fills, after a
    /// short delay so the asynchronous shape of a real ad is preserved and callback-ordering
    /// bugs surface in the Editor rather than on device.
    /// </summary>
    public class SimulatedRewardedAdProvider : IRewardedAdProvider
    {
        private readonly MonoBehaviour coroutineRunner;
        private readonly float simulatedWatchSeconds;

        public bool IsReady => true;

        public SimulatedRewardedAdProvider(MonoBehaviour coroutineRunner, float simulatedWatchSeconds = 0.75f)
        {
            this.coroutineRunner = coroutineRunner;
            this.simulatedWatchSeconds = simulatedWatchSeconds;
        }

        public void Initialize()
        {
            Debug.Log("[Ads] Simulated rewarded provider active - no real ads will be served.");
        }

        public void Load()
        {
            // Nothing to load; always ready.
        }

        public void Show(Action onRewarded, Action onFailedOrDismissed)
        {
            if (coroutineRunner == null)
            {
                onRewarded?.Invoke();
                return;
            }

            coroutineRunner.StartCoroutine(SimulateWatch(onRewarded));
        }

        private IEnumerator SimulateWatch(Action onRewarded)
        {
            // Unscaled: the game is paused at timeScale 0 while the Game Over panel is up.
            yield return new WaitForSecondsRealtime(simulatedWatchSeconds);
            onRewarded?.Invoke();
        }
    }
}
