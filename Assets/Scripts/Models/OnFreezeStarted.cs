/// <summary>
/// Dispatched when a Time bonus freezes the field. Balloons are frozen directly by
/// BonusEffectController; spawner, clouds and the HUD overlay react to this.
/// </summary>
public class OnFreezeStarted
{
    public float duration;
}
