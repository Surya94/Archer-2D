using UnityEngine;
using UnityEngine.UI;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Interface;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Manager;

namespace Archer.Scripts.View.Panels
{
    public class GameplayHUDPanel : BaseUIPanel
    {
        [SerializeField] private Button pauseButton;

        public override void Initialize()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveAllListeners();
                pauseButton.onClick.AddListener(OnPauseClicked);
            }
        }

        // Entering gameplay is the single definition of "the game is running". Owning the
        // unpause here rather than in PauseMenuPanel.OnStateExit means every route back -
        // the Resume button, GoBack(), the Escape key, the post-revive transition - unpauses,
        // while Pause -> Settings -> Pause never accidentally does.
        public override void OnStateEnter()
        {
            GameManager.Instance.Resume();
        }

        private void OnPauseClicked()
        {
            SoundManger.Instance.PlayButtonClickSound();
            // Explicit Fade, and addToHistory so the pause menu's Resume/GoBack has somewhere
            // to return to. ShowPauseMenu() would transition instantly.
            UIManager.Instance.ChangeState(UIState.PauseMenu, UITransitionType.Fade, true);
        }
    }
}
