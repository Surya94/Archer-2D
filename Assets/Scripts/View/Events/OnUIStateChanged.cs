using Archer.Scripts.View.Interface;
using UnityEngine;


namespace Archer.Scripts.View.Events
{
    public class OnUIStateChanged
    {
        public UIState previousState;
        public UIState newState;
        public bool allowBackNavigation;
    }

}