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
            _ = AdsManager.Instance;
        }
    }


}