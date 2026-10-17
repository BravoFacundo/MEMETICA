//First Person Player Movement (CharacterController).
//  Move     walk / run, keeps the take-off speed in the air with some air control
//  Jump     coyote time (jump a moment after leaving a ledge) and jump buffer (press a moment before landing)
//  Ground   anything you stand on counts (optional: only the Ground layer), snaps down slopes,
//           and carries you with moving ground (bridges, islands, platforms)
//  Respawn  back to the last island stepped on after falling into the void (before: PlayerRespawn)

using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("Player Move")]
    public bool playerCanMove = true;
    public float walkSpeed = 5f;
    [Tooltip("With Shift, on the ground. In the air you keep the speed you jumped with")]
    public float runSpeed = 10f;
    [Tooltip("How fast you can change direction in the air (m/s²). Higher = more control, lower = more inertia")]
    public float airAcceleration = 15f;

    [Header("Player Jump")]
    public float jumpHeight = 3f;
    public float gravity = -9.81f;
    [Tooltip("Max falling speed (m/s)")]
    public float maxFallSpeed = 25f;
    [Tooltip("Seconds after leaving a ledge when you can still jump")]
    public float coyoteTime = 0.2f;
    [Tooltip("Seconds a jump press is remembered before landing")]
    public float jumpBuffer = 0.2f;

    [Header("Ground")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    [Tooltip("Off: anything you stand on is ground (trees, rocks, memes). On: only Ground Mask counts, e.g. to limit where you can jump from")]
    public bool onlyGroundLayer = false;
    public LayerMask groundMask;
    [Tooltip("Meters it sticks down when walking off a slope or step, instead of flying off for a moment. 0 = off")]
    public float groundSnapDistance = 0.3f;
    [Tooltip("Moves with the ground you stand on (rising bridges, floating islands, falling platforms)")]
    public bool followMovingGround = true;
    [Tooltip("If the ground drops faster than this (m/s), you let go of it and fall on your own")]
    public float maxGroundFollowDownSpeed = 6f;
    public bool isGrounded;

    [Header("Cruces con meme (puente → isla)")]
    public bool hit1Lock = false;
    public bool hit2Lock = false;
    public GameObject prevIsland;
    public GameObject prevBridge;

    [Header("Final")]
    public bool ascend = false;   // lo prende el LevelManager al terminar el juego

    private CharacterController characterController;
    private PlayerLook look;   // está en la Main Camera; se guarda una vez

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float airSpeed;
    private float lastGroundedTime = -99f;
    private float lastJumpPressedTime = -99f;
    private bool wasGrounded;

    private Transform movingGround;
    private Vector3 movingGroundLocalPoint;

    private Transform respawnPoint;
    private Transform lastGroundHit;
    private GameObject lastIsland;
    private float lastGroundY;   // altura del último piso pisado: caer muy por debajo = cayó al vacío

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = transform.Find("GroundCheck").GetComponent<Transform>();
        look = Camera.main.GetComponent<PlayerLook>();
        airSpeed = walkSpeed;

        // El juego arranca en el tutorial (la isla del tutorial no tiene el tag "Isla", así que se fija acá)
        respawnPoint = LevelManager.Instance.startRespawnPoint;
        lastGroundY = transform.position.y;
    }

    void Update()
    {
        if (CheckFallAndRespawn()) return;

        if (playerCanMove)
        {
            FollowMovingGround();
            UpdateGrounded();
            UpdateHorizontalVelocity();
            UpdateVerticalVelocity();

            CollisionFlags flags = characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;   // bumped the ceiling

            SnapToGround();
            RememberMovingGround();
        }

        if (ascend) verticalVelocity += 25f * Time.deltaTime;

        UpdateGroundInteractions();
    }

    //---- Ground

    private void UpdateGrounded()
    {
        int mask = onlyGroundLayer ? groundMask.value : ~(1 << gameObject.layer);   // never the player itself
        bool sphereHit = Physics.CheckSphere(groundCheck.position, groundDistance, mask, QueryTriggerInteraction.Ignore);
        isGrounded = onlyGroundLayer ? sphereHit : sphereHit || characterController.isGrounded;
        isGrounded &= verticalVelocity <= 0.01f;   // going up after a jump is never grounded

        if (isGrounded) lastGroundedTime = Time.time;
        if (!isGrounded) movingGround = null;
        wasGrounded = isGrounded;
    }

    // Walking off a slope or a small step: stick to the ground instead of flying off for a frame
    private void SnapToGround()
    {
        if (groundSnapDistance <= 0f || !wasGrounded || verticalVelocity > 0f || characterController.isGrounded) return;

        float castRadius = characterController.radius * 0.9f;
        float toFeet = characterController.height * 0.5f - castRadius;   // from the sphere's bottom to the feet
        Vector3 origin = transform.position + characterController.center;
        if (!Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, toFeet + groundSnapDistance + characterController.skinWidth, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore)) return;
        if (Vector3.Angle(hit.normal, Vector3.up) > characterController.slopeLimit) return;

        float gap = hit.distance - toFeet;
        if (gap > 0.01f) characterController.Move(Vector3.down * gap);
    }

    // The ground under the player moved since last frame: move the same amount
    private void FollowMovingGround()
    {
        if (!followMovingGround || movingGround == null) return;

        Vector3 delta = movingGround.TransformPoint(movingGroundLocalPoint) - transform.position;
        if (delta.y < -maxGroundFollowDownSpeed * Time.deltaTime) { movingGround = null; return; }   // it drops too fast: let go
        if (delta.sqrMagnitude > 0.0000001f) characterController.Move(delta);
    }

    private void RememberMovingGround()
    {
        if (movingGround != null) movingGroundLocalPoint = movingGround.InverseTransformPoint(transform.position);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.7f && movingGround != hit.transform)
        {
            movingGround = hit.transform;
            movingGroundLocalPoint = movingGround.InverseTransformPoint(transform.position);
        }
    }

    //---- Move & Jump

    private void UpdateHorizontalVelocity()
    {
        Vector3 input = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
        input = Vector3.ClampMagnitude(input, 1f);   // diagonal is not faster

        if (isGrounded)
        {
            airSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            horizontalVelocity = input * airSpeed;
        }
        else
        {
            // Air: keeps the take-off speed, steering is gradual
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, input * airSpeed, airAcceleration * Time.deltaTime);
        }
    }

    private void UpdateVerticalVelocity()
    {
        if (Input.GetKeyDown(KeyCode.Space)) lastJumpPressedTime = Time.time;

        if (isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;   // keeps it pressed to the ground

        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBuffer;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (jumpBuffered && canJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -99f;
            lastGroundedTime = -99f;   // one jump per take-off
            movingGround = null;
            wasGrounded = false;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -maxFallSpeed);
    }

    //---- What is under the player

    private void UpdateGroundInteractions()
    {
        // Un solo rayo hacia abajo para todo
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit)) return;

        // Plataformas que caen
        if (isGrounded && hit.transform.TryGetComponent(out FallingPlatform platform)) platform.StepOn();

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
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        movingGround = null;

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
            if (t.parent.CompareTag(tag)) return t.parent.gameObject;
            t = t.parent.transform;
        }
        return null;
    }

    public void ActivateMoveTutorial() => playerCanMove = false;
    public void DeactivateMoveTutorial() => playerCanMove = true;
    public void LookTutorialCompleted() => playerCanMove = true;
}
