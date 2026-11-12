using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Manager;
using Archer.Scripts.Utility;

namespace Archer.Scripts.View.Panels
{
    /// <summary>
    /// Sound, Music and Vibration switches, persisted through GameSettings. The Music row hides
    /// itself while no track is assigned (BowSoundData.music), so the screen never offers a
    /// switch that does nothing.
    /// </summary>
    public class SettingsPanel : BaseUIPanel
    {
        [System.Serializable]
        public class ToggleRow
        {
            public GameObject root;
            public Button button;
            public TMP_Text icon;
            public TMP_Text state;
            public Image stateBackground;
            [Tooltip("Icon-font glyphs for the on and off states.")]
            public string onGlyph;
            public string offGlyph;
        }

        [SerializeField] private Button backButton;
        [SerializeField] private ToggleRow soundRow;
        [SerializeField] private ToggleRow musicRow;
        [SerializeField] private ToggleRow vibrationRow;
        [SerializeField] private PanelIntro intro;

        [SerializeField] private Color onColour = new Color32(0x7E, 0xD9, 0x57, 0xFF);
        [SerializeField] private Color offColour = new Color32(0xD8, 0xD2, 0xC4, 0xFF);

        public override void Initialize()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            Bind(soundRow, () => GameSettings.SfxOn = !GameSettings.SfxOn);
            Bind(musicRow, () => GameSettings.MusicOn = !GameSettings.MusicOn);
            Bind(vibrationRow, () =>
            {
                GameSettings.HapticsOn = !GameSettings.HapticsOn;
                // Confirm the switch with the thing it controls.
                if (GameSettings.HapticsOn) Haptics.Medium();
            });
        }

        public override void OnStateEnter()
        {
            if (musicRow != null && musicRow.root != null)
            {
                SoundManger audioManager = SoundManger.Instance;
                musicRow.root.SetActive(audioManager != null && audioManager.HasMusic);
            }

            Refresh(false);
            if (intro != null) intro.Play();
        }

        private void Bind(ToggleRow row, System.Action toggle)
        {
            if (row == null || row.button == null) return;

            row.button.onClick.RemoveAllListeners();
            row.button.onClick.AddListener(() =>
            {
                toggle();
                Refresh(true, row);
            });
        }

        private void Refresh(bool animate, ToggleRow changed = null)
        {
            Apply(soundRow, GameSettings.SfxOn, animate && changed == soundRow);
            Apply(musicRow, GameSettings.MusicOn, animate && changed == musicRow);
            Apply(vibrationRow, GameSettings.HapticsOn, animate && changed == vibrationRow);
        }

        private void Apply(ToggleRow row, bool on, bool animate)
        {
            if (row == null) return;

            if (row.state != null) row.state.text = on ? "ON" : "OFF";
            if (row.stateBackground != null) row.stateBackground.color = on ? onColour : offColour;
            if (row.icon != null) row.icon.text = on ? row.onGlyph : row.offGlyph;

            if (animate && row.stateBackground != null)
            {
                RectTransform rt = row.stateBackground.rectTransform;
                rt.DOKill();
                rt.localScale = Vector3.one;
                rt.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.8f).SetUpdate(true).SetLink(gameObject);
            }
        }

        private void OnBackClicked()
        {
            UIManager.Instance.GoBack();
        }
    }
}
