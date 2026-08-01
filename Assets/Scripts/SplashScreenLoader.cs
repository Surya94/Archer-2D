using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        }
    }


}