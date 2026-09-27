using UnityEngine;
using UnityEngine.UI;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Manager;

namespace Archer.Scripts.View.Panels
{
    // Shell only for now: no functional audio controls until SoundManger's
    // stubbed Set*Enabled/Set*Volume methods are implemented (PLAN.md Stage 3/4).
    public class SettingsPanel : BaseUIPanel
    {
        [SerializeField] private Button backButton;

        public override void Initialize()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        private void OnBackClicked()
        {
            UIManager.Instance.GoBack();
        }
    }
}
