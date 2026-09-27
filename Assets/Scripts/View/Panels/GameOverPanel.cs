using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Interface;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Manager;
using Archer.Scripts.Manager.Ads;

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

        private ScoreManager scoreManager;
        private bool isShowingAd;

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
            RefreshScores();
            RefreshAdButton();
        }

        private void RefreshScores()
        {
            // ScoreManager promotes CurrentScore into BestScore on the OnGameOver dispatch,
            // which runs synchronously before UIManager processes this queued transition, so
            // both values are already final by the time we read them here.
            if (scoreText != null)
                scoreText.text = Score.CurrentScore.ToString();

            if (bestText != null)
                bestText.text = Score.BestScore.ToString();
        }

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
            SoundManger.Instance.PlayButtonClickSound();

            // Lock the button for the whole round-trip: the reward callback can land a frame
            // or many frames later, and a second tap in between would burn the revive twice.
            watchAdButton.interactable = false;
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
        }

        private void OnRestartClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            GameManager.Instance.Restart();
        }

        private void OnHomeClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            GameManager.Instance.ReturnToMenu();
        }
    }
}
