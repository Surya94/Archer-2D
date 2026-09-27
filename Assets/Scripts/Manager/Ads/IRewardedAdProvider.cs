using System;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Seam between the game and whatever actually serves rewarded video. Keeping this an
    /// interface means the Game Over revive flow is fully playable in the Editor before the
    /// AppLovin MAX plugin is imported, and swapping in the real network changes no UI code.
    /// </summary>
    public interface IRewardedAdProvider
    {
        /// <summary>An ad is loaded and can be shown right now.</summary>
        bool IsReady { get; }

        /// <summary>Called once when the provider is created.</summary>
        void Initialize();

        /// <summary>Request the next ad. Safe to call when one is already loading.</summary>
        void Load();

        /// <summary>
        /// Show the loaded ad. Exactly one callback is invoked: <paramref name="onRewarded"/>
        /// when the user earned the reward, otherwise <paramref name="onFailedOrDismissed"/>.
        /// </summary>
        void Show(Action onRewarded, Action onFailedOrDismissed);
    }
}
