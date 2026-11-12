using UnityEngine;
using Archer.Scripts.Models;

namespace Archer.Scripts.Utility
{
    /// <summary>
    /// Player-facing options, persisted in PlayerPrefs. Deliberately tiny and static: the planned
    /// EconomyManager (PLAN.md Stage 4) can take these keys over without any caller changing.
    /// </summary>
    public static class GameSettings
    {
        private const string SfxKey = "Settings.SfxOn";
        private const string MusicKey = "Settings.MusicOn";
        private const string HapticsKey = "Settings.HapticsOn";
        private const string SfxVolumeKey = "Settings.SfxVolume";
        private const string MusicVolumeKey = "Settings.MusicVolume";

        private static bool loaded;
        private static bool sfxOn, musicOn, hapticsOn;
        private static float sfxVolume, musicVolume;

        // Statics survive play-mode restarts when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => loaded = false;

        public static bool SfxOn
        {
            get { Load(); return sfxOn; }
            set { Load(); if (sfxOn == value) return; sfxOn = value; Save(SfxKey, value); }
        }

        public static bool MusicOn
        {
            get { Load(); return musicOn; }
            set { Load(); if (musicOn == value) return; musicOn = value; Save(MusicKey, value); }
        }

        public static bool HapticsOn
        {
            get { Load(); return hapticsOn; }
            set { Load(); if (hapticsOn == value) return; hapticsOn = value; Save(HapticsKey, value); }
        }

        public static float SfxVolume
        {
            get { Load(); return sfxVolume; }
            set { Load(); sfxVolume = Mathf.Clamp01(value); Save(SfxVolumeKey, sfxVolume); }
        }

        public static float MusicVolume
        {
            get { Load(); return musicVolume; }
            set { Load(); musicVolume = Mathf.Clamp01(value); Save(MusicVolumeKey, musicVolume); }
        }

        private static void Load()
        {
            if (loaded) return;

            sfxOn = PlayerPrefs.GetInt(SfxKey, 1) == 1;
            musicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            hapticsOn = PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);
            loaded = true;
        }

        private static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Notify();
        }

        private static void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Notify();
        }

        private static void Notify()
        {
            // Instance is null while the application is quitting.
            SignalManager.Instance?.DispatchSignal(new OnSettingsChanged());
        }
    }
}
