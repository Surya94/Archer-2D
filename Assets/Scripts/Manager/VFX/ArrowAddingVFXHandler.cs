using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowAddingVFXHandler : VFXHandler
{
    public override void Init()
    {
        SignalManager.Instance.AddObserver<OnAddArrows>(OnAddArrows);
        base.Init();
    }

    public override void Dinit()
    {
        // Null-conditional: Singleton.Instance returns null once applicationIsQuitting
        // is set, and Dinit now genuinely runs on destroy (the base class callback used
        // to be misspelled OnDestory and never fired).
        SignalManager.Instance?.RemoveObserver<OnAddArrows>(OnAddArrows);
        base.Dinit();
    }

    private void OnAddArrows(OnAddArrows signalData)
    {
        if (signalData == null || signalData.arrowsToGive <= 0)
            return;

        GameObject spawnedObject = GetUnusedObject();
        spawnedObject.GetComponent<ArrowVFXData>().Init(this, signalData.arrowsToGive);
    }
}
