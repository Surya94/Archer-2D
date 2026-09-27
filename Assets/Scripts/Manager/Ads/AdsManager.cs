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
        // Extra dp around the banner in which a press still doesn't start a bow draw - fingers
        // are wider than the pixel the touch reports.
        private const float BannerTouchPaddingDp = 8f;

        private IRewardedAdProvider provider;
        private IBannerAdProvider bannerProvider;
        private AdsConfig config;
        private Coroutine retryRoutine;
        private bool bannerWanted;
        private bool bannerAllowed = true;

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

        private IBannerAdProvider BannerProvider
        {
            get
            {
                if (bannerProvider == null)
                {
                    bannerProvider = CreateBannerProvider();
                    bannerProvider.Initialize();
                }

                return bannerProvider;
            }
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

        private IBannerAdProvider CreateBannerProvider()
        {
            if (config == null)
                config = AdsConfig.Load();

#if APPLOVIN_MAX
            if (config != null && config.HasBannerUnit)
            {
                Debug.Log("[Ads] Using AppLovin MAX banner provider.");
                return new MaxBannerAdProvider(config.BannerAdUnitId);
            }

            Debug.LogWarning("[Ads] APPLOVIN_MAX is defined but AdsConfig has no banner ad unit id - showing the simulated banner.");
#else
            if (config != null && config.HasBannerUnit)
                Debug.LogWarning("[Ads] AdsConfig has a banner ad unit id but the APPLOVIN_MAX scripting define is not set - showing the simulated banner.");
#endif

            return new SimulatedBannerAdProvider(transform);
        }

        /// <summary>Show the bottom-centre banner. It persists across scene loads.</summary>
        public void ShowBanner()
        {
            bannerWanted = true;
            if (bannerAllowed)
                BannerProvider.Show();
        }

        public void HideBanner()
        {
            bannerWanted = false;
            bannerProvider?.Hide();
        }

        /// <summary>
        /// Master switch for the "Remove Ads" purchase (Stage 6). Nothing calls this with false
        /// yet - the purchase flow doesn't exist - but it is the one place that flow must flip.
        /// </summary>
        public void SetBannerAllowed(bool allowed)
        {
            bannerAllowed = allowed;
            if (!allowed)
                bannerProvider?.Hide();
            else if (bannerWanted)
                BannerProvider.Show();
        }

        /// <summary>
        /// True when a screen point (Input.mousePosition space) is on the banner, with a little
        /// padding. Bow uses this so a drag can't start on the ad - accidental banner taps during
        /// play count as invalid traffic with ad networks.
        /// </summary>
        public bool IsPointOverBanner(Vector2 screenPoint)
        {
            if (bannerProvider == null || !bannerProvider.IsShowing) return false;

            Rect rect = bannerProvider.ScreenRect;
            float padding = BannerTouchPaddingDp * BannerMetrics.Density;
            rect.xMin -= padding;
            rect.xMax += padding;
            rect.yMax += padding;
            return rect.Contains(screenPoint);
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
