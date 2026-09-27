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

        private ScoreManager scoreManager;

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
            }

            // Placeholders only: nothing to open yet, so they stay visibly disabled.
            if (shopButton != null) shopButton.interactable = false;
            if (removeAdsButton != null) removeAdsButton.interactable = false;

            if (comingSoonRow != null)
                comingSoonRow.SetActive(showComingSoonButtons);
        }

        public override void OnStateEnter()
        {
            RefreshBest();
        }

        private void RefreshBest()
        {
            if (bestText == null) return;

            ScoreManager score = Score;
            bestText.text = "BEST  " + (score != null ? score.BestScore : 0);
        }

        private void OnPlayClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            // GameManager is DontDestroyOnLoad; make sure no paused timeScale leaks into the run.
            SceneLoader.Instance.LoadScene(GameSceneName, GameManager.Instance.Resume);
        }

        private void OnSettingsClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            UIManager.Instance.ChangeState(UIState.Settings, UITransitionType.Fade);
        }

        private void OnQuitClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            Application.Quit();
        }
    }
}
