using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archer.Scripts.View.Interface
{
    public enum UIState
    {
        None,
        MainMenu,
        Gameplay,
        PauseMenu,
        GameOver,
        Settings,
        Loading
    }

    public enum UITransitionType
    {
        Instant,
        Fade,
        Slide,
        Scale,
        Custom
    }

    public interface IUIPanel
    {
        UIState PanelState { get; }
        bool IsVisible { get; }
        void Show(UITransitionType transition = UITransitionType.Instant, Action onComplete = null);
        void Hide(UITransitionType transition = UITransitionType.Instant, Action onComplete = null);
        void Initialize();
        void Cleanup();
        void OnStateEnter();
        void OnStateExit();
        void OnStateUpdate();
    }
}