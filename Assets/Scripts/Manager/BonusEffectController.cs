using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Runs the bonus balloon effects in response to OnBonusCollected. Every balloon an effect pops
/// goes through Enemy.Burst(), so it pays its normal points and plays its normal pop; the arrow
/// streak is untouched because only Arrow reports streaks. The Time bonus pops nothing: it
/// freezes the balloons (and, via OnFreezeStarted/Ended, the spawner, clouds and HUD overlay).
///
/// All timing is on scaled time (DOTween defaults, WaitForSeconds), so effects freeze with the
/// rest of the game when GameManager pauses.
/// </summary>
public class BonusEffectController : MonoBehaviour
{
    [Header("Extra Arrows")]
    public int extraArrows = 5;
    public Sprite quiverSprite;
    [Tooltip("HUD arrow pill the quiver flies into.")]
    public RectTransform arrowTarget;

    [Header("Time Freeze")]
    public float freezeDuration = 5f;
    public Sprite clockSprite;
    [Tooltip("Soft round sprite for the thaw sparkles (the bonus glow works).")]
    public Sprite sparkleSprite;
    public Color iceColour = new Color(0.62f, 0.86f, 1f, 1f);

    [Header("Lightning")]
    public Sprite boltSprite;
    public int maxLightningJumps = 6;
    public float lightningJumpDelay = 0.1f;
    public Color boltColour = new Color(0.75f, 0.95f, 1f, 1f);

    [Header("Rendering")]
    [Tooltip("Additive material for the bolts and thaw sparkles.")]
    public Material additiveMaterial;
    public string sortingLayer = "Enemy";
    public int sortingOrder = 50;
    [Tooltip("World-space height of the quiver/clock/bolt effect sprites. Sprites differ in pixel size, so each is scaled to this.")]
    public float itemHeight = 0.8f;

    private Camera cam;
    private Coroutine freezeRoutine;
    private float freezeRemaining;

    void Start()
    {
        cam = Camera.main;
        SignalManager.Instance.AddObserver<OnBonusCollected>(HandleBonus);
    }

    void OnDestroy()
    {
        SignalManager.Instance?.RemoveObserver<OnBonusCollected>(HandleBonus);
    }

    private void HandleBonus(OnBonusCollected signalData)
    {
        if (signalData == null) return;

        switch (signalData.type)
        {
            case BonusType.ExtraArrows:
                CollectArrows(signalData.position);
                break;
            case BonusType.TimeFreeze:
                FreezeTime(signalData.position);
                break;
            case BonusType.Lightning:
                StartCoroutine(ChainLightning(signalData.position));
                break;
        }
    }

    // ------------------------------------------------------------------ Extra Arrows

    private void CollectArrows(Vector3 position)
    {
        // Granted now, not when the quiver lands: if this was the player's last arrow, Bow
        // checks the count as soon as that arrow despawns and would end the run first.
        SignalManager.Instance.DispatchSignal(new OnAddArrows(extraArrows));

        SpriteRenderer quiver = CreateSprite("QuiverFX", quiverSprite, position, itemHeight);
        Transform t = quiver.transform;
        float s = t.localScale.x;
        Vector3 target = HudTargetInWorld(arrowTarget, position.z);

        // Pop, then arc into the HUD counter - the same beat as the score popups.
        DOTween.Sequence()
            .Append(t.DOScale(s * 1.35f, 0.18f).SetEase(Ease.OutBack))
            .Append(t.DORotate(new Vector3(0f, 0f, 20f), 0.15f))
            .Append(t.DOMoveX(target.x, 0.45f).SetEase(Ease.InQuad))
            .Join(t.DOMoveY(target.y, 0.45f).SetEase(Ease.OutQuad))
            .Join(t.DOScale(s * 0.35f, 0.45f).SetEase(Ease.InQuad))
            .SetLink(quiver.gameObject)
            .OnComplete(() => Destroy(quiver.gameObject));
    }

    /// <summary>
    /// The HUD canvas is Screen Space - Camera, so its RectTransforms sit on a plane 100 units
    /// in front of the camera. Map the pill to the same screen point at the effect's depth.
    /// </summary>
    private Vector3 HudTargetInWorld(RectTransform target, float z)
    {
        if (target == null || cam == null)
            return cam != null ? cam.ViewportToWorldPoint(new Vector3(0.1f, 0.9f, z - cam.transform.position.z)) : Vector3.zero;

        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera canvasCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCam, target.position);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z - cam.transform.position.z));
    }

    // ------------------------------------------------------------------ Time Freeze

    private void FreezeTime(Vector3 position)
    {
        ShowClock(position);

        // Only one bonus is alive at a time, so a second freeze shouldn't happen - but if it
        // ever does, extend the running one rather than stacking two.
        if (freezeRoutine != null)
        {
            freezeRemaining = freezeDuration;
            FreezeAlive();
            return;
        }

        freezeRoutine = StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        if (SoundManger.Instance != null)
            SoundManger.Instance.PlayFreezeSound();

        FreezeAlive();
        SignalManager.Instance.DispatchSignal(new OnFreezeStarted { duration = freezeDuration });

        // Scaled time, so the countdown holds while the game is paused or on Game Over.
        freezeRemaining = freezeDuration;
        while (freezeRemaining > 0f)
        {
            yield return null;
            freezeRemaining -= Time.deltaTime;
        }

        Thaw();
        freezeRoutine = null;
        SignalManager.Instance.DispatchSignal(new OnFreezeEnded());
    }

    private void FreezeAlive()
    {
        // Snapshot: freezing doesn't change the list, but an arrow landing on the same frame can.
        foreach (Enemy enemy in new List<Enemy>(Enemy.Alive))
        {
            if (enemy != null && enemy.IsAlive)
                enemy.SetFrozen(true, iceColour);
        }
    }

    private void Thaw()
    {
        if (SoundManger.Instance != null)
            SoundManger.Instance.PlayThawSound();

        foreach (Enemy enemy in new List<Enemy>(Enemy.Alive))
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsFrozen) continue;

            enemy.SetFrozen(false, iceColour);
            if (enemy.IsOnScreen(cam, 0f))
                Sparkle(enemy.transform.position);
        }
    }

    /// <summary>The carried stopwatch pops up, spins a quarter turn and fades.</summary>
    private void ShowClock(Vector3 position)
    {
        SpriteRenderer clock = CreateSprite("ClockFX", clockSprite, position, itemHeight);
        Transform t = clock.transform;
        float s = t.localScale.x;

        DOTween.Sequence()
            .Append(t.DOScale(s * 1.6f, 0.25f).SetEase(Ease.OutBack))
            .Join(t.DOMoveY(position.y + 0.4f, 0.25f).SetEase(Ease.OutQuad))
            .Append(t.DORotate(new Vector3(0f, 0f, -90f), 0.35f).SetEase(Ease.InOutQuad))
            .Join(clock.DOColor(iceColour, 0.35f))
            .Append(clock.DOFade(0f, 0.3f))
            .Join(t.DOScale(s * 2.2f, 0.3f))
            .SetLink(clock.gameObject)
            .OnComplete(() => Destroy(clock.gameObject));
    }

    /// <summary>A few icy glints bursting off a thawing balloon.</summary>
    private void Sparkle(Vector3 position)
    {
        const int count = 5;
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer spark = CreateSprite("ThawSparkle", sparkleSprite, position, 0.22f);
            if (additiveMaterial != null) spark.sharedMaterial = additiveMaterial;
            spark.color = Color.Lerp(iceColour, Color.white, 0.5f);

            float angle = (i / (float)count + Random.Range(-0.08f, 0.08f)) * Mathf.PI * 2f;
            Vector3 to = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(0.45f, 0.7f);

            Transform t = spark.transform;
            float s = t.localScale.x;
            DOTween.Sequence()
                .Append(t.DOMove(to, 0.4f).SetEase(Ease.OutCubic))
                .Join(t.DOScale(s * 0.2f, 0.4f).SetEase(Ease.InQuad))
                .Join(spark.DOFade(0f, 0.4f).SetEase(Ease.InQuad))
                .SetLink(spark.gameObject)
                .OnComplete(() => Destroy(spark.gameObject));
        }
    }

    // ------------------------------------------------------------------ Lightning

    private IEnumerator ChainLightning(Vector3 origin)
    {
        SpriteRenderer bolt = CreateSprite("BoltFX", boltSprite, origin, itemHeight);
        float boltScale = bolt.transform.localScale.x;
        DOTween.Sequence()
            .Append(bolt.transform.DOScale(boltScale * 1.4f, 0.15f).SetEase(Ease.OutBack))
            .Append(bolt.DOFade(0f, 0.3f))
            .SetLink(bolt.gameObject)
            .OnComplete(() => Destroy(bolt.gameObject));

        Vector3 from = origin;
        for (int jump = 0; jump < maxLightningJumps; jump++)
        {
            Enemy target = NearestTarget(from);
            if (target == null) break;

            Vector3 to = target.transform.position;
            DrawBolt(from, to);
            // Burst immediately so this balloon can't be picked again by this chain.
            target.Burst();
            PlayPop();
            from = to;

            yield return new WaitForSeconds(lightningJumpDelay);
        }
    }

    private Enemy NearestTarget(Vector3 from)
    {
        Enemy best = null;
        float bestDistance = float.MaxValue;

        foreach (Enemy enemy in Enemy.Alive)
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsOnScreen(cam)) continue;

            float distance = (enemy.transform.position - from).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }

    private void DrawBolt(Vector3 from, Vector3 to)
    {
        // Two lines: a wide faint glow under a thin bright core.
        LineRenderer glow = CreateLine("BoltGlow", 0.45f, new Color(boltColour.r, boltColour.g, boltColour.b, 0.35f), sortingOrder);
        LineRenderer core = CreateLine("BoltCore", 0.12f, Color.white, sortingOrder + 1);

        Vector3[] points = JaggedPath(from, to);
        glow.positionCount = core.positionCount = points.Length;
        glow.SetPositions(points);
        core.SetPositions(points);

        // Flicker by re-jaggedising twice, then fade out.
        Sequence seq = DOTween.Sequence();
        for (int i = 0; i < 2; i++)
        {
            seq.AppendInterval(0.05f);
            seq.AppendCallback(() =>
            {
                Vector3[] next = JaggedPath(from, to);
                glow.SetPositions(next);
                core.SetPositions(next);
            });
        }
        seq.Append(DOVirtual.Float(1f, 0f, 0.2f, a =>
        {
            SetLineAlpha(core, a);
            SetLineAlpha(glow, a * 0.35f);
        }));
        seq.SetLink(core.gameObject);
        // OnKill runs after completion too (auto-kill) and if the link kills it on unload, so it
        // is the single place both lines get cleaned up.
        seq.OnKill(() =>
        {
            if (core != null) Destroy(core.gameObject);
            if (glow != null) Destroy(glow.gameObject);
        });
    }

    private static Vector3[] JaggedPath(Vector3 from, Vector3 to)
    {
        float length = Vector2.Distance(from, to);
        int segments = Mathf.Clamp(Mathf.CeilToInt(length / 0.6f), 3, 14);
        Vector3 direction = (to - from).normalized;
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f);

        Vector3[] points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 point = Vector3.Lerp(from, to, t);
            // Endpoints stay pinned to the balloons; the middle jitters most.
            if (i > 0 && i < segments)
                point += normal * Random.Range(-0.35f, 0.35f) * Mathf.Sin(t * Mathf.PI);
            points[i] = point;
        }

        return points;
    }

    private LineRenderer CreateLine(string name, float width, Color colour, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sharedMaterial = additiveMaterial;
        line.startColor = line.endColor = colour;
        line.sortingLayerName = sortingLayer;
        line.sortingOrder = order;
        return line;
    }

    private static void SetLineAlpha(LineRenderer line, float alpha)
    {
        Color start = line.startColor;
        start.a = alpha;
        line.startColor = line.endColor = start;
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>A throwaway effect sprite, scaled so it is worldHeight units tall.</summary>
    private SpriteRenderer CreateSprite(string name, Sprite sprite, Vector3 position, float worldHeight)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        float spriteHeight = sprite != null ? sprite.bounds.size.y : 1f;
        go.transform.localScale = Vector3.one * (worldHeight / Mathf.Max(spriteHeight, 0.0001f));

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void PlayPop()
    {
        if (SoundManger.Instance != null)
            SoundManger.Instance.PlayHitSound();
    }
}
