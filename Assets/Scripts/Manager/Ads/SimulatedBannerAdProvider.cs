using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archer.Scripts.Manager.Ads
{
    /// <summary>
    /// Stand-in banner used whenever the real network is unavailable (no APPLOVIN_MAX define,
    /// or no banner ad unit id). Draws a grey placeholder at the real banner size and position,
    /// on its own top-most canvas, so layout and the bow's input exclusion behave as they will
    /// on device.
    /// </summary>
    public class SimulatedBannerAdProvider : IBannerAdProvider
    {
        private readonly Transform parent;
        private GameObject root;
        private RectTransform bar;

        public bool IsShowing => root != null && root.activeSelf;

        public Rect ScreenRect => IsShowing ? BannerMetrics.BottomCentreRect : Rect.zero;

        public SimulatedBannerAdProvider(Transform parent)
        {
            this.parent = parent;
        }

        public void Initialize()
        {
            Debug.Log("[Ads] Simulated banner provider active - no real ads will be served.");

            // Parented to AdsManager, which is DontDestroyOnLoad, so it survives scene loads
            // exactly like a native banner view would.
            root = new GameObject("SimulatedBanner", typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // A native banner sits above everything Unity draws.
            canvas.sortingOrder = short.MaxValue;
            // No CanvasScaler: sizes below are raw screen pixels, matching BannerMetrics.

            GameObject barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(root.transform, false);
            bar = (RectTransform)barGo.transform;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            // Blocks UI clicks underneath, as the native view does.
            barGo.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 0.92f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(bar, false);
            RectTransform labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.8f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 40f;
            label.raycastTarget = false;
            Vector2 dp = BannerMetrics.SizeDp;
            label.text = "AD  " + dp.x + "×" + dp.y + "  (simulated)";

            UpdateSize();
            root.SetActive(false);
        }

        public void Show()
        {
            if (root == null) return;
            UpdateSize();
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        private void UpdateSize()
        {
            if (bar != null)
                bar.sizeDelta = BannerMetrics.SizePixels;
        }
    }
}
