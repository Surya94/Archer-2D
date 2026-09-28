using System;
using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
#if APPLOVIN_MAX
    /// <summary>
    /// Starts the AppLovin SDK exactly once, however many ad formats need it. A provider that
    /// called MaxSdk.InitializeSdk() itself would initialise twice as soon as a second format
    /// (e.g. interstitial) is added. Providers call WhenReady instead.
    /// </summary>
    public static class MaxSdkBootstrap
    {
        private static bool started;
        private static bool ready;
        private static Action pending;

        public static bool IsReady => ready;

        /// <summary>Run the callback once the SDK is initialised - immediately if it already is.</summary>
        public static void WhenReady(Action callback)
        {
            if (ready)
            {
                callback?.Invoke();
                return;
            }

            pending += callback;
            Start();
        }

        private static void Start()
        {
            if (started) return;
            started = true;

            MaxSdkCallbacks.OnSdkInitializedEvent += OnInitialized;
            // The SDK key is read from AppLovinSettings, set in AppLovin > Integration Manager.
            MaxSdk.InitializeSdk();
        }

        private static void OnInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            ready = true;
            Action callbacks = pending;
            pending = null;
            callbacks?.Invoke();
        }

        // Statics survive into the next Play session when domain reload is disabled in the
        // Editor; reset so each session initialises cleanly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            started = false;
            ready = false;
            pending = null;
        }
    }
#endif
}
