using UnityEngine;

namespace Archer.Scripts.Utility
{
    /// <summary>
    /// Insets a full-screen RectTransform to Screen.safeArea, so HUD elements pinned to its
    /// corners clear a notch, punch-hole camera or rounded corner. The project renders outside
    /// the safe area (androidRenderOutsideSafeArea), so without this the corner pills can sit
    /// under the cutout in landscape.
    ///
    /// Put it on a child that stretches across the whole canvas - never on the canvas itself,
    /// whose RectTransform Unity drives. Full-bleed art (dimmers, frost) should stay outside it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect appliedArea;
        private Vector2Int appliedScreen;

        void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        // The safe area changes when the device rotates between the two landscape orientations.
        void Update()
        {
            if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
                Apply();
        }

        private void Apply()
        {
            Rect area = Screen.safeArea;
            appliedArea = area;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
