namespace Archer.Scripts.Models
{
    /// <summary>
    /// Dispatched when the player lifts their finger after a bow drag. fired is false when the
    /// pull was too short to loose the arrow.
    /// </summary>
    public class OnBowDragEnded
    {
        public bool fired;
        public float power;
    }
}
