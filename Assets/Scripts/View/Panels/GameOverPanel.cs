using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Interface;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Manager;
using Archer.Scripts.Manager.Ads;
using Archer.Scripts.Utility;

namespace Archer.Scripts.View.Panels
{
    public class GameOverPanel : BaseUIPanel
    {
        [Header("Score Readout")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text bestText;

        [Header("Buttons")]
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TMP_Text watchAdLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button homeButton;

        [Header("Revive")]
        [Tooltip("Arrows granted by the rewarded ad. Allowed once per run.")]
        [SerializeField] private int reviveArrowCount = 5;

        [Header("Celebration")]
        [SerializeField] private PanelIntro intro;
        [Tooltip("Blocks the buttons until the card has landed, so a tap meant for the game can't hit RESTART.")]
        [SerializeField] private CanvasGroup buttonGroup;
        [SerializeField] private RectTransform newBestRibbon;
        [SerializeField] private UIConfetti confetti;
        [SerializeField] private float countUpDuration = 0.8f;

        private ScoreManager scoreManager;
        private bool isShowingAd;
        private Sequence sequence;
        private Tween adPulse;

        private ScoreManager Score => scoreManager ??= DependencyResolver.Resolve<ScoreManager>();

        public override void Initialize()
        {
            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveAllListeners();
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(OnHomeClicked);
            }

            // Subscribed here rather than OnEnable: this panel starts scene-active with
            // hideOnStart deactivating it during Awake, which pre-empts OnEnable ever firing
            // for the initial activation (Unity's Awake -> OnEnable -> Start order). Initialize()
            // is called directly by UIManager regardless of active state, so it's the reliable hook.
            SignalManager.Instance?.RemoveObserver<OnGameOver>(HandleGameOver);
            SignalManager.Instance?.AddObserver<OnGameOver>(HandleGameOver);
        }

        public override void Cleanup()
        {
            SignalManager.Instance?.RemoveObserver<OnGameOver>(HandleGameOver);
            StopAnimations();
        }

        private void HandleGameOver(OnGameOver signalData)
        {
            // Not ShowGameOver(): that convenience method defaults to an Instant transition.
            // addToHistory is false so the Escape/Android-back handler cannot quietly drop the
            // player back into a run that has already ended.
            UIManager.Instance.ChangeState(UIState.GameOver, UITransitionType.Fade, false);
        }

        public override void OnStateEnter()
        {
            isShowingAd = false;
            RefreshAdButton();
            PlayEntrance();
        }

        public override void OnStateExit()
        {
            StopAnimations();
        }

        // ------------------------------------------------------------------ Entrance

        /// <summary>
        /// Card pops in, the score counts up from zero, and a run that beat the record gets the
        /// NEW BEST ribbon, confetti and a fanfare. Unscaled time: the game is paused here.
        ///
        /// ScoreManager promotes CurrentScore into BestScore on the OnGameOver dispatch, which
        /// runs synchronously before UIManager processes this queued transition, so both values
        /// are already final by the time they are read.
        /// </summary>
        private void PlayEntrance()
        {
            StopAnimations();

            int finalScore = Score.CurrentScore;
            bool newBest = Score.IsNewBest;

            SetScore(0);
            if (bestText != null)
                bestText.text = Format(newBest ? Score.BestAtRunStart : Score.BestScore);
            if (newBestRibbon != null) newBestRibbon.gameObject.SetActive(false);
            if (buttonGroup != null) buttonGroup.interactable = false;

            float introTime = 0.3f;
            if (intro != null)
            {
                intro.Play();
                introTime = intro.TotalDuration;
            }

            if (!newBest && SoundManger.Instance != null)
                SoundManger.Instance.PlayGameOverSound();

            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.InsertCallback(Mathf.Min(introTime, 0.45f), () =>
            {
                if (buttonGroup != null) buttonGroup.interactable = true;
                StartAdPulse();
            });

            int shown = 0;
            sequence.Insert(0.25f, DOTween.To(() => shown, value => { shown = value; SetScore(value); },
                    finalScore, finalScore > 0 ? countUpDuration : 0f)
                .SetEase(Ease.OutCubic));

            if (newBest)
                sequence.InsertCallback(0.25f + countUpDuration, () => Celebrate(finalScore));
        }

        private void Celebrate(int finalScore)
        {
            if (bestText != null)
            {
                bestText.text = Format(finalScore);
                bestText.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.35f, 7, 0.8f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            if (scoreText != null)
            {
                scoreText.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 7, 0.8f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            if (newBestRibbon != null)
            {
                // Stamped on: starts oversized and tilted, lands with a bounce.
                newBestRibbon.gameObject.SetActive(true);
                newBestRibbon.localScale = Vector3.one * 2.4f;
                newBestRibbon.localRotation = Quaternion.Euler(0f, 0f, 14f);
                DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                    .Append(newBestRibbon.DOScale(1f, 0.28f).SetEase(Ease.OutBack, 2f))
                    .Join(newBestRibbon.DOLocalRotate(new Vector3(0f, 0f, -6f), 0.28f).SetEase(Ease.OutQuad));
            }

            if (confetti != null) confetti.Burst();
            if (SoundManger.Instance != null) SoundManger.Instance.PlayNewBestSound();
            Haptics.Success();
        }

        private void SetScore(int value)
        {
            if (scoreText != null) scoreText.text = Format(value);
        }

        private static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>A slow breathing pulse on WATCH AD while it can still be used.</summary>
        private void StartAdPulse()
        {
            adPulse?.Kill();
            if (watchAdButton == null) return;

            watchAdButton.transform.localScale = Vector3.one;
            if (!watchAdButton.interactable) return;

            adPulse = watchAdButton.transform.DOScale(1.035f, 0.7f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true).SetLink(gameObject);
        }

        private void StopAnimations()
        {
            sequence?.Kill();
            sequence = null;
            adPulse?.Kill();
            adPulse = null;

            if (watchAdButton != null) watchAdButton.transform.localScale = Vector3.one;
            if (buttonGroup != null) buttonGroup.interactable = true;
            if (confetti != null) confetti.Stop();
        }

        // ------------------------------------------------------------------ Ad / buttons

        private void RefreshAdButton()
        {
            if (watchAdButton == null) return;

            bool reviveSpent = GameManager.Instance.HasUsedRevive;
            bool canRevive = !reviveSpent && !isShowingAd && AdsManager.Instance.IsRewardedReady;

            watchAdButton.interactable = canRevive;

            if (watchAdLabel != null)
            {
                watchAdLabel.text = reviveSpent
                    ? "AD USED"
                    : $"WATCH AD  +{reviveArrowCount}";
            }
        }

        private void OnWatchAdClicked()
        {
            if (isShowingAd || GameManager.Instance.HasUsedRevive) return;

            isShowingAd = true;

            // Lock the button for the whole round-trip: the reward callback can land a frame
            // or many frames later, and a second tap in between would burn the revive twice.
            watchAdButton.interactable = false;
            adPulse?.Kill();
            watchAdButton.transform.localScale = Vector3.one;
            if (watchAdLabel != null)
                watchAdLabel.text = "LOADING...";

            AdsManager.Instance.ShowRewarded(OnRewardEarned, OnAdFailedOrDismissed);
        }

        private void OnRewardEarned()
        {
            isShowingAd = false;

            // Order is load-bearing. Arrows must exist before anything asks the bow to nock
            // one, and Time.timeScale must be back to 1 before gameplay resumes; dispatching
            // OnRunContinued last is what actually re-arms the bow, since Bow.Update()
            // early-returns for as long as newArrow is null.
            SignalManager.Instance.DispatchSignal(new OnAddArrows(reviveArrowCount));
            GameManager.Instance.ContinueRun();
            UIManager.Instance.ChangeState(UIState.Gameplay, UITransitionType.Fade, false);
            SignalManager.Instance.DispatchSignal(new OnRunContinued());
        }

        private void OnAdFailedOrDismissed()
        {
            // No reward: the revive is not consumed, so let the player try again.
            isShowingAd = false;
            RefreshAdButton();
            StartAdPulse();
        }

        // Click sound and haptic come from UIButtonFeel on each button.
        private void OnRestartClicked() => GameManager.Instance.Restart();
        private void OnHomeClicked() => GameManager.Instance.ReturnToMenu();
    }
}
