using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbPosition : MonoBehaviour
{    
    public bool isGrounded;
    public bool isGroundedLock;
    public bool isHolded;
    public bool isHoldedLock;
    public LayerMask groundMask;
    public Rigidbody rb;

    void Start()
    {
        groundMask = LayerMask.GetMask("Ground");
        rb = this.gameObject.GetComponent<Rigidbody>();
    }
        
    void Update()
    {
        isGrounded = Physics.CheckSphere(this.gameObject.transform.position, 0.6f, groundMask);

        if (isGrounded && !isGroundedLock) //&& !holded?
        {
            StartCoroutine("Parar");
            isGroundedLock = true;
        }
        if (isHolded && !isHoldedLock) 
        {
            StopCoroutine("Parar");
        }
            

    }

    public IEnumerator Parar()
    {
        yield return new WaitForSeconds(3f);
        if (isGrounded)
        {
            if (Camera.main.transform.GetComponent<PlayerHoldObjects>().FueAgarrada == false)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                isGroundedLock = false;
                StopCoroutine("Parar");
            }
        }
        else 
        {
            isGroundedLock = false;
            StopCoroutine("Parar");
        }
    }
}
