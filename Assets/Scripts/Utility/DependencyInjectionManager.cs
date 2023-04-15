using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DependencyInjectionManager
{
    public static void Init()
    {
        DependencyResolver.Register<ScoreManager>();
    }

    public static void LoadData()
    {
        DependencyResolver.Resolve<ScoreManager>();
    }
}
