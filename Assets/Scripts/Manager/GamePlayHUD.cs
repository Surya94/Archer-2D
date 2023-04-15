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
        SignalManager.Instance.AddListener<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.AddListener<OnArrowsAdded>(UpdateArrows);
        UpdateScore();
    }

    private void UpdateArrows(OnArrowsAdded signalData)
    {
        if (arrowCount != null)
            arrowCount.text =": "+ scoreManager.arrowCount.ToString();
    }

    void OnDestory()
    {
        SignalManager.Instance.RemoveListener<OnUpdateScore>(UpdateScore);
        SignalManager.Instance.RemoveListener<OnArrowsAdded>(UpdateArrows);
    }

    private void UpdateScore(OnUpdateScore signalData)
    {
        UpdateScore();
    }

    private void UpdateScore()
    {
        if (score != null)
            score.text ="Score: "+ scoreManager.CurrentScore.ToString();
    }
}
