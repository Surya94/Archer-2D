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
        get => PlayerPrefs.GetInt(CURRENT_SCORE, 0);
        set => PlayerPrefs.SetInt(CURRENT_SCORE, value);
    }

    /// <summary>
    /// Best score achieved across all runs. Backed by the PREVIOUS_SCORE key, which was
    /// previously exposed as an unused PreviousScore property (no readers or writers existed
    /// anywhere in the project), so no persisted data or caller is affected by the rename.
    /// </summary>
    public int BestScore
    {
        get => PlayerPrefs.GetInt(PREVIOUS_SCORE, 0);
        set => PlayerPrefs.SetInt(PREVIOUS_SCORE, value);
    }

    public int arrowCount { get; set; }
    public int streakCount { get; set; }

    public ScoreManager()
    {
        SignalManager.Instance.AddObserver<OnBalloonBurst>(UpdateScore);
        SignalManager.Instance.AddObserver<OnAddArrows>(GiveArrows);
        SignalManager.Instance.AddObserver<OnUpdateStreak>(UpdateStreak);
        SignalManager.Instance.AddObserver<OnGameOver>(PromoteBestScore);
    }

    ~ScoreManager()
    {
        SignalManager.Instance.RemoveObserver<OnBalloonBurst>(UpdateScore);
        SignalManager.Instance.RemoveObserver<OnAddArrows>(GiveArrows);
        SignalManager.Instance.RemoveObserver<OnUpdateStreak>(UpdateStreak);
        SignalManager.Instance.RemoveObserver<OnGameOver>(PromoteBestScore);
    }

    /// <summary>
    /// Promote the finished run's score to the best score. Must happen on the game-over
    /// dispatch rather than in ResetGame(), because Bow.Start() calls ResetGame() and that
    /// zeroes CurrentScore before the next run's first frame. Using Max keeps this idempotent,
    /// so a rewarded-ad revive re-running it later in the same run does no harm.
    /// </summary>
    private void PromoteBestScore(OnGameOver signalData)
    {
        if (CurrentScore > BestScore)
            BestScore = CurrentScore;
    }

    private void UpdateStreak(OnUpdateStreak signalData)
    {
        if (signalData.isStreakMaintained)
        {
            streakCount++;
            if (streakCount > 1)
                SignalManager.Instance.DispatchSignal(new OnAddArrows(1));
        }
        else
        {
            streakCount = 0;
        }
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
