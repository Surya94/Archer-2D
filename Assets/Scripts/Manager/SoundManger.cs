using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Archer.Scripts.Models;
using Archer.Scripts.Utility;

/// <summary>
/// All game audio. Sound effects play through a small pool of 2D AudioSources (the old
/// AudioSource.PlayClipAtPoint spawned a 3D source at the world origin for every sound, so
/// volume depended on where the camera happened to be). Each call can vary pitch slightly, and
/// a per-clip cooldown stops a burst of identical sounds - a lightning chain popping six
/// balloons - from stacking into one loud click.
///
/// Created lazily and DontDestroyOnLoad (see Singleton), so it must never be placed in a scene.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundManger : Singleton<SoundManger>
{
    public const string BOW_SOUND_MANAGER = "Scriptables/Sounds/BowSoundData";

    private const int VoiceCount = 10;
    private const float DefaultCooldown = 0.03f;
    private const float Semitone = 1.059463f;

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

    /// <summary>True when a music track is assigned - the Settings panel hides its Music row otherwise.</summary>
    public bool HasMusic => BowSoundManager != null && BowSoundManager.music != null;

    private readonly List<AudioSource> voices = new List<AudioSource>();
    private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
    private AudioSource musicSource;
    private Coroutine musicFade;
    private int nextVoice;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        musicSource = GetComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;

        for (int i = 0; i < VoiceCount; i++)
        {
            GameObject go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices.Add(source);
        }

        SignalManager.Instance?.AddObserver<OnSettingsChanged>(HandleSettingsChanged);
    }

    protected override void OnDestroy()
    {
        SignalManager.Instance?.RemoveObserver<OnSettingsChanged>(HandleSettingsChanged);
        base.OnDestroy();
    }

    /// <summary>Kept for existing callers: touching Instance is what creates the manager.</summary>
    public void Init()
    {
        RefreshMusic();
    }

    // ------------------------------------------------------------------ Core

    /// <summary>
    /// Play a one-shot. Null clips are ignored, so optional slots can stay empty.
    /// pitchJitter is +/- that fraction around pitch; cooldown is the minimum gap, in real
    /// seconds, before the same clip may play again.
    /// </summary>
    public void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pitchJitter = 0f,
        float cooldown = DefaultCooldown)
    {
        if (clip == null || !GameSettings.SfxOn || voices.Count == 0) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < cooldown) return;
        lastPlayed[clip] = now;

        AudioSource source = NextVoice();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * GameSettings.SfxVolume);
        source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        source.Play();
    }

    /// <summary>A free voice if there is one, otherwise the oldest - which is the least missed.</summary>
    private AudioSource NextVoice()
    {
        for (int i = 0; i < voices.Count; i++)
        {
            int index = (nextVoice + i) % voices.Count;
            if (!voices[index].isPlaying)
            {
                nextVoice = (index + 1) % voices.Count;
                return voices[index];
            }
        }

        AudioSource stolen = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        return stolen;
    }

    // ------------------------------------------------------------------ Bow

    /// <param name="power">0..1 draw strength; a fuller draw releases lower and louder.</param>
    public void PlayFireSound(float power = 1f)
    {
        Play(BowSoundManager.fireSound, Mathf.Lerp(0.7f, 1f, power), Mathf.Lerp(1.15f, 0.92f, power), 0.04f);
    }

    public void PlayLoadSound()
    {
        Play(BowSoundManager.loadSound, 0.8f, 1f, 0.05f, 0.15f);
    }

    public void PlayHitSound()
    {
        Play(BowSoundManager.hitSound, 0.75f, 1f, 0.08f);
    }

    // ------------------------------------------------------------------ Balloons

    /// <summary>
    /// Balloon pop. Each consecutive hit in a streak raises the pitch a semitone (capped), so a
    /// good run climbs audibly. Falls back to the arrow-impact clip if no pop clip is assigned.
    /// </summary>
    public void PlayPop(int streak = 0, bool big = false)
    {
        BowSoundManager data = BowSoundManager;
        AudioClip clip = big && data.bigPopSound != null ? data.bigPopSound : data.popSound;
        if (clip == null) clip = data.hitSound;

        float pitch = Mathf.Pow(Semitone, Mathf.Clamp(streak, 0, 10));
        Play(clip, 1f, pitch, 0.04f, 0.04f);
    }

    /// <param name="tier">0-based callout tier; higher tiers ring higher.</param>
    public void PlayComboStep(int tier)
    {
        Play(BowSoundManager.comboStepSound, 0.9f, Mathf.Pow(Semitone, Mathf.Clamp(tier, 0, 6) * 2f));
    }

    public void PlayArrowGain() => Play(BowSoundManager.arrowGainSound, 0.8f, 1f, 0.03f, 0.08f);
    public void PlayMiss() => Play(BowSoundManager.missSound, 0.55f, 1f, 0.05f, 0.1f);

    // ------------------------------------------------------------------ Bonus

    public void PlayFreezeSound() => Play(BowSoundManager.freezeSound);
    public void PlayThawSound() => Play(BowSoundManager.thawSound);
    public void PlayBonusCollect() => Play(BowSoundManager.bonusCollectSound, 0.9f);
    public void PlayLightningZap() => Play(BowSoundManager.lightningZapSound, 0.8f, 1f, 0.12f, 0.05f);
    public void PlayShurikenThrow() => Play(BowSoundManager.shurikenSound, 0.85f);
    public void PlayShurikenHit() => Play(BowSoundManager.shurikenHitSound, 0.7f, 1f, 0.12f, 0.05f);

    // ------------------------------------------------------------------ UI

    public void PlayButtonClickSound() => Play(BowSoundManager.uiClickSound, 0.8f, 1f, 0.03f);
    public void PlayBackSound() => Play(BowSoundManager.uiBackSound, 0.8f, 1f, 0.03f);

    public void PlayToggleSound()
    {
        BowSoundManager data = BowSoundManager;
        Play(data.uiToggleSound != null ? data.uiToggleSound : data.uiClickSound, 0.8f, 1f, 0.03f);
    }
    public void PlayGameOverSound() => Play(BowSoundManager.gameOverSound, 0.9f);
    public void PlayNewBestSound() => Play(BowSoundManager.newBestSound, 1f);

    public void PlaySFX(AudioClip clip) => Play(clip);

    // ------------------------------------------------------------------ Settings

    public void SetSFXEnabled(bool enabled) => GameSettings.SfxOn = enabled;
    public void SetMusicEnabled(bool enabled) => GameSettings.MusicOn = enabled;
    public void SetSFXVolume(float volume) => GameSettings.SfxVolume = volume;
    public void SetMusicVolume(float volume) => GameSettings.MusicVolume = volume;

    private void HandleSettingsChanged(OnSettingsChanged signalData)
    {
        if (!GameSettings.SfxOn)
        {
            foreach (AudioSource voice in voices)
                voice.Stop();
        }

        RefreshMusic();
    }

    // ------------------------------------------------------------------ Music

    /// <summary>
    /// Start, stop or re-level the music to match the current settings. Does nothing when no
    /// track is assigned, so the game ships silent-by-design until one is dropped into
    /// BowSoundData.music.
    /// </summary>
    public void RefreshMusic()
    {
        if (musicSource == null || !HasMusic) return;

        bool shouldPlay = GameSettings.MusicOn;
        float target = shouldPlay ? GameSettings.MusicVolume : 0f;

        if (shouldPlay && !musicSource.isPlaying)
        {
            musicSource.clip = BowSoundManager.music;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(target, 0.6f, !shouldPlay));
    }

    // Unscaled: the music must keep fading while the game is paused (timeScale 0).
    private IEnumerator FadeMusic(float target, float duration, bool stopAtEnd)
    {
        float start = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        musicSource.volume = target;
        if (stopAtEnd) musicSource.Stop();
        musicFade = null;
    }
}
