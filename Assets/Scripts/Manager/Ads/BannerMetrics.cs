using UnityEngine;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Standard (non-adaptive) banner size in screen pixels: 320x50 dp on phones, 728x90 dp on
    /// tablets - the sizes MAX serves when adaptive banners are disabled.
    /// </summary>
    public static class BannerMetrics
    {
        // Android's density-independent pixel is defined against a 160 dpi screen.
        private const float BaselineDpi = 160f;
        // Android's own tablet cut-off: smallest screen width of 600 dp.
        private const float TabletMinDp = 600f;
        // A phone's short side is roughly this many dp. In the Editor the Game view is treated
        // as such a phone, because Screen.dpi there reports the desktop monitor and would draw
        // an unrealistically tiny banner. Short side, not height, so an oddly shaped Game view
        // window still gets a sane size.
        private const float EditorPhoneShortSideDp = 393f;

        /// <summary>Screen pixels per dp.</summary>
        public static float Density
        {
            get
            {
                if (Application.isEditor)
                    return Mathf.Max(Mathf.Min(Screen.width, Screen.height) / EditorPhoneShortSideDp, 0.1f);

                float dpi = Screen.dpi > 0f ? Screen.dpi : BaselineDpi;
                return dpi / BaselineDpi;
            }
        }

        public static bool IsTablet
        {
            get
            {
                float smallestDp = Mathf.Min(Screen.width, Screen.height) / Density;
                return smallestDp >= TabletMinDp;
            }
        }

        public static Vector2 SizeDp => IsTablet ? new Vector2(728f, 90f) : new Vector2(320f, 50f);

        public static Vector2 SizePixels => SizeDp * Density;

        /// <summary>Bottom-centre banner rect in screen pixels (origin bottom-left).</summary>
        public static Rect BottomCentreRect
        {
            get
            {
                Vector2 size = SizePixels;
                return new Rect((Screen.width - size.x) * 0.5f, 0f, size.x, size.y);
            }
        }
    }
}
