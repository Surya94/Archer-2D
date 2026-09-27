using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Archer.Scripts.Manager;
using Archer.Scripts.Manager.Ads;

public class Bow : MonoBehaviour
{
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
        if (bowData.enableAimAssit)
        {
            points = new GameObject[bowData.numberOfPoints];
            for (int i = 0; i < bowData.numberOfPoints; i++)
            {
                points[i] = Instantiate(pointsObj, drawStartPoint.position, Quaternion.identity);
                points[i].SetActive(false);
            }
        }
        drawDistance = Vector2.Distance(drawStartPoint.position, drawEndPoint.position);
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
            return;

        // A press that lands on the banner ad must not start a draw: during play, fingers go
        // everywhere, and taps on the ad from that count as invalid traffic.
        if (Input.GetMouseButtonDown(0) && !AdsManager.Instance.IsPointOverBanner(Input.mousePosition))
        {
            OnStartDrag();
        }

        if (Input.GetMouseButton(0) && isDragStarted)
        {
            OnDragBow();
        }

        if (Input.GetMouseButtonUp(0) && isDraging)
        {
            OnReleaseArrow();
        }

        if (bowData.enableAimAssit)
            DrawAimPoints();
    }

    private void OnStartDrag()
    {
        isDragStarted = true;
        canPlayLoadSound = true;
        mousePosA = Camera.main.ScreenToWorldPoint(Input.mousePosition);
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

    private void RenderBowString()
    {
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, endPointA.position);
        lineRenderer.SetPosition(1, newArrow.GetComponent<Arrow>().endPoint.transform.position);
        lineRenderer.SetPosition(2, endPointB.position);
    }

    private void ResetBowString()
    {
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
        arrow.SetFireData(launchForce / bowData.MaxForce);
        arrow.rb.bodyType = RigidbodyType2D.Dynamic;
        arrow.rb.linearVelocity = transform.right * launchForce;
        arrow.isFired = true;
        newArrow = null;
        ResetBowString();
        SoundManger.Instance.PlayFireSound();
    }

    private Vector2 pointPosition(float t)
    {
        Vector2 pos = (Vector2)newArrow.transform.position + (direction.normalized * launchForce * t) + 0.5f * Physics2D.gravity * (t * t);
        return pos;
    }
}
