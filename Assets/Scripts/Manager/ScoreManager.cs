using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager
{
    public const string PREVIOUS_SCORE = "PreviousScorePP";
    public const string CURRENT_SCORE = "CurrentScorePP";

    public const int MAX_ARROW = 10;

    public int CurrentScore
    {
        get=> PlayerPrefs.GetInt(CURRENT_SCORE, 0);
        set => PlayerPrefs.SetInt(CURRENT_SCORE,value);
    }

    public int PreviousScore
    {
        get => PlayerPrefs.GetInt(PREVIOUS_SCORE, 0);
        set => PlayerPrefs.SetInt(PREVIOUS_SCORE, value);
    }

    public int arrowCount { get; set; }

    public ScoreManager()
    {
        SignalManager.Instance.AddListener<OnBalloonBurst>(UpdateScore);
        SignalManager.Instance.AddListener<OnAddArrows>(GiveArrows);
    }

    ~ScoreManager()
    {
        SignalManager.Instance.RemoveListener<OnBalloonBurst>(UpdateScore);
        SignalManager.Instance.RemoveListener<OnAddArrows>(GiveArrows);
    }

    private void GiveArrows(OnAddArrows signalData)
    {
        arrowCount += signalData.arrowsToGive;
        SignalManager.Instance.DispatchSignal(new OnArrowsAdded());
    }

    private void UpdateScore(OnBalloonBurst signalData)
    {
        CurrentScore += signalData.pointsToGive;
        SignalManager.Instance.DispatchSignal(new OnUpdateScore());
    }

    public void ResetGame()
    {
        CurrentScore = 0;
        arrowCount = MAX_ARROW;
    }
}
