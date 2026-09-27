using UnityEngine;

/// <summary>
/// Dispatched when a bonus balloon is popped. BonusEffectController listens and runs the
/// effect; position is where the carried item was, so effects start from it.
/// </summary>
public class OnBonusCollected
{
    public BonusType type;
    public Vector3 position;
}
