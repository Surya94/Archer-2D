using System.Collections;
using UnityEngine;

public class ObjectVisibilityChecker : MonoBehaviour
{
    private Renderer objectRenderer;
    private Camera mainCamera;
    private Arrow parentArrow;
    private bool canCheckVisiblity;
    private Plane[] frustumPlanes;

    private void Start()
    {
        parentArrow = GetComponentInParent<Arrow>();
        objectRenderer = GetComponent<Renderer>();
        mainCamera = Camera.main;
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        StartCoroutine(CheckVisibility());
    }

    private void OnEnable()
    {
        canCheckVisiblity = true;
    }

    private IEnumerator CheckVisibility()
    {
        while (canCheckVisiblity)
        {
            bool isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, objectRenderer.bounds);
            if (!isVisible)
            {
                Debug.Log("Object moved out of screen".ToColor("orange"));
                canCheckVisiblity = false;
                parentArrow.DisableArrow();
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}
