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

    [Header("Respawn")]
    [Tooltip("After reappearing it falls fast, then brakes over these last meters before the ground. 0 = no brake")]
    public float respawnBrakeDistance = 6f;
    [Tooltip("Falling speed it brakes down to (m/s); the last bit it falls on its own")]
    public float respawnLandingSpeed = 3f;

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

    [Header("Debug")]
    [Tooltip("Logs each frame the body moves more than 5 mm up or down in one step, with every step of that frame (to find the bumps). Slows the game while on")]
    public bool logBumps = false;

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
    private float knockbackUntil;
    private bool landingBrake;   // just respawned: brakes before touching the ground
    private bool wasGrounded;

    private Transform movingGround;
    private Vector3 movingGroundLocalPoint;

    private float carriedHeight;   // how much the ground lifted or lowered the player this frame (PlayerLook smooths the rest)
    private float debugHeight;
    private string debugSteps = "";
    private bool debugJump;

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
        debugHeight = transform.position.y;

        // El juego arranca en el tutorial (la isla del tutorial no tiene el tag "Isla", así que se fija acá)
        respawnPoint = LevelManager.Instance.startRespawnPoint;
        lastGroundY = transform.position.y;
    }

    void Update()
    {
        LogStep("between frames");
        if (CheckFallAndRespawn()) return;

        if (playerCanMove)
        {
            // The ground may have moved before this script ran (FloatMotion runs first): follow it now,
            // before measuring the ground again. LateUpdate follows what moves after (lifts, bridges)
            FollowMovingGround();
            LogStep("FollowMovingGround (early)");

            UpdateGrounded();
            UpdateHorizontalVelocity();
            UpdateVerticalVelocity();

            CollisionFlags flags = characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;   // bumped the ceiling
            LogStep("Move");

            SnapToGround();
            LogStep("SnapToGround");
            FindGroundBelow();
        }

        if (ascend) verticalVelocity += 25f * Time.deltaTime;

        UpdateGroundInteractions();
    }

    // After what moves the ground later in the frame (lifts and bridges in coroutines, platforms): follow it in the same frame
    void LateUpdate()
    {
        if (playerCanMove) FollowMovingGround();
        LogStep("FollowMovingGround");
        LogFrame();

        look.FollowBody(carriedHeight, isGrounded && playerCanMove);   // the camera smooths what the ground didn't carry
        carriedHeight = 0f;
    }

    // Debug: every step that moved the body more than 3 cm up or down this frame, in one line per frame
    private void LogStep(string step)
    {
        float change = transform.position.y - debugHeight;
        debugHeight = transform.position.y;
        if (!logBumps) return;
        debugSteps += $"{step} {change:+0.0000;-0.0000}  ";
        if (Mathf.Abs(change) > 0.005f) debugJump = true;   // small too: the sinking between bumps
    }

    private void LogFrame()
    {
        if (debugJump)
        {
            Vector3 flat = horizontalVelocity; flat.y = 0f;
            string ground = movingGround != null ? PathOf(movingGround) : "none";
            Debug.Log($"[PlayerMove] Frame {Time.frameCount}: {debugSteps}| ground carried {carriedHeight:+0.000;-0.000}, walking {flat.magnitude:F1} m/s, grounded {isGrounded}, " +
                      $"controller grounded {characterController.isGrounded}, vertical speed {verticalVelocity:F2}, ground: {ground}", this);
        }
        debugSteps = "";
        debugJump = false;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    //---- Ground

    private void UpdateGrounded()
    {
        int mask = onlyGroundLayer ? groundMask.value : ~(1 << gameObject.layer);   // never the player itself
        bool sphereHit = Physics.CheckSphere(groundCheck.position, groundDistance, mask, QueryTriggerInteraction.Ignore);
        isGrounded = onlyGroundLayer ? sphereHit : sphereHit || characterController.isGrounded;
        isGrounded &= verticalVelocity <= 0.01f;   // going up after a jump is never grounded

        if (isGrounded) lastGroundedTime = Time.time;
        wasGrounded = isGrounded;
    }

    // Holds the player on the ground at resting distance every grounded frame (also down slopes and small steps)
    private void SnapToGround()
    {
        if (groundSnapDistance <= 0f || !wasGrounded || verticalVelocity > 0f) return;   // every grounded frame: it is what holds the player on the ground

        // Same sphere as the bottom of the capsule, starting a bit higher so it never starts inside the ground.
        // Before: a smaller sphere from the middle of the body, which on slopes measured the ground lower than it is
        // and pulled the player a few mm into it every frame, until Unity popped it out half a meter
        float radius = characterController.radius;
        float lift = 0.1f;
        Vector3 bottomSphere = transform.position + characterController.center + Vector3.down * (characterController.height * 0.5f - radius);
        float reach = lift + characterController.skinWidth + groundSnapDistance;
        if (!Physics.SphereCast(bottomSphere + Vector3.up * lift, radius, Vector3.down, out RaycastHit hit, reach, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore)) return;
        if (Vector3.Angle(hit.normal, Vector3.up) > characterController.slopeLimit) return;

        // Down to resting distance: the controller keeps Skin Width from the ground, measured along the slope
        float gap = (hit.distance - lift) - characterController.skinWidth / Mathf.Max(hit.normal.y, 0.5f);
        if (gap > 0.002f) characterController.Move(Vector3.down * gap);
    }

    // The ground under the player moved since last frame: move the same amount
    private void FollowMovingGround()
    {
        if (!followMovingGround || movingGround == null) return;

        Vector3 delta = movingGround.TransformPoint(movingGroundLocalPoint) - transform.position;
        if (delta.y < -maxGroundFollowDownSpeed * Time.deltaTime) { movingGround = null; return; }   // it drops too fast: let go

        // Moved by hand, not with Move: the ground already went up into the feet, and Move would first push the
        // player out of it and then add the delta on top (twice the rise = the snap)
        transform.position += delta;
        carriedHeight += delta.y;
        Physics.SyncTransforms();   // player and ground colliders where they are now, together
        FindGroundBelow();
    }

    // What the player stands on, and where on it, for next frame. Looked for with a cast from the body's middle
    // (before: collision callbacks, which skip frames while the ground pushes up into the feet, so the player
    // stopped following for a frame and got bumped up)
    private void FindGroundBelow()
    {
        movingGround = null;
        if (!followMovingGround || verticalVelocity > 0f) return;   // jumping: on its own

        float castRadius = characterController.radius * 0.9f;
        float toFeet = characterController.height * 0.5f - castRadius;
        Vector3 origin = transform.position + characterController.center;
        if (!Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, toFeet + 0.3f + characterController.skinWidth, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y < 0.7f) return;

        movingGround = hit.transform;
        movingGroundLocalPoint = movingGround.InverseTransformPoint(transform.position);
    }

    //---- Move & Jump

    private void UpdateHorizontalVelocity()
    {
        if (Time.time < knockbackUntil) return;   // being thrown: no steering for a moment
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

        // Standing: no push into the ground (SnapToGround keeps it on it). Before: pressed down at 2 m/s every frame,
        // the controller crept a few mm per frame into the ground and got popped out 8 cm every ~10 frames
        if (isGrounded && verticalVelocity < 0f) verticalVelocity = 0f;

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

        if (!isGrounded || verticalVelocity > 0f) verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -maxFallSpeed);
        if (isGrounded) landingBrake = false;
        else if (landingBrake) BrakeBeforeGround();
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

    //---- Respawn landing

    // Only the vertical speed: it can keep moving sideways while it brakes, wherever it is now
    private void BrakeBeforeGround()
    {
        if (verticalVelocity >= -respawnLandingSpeed) return;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 200f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore)) return;

        float height = ground.distance - characterController.height * 0.5f - 0.6f;   // brakes until 0.6 m above, then drops
        if (height > respawnBrakeDistance) return;
        if (height <= 0f) { landingBrake = false; return; }

        float speed = -verticalVelocity;
        float braking = (speed * speed - respawnLandingSpeed * respawnLandingSpeed) / (2f * height);   // stops right at that height
        verticalVelocity = -Mathf.Max(respawnLandingSpeed, speed - braking * Time.deltaTime);
    }

    //---- Thrown

    // Something throws the player (a bridge bursting out of the ground): off the ground with this velocity,
    // without steering for a moment so the throw is felt
    public void Knockback(Vector3 velocity)
    {
        horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        verticalVelocity = Mathf.Max(velocity.y, 0.5f);
        airSpeed = Mathf.Max(walkSpeed, horizontalVelocity.magnitude * 0.5f);
        knockbackUntil = Time.time + 0.35f;
        movingGround = null;
        lastGroundedTime = -99f;   // no coyote jump out of it
        wasGrounded = false;
    }

    //---- Teleport (DebugTools)

    // Moves the player there and makes it its respawn, with no leftover speed
    public void TeleportTo(Transform point)
    {
        transform.SetParent(null);   // off any elevator that had boarded it
        playerCanMove = true;
        characterController.enabled = false;
        transform.position = point.position;
        characterController.enabled = true;
        SetRespawn(point);
        ResetMotion();
    }

    // Clears speed, moving ground and the fall reference (after a teleport or the noclip)
    public void ResetMotion()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        movingGround = null;
        lastGroundHit = null;

        // Fall reference = the ground below, however far (a spawn point high in the air must not count as falling into the void)
        bool groundBelow = Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 500f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
        lastGroundY = groundBelow ? ground.point.y : transform.position.y;
        look.ResetSmoothing();
        debugHeight = transform.position.y;
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
        landingBrake = respawnBrakeDistance > 0f;
        look.ResetSmoothing();
        debugHeight = transform.position.y;

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
