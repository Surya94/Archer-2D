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
            // The banner is shown once here and persists across every scene (menus and play).
            AdsManager.Instance.ShowBanner();
        }
    }


}