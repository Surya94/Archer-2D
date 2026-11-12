namespace Archer.Scripts.Models
{
    /// <summary>
    /// Dispatched whenever a player setting (sound, music, vibration) changes, so anything that
    /// caches the value - SoundManger's music source, the Settings toggles - can re-read it.
    /// </summary>
    public class OnSettingsChanged
    {
    }
}
