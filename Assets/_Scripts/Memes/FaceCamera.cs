using UnityEngine;

// Always faces the camera (before: LookAtPlayer)
public class FaceCamera : MonoBehaviour
{
    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        transform.LookAt(cameraTransform);
    }
}
