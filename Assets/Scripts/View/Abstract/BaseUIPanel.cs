using System;
using System.Collections.Generic;
using UnityEngine;
using Archer.Scripts.View.Interface;

namespace Archer.Scripts.View.Abstract
{
    public abstract class BaseUIPanel : MonoBehaviour, IUIPanel
    {
        [Header("Panel Configuration")]
        [SerializeField] protected UIState panelState;
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected RectTransform rectTransform;
        [SerializeField] protected bool hideOnStart = true;
        [SerializeField] protected float defaultTransitionDuration = 0.3f;

        public UIState PanelState => panelState;
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            // IsVisible must mirror the panel's actual on-screen state from the very first
            // frame. A panel with hideOnStart == false is already visible, and leaving
            // IsVisible false would make the first Hide() early-return without invoking its
            // onComplete callback, deadlocking UIManager.ExecuteTransition's WaitUntil.
            IsVisible = !hideOnStart;

            if (hideOnStart)
            {
                SetVisibilityImmediate(false);
            }
        }

        protected virtual void Start()
        {
            Initialize();
        }

        protected virtual void OnDestroy()
        {
            Cleanup();
        }

        public virtual void Initialize()
        {
            // Override in derived classes for specific initialization
        }

        public virtual void Cleanup()
        {
            // Override in derived classes for cleanup
        }

        public virtual void OnStateEnter()
        {
            // Called when this panel becomes active
        }

        public virtual void OnStateExit()
        {
            // Called when this panel becomes inactive
        }

        public virtual void OnStateUpdate()
        {
            // Called every frame while this panel is active
        }

        public virtual void Show(UITransitionType transition = UITransitionType.Instant, Action onComplete = null)
        {
            // Already in the requested state: nothing to animate, but the callback MUST still
            // fire. UIManager.ExecuteTransition blocks on `yield return new WaitUntil(...)`
            // for this callback, so swallowing it wedges isTransitioning at true forever and
            // kills all further UI navigation.
            if (IsVisible)
            {
                onComplete?.Invoke();
                return;
            }

            gameObject.SetActive(true);
            IsVisible = true;

            switch (transition)
            {
                case UITransitionType.Instant:
                    SetVisibilityImmediate(true);
                    onComplete?.Invoke();
                    break;
                case UITransitionType.Fade:
                    StartCoroutine(FadeTransition(true, onComplete));
                    break;
                case UITransitionType.Scale:
                    StartCoroutine(ScaleTransition(true, onComplete));
                    break;
                default:
                    SetVisibilityImmediate(true);
                    onComplete?.Invoke();
                    break;
            }
        }

        public virtual void Hide(UITransitionType transition = UITransitionType.Instant, Action onComplete = null)
        {
            // See Show(): the callback must fire even when there is nothing to hide.
            if (!IsVisible)
            {
                onComplete?.Invoke();
                return;
            }

            IsVisible = false;

            switch (transition)
            {
                case UITransitionType.Instant:
                    SetVisibilityImmediate(false);
                    onComplete?.Invoke();
                    break;
                case UITransitionType.Fade:
                    StartCoroutine(FadeTransition(false, () => {
                        gameObject.SetActive(false);
                        onComplete?.Invoke();
                    }));
                    break;
                case UITransitionType.Scale:
                    StartCoroutine(ScaleTransition(false, () => {
                        gameObject.SetActive(false);
                        onComplete?.Invoke();
                    }));
                    break;
                default:
                    SetVisibilityImmediate(false);
                    onComplete?.Invoke();
                    break;
            }
        }

        protected virtual void SetVisibilityImmediate(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (!visible)
                gameObject.SetActive(false);
        }

        protected virtual System.Collections.IEnumerator FadeTransition(bool fadeIn, Action onComplete)
        {
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = startAlpha;
                canvasGroup.interactable = fadeIn;
                canvasGroup.blocksRaycasts = fadeIn;

                while (elapsed < defaultTransitionDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = elapsed / defaultTransitionDuration;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                    yield return null;
                }

                canvasGroup.alpha = endAlpha;
            }

            onComplete?.Invoke();
        }

        protected virtual System.Collections.IEnumerator ScaleTransition(bool scaleIn, Action onComplete)
        {
            Vector3 startScale = scaleIn ? Vector3.zero : Vector3.one;
            Vector3 endScale = scaleIn ? Vector3.one : Vector3.zero;
            float elapsed = 0f;

            if (rectTransform != null)
            {
                rectTransform.localScale = startScale;

                if (canvasGroup != null)
                {
                    canvasGroup.interactable = scaleIn;
                    canvasGroup.blocksRaycasts = scaleIn;
                }

                while (elapsed < defaultTransitionDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = elapsed / defaultTransitionDuration;
                    // Use eased curve for better feel
                    float easedProgress = scaleIn ?
                        1f - Mathf.Pow(1f - progress, 3f) : // Ease out cubic
                        Mathf.Pow(progress, 3f); // Ease in cubic

                    rectTransform.localScale = Vector3.Lerp(startScale, endScale, easedProgress);
                    yield return null;
                }

                rectTransform.localScale = endScale;
            }

            onComplete?.Invoke();
        }
    }

}