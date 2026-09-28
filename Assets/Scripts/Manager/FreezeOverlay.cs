using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archer.Scripts.Manager
{
    /// <summary>
    /// HUD side of the Time bonus: a frosted screen edge, a white flash as it hits, and a countdown
    /// pill. Purely reactive (OnFreezeStarted / OnFreezeEnded), like GamePlayHUD. Everything runs on
    /// scaled time, so it holds with the rest of the game while paused.
    /// </summary>
    public class FreezeOverlay : MonoBehaviour
    {
        public Image frost;
        public Image flash;
        public RectTransform countdownPill;
        public TextMeshProUGUI countdownText;
        public RectTransform countdownIcon;

        [Range(0f, 1f)] public float frostAlpha = 0.8f;
        public int warningSeconds = 2;
        public Color normalColour = new Color32(0x2E, 0x40, 0x57, 0xFF);
        public Color warningColour = new Color32(0xE8, 0x54, 0x3F, 0xFF);

        private float remaining;
        private int shownSeconds;
        private bool running;

        private Sequence frostSeq;
        private Tween frostBreath;
        private Tween flashTween;
        private Tween pillTween;
        private Tween punchTween;
        private Tween spinTween;

        void Start()
        {
            SignalManager.Instance.AddObserver<OnFreezeStarted>(HandleFreezeStarted);
            SignalManager.Instance.AddObserver<OnFreezeEnded>(HandleFreezeEnded);
            HideImmediate();
        }

        void OnDestroy()
        {
            SignalManager.Instance?.RemoveObserver<OnFreezeStarted>(HandleFreezeStarted);
            SignalManager.Instance?.RemoveObserver<OnFreezeEnded>(HandleFreezeEnded);
            frostSeq?.Kill();
            frostBreath?.Kill();
        }

        void Update()
        {
            if (!running) return;

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != shownSeconds && seconds > 0)
                ShowSeconds(seconds);
        }

        private void HandleFreezeStarted(OnFreezeStarted signalData)
        {
            remaining = signalData.duration;
            running = true;
            shownSeconds = -1;

            if (flash != null)
            {
                flashTween?.Kill();
                flash.gameObject.SetActive(true);
                SetAlpha(flash, 0.75f);
                flashTween = flash.DOFade(0f, 0.35f).SetEase(Ease.OutQuad)
                    .SetLink(gameObject)
                    .OnComplete(() => flash.gameObject.SetActive(false));
            }

            if (frost != null)
            {
                frostSeq?.Kill();
                frostBreath?.Kill();
                frost.gameObject.SetActive(true);
                SetAlpha(frost, 0f);
                frost.rectTransform.localScale = Vector3.one * 1.15f;
                // Frost creeps in, then breathes gently while it holds. The breath is its own tween:
                // a Sequence can't hold an infinitely looping child.
                frostSeq = DOTween.Sequence()
                    .Append(frost.DOFade(frostAlpha, 0.3f).SetEase(Ease.OutQuad))
                    .Join(frost.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutQuad))
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        frostBreath = frost.DOFade(frostAlpha * 0.75f, 0.9f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetLink(gameObject);
                    });
            }

            if (countdownPill != null)
            {
                pillTween?.Kill();
                countdownPill.gameObject.SetActive(true);
                countdownPill.localScale = Vector3.zero;
                pillTween = countdownPill.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            if (countdownIcon != null)
            {
                spinTween?.Kill();
                countdownIcon.localRotation = Quaternion.identity;
                spinTween = countdownIcon.DOLocalRotate(new Vector3(0f, 0f, -360f), 4f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetLink(gameObject);
            }

            ShowSeconds(Mathf.CeilToInt(remaining));
        }

        private void HandleFreezeEnded(OnFreezeEnded signalData)
        {
            running = false;
            spinTween?.Kill();

            if (frost != null)
            {
                frostSeq?.Kill();
                frostBreath?.Kill();
                // Thaw: the frost blows outward and melts away.
                frostSeq = DOTween.Sequence()
                    .Append(frost.DOFade(0f, 0.4f).SetEase(Ease.InQuad))
                    .Join(frost.rectTransform.DOScale(1.2f, 0.4f).SetEase(Ease.InQuad))
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        frost.gameObject.SetActive(false);
                        frost.rectTransform.localScale = Vector3.one;
                    });
            }

            if (countdownPill != null)
            {
                pillTween?.Kill();
                pillTween = countdownPill.DOScale(0f, 0.2f).SetEase(Ease.InBack)
                    .SetLink(gameObject)
                    .OnComplete(() => countdownPill.gameObject.SetActive(false));
            }
        }

        private void ShowSeconds(int seconds)
        {
            shownSeconds = seconds;
            if (countdownText == null) return;

            countdownText.text = seconds.ToString(CultureInfo.InvariantCulture);
            bool warning = seconds <= warningSeconds;
            countdownText.color = warning ? warningColour : normalColour;

            punchTween?.Kill();
            RectTransform rt = countdownText.rectTransform;
            rt.localScale = Vector3.one;
            punchTween = rt.DOPunchScale(Vector3.one * (warning ? 0.45f : 0.25f), 0.3f, 7, 0.8f)
                .SetLink(gameObject);
        }

        private void HideImmediate()
        {
            if (frost != null) frost.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
            if (countdownPill != null) countdownPill.gameObject.SetActive(false);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
