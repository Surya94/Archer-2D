using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Archer.Scripts.Utility;

namespace Archer.Scripts.View
{
    /// <summary>
    /// Makes a button feel pressed: the face sinks onto its lip while held and springs back on
    /// release, with a click sound and a light haptic tick. All motion runs on unscaled time,
    /// because most buttons live on panels shown while the game is paused (timeScale 0).
    ///
    /// Only the inner "content" moves - the button's own transform is left free for layout
    /// groups and for attention animations such as the PLAY button's breathing.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IPointerClickHandler
    {
        // Serialized by index on every button prefab: only ever append to this list.
        public enum ClickSound { Click, Back, None, Toggle }

        public RectTransform content;
        [Tooltip("The darker edge under the face. Greyed out with the button.")]
        public Image lip;
        public Graphic[] dimWhenDisabled;
        public ClickSound sound = ClickSound.Click;
        public float pressDepth = 8f;

        public Color lipColour = Color.gray;
        public Color disabledLipColour = new Color32(0xB4, 0xAE, 0xA0, 0xFF);

        private Button button;
        private Vector2 restPosition;
        private Tween tween;
        private bool pressed;
        private bool lastInteractable = true;
        private bool initialised;

        void Awake()
        {
            button = GetComponent<Button>();
            if (content != null) restPosition = content.anchoredPosition;
            initialised = true;
            ApplyInteractable(button.interactable);
        }

        void OnDisable()
        {
            tween?.Kill();
            pressed = false;
            if (content != null && initialised) content.anchoredPosition = restPosition;
        }

        // Button exposes no "interactable changed" event, and the panels flip it at runtime
        // (the rewarded-ad button), so poll - one bool compare per button per frame.
        void LateUpdate()
        {
            if (button.interactable != lastInteractable)
                ApplyInteractable(button.interactable);
        }

        private void ApplyInteractable(bool interactable)
        {
            lastInteractable = interactable;
            if (lip != null) lip.color = interactable ? lipColour : disabledLipColour;

            if (dimWhenDisabled == null) return;
            foreach (Graphic graphic in dimWhenDisabled)
            {
                if (graphic == null) continue;
                Color c = graphic.color;
                c.a = interactable ? 1f : 0.45f;
                graphic.color = c;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!button.interactable || content == null) return;

            pressed = true;
            tween?.Kill();
            tween = content.DOAnchorPosY(restPosition.y - pressDepth, 0.05f)
                .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!pressed || content == null) return;

            pressed = false;
            tween?.Kill();
            tween = content.DOAnchorPosY(restPosition.y, 0.22f)
                .SetEase(Ease.OutBack, 2.5f).SetUpdate(true).SetLink(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!button.interactable) return;

            SoundManger audio = SoundManger.Instance;
            if (audio != null)
            {
                if (sound == ClickSound.Click) audio.PlayButtonClickSound();
                else if (sound == ClickSound.Back) audio.PlayBackSound();
                else if (sound == ClickSound.Toggle) audio.PlayToggleSound();
            }

            Haptics.Light();
        }
    }
}
