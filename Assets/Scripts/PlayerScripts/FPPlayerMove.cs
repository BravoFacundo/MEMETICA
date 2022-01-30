using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPPlayerMove : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    public float jumpHeight = 3f;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;
    Vector3 velocity;
    public bool isGrounded;
    public bool lockOneTime;
    public bool hit1Lock = false;
    public bool hit2Lock = false;
    public GameObject prevIsland;
    public GameObject prevBridge;


    public bool canMove = true;
    public bool ascend = false;

    public bool creativeMode = false;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        groundCheck = GameObject.Find("GroundCheck").GetComponent<Transform>();
    }

    void Update()
    {
        if (canMove)
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


            controller.Move(move * speed * Time.deltaTime);

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

            controller.Move(velocity * Time.deltaTime);
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
            if (hit.transform.tag == "Platform" && (isGrounded == true) && hit.transform.gameObject.GetComponent<FallingPlatform>() != null)
            {
                hit.transform.gameObject.GetComponent<FallingPlatform>().onTop = true;
            }
        }

        //Orbes que cambian 
        RaycastHit hit1;
        if (Physics.Raycast(this.gameObject.transform.position, transform.TransformDirection(Vector3.down), out hit1))
        {
            if (Camera.main.GetComponent<HoldObjects>().FueAgarrada == true)
            {
                if (ParentWithTag(hit.transform.gameObject, "Puente") != null && hit1Lock == false
                    && prevBridge != ParentWithTag(hit.transform.gameObject, "Puente"))
                {
                    
                    
                    if (Camera.main.GetComponent<HoldObjects>().heldObject != null)
                    {
                        Camera.main.GetComponent<HoldObjects>().heldObject.GetComponent<Memes>().isCrossing = true;
                        Camera.main.GetComponent<HoldObjects>().heldObject.GetComponent<Memes>().changeLock = false;
                    }
                    hit2Lock = false;
                    hit1Lock = true;
                    prevBridge = ParentWithTag(hit.transform.gameObject, "Puente");
                }
                if (ParentWithTag(hit.transform.gameObject, "Isla") != null && hit2Lock == false
                    && prevIsland != ParentWithTag(hit.transform.gameObject, "Isla"))
                {                    
                    hit1Lock = false;
                    hit2Lock = true;
                    prevIsland = ParentWithTag(hit.transform.gameObject, "Isla");
                }
            }            
        }
    }

    public GameObject ParentWithTag(GameObject childObject, string tag)
    {
        Transform t = childObject.transform;
        while (t.parent != null)
        {
            if (t.parent.tag == tag)
            {                
                return t.parent.gameObject;
            }
            t = t.parent.transform;
        }
        return null; // Could not find a parent with given tag.
    }

    public void ActivateMoveTutorial()
    {
        canMove = false;
    }
    public void DeactivateMoveTutorial()
    {
        canMove = true;
    }
    public void LookTutorialCompleted()
    {
        canMove = true;
    }
}
