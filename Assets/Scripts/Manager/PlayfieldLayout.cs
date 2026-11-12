using System.Collections.Generic;
using UnityEngine;

namespace Archer.Scripts.Manager
{
    /// <summary>
    /// Places the bow and the balloon spawn columns horizontally as fractions of the screen
    /// width, so the layout fits any phone aspect ratio. The bow sits in from the left edge,
    /// which leaves room for the finger to drag back without covering it. Only world X is
    /// changed: heights stay as authored in the scene.
    /// Runs in Awake, before Bow.Start reads its string/arrow positions and before any spawn.
    /// </summary>
    public class PlayfieldLayout : MonoBehaviour
    {
        [Tooltip("Root of the bow (the scene 'Player' object).")]
        public Transform bowRoot;

        [Range(0f, 1f)]
        [Tooltip("Bow X as a fraction of screen width (0 = left edge, 1 = right edge).")]
        public float bowViewportX = 0.30f;

        [Tooltip("Spawner whose spawnPoints are spread across the range below.")]
        public EnemySpawner spawner;

        [Tooltip("Leftmost and rightmost spawn column, as fractions of screen width.")]
        public Vector2 spawnViewportXRange = new Vector2(0.50f, 0.92f);

        void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[PlayfieldLayout] No main camera; layout left as authored.");
                return;
            }

            if (bowRoot != null)
                SetWorldX(bowRoot, ViewportToWorldX(cam, bowViewportX));

            if (spawner == null || spawner.spawnPoints == null) return;

            var columns = new List<Transform>();
            foreach (Transform point in spawner.spawnPoints)
                if (point != null) columns.Add(point);
            if (columns.Count == 0) return;

            // Keep the authored left-to-right order of the columns.
            columns.Sort((a, b) => a.position.x.CompareTo(b.position.x));

            float left = ViewportToWorldX(cam, spawnViewportXRange.x);
            float right = ViewportToWorldX(cam, spawnViewportXRange.y);
            for (int i = 0; i < columns.Count; i++)
            {
                float t = columns.Count == 1 ? 0.5f : (float)i / (columns.Count - 1);
                SetWorldX(columns[i], Mathf.Lerp(left, right, t));
            }
        }

        private static float ViewportToWorldX(Camera cam, float viewportX)
        {
            return cam.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, cam.nearClipPlane)).x;
        }

        private static void SetWorldX(Transform t, float x)
        {
            Vector3 p = t.position;
            p.x = x;
            t.position = p;
        }
    }
}
