using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreAddingVFXHandler : VFXHandler
{
    public override void Init()
    {
        SignalManager.Instance.AddObserver<OnBalloonBurst>(UpdateScore);
        base.Init();
    }

    public override void Dinit()
    {
        SignalManager.Instance.RemoveObserver<OnBalloonBurst>(UpdateScore);
        base.Dinit();
    }

    private void UpdateScore(OnBalloonBurst signalData)
    {
        if (signalData == null)
            return;

        GameObject spawnedObject = GetUnusedObject();
        spawnedObject.GetComponentInChildren<ScoreVFXData>().Init(this, signalData.pointsToGive);

        // Get the screen position of the world position
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(signalData.position);

        // Convert the screen position to canvas position
        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPosition, canvas.worldCamera, out canvasPosition);
      
        // Set the position of the UI element to the canvas position
        spawnedObject.transform.localPosition = canvasPosition;
    }

}
