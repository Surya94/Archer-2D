using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using Archer.Scripts.Manager;
using Archer.Scripts.Models;
using Archer.Scripts.Utility;

public class Bow : MonoBehaviour
{
    [Header("Feel")]
    [Tooltip("The bow sprite that recoils on release. Defaults to the parent of the string end points; the root is left alone because it owns the aim rotation.")]
    public Transform bowVisual;

    private Vector3 visualRestPosition;
    private Sequence recoil;
    private Tween twang;
    private bool reachedFullDraw;

    public GameObject arroeObj;
    public GameObject pointsObj;
    public Transform drawStartPoint;
    public Transform drawEndPoint;
    public Transform endPointA;
    public Transform endPointB;
    public LineRenderer lineRenderer;
    public BowData bowData;
    public float minAngle = -45f; // Minimum angle (e.g., -45 degrees)
    public float maxAngle = 45f;  // Maximum angle (e.g., 45 degrees)

    private float launchForce;

    private GameObject[] points;
    private Vector2 direction;
    private Vector2 mousePosA;
    private Vector2 mousePosB;
    private bool isDraging;
    private bool isDragStarted;
    private bool canPlayLoadSound;
    private GameObject newArrow;
    private float drawDistance;
    private float waitTimeTimer = 0.5f;
    private ScoreManager scoreManager;
    void Start()
    {
        scoreManager = DependencyResolver.Resolve<ScoreManager>();
        scoreManager.ResetGame();

        if (bowVisual == null && endPointA != null)
            bowVisual = endPointA.parent;
        if (bowVisual != null)
            visualRestPosition = bowVisual.localPosition;

        if (bowData.enableAimAssit)
        {
            points = new GameObject[bowData.numberOfPoints];
            for (int i = 0; i < bowData.numberOfPoints; i++)
            {
                points[i] = Instantiate(pointsObj, drawStartPoint.position, Quaternion.identity);
                points[i].SetActive(false);
            }
        }
        // Local, not world: this is applied as a localPosition offset in OnDragBow, so it must
        // stay in the bow root's space or a scaled bow would over-pull the arrow.
        drawDistance = Vector2.Distance(drawStartPoint.localPosition, drawEndPoint.localPosition);
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, endPointA.position);
        lineRenderer.SetPosition(1, endPointB.position);
        SoundManger.Instance.Init();
        SpawnArrow();
    }

    private void OnEnable()
    {
        if (SignalManager.Instance != null)
        {
            SignalManager.Instance.AddObserver<OnArrowDestoryed>(OnArrowDestory);
            SignalManager.Instance.AddObserver<OnRunContinued>(OnRunContinue);
        }
    }

    private void OnDisable()
    {
        if (SignalManager.Instance != null)
        {
            SignalManager.Instance.RemoveObserver<OnArrowDestoryed>(OnArrowDestory);
            SignalManager.Instance.RemoveObserver<OnRunContinued>(OnRunContinue);
        }

        recoil?.Kill();
        twang?.Kill();
    }

    private void OnArrowDestory(OnArrowDestoryed destoryed)
    {
        SpawnArrow();
    }

    // The run ended (no arrows left) and was revived. Update() early-returns while
    // newArrow is null, so the bow stays inert until an arrow is nocked again from here.
    private void OnRunContinue(OnRunContinued continued)
    {
        SpawnArrow();
    }

    private void SpawnArrow()
    {
        if (scoreManager.arrowCount >= 1)
        {
            SignalManager.Instance.DispatchSignal(new OnAddArrows(-1));
            waitTimeTimer = bowData.waitTime;
            var arrow = ObjectPoolManager.Instance.SpawnObject(arroeObj.GetComponent<Arrow>(), drawStartPoint.position, drawStartPoint.rotation);

            // SpawnObject returns null when the prefab or its poolable type is invalid. The
            // result is dereferenced five times below, and this runs from Start(), so an
            // unguarded null here aborts Start() and leaves the bow dead for the whole run.
            if (arrow == null)
            {
                Debug.LogError("[Bow] Failed to spawn an arrow from the pool; the bow has no nocked arrow.");
                return;
            }

            newArrow = arrow.gameObject;
            newArrow.SetActive(true);
            newArrow.transform.position = drawStartPoint.position;
            newArrow.transform.rotation = drawStartPoint.rotation;
            newArrow.transform.parent = drawStartPoint.parent;
            arrow.rb.linearVelocity = Vector2.zero;
            arrow.rb.bodyType = RigidbodyType2D.Kinematic;
            arrow.isFired = false;
            ResetBowString();
        }
        else
        {
            SignalManager.Instance.DispatchSignal(new OnGameOver());
        }
    }

    void Update()
    {
        Vector2 bowPos = transform.position;
        //if (newArrow == null)
        //{
        //    if (waitTimeTimer <= 0 && scoreManager.arrowCount >= 1)
        //    {
        //        SignalManager.Instance.DispatchSignal(new OnAddArrows(-1));
        //        waitTimeTimer = bowData.waitTime;
        //        var arrow = ObjectPoolManager.Instance.SpawnObject(arroeObj.GetComponent<Arrow>(), drawStartPoint.position, drawStartPoint.rotation);
        //        newArrow=arrow.gameObject;
        //        newArrow.SetActive(true);
        //        newArrow.transform.position = drawStartPoint.position;
        //        newArrow.transform.rotation = drawStartPoint.rotation;
        //        newArrow.transform.parent = drawStartPoint.parent;
        //        arrow.rb.linearVelocity = Vector2.zero;
        //        arrow.rb.bodyType = RigidbodyType2D.Kinematic;
        //        arrow.isFired = false;
        //        ResetBowString();
        //    }
        //    else
        //    {
        //        waitTimeTimer -= Time.deltaTime;
        //    }
        //}



        if (newArrow == null)
            return;

        // Input is read raw, not through the UI, so the pause/game-over dimmer and the loading
        // overlay don't block it - a tap on RESUME or during a scene load would draw the bow.
        if (GameManager.Instance.IsPaused)
        {
            // Paused mid-pull (Android back button): there will be no mouse-up to end the
            // drag, so let go of it now rather than resuming with a half-drawn bow.
            if (isDragStarted)
                CancelDrag();
            return;
        }

        // A press that lands on a button (pause) belongs to the UI. Without this, pressing
        // pause and sliding off it drew the bow and could loose an arrow.
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            OnStartDrag();
        }

        if (Input.GetMouseButton(0) && isDragStarted)
        {
            OnDragBow();
        }

        if (Input.GetMouseButtonUp(0) && isDragStarted)
        {
            if (isDraging)
                OnReleaseArrow();
            else
                CancelDrag();
        }

        if (bowData.enableAimAssit)
            DrawAimPoints();
    }

    private void OnStartDrag()
    {
        isDragStarted = true;
        canPlayLoadSound = true;
        mousePosA = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        reachedFullDraw = false;
        StopReleaseTweens();
        SignalManager.Instance.DispatchSignal(new OnBowDragStarted
        {
            bow = this,
            screenPosition = Input.mousePosition
        });
    }

    /// <summary>
    /// End a drag without firing: the pull was too short, or the game paused mid-pull. Puts the
    /// arrow back on the rest position and straightens the string.
    /// </summary>
    private void CancelDrag()
    {
        isDragStarted = false;
        isDraging = false;

        if (newArrow != null)
            newArrow.transform.localPosition = drawStartPoint.localPosition;
        ResetBowString();

        SignalManager.Instance.DispatchSignal(new OnBowDragEnded { fired = false, power = 0f });
    }

    private static bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        // The parameterless overload only knows about the mouse; a touch needs its finger id.
        if (Input.touchCount > 0)
            return eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return eventSystem.IsPointerOverGameObject();
    }

    private void OnDragBow()
    {
        mousePosB = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction = mousePosA - mousePosB;
        float distance = Vector2.Distance(mousePosA, mousePosB) * 10;
        launchForce = Mathf.Clamp(distance, 0, bowData.MaxForce);
        float dragDelta = launchForce / bowData.MaxForce;
        newArrow.transform.localPosition = drawStartPoint.localPosition - new Vector3(drawDistance * dragDelta, 0, 0);
        RenderBowString();

        // A light tick the moment the bow reaches full draw; re-armed once it eases off.
        if (dragDelta >= 0.999f)
        {
            if (!reachedFullDraw)
            {
                reachedFullDraw = true;
                Haptics.Light();
            }
        }
        else if (dragDelta < 0.9f)
        {
            reachedFullDraw = false;
        }

        isDraging = launchForce > bowData.MinForce;
        if (isDraging)
        {
            //transform.right = direction;
            // Calculate the angle of the bow
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Clamp the angle to the specified range
            angle = Mathf.Clamp(angle, minAngle, maxAngle);

            // Apply the clamped angle to the bow's rotation
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            canPlayLoadSound = true;
        }
        if (canPlayLoadSound && isDraging)
        {
            canPlayLoadSound = false;
            SoundManger.Instance.PlayLoadSound();
        }
    }

    private void OnReleaseArrow()
    {
        isDraging = false;
        isDragStarted = false;
        Shoot();
    }

    private void DrawAimPoints()
    {
        if (isDraging)
        {
            for (int i = 0; i < bowData.numberOfPoints; i++)
            {
                points[i].SetActive(true);
                points[i].transform.position = pointPosition((i + 3) * bowData.spaceBtwpoints);
            }
        }
        else
        {
            for (int i = 0; i < bowData.numberOfPoints; i++)
            {
                points[i].SetActive(false);
            }
        }
    }

    // ------------------------------------------------------------------ Feel

    /// <summary>Release: the bow kicks forward and settles back while the string twangs.</summary>
    private void PlayRelease(float power)
    {
        if (bowVisual != null)
        {
            recoil?.Kill();
            bowVisual.localPosition = visualRestPosition;
            recoil = DOTween.Sequence()
                .Append(bowVisual.DOLocalMoveX(visualRestPosition.x + 0.07f * power, 0.05f).SetEase(Ease.OutQuad))
                .Append(bowVisual.DOLocalMoveX(visualRestPosition.x, 0.22f).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }

        twang?.Kill();
        float amplitude = 0.14f * power;
        twang = DOVirtual.Float(0f, 1f, 0.3f, t =>
            {
                // A decaying wobble of the string's midpoint along the shot direction.
                float offset = amplitude * Mathf.Exp(-5f * t) * Mathf.Sin(t * Mathf.PI * 7f);
                Vector3 mid = (endPointA.position + endPointB.position) * 0.5f + transform.right * offset;
                lineRenderer.positionCount = 3;
                lineRenderer.SetPosition(0, endPointA.position);
                lineRenderer.SetPosition(1, mid);
                lineRenderer.SetPosition(2, endPointB.position);
            })
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, endPointA.position);
                lineRenderer.SetPosition(1, endPointB.position);
            });
    }

    private void StopReleaseTweens()
    {
        recoil?.Kill();
        twang?.Kill();

        if (bowVisual != null)
            bowVisual.localPosition = visualRestPosition;
    }

    private void RenderBowString()
    {
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, endPointA.position);
        lineRenderer.SetPosition(1, newArrow.GetComponent<Arrow>().endPoint.transform.position);
        lineRenderer.SetPosition(2, endPointB.position);
    }

    private void ResetBowString()
    {
        // A new arrow can be nocked while the release twang is still ringing.
        twang?.Kill();
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, endPointA.position);
        lineRenderer.SetPosition(1, endPointB.position);
    }

    private void Shoot()
    {
        if (newArrow == null)
            return;
        newArrow.transform.parent = null;
        var arrow = newArrow.GetComponent<Arrow>();
        float power = launchForce / bowData.MaxForce;
        arrow.SetFireData(power);
        arrow.rb.bodyType = RigidbodyType2D.Dynamic;
        arrow.rb.linearVelocity = transform.right * launchForce;
        arrow.isFired = true;
        newArrow = null;
        ResetBowString();

        PlayRelease(power);
        SoundManger.Instance.PlayFireSound(power);
        if (power > 0.7f) Haptics.Medium(); else Haptics.Light();
        SignalManager.Instance.DispatchSignal(new OnBowDragEnded { fired = true, power = power });
    }

    private Vector2 pointPosition(float t)
    {
        Vector2 pos = (Vector2)newArrow.transform.position + (direction.normalized * launchForce * t) + 0.5f * Physics2D.gravity * (t * t);
        return pos;
    }
}
