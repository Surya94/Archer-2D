using DG.Tweening;
using UnityEngine;

/// <summary>
/// A balloon carrying a bonus item on a string beneath it. Popping it hides the item and
/// dispatches OnBonusCollected; BonusEffectController does the rest. The prefab sets
/// maxHealth to one arrow's damage, so a single hit collects it.
/// </summary>
public class BonusBalloon : Enemy
{
    public BonusType bonusType;

    [Tooltip("Pivot the string and item hang from - rotated for the sway.")]
    public Transform tether;
    [Tooltip("The carried item. Its position is where the effect starts.")]
    public Transform payload;
    [Tooltip("Soft halo behind the balloon marking it as special.")]
    public SpriteRenderer glow;

    public float swayAngle = 9f;
    public float swayPeriod = 0.9f;

    private Tween sway;
    private Tween glowPulse;
    private Vector3 glowBaseScale = Vector3.one;

    // The prefab authors the halo's size; the pulse is relative to it.
    void Awake()
    {
        if (glow != null)
            glowBaseScale = glow.transform.localScale;
    }

    public override void OnObjectSpawn()
    {
        base.OnObjectSpawn();

        if (tether != null)
        {
            tether.gameObject.SetActive(true);
            tether.localRotation = Quaternion.Euler(0f, 0f, -swayAngle);
            sway?.Kill();
            sway = tether.DOLocalRotate(new Vector3(0f, 0f, swayAngle), swayPeriod)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        if (glow != null)
        {
            glow.gameObject.SetActive(true);
            glow.transform.localScale = glowBaseScale;
            glowPulse?.Kill();
            glowPulse = glow.transform.DOScale(glowBaseScale * 1.15f, 0.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }
    }

    public override void SetFrozen(bool frozen, Color iceColour)
    {
        if (!IsAlive || frozen == IsFrozen) return;
        base.SetFrozen(frozen, iceColour);

        // The sway and halo pulse are on other transforms, so the base DOPause misses them.
        if (frozen)
        {
            sway?.Pause();
            glowPulse?.Pause();
        }
        else
        {
            sway?.Play();
            glowPulse?.Play();
        }
    }

    protected override void OnBurst()
    {
        Vector3 itemPosition = payload != null ? payload.position : transform.position;

        StopTweens();
        // The effect takes over the item's visuals from here, so the balloon's copy vanishes.
        if (tether != null) tether.gameObject.SetActive(false);
        if (glow != null) glow.gameObject.SetActive(false);

        SignalManager.Instance.DispatchSignal(new OnBonusCollected
        {
            type = bonusType,
            position = itemPosition
        });
    }

    public override void OnObjectDespawn()
    {
        StopTweens();
        base.OnObjectDespawn();
    }

    protected override void OnDestroy()
    {
        StopTweens();
        base.OnDestroy();
    }

    private void StopTweens()
    {
        sway?.Kill();
        glowPulse?.Kill();
        sway = null;
        glowPulse = null;
    }
}
