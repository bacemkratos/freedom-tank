using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;

        // Make sprite face the camera
        transform.forward = cam.transform.forward;
    }
}
