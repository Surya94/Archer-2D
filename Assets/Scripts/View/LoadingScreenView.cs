using System.Collections;
using UnityEngine;

namespace Archer.Scripts.View
{
    /// <summary>
    /// The visual half of SceneLoader: a full-screen overlay with its own Screen Space - Overlay
    /// canvas. Deliberately not a BaseUIPanel - UIManager and its panels are scene-local and
    /// are destroyed by the very scene change this screen has to cover.
    ///
    /// Everything runs on unscaled time: Restart and Home load while the game is paused.
    /// </summary>
    public class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private RectTransform[] clouds;
        [SerializeField] private float cloudSpeed = 40f;

        private float[] cloudSpeeds;
        private RectTransform root;

        private void Awake()
        {
            root = (RectTransform)transform;
            cloudSpeeds = new float[clouds.Length];
            for (int i = 0; i < clouds.Length; i++)
                cloudSpeeds[i] = cloudSpeed * Random.Range(0.6f, 1.4f);

            SetVisibleImmediate(false);
        }

        private void Update()
        {
            if (!canvasGroup.blocksRaycasts) return;

            float halfWidth = root.rect.width * 0.5f;
            for (int i = 0; i < clouds.Length; i++)
            {
                RectTransform cloud = clouds[i];
                Vector2 pos = cloud.anchoredPosition;
                pos.x += cloudSpeeds[i] * Time.unscaledDeltaTime;

                float halfCloud = cloud.rect.width * cloud.localScale.x * 0.5f;
                if (pos.x - halfCloud > halfWidth)
                    pos.x = -halfWidth - halfCloud;

                cloud.anchoredPosition = pos;
            }
        }

        /// <summary>0..1. The fill is a stretched rect, so it scales with any screen width.</summary>
        public void SetProgress(float progress)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), progressFill.anchorMax.y);
        }

        public IEnumerator Fade(bool fadeIn, float duration)
        {
            // Block input for the whole transition, including while fading out, so a tap
            // can't land on the scene underneath half-way through.
            canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            float from = canvasGroup.alpha;
            float to = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            SetVisibleImmediate(fadeIn);
        }

        private void SetVisibleImmediate(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = false;
        }
    }
}
