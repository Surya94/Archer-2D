using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObject", menuName = "ScriptableObjects/BowSoundData", order = 2)]
public class BowSoundManager : ScriptableObject
{
    public AudioClip fireSound;
    public AudioClip loadSound;
    public AudioClip hitSound;
    public void PlayFireSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(fireSound, pos);
    }
    public void PlayLoadSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(loadSound, pos);
    }
    public void PlayHitSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(hitSound, pos);
    }
}
