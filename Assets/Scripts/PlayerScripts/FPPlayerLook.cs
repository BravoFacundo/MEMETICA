using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPPlayerLook : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public Transform playerBody;
    float xRotation = 0f;
    public bool canLook = false;

    //zoom

    public int zoom = 20;
    public int normal = 60;
    public float smooth = 5;
    public float slow = 1;
    private bool isZoomed = false;

    void Start()
    {
        playerBody = GameObject.Find("Player").GetComponent<Transform>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    
    void Update()
    {
        
        if (canLook)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity / slow; //supuestamente los inputs del mouse son independientes del framerate
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity / slow; //se nota mas al exportarlo, pero sino, volver a poner *Time.Deltatime

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);

            if (Input.GetMouseButtonDown(1))                  
                isZoomed = true; //isZoomed = !isZoomed;
            else if (Input.GetMouseButtonUp(1))
                isZoomed = false;

            if (isZoomed)
            {
                GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, zoom, Time.deltaTime * smooth);
                slow = 10;
            }
            else
            {
                GetComponent<Camera>().fieldOfView = Mathf.Lerp(GetComponent<Camera>().fieldOfView, normal, Time.deltaTime * smooth);
                slow = 1;
            }
        }
    }
}
