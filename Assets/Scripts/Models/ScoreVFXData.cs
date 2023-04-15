using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreVFXData : MonoBehaviour
{
    public Text arrowCount;
    private VFXHandler VFXHandler;

    public void Init(VFXHandler handler, int points)
    {
        VFXHandler = handler;
        SetArrowData(points);
    }
    public void SetArrowData(int count)
    {
        arrowCount.text = "+" + count;
    }

    public void DisableObject()
    {
        if (VFXHandler != null)
            VFXHandler.ReturnObjectVFXToPool(gameObject);
    }
}
