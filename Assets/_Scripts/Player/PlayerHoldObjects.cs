//In the project, there are some orbs that the player must carry in order to solve the puzzles.
//This script is used to allow the player to hold and move this objects.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHoldObjects : MonoBehaviour
{
    public float pickUpRange = 2.5f;
    public float moveForce = 250;
    public Transform holdParent;
    public GameObject heldObject;
    public GameObject grabIcon;

    public bool FueAgarrada = false;
    public bool canHold = true;
    public GameObject UltimoAgarrado;

    void Start()
    {
        holdParent = GameObject.Find("GrabPivot").GetComponent<Transform>();
        grabIcon = GameObject.Find("GrabIcon");
    }

    void Update()
    {
        if (!PauseMenu.GameIsPaused || canHold)
        {
            RaycastHit hit2;
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit2, pickUpRange))
            {
                //Debug.Log(hit2.transform.name);
                if (hit2.transform.tag == "Meme")
                {
                    if (heldObject != null)
                    {
                        grabIcon.GetComponent<Image>().enabled = false;
                    }
                    else if (heldObject == null)
                    {
                        grabIcon.GetComponent<Image>().enabled = true;

                    }
                }
                else if (hit2.transform.tag != "Meme")
                {
                    grabIcon.GetComponent<Image>().enabled = false;
                }
            }
            else
            {
                grabIcon.GetComponent<Image>().enabled = false;

            }

            if (Input.GetMouseButtonDown(0))
            {
                if (heldObject == null)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, pickUpRange))
                    {
                        //Debug.Log(hit.transform.name);
                        if (hit.transform.tag == "Meme")
                        {
                            PickUpObject(hit.transform.gameObject);
                        }
                    }
                }
                else
                {
                    string name; name = heldObject.transform.name; 
                    GameObject.Find(name).GetComponent<OrbMemes>().StartCoroutine("NotHolding"); //no funciono para detener el reverb

                    DropObject();
                }
            }

            if (heldObject != null)
            {
                MoveObject();
            }
        }
    }

    void MoveObject()
    {
        if(Vector3.Distance(heldObject.transform.position, holdParent.position) > 0.1f)
        {
            Vector3 moveDirection = (holdParent.position - heldObject.transform.position);
            heldObject.GetComponent<Rigidbody>().AddForce(moveDirection * moveForce);
        }
    }
    void PickUpObject(GameObject pickObject)
    {
        if (pickObject.GetComponent<Rigidbody>())
        {
            Rigidbody objectRb = pickObject.GetComponent<Rigidbody>();
            objectRb.constraints = RigidbodyConstraints.None;
            objectRb.isKinematic = false;
            objectRb.useGravity = false;
            objectRb.freezeRotation = true;
            objectRb.drag = 10;

            objectRb.transform.parent = holdParent;
            heldObject = pickObject;

            UltimoAgarrado = objectRb.gameObject;
            FueAgarrada = true;

            string name; name = heldObject.transform.name;
            GameObject.Find(name).GetComponent<OrbMemes>().isHolded = true;
            GameObject.Find(name).GetComponent<OrbPosition>().isHolded = true;
        }
    }
    public void DropObject()
    {        
        Rigidbody heldRig = heldObject.GetComponent<Rigidbody>();
        //heldRig.isKinematic = false; 
        heldRig.useGravity = true;
        heldRig.freezeRotation = false;
        heldRig.drag = 1;

        string name; name = heldObject.transform.name;
        GameObject.Find(name).GetComponent<OrbMemes>().isHolded = false;
        GameObject.Find(name).GetComponent<OrbPosition>().isHolded = false;
        GameObject.Find(name).GetComponent<OrbPosition>().isGroundedLock = false;

        heldObject.transform.parent = null;
        heldObject = null;

        FueAgarrada = false;
    }


    //To avoid bugs or cheating, when the orb gets close enough to the pedestal it relocates until it is grabbed again.
    public void SnapObject(GameObject orbe) //Solo se usa al ser llamado por el pedestal
    {        
        Rigidbody snapRig = orbe.GetComponent<Rigidbody>();
        snapRig.useGravity = true;
        snapRig.drag = 1;
        snapRig.constraints = RigidbodyConstraints.FreezeAll;

        if (heldObject != null && heldObject == orbe) //si tenes un objeto agarrado y si es el mismo que entro al trigger
        {
            string name; name = heldObject.transform.name;
            GameObject.Find(name).GetComponent<OrbMemes>().isHolded = false;
            GameObject.Find(name).GetComponent<OrbMemes>().StartCoroutine("NotHolding");

            if (heldObject.transform.parent != null) heldObject.transform.parent = null; //si tiene padre entonces lo sacas del padre
            heldObject = null; //ya no hay mas objeto agarrado
        }
    }
}
