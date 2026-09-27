using System;
using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
#if APPLOVIN_MAX
    /// <summary>
    /// Real AppLovin MAX rewarded implementation.
    ///
    /// Compiled only when the APPLOVIN_MAX scripting define symbol is set, which you add in
    /// Player Settings AFTER importing AppLovin-MAX-Unity-Plugin.unitypackage and running
    /// AppLovin > Integration Manager. Until then AdsManager falls back to the simulated
    /// provider, so the project always compiles and the revive flow is always playable.
    /// </summary>
    public class MaxRewardedAdProvider : IRewardedAdProvider
    {
        private const int MaxRetryCount = 6;

        private readonly string adUnitId;

        private Action onRewarded;
        private Action onFailedOrDismissed;
        private bool earnedRewardThisShow;
        private int retryAttempt;
        private bool sdkInitialized;

        public bool IsReady => sdkInitialized && MaxSdk.IsRewardedAdReady(adUnitId);

        public MaxRewardedAdProvider(string adUnitId)
        {
            this.adUnitId = adUnitId;
        }

        public void Initialize()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnAdHidden;

            // Shared with the banner provider, so the SDK is initialised once.
            MaxSdkBootstrap.WhenReady(OnSdkInitialized);
        }

        private void OnSdkInitialized()
        {
            sdkInitialized = true;
            Load();
        }

        public void Load()
        {
            if (!sdkInitialized) return;
            MaxSdk.LoadRewardedAd(adUnitId);
        }

        public void Show(Action onRewarded, Action onFailedOrDismissed)
        {
            this.onRewarded = onRewarded;
            this.onFailedOrDismissed = onFailedOrDismissed;
            earnedRewardThisShow = false;

            if (!IsReady)
            {
                Resolve(false);
                return;
            }

            MaxSdk.ShowRewardedAd(adUnitId);
        }

        private void OnAdLoaded(string adUnit, MaxSdkBase.AdInfo adInfo)
        {
            retryAttempt = 0;
        }

        private void OnAdLoadFailed(string adUnit, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Exponential backoff, capped so a long offline stretch doesn't push the retry
            // interval out to hours.
            retryAttempt = Mathf.Min(retryAttempt + 1, MaxRetryCount);
            double retryDelay = Math.Pow(2, retryAttempt);
            AdsManager.Instance.RetryLoadAfter((float)retryDelay);
        }

        private void OnAdDisplayFailed(string adUnit, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Load();
            Resolve(false);
        }

        private void OnAdReceivedReward(string adUnit, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // Fires before OnAdHidden. Record it and pay out on dismissal so the reward lands
            // exactly once, after the ad UI has actually gone away.
            earnedRewardThisShow = true;
        }

        private void OnAdHidden(string adUnit, MaxSdkBase.AdInfo adInfo)
        {
            Load();
            Resolve(earnedRewardThisShow);
        }

        private void Resolve(bool rewarded)
        {
            Action rewardedCallback = onRewarded;
            Action failedCallback = onFailedOrDismissed;
            onRewarded = null;
            onFailedOrDismissed = null;
            earnedRewardThisShow = false;

            if (rewarded)
                rewardedCallback?.Invoke();
            else
                failedCallback?.Invoke();
        }
    }
#endif
}
