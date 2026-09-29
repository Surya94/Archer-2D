using UnityEngine;

namespace Archer.Scripts.Utility
{
    /// <summary>
    /// Adapts a world object to the screen's aspect ratio. The camera keeps a fixed height
    /// (orthographic size), so a wider phone simply sees more world to the sides; the scene was
    /// laid out for 16:9, and this moves or scales things so they still sit where they were
    /// designed to relative to the visible area. At exactly 16:9 every mode is a no-op.
    ///
    /// Runs once in Awake, before gameplay Start()s read positions (Bow, EnemySpawner).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AspectFitter : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Keep the designed distance from the left edge of the view.</summary>
            AnchorLeft,
            /// <summary>Keep the designed distance from the right edge of the view.</summary>
            AnchorRight,
            /// <summary>Scale x about the view centre with the view width (spreads spawn lanes).</summary>
            Proportional,
            /// <summary>Scale up uniformly until the child sprites cover the view; bottom stays put.</summary>
            CoverBackground
        }

        [SerializeField] private Mode mode = Mode.AnchorLeft;
        [Tooltip("The aspect ratio the scene was laid out for.")]
        [SerializeField] private Vector2 referenceAspect = new Vector2(16f, 9f);

        private void Awake()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float refHalfWidth = halfHeight * (referenceAspect.x / referenceAspect.y);
            float camX = cam.transform.position.x;
            Vector3 pos = transform.position;

            switch (mode)
            {
                case Mode.AnchorLeft:
                    pos.x = (camX - halfWidth) + (pos.x - (camX - refHalfWidth));
                    transform.position = pos;
                    break;

                case Mode.AnchorRight:
                    pos.x = (camX + halfWidth) - ((camX + refHalfWidth) - pos.x);
                    transform.position = pos;
                    break;

                case Mode.Proportional:
                    pos.x = camX + (pos.x - camX) * (halfWidth / refHalfWidth);
                    transform.position = pos;
                    break;

                case Mode.CoverBackground:
                    Cover(cam, halfWidth, halfHeight);
                    break;
            }
        }

        private void Cover(Camera cam, float halfWidth, float halfHeight)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (SpriteRenderer sr in renderers) bounds.Encapsulate(sr.bounds);

            Vector3 camPos = cam.transform.position;
            float halfBoundsWidth = bounds.extents.x;
            float height = bounds.size.y;
            if (halfBoundsWidth <= 0f || height <= 0f) return;

            // Scale about the bottom-centre of the art: the ground stays where it was designed,
            // and any extra height goes off the top of the sky.
            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
            float k = 1f;
            k = Mathf.Max(k, (bounds.center.x - (camPos.x - halfWidth)) / halfBoundsWidth);
            k = Mathf.Max(k, ((camPos.x + halfWidth) - bounds.center.x) / halfBoundsWidth);
            k = Mathf.Max(k, ((camPos.y + halfHeight) - bounds.min.y) / height);
            if (k <= 1f) return;

            transform.position = pivot + (transform.position - pivot) * k;
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(scale.x * k, scale.y * k, scale.z);
        }
    }
}
