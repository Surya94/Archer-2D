using UnityEngine;

public class ObjectVisibilityChecker : MonoBehaviour
{
    private Renderer objectRenderer;
    private Camera mainCamera;
    private Arrow parentArrow;
    private bool canCheckVisiblity;
    private void Start()
    {
        parentArrow = GetComponentInParent<Arrow>();
        objectRenderer = GetComponent<Renderer>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        canCheckVisiblity = true;
    }    

    private void Update()
    {
        if (canCheckVisiblity)
        {
            bool isVisible = GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(mainCamera), objectRenderer.bounds);
            if (!isVisible)
            {
                Debug.Log("Object moved out of screen".ToColor("orange"));
                canCheckVisiblity = false;
                parentArrow.DisableArrow();
            }
        }
    }
}
