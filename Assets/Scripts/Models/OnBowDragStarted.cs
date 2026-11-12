using UnityEngine;

namespace Archer.Scripts.Models
{
    /// <summary>
    /// Dispatched when the player presses down to start drawing the bow. The first-run hint
    /// reacts to it; the bow reference identifies which bow the drag belongs to.
    /// </summary>
    public class OnBowDragStarted
    {
        public Bow bow;
        public Vector2 screenPosition;
    }
}
