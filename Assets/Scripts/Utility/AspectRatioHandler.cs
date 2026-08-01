using UnityEngine;

namespace MyProject.Core
{
    /// <summary>
    /// Handles aspect ratio letterboxing/pillarboxing for cameras to maintain consistent visuals across different screen sizes.
    /// Optimized for performance with caching, reduced allocations, and smart update patterns.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AspectRatioHandler : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Aspect Ratio Settings")]
        [SerializeField] private Vector2 targetAspectRatio = new Vector2(16f, 9f);

        [Header("Camera Settings")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool useMainCameraIfNull = true;

        [Header("Performance Settings")]
        [SerializeField] private bool updateInEditor = true;
        [SerializeField] private float updateThreshold = 0.1f; // Minimum change to trigger update

        #endregion

        #region Private Fields

        // Cached values to avoid allocations
        private Vector2Int _lastResolution;
        private float _cachedTargetRatio;
        private bool _isDirty = true;

        // Pre-allocated structs to avoid GC
        private Rect _tempRect;
        private Vector2 _tempSize;
        private Vector2 _tempCenter = new Vector2(0.5f, 0.5f);

        // Performance optimization flags
        private bool _isInitialized;
        private bool _hasValidCamera;

        #endregion

        #region Properties

        /// <summary>
        /// The target aspect ratio for the camera viewport
        /// </summary>
        public Vector2 TargetAspectRatio
        {
            get => targetAspectRatio;
            set
            {
                if (targetAspectRatio != value)
                {
                    targetAspectRatio = value;
                    _cachedTargetRatio = value.x / value.y;
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// Current screen aspect ratio
        /// </summary>
        public float CurrentAspectRatio => (float)Screen.width / Screen.height;

        /// <summary>
        /// Whether the screen is wider than the target aspect ratio
        /// </summary>
        public bool IsWideScreen => CurrentAspectRatio > _cachedTargetRatio;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeCamera();
            CacheTargetRatio();
            _lastResolution = new Vector2Int(Screen.width, Screen.height);
        }

        private void Start()
        {
            if (_hasValidCamera)
            {
                UpdateCameraRect();
                _isInitialized = true;
            }
        }

        private void LateUpdate()
        {
            // Skip if not in play mode and editor updates are disabled
            if (!Application.isPlaying && !updateInEditor)
                return;

            if (!_hasValidCamera)
            {
                InitializeCamera();
                if (!_hasValidCamera) return;
            }

            CheckForResolutionChange();

            if (_isDirty)
            {
                UpdateCameraRect();
                _isDirty = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure target aspect ratio is valid
            if (targetAspectRatio.x <= 0) targetAspectRatio.x = 16f;
            if (targetAspectRatio.y <= 0) targetAspectRatio.y = 9f;

            CacheTargetRatio();
            _isDirty = true;

            // Update immediately in editor for preview
            if (updateInEditor && _hasValidCamera)
            {
                UpdateCameraRect();
            }
        }
#endif

        #endregion

        #region Initialization

        private void InitializeCamera()
        {
            // Try assigned camera first
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            // Fallback to main camera if enabled
            if (targetCamera == null && useMainCameraIfNull)
            {
                targetCamera = Camera.main;
            }

            _hasValidCamera = targetCamera != null;

            if (!_hasValidCamera)
            {
                Debug.LogWarning($"[AspectRatioHandler] No valid camera found on {gameObject.name}. " +
                               "Please assign a camera or ensure this GameObject has a Camera component.", this);
            }
        }

        private void CacheTargetRatio()
        {
            _cachedTargetRatio = targetAspectRatio.x / targetAspectRatio.y;
        }

        #endregion

        #region Resolution Management

        private void CheckForResolutionChange()
        {
            var currentResolution = new Vector2Int(Screen.width, Screen.height);

            // Use Vector2Int comparison for better performance
            if (_lastResolution != currentResolution)
            {
                // Check if change is significant enough (for editor preview)
                float changeRatio = Mathf.Abs(CurrentAspectRatio - (_lastResolution.x / (float)_lastResolution.y));

                if (changeRatio > updateThreshold || !_isInitialized)
                {
                    _lastResolution = currentResolution;
                    _isDirty = true;
                }
            }
        }

        #endregion

        #region Camera Rect Calculation

        private void UpdateCameraRect()
        {
            if (!_hasValidCamera) return;

            float currentRatio = CurrentAspectRatio;

            // Calculate viewport dimensions based on aspect ratio comparison
            if (currentRatio > _cachedTargetRatio)
            {
                // Screen is wider than target - add pillarboxing (black bars on sides)
                CalculatePillarboxing(currentRatio);
            }
            else if (currentRatio < _cachedTargetRatio)
            {
                // Screen is taller than target - add letterboxing (black bars on top/bottom)
                CalculateLetterboxing(currentRatio);
            }
            else
            {
                // Perfect match - use full screen
                _tempRect = new Rect(0f, 0f, 1f, 1f);
            }

            // Apply the calculated rect to the camera
            targetCamera.rect = _tempRect;
        }

        private void CalculatePillarboxing(float currentRatio)
        {
            // Calculate width to maintain target aspect ratio
            float width = _cachedTargetRatio / currentRatio;
            float x = (1f - width) * 0.5f;

            _tempRect.x = x;
            _tempRect.y = 0f;
            _tempRect.width = width;
            _tempRect.height = 1f;
        }

        private void CalculateLetterboxing(float currentRatio)
        {
            // Calculate height to maintain target aspect ratio
            float height = currentRatio / _cachedTargetRatio;
            float y = (1f - height) * 0.5f;

            _tempRect.x = 0f;
            _tempRect.y = y;
            _tempRect.width = 1f;
            _tempRect.height = height;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Force an immediate update of the camera rect
        /// </summary>
        public void ForceUpdate()
        {
            _isDirty = true;
            UpdateCameraRect();
        }

        /// <summary>
        /// Reset camera rect to full screen
        /// </summary>
        public void ResetToFullScreen()
        {
            if (_hasValidCamera)
            {
                targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        /// <summary>
        /// Set a new target aspect ratio
        /// </summary>
        /// <param name="width">Width component</param>
        /// <param name="height">Height component</param>
        public void SetTargetAspectRatio(float width, float height)
        {
            TargetAspectRatio = new Vector2(width, height);
        }

        /// <summary>
        /// Get the current viewport size in screen pixels
        /// </summary>
        /// <returns>Viewport size in pixels</returns>
        public Vector2Int GetViewportPixelSize()
        {
            if (!_hasValidCamera) return Vector2Int.zero;

            var rect = targetCamera.rect;
            return new Vector2Int(
                Mathf.RoundToInt(Screen.width * rect.width),
                Mathf.RoundToInt(Screen.height * rect.height)
            );
        }

        #endregion

        #region Debug

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_hasValidCamera) return;

            // Draw viewport bounds in scene view
            var rect = targetCamera.rect;
            var screenSize = new Vector2(Screen.width, Screen.height);
            var viewportSize = new Vector2(screenSize.x * rect.width, screenSize.y * rect.height);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);

            // Draw aspect ratio info
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.2f,
                $"Target: {targetAspectRatio.x}:{targetAspectRatio.y}\n" +
                $"Current: {CurrentAspectRatio:F2}\n" +
                $"Viewport: {viewportSize.x}x{viewportSize.y}");
        }
#endif

        #endregion
    }
}