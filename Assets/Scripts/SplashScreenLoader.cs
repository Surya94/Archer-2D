using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Archer.Scripts.Manager;
using Archer.Scripts.Manager.Ads;

namespace Archer.Scripts
{
    public class SplashScreenLoader
    {
        /// <summary>
        /// Release builds drop plain Debug.Log output (one fires per balloon, plus the signal/DI
        /// chatter), which costs CPU and battery on a phone. Warnings and errors still log.
        /// Runs before any scene loads so Awake-time logs are filtered too. Editor and
        /// Development builds keep full logging.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ConfigureLogging()
        {
            Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;
        }

        [RuntimeInitializeOnLoadMethod]
        public static void LoadDataInRunTime()
        {
            Debug.Log("Game Initialized on run time");
            // Android defaults to 30 fps, which makes every tween and the bow drag feel sluggish.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            DependencyInjectionManager.Initialize();
            DependencyInjectionManager.InitializeServices();
            _ = GameManager.Instance;
            // Start the ad SDK and the first rewarded load now, so a revive ad has had the
            // whole first run to load by the time Game Over asks for it.
            AdsManager.Instance.Preload();
        }
    }


}