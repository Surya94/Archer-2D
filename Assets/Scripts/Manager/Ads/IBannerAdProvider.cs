using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Seam for the bottom-centre banner, mirroring IRewardedAdProvider: the simulated
    /// placeholder and the AppLovin MAX banner are interchangeable, so placement and input
    /// handling are testable in the Editor before any real ad serves.
    /// </summary>
    public interface IBannerAdProvider
    {
        /// <summary>Called once when the provider is created.</summary>
        void Initialize();

        void Show();

        void Hide();

        bool IsShowing { get; }

        /// <summary>
        /// Area the banner covers, in screen pixels with the origin bottom-left (the same space
        /// as Input.mousePosition). Empty while hidden.
        /// </summary>
        Rect ScreenRect { get; }
    }
}
