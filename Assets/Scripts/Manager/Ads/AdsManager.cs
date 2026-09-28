using System;
using System.Collections;
using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Single entry point for showing ads. Picks a concrete provider once, lazily, and hides
    /// which one it landed on from every caller.
    /// </summary>
    public class AdsManager : Singleton<AdsManager>
    {
        private IRewardedAdProvider provider;
        private AdsConfig config;
        private Coroutine retryRoutine;

        /// <summary>
        /// Provider is created on first touch rather than in Awake/Start, so callers get a
        /// working manager no matter how the singleton came into being (scene object, lazy
        /// auto-creation, or the eager warm-up in SplashScreenLoader).
        /// </summary>
        private IRewardedAdProvider Provider
        {
            get
            {
                if (provider == null)
                {
                    provider = CreateProvider();
                    provider.Initialize();
                    provider.Load();
                }

                return provider;
            }
        }

        public bool IsRewardedReady => Provider.IsReady;

        /// <summary>
        /// Start the SDK and the first rewarded load at boot. Left lazy, nothing touched the
        /// provider until the first Game Over, so the first revive offer almost never had an ad.
        /// </summary>
        public void Preload()
        {
            _ = Provider;
        }

        private IRewardedAdProvider CreateProvider()
        {
            config = AdsConfig.Load();

            if (config == null)
            {
                Debug.LogWarning($"[Ads] No AdsConfig found at Resources/{AdsConfig.ResourcesPath} - falling back to simulated ads.");
                return new SimulatedRewardedAdProvider(this);
            }

#if APPLOVIN_MAX
            if (config.HasRewardedUnit)
            {
                Debug.Log("[Ads] Using AppLovin MAX rewarded provider.");
                return new MaxRewardedAdProvider(config.RewardedAdUnitId);
            }

            Debug.LogWarning("[Ads] APPLOVIN_MAX is defined but AdsConfig has no rewarded ad unit id - falling back to simulated ads.");
#else
            if (config.HasRewardedUnit)
            {
                Debug.LogWarning("[Ads] AdsConfig has a rewarded ad unit id but the APPLOVIN_MAX scripting define is not set - falling back to simulated ads.");
            }
#endif

            return new SimulatedRewardedAdProvider(this);
        }

        /// <summary>
        /// Show a rewarded ad. Exactly one of the callbacks runs. Callers must tolerate the
        /// callback arriving on a later frame, and while Time.timeScale is 0.
        /// </summary>
        public void ShowRewarded(Action onRewarded, Action onFailedOrDismissed = null)
        {
            Provider.Show(onRewarded, onFailedOrDismissed);
        }

        /// <summary>Re-request an ad after a backoff delay. Used by the MAX provider's retry path.</summary>
        public void RetryLoadAfter(float seconds)
        {
            if (retryRoutine != null)
                StopCoroutine(retryRoutine);

            retryRoutine = StartCoroutine(RetryLoadRoutine(seconds));
        }

        private IEnumerator RetryLoadRoutine(float seconds)
        {
            // Unscaled: retries must keep ticking while the game is paused on the Game Over panel.
            yield return new WaitForSecondsRealtime(seconds);
            retryRoutine = null;
            Provider.Load();
        }
    }
}
