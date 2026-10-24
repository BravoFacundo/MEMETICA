using UnityEngine;

// Faces the camera (before: LookAtPlayer).
//   Toward Camera  1: looks right at the camera (a billboard). 0: parallel to the screen, looking straight ahead.
//                  In between: mostly ahead, turned a little toward the camera (it feels 3D, not a flat canvas)
//   Camera Up      on: upright on the screen (it tilts with the camera: things you carry). Off: upright in the world
public class FaceCamera : MonoBehaviour
{
    [Range(0f, 1f)] public float towardCamera = 1f;
    public bool cameraUp = false;

    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        Vector3 up = cameraUp ? cameraTransform.up : Vector3.up;
        Vector3 toCamera = cameraTransform.position - transform.position;
        if (toCamera.sqrMagnitude < 1e-6f) return;
        Quaternion atCamera = Quaternion.LookRotation(toCamera, up);
        Quaternion ahead = Quaternion.LookRotation(-cameraTransform.forward, up);
        transform.rotation = Quaternion.Slerp(ahead, atCamera, towardCamera);
    }
}
