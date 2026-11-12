using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObject", menuName = "ScriptableObjects/BowSoundData", order = 2)]
public class BowSoundManager : ScriptableObject
{
    [Header("Bow")]
    public AudioClip fireSound;
    public AudioClip loadSound;
    public AudioClip hitSound;

    [Header("Balloons")]
    public AudioClip popSound;
    public AudioClip bigPopSound;

    [Header("Streak")]
    public AudioClip comboStepSound;
    public AudioClip arrowGainSound;
    public AudioClip missSound;

    [Header("Bonus")]
    public AudioClip freezeSound;
    public AudioClip thawSound;
    public AudioClip bonusCollectSound;
    public AudioClip lightningZapSound;
    public AudioClip shurikenSound;
    public AudioClip shurikenHitSound;

    [Header("UI")]
    public AudioClip uiClickSound;
    public AudioClip uiBackSound;
    [Tooltip("Settings ON/OFF switches. Falls back to the click when empty.")]
    public AudioClip uiToggleSound;
    public AudioClip gameOverSound;
    public AudioClip newBestSound;

    [Header("Music (optional - leave empty for no music)")]
    public AudioClip music;
}
