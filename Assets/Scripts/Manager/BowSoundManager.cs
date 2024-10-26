using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObject", menuName = "ScriptableObjects/BowSoundData", order = 2)]
public class BowSoundManager : ScriptableObject
{
    public AudioClip fireSound;
    public AudioClip loadSound;
    public AudioClip hitSound;

}
