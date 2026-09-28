using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Archer.Scripts.Manager;
using Archer.Scripts.Manager.Ads;

namespace Archer.Scripts
{
    public class SplashScreenLoader
    {
        [RuntimeInitializeOnLoadMethod]
        public static void LoadDataInRunTime()
        {
            Debug.Log("Game Initialized on run time");
            DependencyInjectionManager.Initialize();
            DependencyInjectionManager.InitializeServices();
            _ = GameManager.Instance;
            // Start the ad SDK and the first rewarded load now, so a revive ad has had the
            // whole first run to load by the time Game Over asks for it.
            AdsManager.Instance.Preload();
        }
    }


}