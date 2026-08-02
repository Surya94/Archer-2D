using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayHUD : MonoBehaviour
{
    public Text score;
    public Text arrowCount;
    public ScoreManager scoreManager;

    void Start()
    {
        scoreManager = DependencyResolver.Resolve<ScoreManager>();
        SignalManager.Instance.AddObserver<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.AddObserver<OnArrowsAdded>(UpdateArrows);
        SetScore();
        SetArrowCount();
    }

    private void UpdateArrows(OnArrowsAdded signalData)
    {
        SetArrowCount();
    }

    private void SetArrowCount()
    {
        if (arrowCount != null)
            arrowCount.text =": "+ scoreManager.arrowCount.ToString();
    }

    void OnDestroy()
    {
        SignalManager.Instance.RemoveObserver<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.RemoveObserver<OnArrowsAdded>(UpdateArrows);
    }

    private void UpdateScore(OnUpdateScore signalData)
    {
        SetScore();
    }

    private void SetScore()
    {
        if (score != null)
            score.text ="Score: "+ scoreManager.CurrentScore.ToString();
    }
}
