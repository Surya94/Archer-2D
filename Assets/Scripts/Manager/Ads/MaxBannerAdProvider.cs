using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
#if APPLOVIN_MAX
    /// <summary>
    /// AppLovin MAX banner, pinned bottom-centre. Adaptive banners are disabled so it stays a
    /// fixed 320x50 (728x90 on tablets) rather than stretching edge to edge - a smaller target
    /// for the accidental taps a gameplay banner attracts. MAX auto-refreshes the creative.
    /// </summary>
    public class MaxBannerAdProvider : IBannerAdProvider
    {
        private readonly string adUnitId;
        private bool created;
        private bool wantVisible;
        private bool hasAd;

        // Only claims screen space once an ad has actually filled: an unfilled banner is an
        // invisible native view, and blocking the bow over it would feel like a dead zone.
        public bool IsShowing => created && wantVisible && hasAd;

        public Rect ScreenRect => IsShowing ? BannerMetrics.BottomCentreRect : Rect.zero;

        public MaxBannerAdProvider(string adUnitId)
        {
            this.adUnitId = adUnitId;
        }

        public void Initialize()
        {
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnAdLoadFailed;

            MaxSdkBootstrap.WhenReady(Create);
        }

        private void Create()
        {
            var configuration = new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.BottomCenter)
            {
                IsAdaptive = false
            };
            MaxSdk.CreateBanner(adUnitId, configuration);
            created = true;

            if (wantVisible)
                MaxSdk.ShowBanner(adUnitId);
        }

        public void Show()
        {
            wantVisible = true;
            if (created)
                MaxSdk.ShowBanner(adUnitId);
        }

        public void Hide()
        {
            wantVisible = false;
            if (created)
                MaxSdk.HideBanner(adUnitId);
        }

        private void OnAdLoaded(string adUnit, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnit == adUnitId)
                hasAd = true;
        }

        private void OnAdLoadFailed(string adUnit, MaxSdkBase.ErrorInfo errorInfo)
        {
            // A failed refresh keeps the previous creative on screen, so hasAd is left as is.
            // MAX retries banner loads on its own refresh timer; no manual backoff needed.
            if (adUnit == adUnitId)
                Debug.LogWarning("[Ads] Banner load failed: " + errorInfo.Message);
        }
    }
#endif
}
