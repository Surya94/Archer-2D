using DG.Tweening;
using UnityEngine;

namespace Archer.Scripts.View
{
    /// <summary>
    /// Entrance animation for a panel: the card pops in with a little overshoot and its rows
    /// follow one after another. Panels call Play() from OnStateEnter; it runs alongside
    /// BaseUIPanel's own alpha fade and does not replace it, so UIManager's transition callbacks
    /// are untouched.
    ///
    /// Unscaled time throughout - these panels appear while the game is paused.
    /// </summary>
    public class PanelIntro : MonoBehaviour
    {
        public RectTransform card;
        [Tooltip("Rows revealed in order. Scale only, so a layout group can keep driving their size and position.")]
        public RectTransform[] items;
        public float cardDuration = 0.32f;
        public float itemDuration = 0.26f;
        public float stagger = 0.045f;
        public float firstItemDelay = 0.08f;

        private Sequence sequence;

        /// <summary>Seconds until the last row has landed.</summary>
        public float TotalDuration =>
            firstItemDelay + (items != null ? Mathf.Max(0, items.Length - 1) * stagger : 0f) + itemDuration;

        public void Play()
        {
            sequence?.Kill();
            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (card != null)
            {
                card.localScale = Vector3.one * 0.82f;
                sequence.Insert(0f, card.DOScale(1f, cardDuration).SetEase(Ease.OutBack, 1.6f));
            }

            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
            {
                RectTransform item = items[i];
                if (item == null) continue;

                item.localScale = Vector3.zero;
                sequence.Insert(firstItemDelay + i * stagger,
                    item.DOScale(1f, itemDuration).SetEase(Ease.OutBack, 1.4f));
            }
        }

        // A panel hidden mid-intro must not come back next time with rows stuck at scale zero.
        void OnDisable()
        {
            sequence?.Kill();
            sequence = null;

            if (card != null) card.localScale = Vector3.one;
            if (items == null) return;
            foreach (RectTransform item in items)
            {
                if (item != null) item.localScale = Vector3.one;
            }
        }
    }
}
