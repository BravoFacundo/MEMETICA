//Simple script for First Person Camera Movement with Zooming functionality.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPPlayerLook : MonoBehaviour
{
    
    private Transform playerBody;
    
    [Header("Player Look")]
    public bool playerCanLook = false;
    public float mouseSensitivity = 100f;
    private float sensitivityReduction = 1;
    private float xRotation = 0f;
    
    [Header("Player Zoom")]
    public bool isZooming = false;
    public float zoomingSpeed = 5;
    public int normalFOV = 90;
    public int zoomingFOV = 20;

    void Start()
    {
        playerBody = GameObject.Find("Player").GetComponent<Transform>();
        Cursor.lockState = CursorLockMode.Locked;
    }
    
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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity / sensitivityReduction;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity / sensitivityReduction;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }

    private void ZoomOnPlayerView()
    {
        if (Input.GetMouseButtonDown(1))
            isZooming = true;
        else if (Input.GetMouseButtonUp(1))
            isZooming = false;

        if (isZooming)
        {
            GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, zoomingFOV, Time.deltaTime * zoomingSpeed);
            sensitivityReduction = 10;
        }
        else
        {
            GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, normalFOV, Time.deltaTime * zoomingSpeed);
            sensitivityReduction = 1;
        }
    }
}
