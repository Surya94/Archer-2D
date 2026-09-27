using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Archer.Scripts.View;

namespace Archer.Scripts.Utility
{
    /// <summary>
    /// Loads scenes asynchronously behind a loading screen. Every scene change goes through here
    /// (Play, Restart, Home).
    ///
    /// Must NOT be placed in a scene. It is created lazily through Instance, which is the only
    /// Singleton path that calls DontDestroyOnLoad - a scene-placed instance would be destroyed
    /// by the load it is running (see Singleton.Awake).
    /// </summary>
    public class SceneLoader : Singleton<SceneLoader>
    {
        private const string LoadingScreenResource = "UI/LoadingScreen";

        private const float FadeInDuration = 0.2f;
        private const float FadeOutDuration = 0.25f;
        // Real loads here are usually far shorter; the floor stops the screen flashing for a frame.
        private const float MinimumShowTime = 0.6f;
        // Frames to keep covering the new scene after activation, so its Start()/pool spawning
        // hitch happens under the overlay rather than on screen.
        private const int SettleFrames = 2;

        private LoadingScreenView view;

        public bool IsLoading { get; private set; }

        /// <param name="onLoaded">Invoked once the new scene is active and settled, just before
        /// the overlay fades out - e.g. to unpause.</param>
        public void LoadScene(string sceneName, Action onLoaded = null)
        {
            // Double-tap on Play, or Home pressed during a Restart: one load at a time.
            if (IsLoading) return;

            StartCoroutine(LoadRoutine(sceneName, onLoaded));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onLoaded)
        {
            IsLoading = true;

            LoadingScreenView screen = GetView();
            if (screen == null)
            {
                // Fallback: never block a scene change just because the visuals are missing.
                SceneManager.LoadScene(sceneName);
                IsLoading = false;
                onLoaded?.Invoke();
                yield break;
            }

            screen.SetProgress(0f);
            yield return screen.Fade(true, FadeInDuration);

            float shownAt = Time.unscaledTime;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            // Unity reports 0..0.9 while loading and stops at 0.9 until activation is allowed.
            float displayed = 0f;
            while (op.progress < 0.9f || Time.unscaledTime - shownAt < MinimumShowTime)
            {
                float target = Mathf.Min(op.progress / 0.9f, (Time.unscaledTime - shownAt) / MinimumShowTime) * 0.9f;
                displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 2f);
                screen.SetProgress(displayed);
                yield return null;
            }

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            for (int i = 0; i < SettleFrames; i++) yield return null;

            screen.SetProgress(1f);
            onLoaded?.Invoke();

            yield return screen.Fade(false, FadeOutDuration);
            IsLoading = false;
        }

        private LoadingScreenView GetView()
        {
            if (view != null) return view;

            LoadingScreenView prefab = Resources.Load<LoadingScreenView>(LoadingScreenResource);
            if (prefab == null)
            {
                Debug.LogError("[SceneLoader] Resources/" + LoadingScreenResource +
                               " missing - run 'Archer/UI/1. Build UI Panel Prefabs'. Loading without a screen.");
                return null;
            }

            // Parented under this DontDestroyOnLoad object, so it survives every load.
            view = Instantiate(prefab, transform);
            return view;
        }
    }
}
