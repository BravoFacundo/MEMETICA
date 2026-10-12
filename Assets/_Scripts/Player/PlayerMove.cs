//Simple script for First Person Player Movement

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private CharacterController characterController;
    private PlayerHoldObjects hold;   // está en la Main Camera; se guarda una vez

    [Header("Player Move")]
    public bool playerCanMove = true;
    public float walkSpeed = 5f;
    [Tooltip("Con Shift, solo en el piso (en el aire se vuelve a Walk Speed, como en el original)")]
    public float runSpeed = 10f;
    private float currentSpeed;
    private Vector3 velocity;

    [Header("Player Jump")]
    public float jumpHeight = 3f;
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;
    public bool isGrounded;

    [Header("Cruces con orbe (puente → isla)")]
    public bool hit1Lock = false;
    public bool hit2Lock = false;
    public GameObject prevIsland;
    public GameObject prevBridge;

    [Header("Final")]
    public bool ascend = false;   // lo prende el LevelManager al terminar el juego

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = transform.Find("GroundCheck").GetComponent<Transform>();
        hold = Camera.main.GetComponent<PlayerHoldObjects>();
        currentSpeed = walkSpeed;
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

            // Caminar o correr: correr solo en el piso; en el aire vuelve a caminar (comportamiento original)
            currentSpeed = (isGrounded && Input.GetKey(KeyCode.LeftShift)) ? runSpeed : walkSpeed;

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            Vector3 move = transform.right * x + transform.forward * z;   // (se sacó el vuelo con la ruedita del mouse)
            characterController.Move(move * currentSpeed * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        if (ascend)
        {
            velocity.y += 25f * Time.deltaTime;
        }

        // Un solo rayo hacia abajo para todo (antes eran dos, más varios GetComponent por frame)
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit)) return;

        // Plataformas que caen
        if (isGrounded && hit.transform.TryGetComponent(out Platform platform))
        {
            platform.onTop = true;
        }

        // Orbes que cambian: cruzar un puente y llegar a una isla nueva con el orbe en la mano
        if (!hold.FueAgarrada) return;

        GameObject bridge = FindParentWithTag(hit.transform.gameObject, "Puente");
        if (bridge != null && !hit1Lock && prevBridge != bridge)
        {
            if (hold.heldObject != null && hold.heldObject.TryGetComponent(out OrbMemes crossingMemes))
            {
                crossingMemes.isCrossing = true;
                crossingMemes.changeLock = false;
            }
            hit2Lock = false;
            hit1Lock = true;
            prevBridge = bridge;
        }

        GameObject island = FindParentWithTag(hit.transform.gameObject, "Isla");
        if (island != null && !hit2Lock && prevIsland != island)
        {
            hit1Lock = false;
            hit2Lock = true;
            prevIsland = island;

            // Cada isla nueva a la que llevás el orbe desbloquea una versión más de su imagen
            if (hold.heldObject != null && hold.heldObject.TryGetComponent(out OrbMemes heldMemes)) heldMemes.IslandVisited(island);
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
