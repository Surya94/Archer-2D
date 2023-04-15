using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreVFXHolder : MonoBehaviour
{
    public ScoreVFXData ScoreVFXData;

    public void DisableVFX()
    {
        if (ScoreVFXData != null)
            ScoreVFXData.DisableObject();
    }
}
