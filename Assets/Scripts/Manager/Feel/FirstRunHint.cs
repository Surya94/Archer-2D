using DG.Tweening;
using UnityEngine;
using Archer.Scripts.Models;

namespace Archer.Scripts.Manager.Feel
{
    /// <summary>
    /// "Drag back and release" - shown until the player has loosed their first arrow ever, then
    /// never again. A hand glides back along the pull direction on a loop; it hides while the
    /// player is actually dragging and returns if they let go without firing.
    /// </summary>
    public class FirstRunHint : MonoBehaviour
    {
        public const string DoneKey = "Tutorial.FirstShotFired";

        public CanvasGroup group;
        public RectTransform hand;
        [Tooltip("How far, in canvas units, the hand travels on each demonstration pull.")]
        public Vector2 pull = new Vector2(-260f, -90f);

        private Vector2 handRest;
        private Sequence loop;
        private Tween fade;
        private bool done;

        void Start()
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1)
            {
                gameObject.SetActive(false);
                return;
            }

            handRest = hand.anchoredPosition;
            group.alpha = 0f;
            SignalManager.Instance.AddObserver<OnBowDragStarted>(HandleStarted);
            SignalManager.Instance.AddObserver<OnBowDragEnded>(HandleEnded);

            StartLoop();
            FadeTo(1f, 0.4f, 0.6f);
        }

        void OnDestroy()
        {
            SignalManager.Instance?.RemoveObserver<OnBowDragStarted>(HandleStarted);
            SignalManager.Instance?.RemoveObserver<OnBowDragEnded>(HandleEnded);
            loop?.Kill();
            fade?.Kill();
        }

        private void StartLoop()
        {
            loop?.Kill();
            hand.anchoredPosition = handRest;
            hand.localScale = Vector3.one;

            // Press, pull back, hold, release - the release snaps back like the string does.
            loop = DOTween.Sequence()
                .Append(hand.DOScale(0.86f, 0.18f).SetEase(Ease.OutQuad))
                .Append(hand.DOAnchorPos(handRest + pull, 0.7f).SetEase(Ease.OutCubic))
                .AppendInterval(0.3f)
                .Append(hand.DOScale(1f, 0.1f))
                .Append(hand.DOAnchorPos(handRest, 0.16f).SetEase(Ease.InQuad))
                .AppendInterval(0.55f)
                .SetLoops(-1)
                .SetLink(gameObject);
        }

        private void HandleStarted(OnBowDragStarted signalData)
        {
            if (!done) FadeTo(0f, 0.15f, 0f);
        }

        private void HandleEnded(OnBowDragEnded signalData)
        {
            if (done) return;

            if (signalData != null && signalData.fired)
            {
                done = true;
                PlayerPrefs.SetInt(DoneKey, 1);
                PlayerPrefs.Save();
                loop?.Kill();
                fade?.Kill();
                fade = group.DOFade(0f, 0.2f).SetLink(gameObject)
                    .OnComplete(() => gameObject.SetActive(false));
                return;
            }

            FadeTo(1f, 0.3f, 0.4f);
        }

        private void FadeTo(float alpha, float duration, float delay)
        {
            fade?.Kill();
            fade = group.DOFade(alpha, duration).SetDelay(delay).SetLink(gameObject);
        }
    }
}
