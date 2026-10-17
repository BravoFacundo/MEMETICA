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

    // Lo lee Crosshair para mostrar la mano
    public bool LookingAtMeme { get; private set; }

    public bool FueAgarrada = false;
    public bool canHold = true;

    void Start()
    {
        holdParent = GameObject.Find("GrabPivot").GetComponent<Transform>();
    }

    void Update()
    {
        if (!PauseMenu.GameIsPaused && canHold)   // before: || let you grab while paused (clicking the pause window) or riding an elevator
        {
            // ¿Está mirando un orbe? (antes prendía y apagaba GrabIcon; ahora la mira la maneja Crosshair)
            // Ignora triggers: si no, la zona del pedestal (que envuelve al orbe) tapa el rayo
            LookingAtMeme = Physics.Raycast(transform.position, transform.forward, out RaycastHit hit2, pickUpRange, ~0, QueryTriggerInteraction.Ignore)
                           && hit2.transform.CompareTag("Meme");

            if (Input.GetMouseButtonDown(0))
            {
                if (heldObject == null)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(transform.position, transform.forward, out hit, pickUpRange, ~0, QueryTriggerInteraction.Ignore))
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
            FueAgarrada = true;

            if (heldObject.TryGetComponent(out Meme meme)) meme.PickUp();
        }
    }
    public void DropObject()
    {        
        Rigidbody heldRig = heldObject.GetComponent<Rigidbody>();
        //heldRig.isKinematic = false; 
        heldRig.useGravity = true;
        heldRig.freezeRotation = false;
        heldRig.drag = 1;

        if (heldObject.TryGetComponent(out Meme meme)) meme.Drop();

        heldObject.transform.parent = null;
        heldObject = null;

        FueAgarrada = false;
    }


    //To avoid bugs or cheating, when the meme gets close enough to the pedestal it relocates until it is grabbed again.
    //The pedestal tells the Meme itself (Meme.PlaceOnPedestal); here only the physics and the hand are released.
    public void SnapObject(GameObject memeObject) //Solo se usa al ser llamado por el pedestal
    {        
        Rigidbody snapRig = memeObject.GetComponent<Rigidbody>();
        snapRig.useGravity = true;
        snapRig.drag = 1;
        snapRig.constraints = RigidbodyConstraints.FreezeAll;

        if (heldObject != null && heldObject == memeObject) //si tenes un objeto agarrado y si es el mismo que entro al trigger
        {
            if (heldObject.transform.parent != null) heldObject.transform.parent = null; //si tiene padre entonces lo sacas del padre
            heldObject = null; //ya no hay mas objeto agarrado
        }
    }
}
