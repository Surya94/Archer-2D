using System.Collections;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class GamePlayHUD : MonoBehaviour
{
    [Header("Score")]
    public RectTransform scorePill;
    public TextMeshProUGUI score;
    public TextMeshProUGUI bestScore;
    public float rollDuration = 0.35f;

    [Header("Arrows")]
    public RectTransform arrowPill;
    public RectTransform arrowIcon;
    public TextMeshProUGUI arrowCount;
    public int lowArrowThreshold = 3;

    [Header("Colours")]
    public Color normalColour = new Color32(0x2E, 0x40, 0x57, 0xFF);
    public Color gainColour = new Color32(0x3E, 0x9B, 0x2F, 0xFF);
    public Color lowColour = new Color32(0xE8, 0x54, 0x3F, 0xFF);
    public Color bestColour = new Color32(0x6B, 0x5B, 0x45, 0xFF);
    public Color newBestColour = new Color32(0xE0, 0x9A, 0x10, 0xFF);

    private ScoreManager scoreManager;
    private bool ready;

    private int displayedScore;
    private int shownArrows;
    private int bestAtRunStart;
    private bool newBestShown;
    private Vector2 iconRestPosition;
    private bool iconRestCaptured;

    private Tween scoreRoll;
    private Tween scorePunch;
    private Tween bestPunch;
    private Tween arrowPunch;
    private Tween iconPunch;
    private Tween arrowFlash;
    private Sequence lowPulse;

    IEnumerator Start()
    {
        scoreManager = DependencyResolver.Resolve<ScoreManager>();
        SignalManager.Instance.AddObserver<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.AddObserver<OnArrowsAdded>(UpdateArrows);

        // Bow.Start resets the run (score 0, full quiver) and nocks the first arrow, and
        // Start order between the two isn't guaranteed. Wait a frame and snap to the settled
        // values instead of animating away from whatever the previous run left behind.
        yield return null;

        displayedScore = scoreManager.CurrentScore;
        shownArrows = scoreManager.arrowCount;
        // Captured once: OnGameOver promotes BestScore mid-run, and a revive would then
        // compare against this run's own score.
        bestAtRunStart = scoreManager.BestScore;

        SetScoreText(displayedScore);
        SetBestText();
        SetArrowText();
        RefreshLowArrowState();
        ready = true;
    }

    void OnDestroy()
    {
        // Null-conditional: Singleton.Instance returns null once applicationIsQuitting is set,
        // so on shutdown this threw a NullReferenceException.
        SignalManager.Instance?.RemoveObserver<OnUpdateScore>(UpdateScore);
        SignalManager.Instance?.RemoveObserver<OnArrowsAdded>(UpdateArrows);

        // SetLink already kills these with the GameObject; explicit for the ones that loop.
        lowPulse?.Kill();
        scoreRoll?.Kill();
    }

    // ------------------------------------------------------------------ Score

    private void UpdateScore(OnUpdateScore signalData)
    {
        if (!ready) return;

        // Hold the roll until the score popup has flown into the pill, so the number ticks
        // on arrival. Each burst schedules its own landing; targets only grow, so a later
        // landing simply retargets the running roll upward.
        // ignoreTimeScale: false - DelayedCall defaults to real time, which would run ahead of
        // the (scaled-time) popup and keep rolling while the game is paused.
        int target = scoreManager.CurrentScore;
        DOVirtual.DelayedCall(ScoreVFXData.TotalDuration, () => RollScoreTo(target), false)
            .SetLink(gameObject);
    }

    private void RollScoreTo(int target)
    {
        scoreRoll?.Kill();
        scoreRoll = DOTween.To(() => displayedScore, SetDisplayedScore, target, rollDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject);

        Punch(scorePill, 0.18f, ref scorePunch);

        if (!newBestShown && bestAtRunStart > 0 && target > bestAtRunStart)
            ShowNewBest();
    }

    private void SetDisplayedScore(int value)
    {
        displayedScore = value;
        SetScoreText(value);
    }

    private void SetScoreText(int value)
    {
        if (score != null)
            score.text = value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void SetBestText()
    {
        if (bestScore == null) return;

        // Nothing to beat on the very first run - hide the line rather than show "BEST 0".
        bestScore.gameObject.SetActive(bestAtRunStart > 0);
        bestScore.color = bestColour;
        bestScore.text = "BEST " + bestAtRunStart.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void ShowNewBest()
    {
        newBestShown = true;
        if (bestScore == null) return;

        bestScore.text = "NEW BEST!";
        bestScore.color = newBestColour;
        Punch((RectTransform)bestScore.transform, 0.4f, ref bestPunch);
    }

    // ------------------------------------------------------------------ Arrows

    private void UpdateArrows(OnArrowsAdded signalData)
    {
        if (!ready) return;

        int previous = shownArrows;
        shownArrows = scoreManager.arrowCount;
        SetArrowText();

        if (shownArrows < previous)
        {
            // Spent one: a quick squash on the number and a kick on the icon, like it left the quiver.
            Punch((RectTransform)arrowCount.transform, -0.15f, ref arrowPunch);
            iconPunch?.Kill();
            arrowIcon.anchoredPosition = iconRestPosition;
            iconPunch = arrowIcon.DOPunchAnchorPos(new Vector2(18f, 0f), 0.25f, 6, 0.5f)
                .SetLink(gameObject);
        }
        else if (shownArrows > previous)
        {
            Punch((RectTransform)arrowCount.transform, 0.45f, ref arrowPunch);
            Punch(arrowIcon, 0.3f, ref iconPunch);
        }

        RefreshLowArrowState();

        // Green flash on a gain - but only when the low-arrow pulse doesn't own the colour.
        if (shownArrows > previous && lowPulse == null)
        {
            arrowFlash?.Kill();
            arrowCount.color = gainColour;
            arrowFlash = arrowCount.DOColor(normalColour, 0.6f)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject);
        }
    }

    private void SetArrowText()
    {
        if (arrowIcon != null && !iconRestCaptured)
        {
            iconRestPosition = arrowIcon.anchoredPosition;
            iconRestCaptured = true;
        }

        if (arrowCount != null)
            arrowCount.text = "×" + shownArrows;
    }

    /// <summary>
    /// A looping red pulse while the quiver is at or below the threshold. arrowCount excludes
    /// the arrow already nocked on the bow, so 0 still means one shot left - the pulse just
    /// gets faster there rather than greying out.
    /// </summary>
    private void RefreshLowArrowState()
    {
        bool low = shownArrows <= lowArrowThreshold;
        float period = shownArrows == 0 ? 0.3f : 0.55f;

        if (!low)
        {
            if (lowPulse != null)
            {
                lowPulse.Kill();
                lowPulse = null;
                arrowPill.localScale = Vector3.one;
                arrowCount.color = normalColour;
            }
            return;
        }

        // Rebuild on every change so the tempo tracks the count.
        lowPulse?.Kill();
        arrowFlash?.Kill();
        arrowPill.localScale = Vector3.one;
        arrowCount.color = lowColour;

        lowPulse = DOTween.Sequence()
            .Append(arrowPill.DOScale(1.08f, period).SetEase(Ease.InOutSine))
            .Join(arrowCount.DOColor(normalColour, period).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>
    /// Punch a transform's scale, first snapping it back to rest so a punch that interrupts
    /// another doesn't leave it permanently shrunk or swollen.
    /// </summary>
    private void Punch(RectTransform target, float strength, ref Tween handle)
    {
        if (target == null) return;

        handle?.Kill();
        target.localScale = Vector3.one;
        handle = target.DOPunchScale(Vector3.one * strength, 0.3f, 7, 0.8f)
            .SetLink(gameObject);
    }
}
