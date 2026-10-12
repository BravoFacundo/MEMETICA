//Simple script for First Person Camera Movement with Zooming functionality.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.

using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    private Transform playerTransform;
    
    [Header("Player Look")]
    public bool playerCanLook = true;   // the game starts looking (tutorial popups no longer block it)
    [Tooltip("Base sensitivity: the value at the middle of the settings slider (the slider scales it x0.25 to x4)")]
    public float mouseSensitivity = 3f;
    private float sensitivityMultiplier = 1f;   // from the settings window
    private float sensitivityReduction = 1;
    private float xRotation = 0f;
    
    [Header("Player Zoom")]
    [SerializeField] private bool isZooming = false;
    [SerializeField] private float zoomingSpeed = 5;
    [SerializeField] private int defaultFOV = 90;
    [SerializeField] private int zoomFOV = 20;

    // Read by Crosshair to show the zoom icons
    public bool IsZooming => isZooming;
    public float DefaultFOV => defaultFOV;
    public float ZoomFOV => zoomFOV;

    void Start()
    {
        playerTransform = transform.parent.parent.GetComponent<Transform>();
        Cursor.lockState = CursorLockMode.Locked;

        // Sensitivity comes from the saved settings and follows the settings window
        ApplySettings();
        GameSettings.Changed += ApplySettings;
    }

    void OnDestroy() => GameSettings.Changed -= ApplySettings;
    void ApplySettings() => sensitivityMultiplier = GameSettings.MouseSensitivityMultiplier;
    
    void Update()
    {        
        if (playerCanLook)
        {
            MovePlayerView();
            ZoomOnPlayerView();
        }
    }

    //In the project, the Player object moves according to its forward vector.
    //This function not only rotates the camera on the Y axis but also rotates the player's body on the X axis to not hinder their movement.
    private void MovePlayerView()
    {
        float sensitivity = mouseSensitivity * sensitivityMultiplier / sensitivityReduction;
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerTransform.Rotate(Vector3.up * mouseX);
    }

    private void ZoomOnPlayerView()
    {
        if (Input.GetMouseButtonDown(1))
            isZooming = true;
        else if (Input.GetMouseButtonUp(1))
            isZooming = false;

        if (isZooming)
        {
            GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, zoomFOV, Time.deltaTime * zoomingSpeed);
            sensitivityReduction = 10;
        }
        else
        {
            GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, defaultFOV, Time.deltaTime * zoomingSpeed);
            sensitivityReduction = 1;
        }
    }

}

