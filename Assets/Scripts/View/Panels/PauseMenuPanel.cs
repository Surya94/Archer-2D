using UnityEngine;
using UnityEngine.UI;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Manager;

namespace Archer.Scripts.View.Panels
{
    public class PauseMenuPanel : BaseUIPanel
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button homeButton;

        public override void Initialize()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
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
        }

        public override void OnStateEnter()
        {
            GameManager.Instance.Pause();
        }

        // Deliberately no OnStateExit -> Resume(). Leaving this panel for Settings would
        // unpause the game behind the settings screen. Resuming is owned by GameplayHUDPanel's
        // OnStateEnter instead, so every route back into gameplay unpauses exactly once -
        // the buttons here, GoBack(), and the Escape key handler alike.

        private void OnResumeClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            UIManager.Instance.GoBack();
        }

        private void OnSettingsClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            UIManager.Instance.ShowSettings();
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
