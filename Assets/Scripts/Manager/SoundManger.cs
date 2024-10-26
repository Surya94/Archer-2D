using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManger : Singleton<SoundManger>
{
    public const string BOW_SOUND_MANAGER = "Scriptables/Sounds/BowSoundData";
    public BowSoundManager BowSoundManager 
    {
        get 
        {
            if (bowSoundManager == null)
                bowSoundManager = Resources.Load<BowSoundManager>(BOW_SOUND_MANAGER);
            return bowSoundManager;
        }
    }
    private BowSoundManager bowSoundManager;

    public void Init()
    {
        
    }
    public void PlayFireSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(BowSoundManager.fireSound, pos);
    }
    public void PlayLoadSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(BowSoundManager.loadSound, pos);
    }
    public void PlayHitSound(Vector3 pos = default)
    {
        AudioSource.PlayClipAtPoint(BowSoundManager.hitSound, pos);
    }
}
