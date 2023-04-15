using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObject", menuName = "ScriptableObjects/Bow Data", order = 1)]
public class BowData : ScriptableObject
{
    public float MaxForce;
    public float MinForce;
    public int numberOfPoints;
    public float spaceBtwpoints;
    public float waitTime = 0.5f;
    public bool enableAimAssit = false;
}
