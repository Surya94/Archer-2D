using System.Collections.Generic;
using UnityEngine;

namespace Archer.Scripts.Manager
{
    /// <summary>
    /// Populates the sky with drifting clouds at randomised depths. Depth drives everything a
    /// viewer reads as distance: far clouds are smaller, slower, higher, fainter and tinted toward
    /// the sky colour; near clouds are larger, faster and sort in front of far ones. Clouds sort
    /// behind the mountain layers, so low ones pass behind the peaks.
    /// </summary>
    public class CloudField : MonoBehaviour
    {
        [SerializeField] private CloudDrifter cloudPrefab;
        [SerializeField] private Sprite[] cloudSprites;
        [SerializeField] private int cloudCount = 7;
        [SerializeField] private int driftDirection = 1;

        [Header("Depth: far (0) -> near (1)")]
        [SerializeField] private Vector2 scaleRange = new Vector2(0.3f, 0.75f);
        [SerializeField] private Vector2 speedRange = new Vector2(0.08f, 0.4f);
        [SerializeField] private Vector2 alphaRange = new Vector2(0.75f, 1f);
        [Tooltip("Viewport heights (0 = bottom, 1 = top). Far clouds sit high, near ones lower.")]
        [SerializeField] private Vector2 farHeightRange = new Vector2(0.74f, 0.92f);
        [SerializeField] private Vector2 nearHeightRange = new Vector2(0.58f, 0.8f);
        [SerializeField] private Vector2Int sortingOrderRange = new Vector2Int(-90, -80);
        [Tooltip("Far clouds are tinted toward this, like haze over distance.")]
        [SerializeField] private Color hazeColour = new Color32(0xB9, 0xE2, 0xFF, 0xFF);
        [SerializeField, Range(0f, 1f)] private float maxHaze = 0.35f;

        [Header("Bob")]
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private Vector2 bobFrequencyRange = new Vector2(0.05f, 0.12f);

        private readonly List<CloudDrifter> clouds = new List<CloudDrifter>();
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
            if (cloudPrefab == null || mainCamera == null || cloudSprites == null || cloudSprites.Length == 0)
            {
                Debug.LogWarning("[CloudField] Missing prefab, camera or sprites - no clouds spawned.");
                return;
            }

            float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
            float left = mainCamera.transform.position.x - halfWidth;
            float slot = (halfWidth * 2f) / cloudCount;

            for (int i = 0; i < cloudCount; i++)
            {
                CloudDrifter cloud = Instantiate(cloudPrefab, transform);
                cloud.name = "Cloud_" + i;
                cloud.driftDirection = driftDirection;
                cloud.Wrapped += OnCloudWrapped;
                clouds.Add(cloud);

                ApplyRandomDepth(cloud);

                // Spread across the view (jittered slots) so the sky is populated on the first
                // frame instead of clouds trickling in from one edge.
                float x = left + slot * (i + Random.Range(0.1f, 0.9f));
                cloud.transform.position = new Vector3(x, cloud.transform.position.y, cloud.transform.position.z);
            }
        }

        private void OnDestroy()
        {
            foreach (CloudDrifter cloud in clouds)
            {
                if (cloud != null) cloud.Wrapped -= OnCloudWrapped;
            }
        }

        private void OnCloudWrapped(CloudDrifter cloud)
        {
            ApplyRandomDepth(cloud);

            // The drifter placed the cloud using its OLD width; re-seat it just off-screen with
            // the new one, or a larger re-rolled cloud would pop in partly visible.
            float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
            float halfSprite = cloud.SpriteRenderer.bounds.extents.x;
            float camX = mainCamera.transform.position.x;
            float x = driftDirection > 0
                ? camX - halfWidth - halfSprite - cloud.wrapBuffer
                : camX + halfWidth + halfSprite + cloud.wrapBuffer;
            cloud.transform.position = new Vector3(x, cloud.transform.position.y, cloud.transform.position.z);
        }

        private void ApplyRandomDepth(CloudDrifter cloud)
        {
            float depth = Random.value;

            SpriteRenderer sr = cloud.SpriteRenderer;
            sr.sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            sr.flipX = Random.value < 0.5f;
            sr.sortingOrder = Mathf.RoundToInt(Mathf.Lerp(sortingOrderRange.x, sortingOrderRange.y, depth));

            Color tint = Color.Lerp(hazeColour, Color.white, Mathf.Lerp(1f - maxHaze, 1f, depth));
            tint.a = Mathf.Lerp(alphaRange.x, alphaRange.y, depth);
            sr.color = tint;

            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, depth) * Random.Range(0.9f, 1.1f);
            cloud.transform.localScale = new Vector3(scale, scale, 1f);

            float far = Random.Range(farHeightRange.x, farHeightRange.y);
            float near = Random.Range(nearHeightRange.x, nearHeightRange.y);
            float viewportY = Mathf.Lerp(far, near, depth);
            float worldY = mainCamera.ViewportToWorldPoint(new Vector3(0f, viewportY, 0f)).y;

            cloud.Configure(
                Mathf.Lerp(speedRange.x, speedRange.y, depth) * Random.Range(0.85f, 1.15f),
                bobAmplitude * Mathf.Lerp(0.5f, 1f, depth),
                Random.Range(bobFrequencyRange.x, bobFrequencyRange.y));
            cloud.SetBaseY(worldY);
        }
    }
}
