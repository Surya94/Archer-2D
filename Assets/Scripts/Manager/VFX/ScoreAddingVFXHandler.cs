using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreAddingVFXHandler : VFXHandler
{
    // Where the popups fly to - the HUD's score pill.
    public RectTransform scoreTarget;

    private ScoreManager scoreManager;

    public override void Init()
    {
        scoreManager = DependencyResolver.Resolve<ScoreManager>();
        SignalManager.Instance.AddObserver<OnBalloonBurst>(UpdateScore);
        base.Init();
    }

    public override void Dinit()
    {
        // Null-conditional: Singleton.Instance returns null once applicationIsQuitting
        // is set, and Dinit now genuinely runs on destroy (the base class callback used
        // to be misspelled OnDestory and never fired).
        SignalManager.Instance?.RemoveObserver<OnBalloonBurst>(UpdateScore);
        base.Dinit();
    }

    private void UpdateScore(OnBalloonBurst signalData)
    {
        if (signalData == null)
            return;

        GameObject spawnedObject = GetUnusedObject();
        ScoreVFXData vfxData = spawnedObject.GetComponentInChildren<ScoreVFXData>();
        vfxData.Init(this, signalData.pointsToGive);

        // Get the screen position of the world position
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(signalData.position);

        // Convert the screen position to canvas position
        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPosition, canvas.worldCamera, out canvasPosition);

        // Set the position of the UI element to the canvas position
        spawnedObject.transform.localPosition = canvasPosition;

        // The popup is a direct child of the canvas, so the pill's position converted into
        // canvas space is the popup's localPosition on arrival.
        Vector3 target = scoreTarget != null
            ? canvas.transform.InverseTransformPoint(scoreTarget.position)
            : (Vector3)canvasPosition;
        target.z = 0f;

        vfxData.Play(target, signalData.pointsToGive, scoreManager);
    }

}
