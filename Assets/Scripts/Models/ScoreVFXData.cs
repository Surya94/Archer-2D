using DG.Tweening;
using TMPro;
using UnityEngine;

public class ScoreVFXData : MonoBehaviour
{
    // GamePlayHUD delays its roll-up by TotalDuration so the counter ticks the moment the
    // popup lands in it. Change the timings here and both stay in sync.
    public const float PopDuration = 0.18f;
    public const float HoldDuration = 0.22f;
    public const float FlyDuration = 0.38f;
    public const float TotalDuration = PopDuration + HoldDuration + FlyDuration;

    public TextMeshProUGUI label;
    public Color comboColour = new Color32(0xE8, 0x54, 0x3F, 0xFF);

    private VFXHandler VFXHandler;
    private Sequence sequence;

    public void Init(VFXHandler handler, int points)
    {
        VFXHandler = handler;
        label.text = "+" + points;
    }

    /// <summary>
    /// Pop in where the balloon burst, then arc into the score counter. targetLocalPosition
    /// is in the same space as this object's localPosition (the canvas).
    /// </summary>
    public void Play(Vector3 targetLocalPosition, int points, ScoreManager scoreManager)
    {
        sequence?.Kill();
        transform.localScale = Vector3.zero;

        // X eases in and Y eases out, so the popup rises first and then swings across -
        // a curve rather than a straight line to the counter.
        sequence = DOTween.Sequence()
            .Append(transform.DOScale(1.25f, PopDuration).SetEase(Ease.OutBack))
            .AppendCallback(() => ShowCombo(points, scoreManager))
            .Append(transform.DOScale(1f, HoldDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOLocalMoveX(targetLocalPosition.x, FlyDuration).SetEase(Ease.InQuad))
            .Join(transform.DOLocalMoveY(targetLocalPosition.y, FlyDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(0.45f, FlyDuration).SetEase(Ease.InQuad))
            .SetLink(gameObject)
            .OnComplete(DisableObject);
    }

    // Read after the pop rather than at spawn: Enemy dispatches OnBalloonBurst from inside
    // TakeDamage, before Arrow dispatches OnUpdateStreak for the same hit, so at spawn time
    // streakCount is still one behind.
    private void ShowCombo(int points, ScoreManager scoreManager)
    {
        if (scoreManager == null || scoreManager.streakCount <= 1)
            return;

        string hex = ColorUtility.ToHtmlStringRGB(comboColour);
        label.text = "+" + points + " <size=70%><color=#" + hex + ">x" + scoreManager.streakCount + "</color></size>";
    }

    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;
    }

    public void DisableObject()
    {
        if (VFXHandler != null)
            VFXHandler.ReturnObjectVFXToPool(gameObject);
    }
}
