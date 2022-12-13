//Simple script for First Person Player Movement

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private CharacterController characterController;
    
    [Header("Player Move")]
    public bool playerCanMove = true;
    public float speed = 12f;
    Vector3 velocity;

    [Header("Player Jump")]
    public float jumpHeight = 3f;
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    public bool isGrounded;

    [Header("Lock bools")]
    public bool lockOneTime;
    public bool hit1Lock = false;
    public bool hit2Lock = false;
    public GameObject prevIsland;
    public GameObject prevBridge;


    //public bool playerCanMove = true;
    public bool ascend = false;

    public bool creativeMode = false;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = transform.Find("GroundCheck").GetComponent<Transform>();
    }

    void Update()
    {
        if (playerCanMove)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            float y = Input.GetAxis("Mouse ScrollWheel");

            //Vector3 move = transform.right * x + transform.forward * z;
            Vector3 move = transform.right * x + transform.forward * z + transform.up * y * speed;


            characterController.Move(move * speed * Time.deltaTime);

            if (Input.GetKeyDown("space") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            if (Input.GetKey("left shift") && isGrounded)
            {
                speed = 10f;
            }
            else
            {
                speed = 5f;
            }

            velocity.y += gravity * Time.deltaTime;

            characterController.Move(velocity * Time.deltaTime);
        }

        if (ascend)
        {
            //canMove = false;
            velocity.y += 25f * Time.deltaTime;
        }
        
        //Plataformas que caen
        RaycastHit hit;
        if (Physics.Raycast(this.gameObject.transform.position, transform.TransformDirection(Vector3.down), out hit)) 
        {
            if (hit.transform.tag == "Platform" && (isGrounded == true) && hit.transform.gameObject.GetComponent<Platform>() != null)
            {
                hit.transform.gameObject.GetComponent<Platform>().onTop = true;
            }
        }

        //Orbes que cambian 
        RaycastHit hit1;
        if (Physics.Raycast(this.gameObject.transform.position, transform.TransformDirection(Vector3.down), out hit1))
        {
            if (Camera.main.GetComponent<PlayerHoldObjects>().FueAgarrada == true)
            {
                if (FindParentWithTag(hit.transform.gameObject, "Puente") != null && hit1Lock == false
                    && prevBridge != FindParentWithTag(hit.transform.gameObject, "Puente"))
                {
                    
                    
                    if (Camera.main.GetComponent<PlayerHoldObjects>().heldObject != null)
                    {
                        Camera.main.GetComponent<PlayerHoldObjects>().heldObject.GetComponent<OrbMemes>().isCrossing = true;
                        Camera.main.GetComponent<PlayerHoldObjects>().heldObject.GetComponent<OrbMemes>().changeLock = false;
                    }
                    hit2Lock = false;
                    hit1Lock = true;
                    prevBridge = FindParentWithTag(hit.transform.gameObject, "Puente");
                }
                if (FindParentWithTag(hit.transform.gameObject, "Isla") != null && hit2Lock == false
                    && prevIsland != FindParentWithTag(hit.transform.gameObject, "Isla"))
                {                    
                    hit1Lock = false;
                    hit2Lock = true;
                    prevIsland = FindParentWithTag(hit.transform.gameObject, "Isla");
                }
            }            
        }
    }

    //In the project, the objects tagged "island" may be too far up the hierarchy from the object the raycast hits.
    //This function returns the parent object with the "Island" tag regardless of how far up the hierarchy it is.
    public GameObject FindParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null)
        {
            if (t.parent.CompareTag(tag))
            {                
                return t.parent.gameObject;
            }
            t = t.parent.transform;
        }
        return null;
    }

    public void ActivateMoveTutorial()
    {
        playerCanMove = false;
    }
    public void DeactivateMoveTutorial()
    {
        playerCanMove = true;
    }
    public void LookTutorialCompleted()
    {
        playerCanMove = true;
    }
}
