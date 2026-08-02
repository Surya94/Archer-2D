using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archer.Scripts.Manager
{
    public class GameManager : Singleton<GameManager>
    {
        public bool IsPaused { get; private set; }
        public bool IsGameOver { get; private set; }

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

        public void Restart()
        {
            IsGameOver = false;
            Resume();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
