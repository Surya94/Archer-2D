using UnityEngine;

public class OnBalloonBurst 
{
    public int pointsToGive;
    public Vector3 position;
    // Presentation only (pop particles and sound); nothing scores off these.
    public Color colour = Color.white;
    /// <summary>World-space height of the balloon's sprite, so effects scale with it.</summary>
    public float size = 1f;
}
