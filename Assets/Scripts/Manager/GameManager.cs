using UnityEngine;
using UnityEngine.SceneManagement;
using Archer.Scripts.Utility;

namespace Archer.Scripts.Manager
{
    public class GameManager : Singleton<GameManager>
    {
        public bool IsPaused { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>
        /// True once this run has spent its single rewarded-ad revive. Cleared only when a new
        /// run begins (Restart) or the player leaves to the menu. This lives here rather than on
        /// the panel because GameManager is DontDestroyOnLoad and so is the only thing that
        /// survives the scene reload Restart() performs.
        /// </summary>
        public bool HasUsedRevive { get; private set; }

        private void OnEnable()
        {
            if (SignalManager.Instance != null)
                SignalManager.Instance.AddObserver<OnGameOver>(HandleGameOver);
        }

        private void OnDisable()
        {
            if (SignalManager.Instance != null)
                SignalManager.Instance.RemoveObserver<OnGameOver>(HandleGameOver);
        }

        private void HandleGameOver(OnGameOver signalData)
        {
            IsGameOver = true;
            Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Resume a run that had already ended, after a rewarded-ad revive. Callers must grant
        /// the arrows BEFORE calling this and dispatch OnRunContinued AFTER it, or the bow will
        /// re-enter its no-arrows-left branch and immediately fire OnGameOver again.
        /// </summary>
        public void ContinueRun()
        {
            if (!IsGameOver) return;

            IsGameOver = false;
            HasUsedRevive = true;
            Resume();
        }

        /// <summary>
        /// Reload the current scene for a fresh run. Stays paused for the whole load: the old
        /// scene keeps running behind the loading overlay, and unpausing now would let balloons
        /// move, arrows fire and OnGameOver trigger under it. Resume happens once the new scene
        /// is active.
        /// </summary>
        public void Restart()
        {
            IsGameOver = false;
            HasUsedRevive = false;
            Pause();
            SceneLoader.Instance.LoadScene(SceneManager.GetActiveScene().name, Resume);
        }

        /// <summary>
        /// Leave the current run for the main menu. Clears game-over state explicitly: this
        /// object is DontDestroyOnLoad, so merely calling Resume() would carry a stale
        /// IsGameOver == true into the menu and on into the next run. Paused during the load
        /// for the same reason as Restart().
        /// </summary>
        public void ReturnToMenu()
        {
            IsGameOver = false;
            HasUsedRevive = false;
            Pause();
            SceneLoader.Instance.LoadScene("MainMenu", Resume);
        }
    }
}
