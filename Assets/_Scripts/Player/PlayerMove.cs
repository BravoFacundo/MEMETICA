//Simple script for First Person Player Movement.
//Also respawns the player on the last island stepped on after falling into the void (before: PlayerRespawn).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private CharacterController characterController;
    private PlayerLook look;   // está en la Main Camera; se guarda una vez

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

    [Header("Cruces con meme (puente → isla)")]
    public bool hit1Lock = false;
    public bool hit2Lock = false;
    public GameObject prevIsland;
    public GameObject prevBridge;

    [Header("Final")]
    public bool ascend = false;   // lo prende el LevelManager al terminar el juego

    private Transform respawnPoint;
    private Transform lastGroundHit;
    private GameObject lastIsland;
    private float lastGroundY;   // altura del último piso pisado: caer muy por debajo = cayó al vacío

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = transform.Find("GroundCheck").GetComponent<Transform>();
        look = Camera.main.GetComponent<PlayerLook>();
        currentSpeed = walkSpeed;

        // El juego arranca en el tutorial (la isla del tutorial no tiene el tag "Isla", así que se fija acá)
        respawnPoint = LevelManager.Instance.startRespawnPoint;
        lastGroundY = transform.position.y;
    }

    void Update()
    {
        if (CheckFallAndRespawn()) return;

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
        if (isGrounded && hit.transform.TryGetComponent(out FallingPlatform platform))
        {
            platform.onTop = true;
        }

        // Cruzar un puente y llegar a una isla nueva con el meme en la mano
        if (!look.FueAgarrada) return;

        GameObject bridge = FindParentWithTag(hit.transform.gameObject, "Puente");
        if (bridge != null && !hit1Lock && prevBridge != bridge)
        {
            if (look.heldObject != null && look.heldObject.TryGetComponent(out Meme crossingMeme)) crossingMeme.OnBridgeCrossed();
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

            // Llevar el meme a una isla nueva (MemeOrb: desbloquea una versión más; MemeWindow: trigger Mover)
            if (look.heldObject != null && look.heldObject.TryGetComponent(out Meme heldMeme)) heldMeme.OnIslandVisited(island);
        }
    }

    //---- Respawn (before: PlayerRespawn)

    // ¿Cayó al vacío? Si no, actualiza el punto de respawn según la isla que pisa
    private bool CheckFallAndRespawn()
    {
        if (LevelManager.Instance != null && transform.position.y < lastGroundY - LevelManager.Instance.fallDistance)
        {
            Respawn();
            return true;
        }

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f)) return false;
        lastGroundY = hit.point.y;                           // el piso de abajo (sigue a islas y ascensores que se mueven)
        if (hit.transform == lastGroundHit) return false;   // sigue parado sobre lo mismo
        lastGroundHit = hit.transform;

        GameObject island = FindParentWithTag(hit.transform.gameObject, "Isla");
        if (island == null || island == lastIsland) return false;   // un puente, o la misma isla
        lastIsland = island;

        Transform point = LevelManager.Instance.RespawnPointOf(island);
        if (point != null) respawnPoint = point;
        return false;
    }

    // Fija el respawn a mano. Lo usa el ascensor: el jugador llega al Área 1 sin haberla pisado.
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;   // obliga a volver a revisar el piso en el próximo frame
    }

    private void Respawn()
    {
        // El CharacterController ignora el cambio de posición si está prendido
        characterController.enabled = false;
        transform.position = respawnPoint.position;
        characterController.enabled = true;

        // El meme en la mano viaja con el jugador y también cuenta como caída (MemeWindow: trigger Caer)
        if (look.heldObject != null && look.heldObject.TryGetComponent(out Meme heldMeme)) heldMeme.OnRespawned();
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
