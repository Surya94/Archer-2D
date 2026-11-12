using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Interface;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Manager;
using Archer.Scripts.Utility;

namespace Archer.Scripts.View.Panels
{
    public class MainMenuPanel : BaseUIPanel
    {
        private const string GameSceneName = "GameScene";

        [Header("Readout")]
        [SerializeField] private TMP_Text bestText;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Coming Soon")]
        [Tooltip("Shop / Remove Ads have no backing systems until PLAN.md Stages 6-7. " +
                 "Untick to hide them entirely, e.g. for an interim release build.")]
        [SerializeField] private bool showComingSoonButtons = true;
        [SerializeField] private GameObject comingSoonRow;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button removeAdsButton;

        [Header("Motion")]
        [SerializeField] private RectTransform title;
        [SerializeField] private PanelIntro intro;

        private ScoreManager scoreManager;
        private Tween titleBob;
        private Tween titleSway;
        private Tween playBreath;

        private ScoreManager Score => scoreManager ??= DependencyResolver.Resolve<ScoreManager>();

        public override void Initialize()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitClicked);
                // Phones leave an app with the system back/home gesture; an on-screen QUIT is a
                // desktop convention and store reviewers flag it on mobile.
                quitButton.gameObject.SetActive(!Application.isMobilePlatform);
            }

            // Placeholders only: nothing to open yet, so they stay visibly disabled.
            if (shopButton != null) shopButton.interactable = false;
            if (removeAdsButton != null) removeAdsButton.interactable = false;

            if (comingSoonRow != null)
                comingSoonRow.SetActive(showComingSoonButtons);

            // The menu is where a session starts, so it is the natural place to begin the music.
            if (SoundManger.Instance != null)
                SoundManger.Instance.RefreshMusic();
        }

        public override void Cleanup()
        {
            StopIdle();
        }

        public override void OnStateEnter()
        {
            RefreshBest();
            if (intro != null) intro.Play();
            StartIdle();
        }

        public override void OnStateExit()
        {
            StopIdle();
        }

        private void RefreshBest()
        {
            if (bestText == null) return;

            ScoreManager score = Score;
            int best = score != null ? score.BestScore : 0;
            bestText.text = "BEST  " + best.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The title floats and sways gently and PLAY breathes, so the menu is never a still
        /// image. Unscaled: GameManager can leave timeScale at 0 for a moment on the way back
        /// from a run.
        /// </summary>
        private void StartIdle()
        {
            StopIdle();

            if (title != null)
            {
                title.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
                titleSway = title.DOLocalRotate(new Vector3(0f, 0f, 1.5f), 2.4f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true).SetLink(gameObject);
                // Drops in with a bounce, then settles into a slow breathing scale.
                title.localScale = Vector3.zero;
                titleBob = title.DOScale(1f, 0.5f).SetEase(Ease.OutBack, 1.8f)
                    .SetUpdate(true).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        titleBob = title.DOScale(1.04f, 1.6f)
                            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                            .SetUpdate(true).SetLink(gameObject);
                    });
            }

            if (playButton != null)
            {
                playBreath = playButton.transform.DOScale(1.045f, 0.8f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(0.8f).SetUpdate(true).SetLink(gameObject);
            }
        }

        private void StopIdle()
        {
            titleBob?.Kill();
            titleSway?.Kill();
            playBreath?.Kill();
            titleBob = titleSway = playBreath = null;

            if (title != null)
            {
                title.localScale = Vector3.one;
                title.localRotation = Quaternion.identity;
            }
            if (playButton != null) playButton.transform.localScale = Vector3.one;
        }

        // Click sounds and haptics come from UIButtonFeel on each button.
        private void OnPlayClicked()
        {
            // GameManager is DontDestroyOnLoad; make sure no paused timeScale leaks into the run.
            SceneLoader.Instance.LoadScene(GameSceneName, GameManager.Instance.Resume);
        }

        private void OnSettingsClicked()
        {
            UIManager.Instance.ChangeState(UIState.Settings, UITransitionType.Fade);
        }

        private void OnQuitClicked()
        {
            Application.Quit();
        }
    }
}
