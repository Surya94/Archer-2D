using System;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(SpriteRenderer))]
public class CloudDrifter : MonoBehaviour
{
    public float minSpeed = 0.15f;
    public float maxSpeed = 0.4f;
    public int driftDirection = 1;
    public float wrapBuffer = 0.5f;

    [Header("Vertical Bob")]
    public float bobAmplitude = 0f;
    public float bobFrequency = 0.3f;

    /// <summary>
    /// Raised after the cloud wraps from one screen edge to the other. CloudField uses this to
    /// re-roll the cloud's depth, so the sky never visibly repeats the same pattern.
    /// </summary>
    public event Action<CloudDrifter> Wrapped;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private float speed;
    private bool isConfigured;
    private float baseY;
    private float bobPhase;
    private float bobTime;
    private bool isFrozen;

    public SpriteRenderer SpriteRenderer => spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
        baseY = transform.position.y;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Start()
    {
        // Hand-placed clouds pick their own speed; CloudField-driven ones are configured.
        if (!isConfigured)
            speed = Random.Range(minSpeed, maxSpeed);
    }

    /// <summary>Overrides the random speed and sets the bob, e.g. from a depth value.</summary>
    public void Configure(float newSpeed, float newBobAmplitude, float newBobFrequency)
    {
        isConfigured = true;
        speed = newSpeed;
        bobAmplitude = newBobAmplitude;
        bobFrequency = newBobFrequency;
    }

    /// <summary>Sets the resting height the bob oscillates around.</summary>
    public void SetBaseY(float y)
    {
        baseY = y;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    /// <summary>Time bonus freeze: stops drift and bob in place.</summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
    }

    void Update()
    {
        if (isFrozen) return;

        // Own bob clock rather than Time.time, so a freeze doesn't make the cloud jump when it
        // resumes.
        bobTime += Time.deltaTime;

        float x = transform.position.x + driftDirection * speed * Time.deltaTime;
        float y = baseY;
        if (bobAmplitude > 0f)
            y += Mathf.Sin(bobTime * bobFrequency * Mathf.PI * 2f + bobPhase) * bobAmplitude;

        transform.position = new Vector3(x, y, transform.position.z);

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float leftEdge = mainCamera.transform.position.x - halfWidth;
        float rightEdge = mainCamera.transform.position.x + halfWidth;
        float halfSpriteWidth = spriteRenderer.bounds.extents.x;

        if (driftDirection > 0 && transform.position.x - halfSpriteWidth > rightEdge)
        {
            transform.position = new Vector3(leftEdge - halfSpriteWidth - wrapBuffer, transform.position.y, transform.position.z);
            Wrapped?.Invoke(this);
        }
        else if (driftDirection < 0 && transform.position.x + halfSpriteWidth < leftEdge)
        {
            transform.position = new Vector3(rightEdge + halfSpriteWidth + wrapBuffer, transform.position.y, transform.position.z);
            Wrapped?.Invoke(this);
        }
    }
}
