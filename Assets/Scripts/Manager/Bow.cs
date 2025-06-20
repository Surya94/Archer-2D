using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    }

    void Update()
    {
        Vector2 bowPos = transform.position;
        if (newArrow == null)
        {
            if (waitTimeTimer <= 0 && scoreManager.arrowCount >= 1)
            {
                SignalManager.Instance.DispatchSignal(new OnAddArrows(-1));
                waitTimeTimer = bowData.waitTime;
                var arrow = ObjectPoolManager.Instance.SpawnObject(arroeObj.GetComponent<Arrow>(), drawStartPoint.position, drawStartPoint.rotation);
                newArrow=arrow.gameObject;
                newArrow.SetActive(true);
                newArrow.transform.position = drawStartPoint.position;
                newArrow.transform.rotation = drawStartPoint.rotation;
                newArrow.transform.parent = drawStartPoint.parent;
                arrow.rb.linearVelocity = Vector2.zero;
                arrow.rb.isKinematic = true;
                arrow.isFired = false;
                ResetBowString();
            }
            else
            {
                waitTimeTimer -= Time.deltaTime;
            }
        }



        if (newArrow == null)
            return;

        if (Input.GetMouseButtonDown(0))
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
        arrow.rb.isKinematic = false;
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
