using Archer.Scripts.View.Abstract;
using Archer.Scripts.View.Events;
using Archer.Scripts.View.Interface;
using System.Collections.Generic;
using System;
using UnityEngine;


namespace Archer.Scripts.View.Manager
{
    public class UIManager : Singleton<UIManager>
    {
        [Header("UI Configuration")]
        [SerializeField] private UITransitionType defaultTransition = UITransitionType.Fade;
        [Tooltip("State entered once panels are initialized. Set to None to stay put, e.g. in a scene that has no MainMenu panel.")]
        [SerializeField] private UIState initialState = UIState.MainMenu;
        [SerializeField] private bool allowBackNavigation = true;
        [SerializeField] private bool debugMode = false;

        // State Management
        private UIState currentState = UIState.None;
        private UIState previousState = UIState.None;
        private readonly Stack<UIState> stateHistory = new Stack<UIState>();

        // Panel Management
        private readonly Dictionary<UIState, IUIPanel> registeredPanels = new Dictionary<UIState, IUIPanel>();
        private IUIPanel currentPanel;

        // Transition Management
        private bool isTransitioning = false;
        private readonly Queue<StateTransition> transitionQueue = new Queue<StateTransition>();

        // Properties
        public UIState CurrentState => currentState;
        public UIState PreviousState => previousState;
        public bool IsTransitioning => isTransitioning;
        public bool CanGoBack => allowBackNavigation && stateHistory.Count > 0;

        // Events
        public event Action<UIState, UIState> OnStateChanged;
        public event Action<UIState, UIState> OnTransitionStarted;
        public event Action<UIState> OnTransitionCompleted;

        private struct StateTransition
        {
            public UIState targetState;
            public UITransitionType transitionType;
            public bool addToHistory;
            public Action onComplete;
        }

        protected void Awake()
        {
            // Auto-register all panels in scene
            AutoRegisterPanels();
        }

        private void Start()
        {
            InitializeUI();
        }

        private void Update()
        {
            // Handle Android back button
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }

            // Process transition queue
            ProcessTransitionQueue();

            // Update current panel
            currentPanel?.OnStateUpdate();
        }

        private void InitializeUI()
        {
            // Initialize all registered panels
            foreach (var panel in registeredPanels.Values)
            {
                panel.Initialize();
            }

            // Set initial state. Scene-configurable: GameScene has no MainMenu panel, and
            // hard-coding MainMenu there logs an error and strands currentState at None,
            // which in turn leaves stateHistory empty so GoBack() can never leave the pause menu.
            if (initialState != UIState.None)
            {
                ChangeState(initialState, UITransitionType.Instant, false);
            }
        }

        private void AutoRegisterPanels()
        {
            // Find all UI panels in the scene
            BaseUIPanel[] panels = FindObjectsByType<BaseUIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (BaseUIPanel panel in panels)
            {
                RegisterPanel(panel.PanelState, panel);
            }

            if (debugMode)
            {
                Debug.Log($"[UIManager] Auto-registered {panels.Length} panels");
            }
        }

        public void RegisterPanel(UIState state, IUIPanel panel)
        {
            if (registeredPanels.ContainsKey(state))
            {
                Debug.LogWarning($"[UIManager] Panel for state {state} already registered. Overwriting.");
            }

            registeredPanels[state] = panel;

            if (debugMode)
            {
                Debug.Log($"[UIManager] Registered panel for state: {state}");
            }
        }

        public void UnregisterPanel(UIState state)
        {
            if (registeredPanels.ContainsKey(state))
            {
                registeredPanels.Remove(state);

                if (debugMode)
                {
                    Debug.Log($"[UIManager] Unregistered panel for state: {state}");
                }
            }
        }

        public void ChangeState(UIState newState, UITransitionType transitionType = UITransitionType.Instant, bool addToHistory = true)
        {
            if (newState == currentState)
            {
                Debug.LogWarning($"[UIManager] Already in state {newState}");
                return;
            }

            if (!registeredPanels.ContainsKey(newState))
            {
                Debug.LogError($"[UIManager] No panel registered for state {newState}");
                return;
            }

            QueueTransition(newState, transitionType, addToHistory, null);
        }

        public void ChangeState(UIState newState, Action onComplete)
        {
            ChangeState(newState, defaultTransition, true, onComplete);
        }

        public void ChangeState(UIState newState, UITransitionType transitionType, bool addToHistory, Action onComplete)
        {
            if (newState == currentState) return;

            if (!registeredPanels.ContainsKey(newState))
            {
                Debug.LogError($"[UIManager] No panel registered for state {newState}");
                return;
            }

            QueueTransition(newState, transitionType, addToHistory, onComplete);
        }

        private void QueueTransition(UIState targetState, UITransitionType transitionType, bool addToHistory, Action onComplete)
        {
            transitionQueue.Enqueue(new StateTransition
            {
                targetState = targetState,
                transitionType = transitionType,
                addToHistory = addToHistory,
                onComplete = onComplete
            });
        }

        private void ProcessTransitionQueue()
        {
            if (isTransitioning || transitionQueue.Count == 0) return;

            StateTransition transition = transitionQueue.Dequeue();
            StartCoroutine(ExecuteTransition(transition));
        }

        private System.Collections.IEnumerator ExecuteTransition(StateTransition transition)
        {
            isTransitioning = true;

            // Store previous state
            previousState = currentState;

            // Add to history if requested
            if (transition.addToHistory && currentState != UIState.None)
            {
                stateHistory.Push(currentState);
            }

            // Notify transition started
            OnTransitionStarted?.Invoke(currentState, transition.targetState);
            SignalManager.Instance?.DispatchSignal(new OnUITransitionStarted
            {
                fromState = currentState,
                toState = transition.targetState,
                transitionType = transition.transitionType
            });

            // Exit current panel
            if (currentPanel != null)
            {
                currentPanel.OnStateExit();

                bool hideCompleted = false;
                currentPanel.Hide(transition.transitionType, () => hideCompleted = true);

                // Wait for hide transition to complete
                yield return new WaitUntil(() => hideCompleted);
            }

            // Update current state
            currentState = transition.targetState;
            currentPanel = registeredPanels[currentState];

            // Enter new panel
            if (currentPanel != null)
            {
                bool showCompleted = false;
                currentPanel.Show(transition.transitionType, () => showCompleted = true);
                currentPanel.OnStateEnter();

                // Wait for show transition to complete
                yield return new WaitUntil(() => showCompleted);
            }

            // Notify transition completed
            OnTransitionCompleted?.Invoke(currentState);
            OnStateChanged?.Invoke(previousState, currentState);
            SignalManager.Instance?.DispatchSignal(new OnUIStateChanged
            {
                previousState = previousState,
                newState = currentState,
                allowBackNavigation = CanGoBack
            });
            SignalManager.Instance?.DispatchSignal(new OnUITransitionCompleted
            {
                currentState = currentState
            });

            isTransitioning = false;
            transition.onComplete?.Invoke();

            if (debugMode)
            {
                Debug.Log($"[UIManager] Transitioned from {previousState} to {currentState}");
            }
        }

        public void GoBack()
        {
            if (!CanGoBack) return;

            UIState targetState = stateHistory.Pop();
            ChangeState(targetState, defaultTransition, false);
        }

        public void ClearHistory()
        {
            stateHistory.Clear();
        }

        private void HandleBackButton()
        {
            switch (currentState)
            {
                case UIState.MainMenu:
                    // Quit application or show quit confirmation
                    Application.Quit();
                    break;

                case UIState.Gameplay:
                    // Pause the game
                    ChangeState(UIState.PauseMenu);
                    break;

                case UIState.PauseMenu:
                case UIState.Settings:
                case UIState.GameOver:
                    // Go back to previous state
                    if (CanGoBack)
                        GoBack();
                    else
                        ChangeState(UIState.MainMenu);
                    break;
            }
        }

        public bool IsInState(UIState state)
        {
            return currentState == state;
        }

        public IUIPanel GetPanel(UIState state)
        {
            return registeredPanels.ContainsKey(state) ? registeredPanels[state] : null;
        }

        public T GetPanel<T>(UIState state) where T : class, IUIPanel
        {
            return GetPanel(state) as T;
        }

        // Utility methods for common transitions
        public void ShowMainMenu() => ChangeState(UIState.MainMenu);
        public void ShowGameplay() => ChangeState(UIState.Gameplay);
        public void ShowPauseMenu() => ChangeState(UIState.PauseMenu);
        public void ShowGameOver() => ChangeState(UIState.GameOver);
        public void ShowSettings() => ChangeState(UIState.Settings);
    }
}