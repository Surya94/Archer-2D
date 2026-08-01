using Archer.Scripts.View.Interface;
using UnityEngine;


namespace Archer.Scripts.View.Events
{
    public class OnUITransitionStarted
    {
        public UIState fromState;
        public UIState toState;
        public UITransitionType transitionType;
    }

}