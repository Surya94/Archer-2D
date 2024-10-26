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
        scoreManager.ResetGame();
        SignalManager.Instance.AddObserver<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.AddObserver<OnArrowsAdded>(UpdateArrows);
        SetScore();
    }

    private void UpdateArrows(OnArrowsAdded signalData)
    {
        if (arrowCount != null)
            arrowCount.text =": "+ scoreManager.arrowCount.ToString();
    }

    void OnDestory()
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
