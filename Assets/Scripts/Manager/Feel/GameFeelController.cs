using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Archer.Scripts.Utility;

namespace Archer.Scripts.Manager.Feel
{
    /// <summary>
    /// The game's gratification layer: pop particles, rising pop pitch, haptics, camera shake,
    /// streak callouts and the combo badge. Purely reactive, like GamePlayHUD and the VFX
    /// handlers - it listens to gameplay signals and owns no gameplay state of its own.
    ///
    /// Everything runs on scaled time, so effects hold with the rest of the game while paused.
    /// </summary>
    public class GameFeelController : MonoBehaviour
    {
        [Header("Pop")]
        public PopBurstFX popBurstPrefab;
        [Tooltip("Sprite height, in world units, that a burst of scale 1 is sized for.")]
        public float referenceBalloonSize = 1.2f;
        [Tooltip("Balloons taller than this use the deeper 'big pop' sound.")]
        public float bigBalloonSize = 1.8f;

        [Header("Camera shake")]
        public float popShake = 0.035f;
        public float bonusShake = 0.16f;

        [Header("HUD")]
        public RectTransform calloutRoot;
        public TextMeshProUGUI calloutText;
        public RectTransform comboBadge;
        public TextMeshProUGUI comboText;
        [Tooltip("Full-screen white image flashed by the lightning bonus.")]
        public Image screenFlash;

        [Header("Callouts")]
        public Color[] tierColours =
        {
            new Color32(0xFF, 0xF4, 0xDC, 0xFF),
            new Color32(0x7E, 0xD9, 0x57, 0xFF),
            new Color32(0x6E, 0xC6, 0xF5, 0xFF),
            new Color32(0xFF, 0xC5, 0x3D, 0xFF),
            new Color32(0xFF, 0x7A, 0x45, 0xFF),
        };

        // Streak length -> callout. Past the last entry it repeats every RepeatEvery hits.
        private static readonly int[] TierStreaks = { 2, 3, 5, 8, 12 };
        private static readonly string[] TierWords = { "NICE!", "GREAT!", "AWESOME!", "ON FIRE!", "UNSTOPPABLE!" };
        private const int RepeatEvery = 5;

        private Camera cam;
        private Vector3 camRest;
        private Tween shakeTween;
        private float activeShake;

        private int streak;
        private Sequence calloutSeq;
        private Sequence badgeSeq;
        private Tween flashTween;
        private Vector2 badgeRest;
        private Quaternion badgeRestRotation = Quaternion.identity;

        void Start()
        {
            cam = Camera.main;
            if (cam != null) camRest = cam.transform.position;

            if (calloutRoot != null) calloutRoot.gameObject.SetActive(false);
            if (comboBadge != null)
            {
                badgeRest = comboBadge.anchoredPosition;
                badgeRestRotation = comboBadge.localRotation;
                comboBadge.gameObject.SetActive(false);
            }
            if (screenFlash != null) screenFlash.gameObject.SetActive(false);

            SignalManager signals = SignalManager.Instance;
            signals.AddObserver<OnBalloonBurst>(HandleBurst);
            signals.AddObserver<OnUpdateStreak>(HandleStreak);
            signals.AddObserver<OnBonusCollected>(HandleBonus);
            signals.AddObserver<OnAddArrows>(HandleArrows);
        }

        void OnDestroy()
        {
            SignalManager signals = SignalManager.Instance;
            if (signals != null)
            {
                signals.RemoveObserver<OnBalloonBurst>(HandleBurst);
                signals.RemoveObserver<OnUpdateStreak>(HandleStreak);
                signals.RemoveObserver<OnBonusCollected>(HandleBonus);
                signals.RemoveObserver<OnAddArrows>(HandleArrows);
            }

            shakeTween?.Kill();
            calloutSeq?.Kill();
            badgeSeq?.Kill();
        }

        // ------------------------------------------------------------------ Pop

        private void HandleBurst(OnBalloonBurst signalData)
        {
            if (signalData == null) return;

            if (popBurstPrefab != null && ObjectPoolManager.Instance != null)
            {
                PopBurstFX burst = ObjectPoolManager.Instance.SpawnObject(
                    popBurstPrefab, signalData.position, Quaternion.identity);
                if (burst != null)
                {
                    float scale = Mathf.Clamp(signalData.size / Mathf.Max(referenceBalloonSize, 0.01f), 0.7f, 2.2f);
                    burst.Play(signalData.colour, scale);
                }
            }

            // Enemy dispatches the burst before Arrow reports the streak for the same hit, so
            // streak is still the count of hits BEFORE this one - the first pop plays at base pitch.
            if (SoundManger.Instance != null)
                SoundManger.Instance.PlayPop(streak, signalData.size > bigBalloonSize);

            Haptics.Light();
            Shake(popShake, 0.12f);
        }

        // ------------------------------------------------------------------ Streak

        private void HandleStreak(OnUpdateStreak signalData)
        {
            if (signalData == null) return;

            if (!signalData.isStreakMaintained)
            {
                if (streak >= 2)
                {
                    DropBadge();
                    if (SoundManger.Instance != null) SoundManger.Instance.PlayMiss();
                }
                streak = 0;
                return;
            }

            streak++;
            if (streak >= 2) ShowBadge();

            int tier = TierFor(streak);
            if (tier >= 0) ShowCallout(tier);
        }

        /// <summary>The callout tier this exact streak length triggers, or -1 for none.</summary>
        private static int TierFor(int value)
        {
            for (int i = 0; i < TierStreaks.Length; i++)
            {
                if (TierStreaks[i] == value) return i;
            }

            int last = TierStreaks[TierStreaks.Length - 1];
            if (value > last && (value - last) % RepeatEvery == 0)
                return TierStreaks.Length - 1;

            return -1;
        }

        private void ShowCallout(int tier)
        {
            if (SoundManger.Instance != null) SoundManger.Instance.PlayComboStep(tier);
            Haptics.Medium();

            if (calloutRoot == null || calloutText == null) return;

            calloutSeq?.Kill();
            calloutText.text = TierWords[tier];
            Color colour = tierColours != null && tierColours.Length > 0
                ? tierColours[Mathf.Min(tier, tierColours.Length - 1)]
                : Color.white;
            calloutText.color = colour;

            calloutRoot.gameObject.SetActive(true);
            calloutRoot.localScale = Vector3.zero;
            calloutRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));

            // Bigger tiers land harder.
            float peak = 1.15f + tier * 0.06f;
            calloutSeq = DOTween.Sequence()
                .Append(calloutRoot.DOScale(peak, 0.16f).SetEase(Ease.OutBack))
                .Append(calloutRoot.DOScale(1f, 0.12f).SetEase(Ease.OutQuad))
                .AppendInterval(0.55f)
                .Append(calloutRoot.DOScale(1.25f, 0.22f).SetEase(Ease.InQuad))
                .Join(calloutText.DOFade(0f, 0.22f).SetEase(Ease.InQuad))
                .SetLink(gameObject)
                .OnComplete(() => calloutRoot.gameObject.SetActive(false));
        }

        private void ShowBadge()
        {
            if (comboBadge == null || comboText == null) return;

            badgeSeq?.Kill();
            comboText.text = "×" + streak;
            comboText.alpha = 1f;
            comboBadge.gameObject.SetActive(true);
            comboBadge.anchoredPosition = badgeRest;
            comboBadge.localRotation = badgeRestRotation;
            comboBadge.localScale = Vector3.one;

            badgeSeq = DOTween.Sequence()
                .Append(comboBadge.DOPunchScale(Vector3.one * 0.35f, 0.3f, 7, 0.8f))
                .SetLink(gameObject);
        }

        /// <summary>Streak lost: the badge tips over and falls away.</summary>
        private void DropBadge()
        {
            if (comboBadge == null || !comboBadge.gameObject.activeSelf) return;

            badgeSeq?.Kill();
            comboBadge.localScale = Vector3.one;
            badgeSeq = DOTween.Sequence()
                .Append(comboBadge.DOAnchorPosY(badgeRest.y - 90f, 0.35f).SetEase(Ease.InQuad))
                .Join(comboBadge.DOLocalRotate(new Vector3(0f, 0f, -25f), 0.35f).SetEase(Ease.InQuad))
                .Join(comboText.DOFade(0f, 0.35f).SetEase(Ease.InQuad))
                .SetLink(gameObject)
                .OnComplete(() => comboBadge.gameObject.SetActive(false));
        }

        // ------------------------------------------------------------------ Bonus / arrows

        private void HandleBonus(OnBonusCollected signalData)
        {
            if (signalData == null) return;

            if (SoundManger.Instance != null) SoundManger.Instance.PlayBonusCollect();
            Haptics.Heavy();
            Shake(bonusShake, 0.3f);

            if (signalData.type == BonusType.Lightning)
                Flash(0.55f, 0.3f);
        }

        private void HandleArrows(OnAddArrows signalData)
        {
            // Negative values are the bow spending an arrow.
            if (signalData == null || signalData.arrowsToGive <= 0) return;

            if (SoundManger.Instance != null) SoundManger.Instance.PlayArrowGain();
        }

        private void Flash(float alpha, float duration)
        {
            if (screenFlash == null) return;

            flashTween?.Kill();
            screenFlash.gameObject.SetActive(true);
            Color c = screenFlash.color;
            c.a = alpha;
            screenFlash.color = c;
            flashTween = screenFlash.DOFade(0f, duration).SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() => screenFlash.gameObject.SetActive(false));
        }

        // ------------------------------------------------------------------ Shake

        /// <summary>
        /// Shake the camera. A weaker shake never interrupts a stronger one, and the camera is
        /// always returned to its rest position - both HUD canvases are Screen Space - Camera,
        /// so they ride along and only the world appears to move.
        /// </summary>
        private void Shake(float strength, float duration)
        {
            if (cam == null || strength <= 0f) return;
            if (shakeTween != null && shakeTween.IsActive() && strength < activeShake) return;

            shakeTween?.Kill();
            cam.transform.position = camRest;
            activeShake = strength;

            shakeTween = cam.transform
                .DOShakePosition(duration, new Vector3(strength, strength, 0f), 24, 90f, false, true)
                .SetLink(gameObject)
                .OnKill(() =>
                {
                    activeShake = 0f;
                    if (cam != null) cam.transform.position = camRest;
                });
        }
    }
}
