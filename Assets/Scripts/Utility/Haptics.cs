using UnityEngine;

namespace Archer.Scripts.Utility
{
    /// <summary>
    /// Short vibration cues. Android only (VibrationEffect, API 26+, which is the project's
    /// minimum SDK); a no-op in the Editor and on every other platform. Respects the Vibration
    /// setting and rate-limits itself so a lightning chain doesn't turn into one long buzz.
    /// </summary>
    public static class Haptics
    {
        private const float MinInterval = 0.045f;
        private static float lastTime = -1f;

        public static void Light() => OneShot(12, 70);
        public static void Medium() => OneShot(22, 150);
        public static void Heavy() => OneShot(38, 255);

        /// <summary>Two rising taps - for a new best score.</summary>
        public static void Success() =>
            Pattern(new long[] { 0, 18, 70, 34 }, new[] { 0, 110, 0, 255 });

        private static bool CanFire()
        {
            if (!GameSettings.HapticsOn) return false;

            float now = Time.unscaledTime;
            if (now - lastTime < MinInterval) return false;
            lastTime = now;
            return true;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool initialised;
        private static bool available;
        private static bool hasAmplitude;
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effectClass;

        private static void Init()
        {
            if (initialised) return;
            initialised = true;

            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                available = vibrator != null && vibrator.Call<bool>("hasVibrator");
                hasAmplitude = available && vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception e)
            {
                available = false;
                Debug.LogWarning("[Haptics] Vibrator unavailable: " + e.Message);
            }
        }

        private static void OneShot(long milliseconds, int amplitude)
        {
            if (!CanFire()) return;
            Init();
            if (!available) return;

            try
            {
                // Without amplitude control every pulse runs at full strength, so the three
                // weights are told apart by duration alone; -1 is DEFAULT_AMPLITUDE.
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, hasAmplitude ? amplitude : -1))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception)
            {
                // Also keeps a Handheld.Vibrate reference in the build, which is what makes
                // Unity add the VIBRATE permission to the generated manifest.
                Handheld.Vibrate();
            }
        }

        private static void Pattern(long[] timings, int[] amplitudes)
        {
            if (!CanFire()) return;
            Init();
            if (!available) return;

            try
            {
                using (AndroidJavaObject effect = hasAmplitude
                    ? effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1)
                    : effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, -1))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
        }
#else
        private static void OneShot(long milliseconds, int amplitude) => CanFire();
        private static void Pattern(long[] timings, int[] amplitudes) => CanFire();
#endif
    }
}
