//First Person Player Movement (CharacterController).
//  Move     walk / run, keeps the take-off speed in the air with some air control
//  Jump     coyote time (jump a moment after leaving a ledge) and jump buffer (press a moment before landing)
//  Ground   anything you stand on counts (optional: only the Ground layer), snaps down slopes,
//           and carries you with moving ground (bridges, islands, platforms)
//  Respawn  back to the last island stepped on after falling into the void (before: PlayerRespawn). It falls from
//           high and brakes before the ground; with a RespawnSlam (on and enabled) it slams down from close instead

using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Section("Move")]
    public bool playerCanMove = true;
    public float walkSpeed = 5f;
    [Tooltip("With Shift, on the ground. In the air you keep the speed you jumped with")]
    public float runSpeed = 10f;
    [Tooltip("How fast you can change direction in the air (m/s²). Higher = more control, lower = more inertia")]
    public float airAcceleration = 15f;

    [Section("Jump")]
    public float jumpHeight = 3f;
    public float gravity = -9.81f;
    [Tooltip("Max falling speed (m/s)")]
    public float maxFallSpeed = 25f;
    [Tooltip("Seconds after leaving a ledge when you can still jump")]
    public float coyoteTime = 0.2f;
    [Tooltip("Seconds a jump press is remembered before landing")]
    public float jumpBuffer = 0.2f;

    [Section("Respawn")]
    [Tooltip("After reappearing it falls fast, then brakes over these last meters before the ground. 0 = no brake")]
    public float respawnBrakeDistance = 6f;
    [Tooltip("Falling speed it brakes down to (m/s); the last bit it falls on its own")]
    public float respawnLandingSpeed = 3f;
    [Tooltip("Meters below the last ground, with nothing under you, when the fall counts as into the void (the meme in your hand turns into its Fall change). Low: right away; too low: the end of a jump over a gap counts too")]
    public float voidFallMargin = 1.5f;

    [Section("Ground")]
    [Tooltip("Meters it sticks down when walking off a slope or step, instead of flying off for a moment. 0 = off")]
    public float groundSnapDistance = 0.3f;
    [Tooltip("It moves with the ground you stand on (rising bridges, floating islands). If the ground drops faster than this (m/s), you let go of it and fall on your own")]
    public float maxGroundFollowDownSpeed = 6f;

    [Section("Advanced", Closed = true)]
    [Tooltip("Found by name (GroundCheck child)")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    [Tooltip("Off: anything you stand on is ground (trees, rocks, memes). On: only Ground Mask counts, e.g. to limit where you can jump from")]
    public bool onlyGroundLayer = false;
    [ShowIf(nameof(onlyGroundLayer))]
    public LayerMask groundMask;

    [Section("Debug", Closed = true)]
    [Tooltip("Logs each frame the body moves more than 5 mm up or down in one step, with every step of that frame (to find the bumps). Slows the game while on")]
    public bool logBumps = false;
    [ReadOnlyField] public bool isGrounded;
    [ReadOnlyField] public bool ascend = false;   // lo prende el LevelManager al terminar el juego

    // Falling with nothing below: it will reappear (the meme in the hand turns into its Fall change on the way down)
    public static event System.Action VoidFallStarted;
    // After that fall and the respawn, it touches the ground (RespawnSlam lands you on your face here)
    public static event System.Action RespawnTouchedDown;
    // Back in play after the respawn: touched the ground, or got up after the slam (the meme in the hand goes back)
    public static event System.Action RespawnLanded;
    // From the respawn until back in play
    public bool Respawning => awaitingLanding;
    public bool FallingIntoVoid { get; private set; }
    // On: walking and jumping keys do nothing (gravity and moving ground still do), e.g. RespawnSlam getting up
    public bool InputLocked { get; set; }

    // The island it stepped on last (null before any)
    public GameObject CurrentIsland => lastIsland;

    private CharacterController characterController;
    private PlayerLook look;   // está en la Main Camera; se guarda una vez
    private RespawnSlam slam;   // optional: the slam landing (Game)

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    // Meters per second it is falling now (0 going up or on the ground)
    public float FallSpeed => Mathf.Max(0f, -verticalVelocity);
    private float airSpeed;
    private float lastGroundedTime = -99f;
    private float lastJumpPressedTime = -99f;
    private float knockbackUntil;
    private bool landingBrake;   // just respawned: brakes before touching the ground
    private bool awaitingLanding;   // just respawned: RespawnLanded not sent yet
    private bool touchedDown;       // just respawned: RespawnTouchedDown already sent
    private bool wasGrounded;

    private Transform movingGround;
    private Vector3 movingGroundLocalPoint;
    private Quaternion movingGroundRotation;   // a turning platform (AimPlatform): the player turns with it
    private bool groundTurns;

    private float carriedHeight;   // how much the ground lifted or lowered the player this frame (PlayerLook smooths the rest)
    private float debugHeight;
    private string debugSteps = "";
    private bool debugJump;

    private Transform respawnPoint;
    private Transform lastGroundHit;
    private GameObject lastIsland;
    private GameObject groundIsland;      // the island under the player now (null on a bridge)
    private float nextRespawnCheck;
    private float lastGroundY;   // altura del último piso pisado: caer muy por debajo = cayó al vacío

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundCheck = transform.Find("GroundCheck").GetComponent<Transform>();
        look = Camera.main.GetComponent<PlayerLook>();
        slam = GetComponent<RespawnSlam>();
        airSpeed = walkSpeed;
        debugHeight = transform.position.y;

        // El juego arranca en el tutorial (la isla del tutorial no tiene el tag "Isla", así que se fija acá)
        respawnPoint = LevelManager.Instance.startRespawnPoint;
        lastGroundY = transform.position.y;
    }

    void Update()
    {
        LogStep("between frames");
        if (Scripted) return;   // moved by something else (going through a portal)
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
        if (playerCanMove && !Scripted) FollowMovingGround();
        LogStep("FollowMovingGround");
        LogFrame();

        look.FollowBody(carriedHeight, isGrounded && playerCanMove && !Scripted);   // the camera smooths what the ground didn't carry
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

    // What the player stands on and measures: everything but itself and the props (PhysicsLayers: solid only for memes)
    private int SolidMask => ~(1 << gameObject.layer | PhysicsLayers.PropsMask);

    private void UpdateGrounded()
    {
        int mask = onlyGroundLayer ? groundMask.value : SolidMask;   // never the player itself nor props
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
        if (!Physics.SphereCast(bottomSphere + Vector3.up * lift, radius, Vector3.down, out RaycastHit hit, reach, SolidMask, QueryTriggerInteraction.Ignore)) return;
        if (Vector3.Angle(hit.normal, Vector3.up) > characterController.slopeLimit) return;

        // Down to resting distance: the controller keeps Skin Width from the ground, measured along the slope
        float gap = (hit.distance - lift) - characterController.skinWidth / Mathf.Max(hit.normal.y, 0.5f);
        if (gap > 0.002f) characterController.Move(Vector3.down * gap);
    }

    // The ground under the player moved since last frame: move the same amount
    private void FollowMovingGround()
    {
        if (movingGround == null) return;

        Vector3 delta = movingGround.TransformPoint(movingGroundLocalPoint) - transform.position;
        if (delta.y < -maxGroundFollowDownSpeed * Time.deltaTime) { movingGround = null; return; }   // it drops too fast: let go

        // Moved by hand, not with Move: the ground already went up into the feet, and Move would first push the
        // player out of it and then add the delta on top (twice the rise = the snap)
        transform.position += delta;
        carriedHeight += delta.y;
        if (groundTurns)   // turned with it, around the vertical
            transform.Rotate(0f, Mathf.DeltaAngle(movingGroundRotation.eulerAngles.y, movingGround.rotation.eulerAngles.y), 0f, Space.World);
        Physics.SyncTransforms();   // player and ground colliders where they are now, together
        FindGroundBelow();
    }

    // What the player stands on, and where on it, for next frame. Looked for with a cast from the body's middle
    // (before: collision callbacks, which skip frames while the ground pushes up into the feet, so the player
    // stopped following for a frame and got bumped up)
    private void FindGroundBelow()
    {
        movingGround = null;
        if (verticalVelocity > 0f) return;   // jumping: on its own

        float castRadius = characterController.radius * 0.9f;
        float toFeet = characterController.height * 0.5f - castRadius;
        Vector3 origin = transform.position + characterController.center;
        if (!Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, toFeet + 0.3f + characterController.skinWidth, SolidMask, QueryTriggerInteraction.Ignore)) return;
        if (hit.normal.y < 0.7f) return;

        groundTurns = hit.transform.GetComponentInParent<AimPlatform>() != null;
        movingGround = hit.transform;
        movingGroundLocalPoint = movingGround.InverseTransformPoint(transform.position);
        movingGroundRotation = movingGround.rotation;
    }

    //---- Move & Jump

    private void UpdateHorizontalVelocity()
    {
        if (Time.time < knockbackUntil) return;   // being thrown: no steering for a moment
        Vector2 move = GameInput.Move;   // keys or left stick (GameInput: a drifting gamepad never walks you)
        Vector3 input = InputLocked ? Vector3.zero : transform.right * move.x + transform.forward * move.y;
        input = Vector3.ClampMagnitude(input, 1f);   // diagonal is not faster

        if (isGrounded)
        {
            airSpeed = GameInput.Run ? runSpeed : walkSpeed;
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
        if (GameInput.JumpDown && !InputLocked) lastJumpPressedTime = Time.time;

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
        if (isGrounded)
        {
            landingBrake = false;
            FallingIntoVoid = false;
            if (awaitingLanding && !touchedDown)
            {
                touchedDown = true;
                RespawnTouchedDown?.Invoke();
                if (!SlamOn) FinishRespawn();   // with the slam, once you got up
            }
        }
        else
        {
            if (landingBrake) BrakeBeforeGround();
            // Already well below where it stood (not just the end of a jump over a gap), with nothing under it
            bool belowLastGround = transform.position.y < lastGroundY - voidFallMargin;
            if (!FallingIntoVoid && !awaitingLanding && verticalVelocity < 0f && belowLastGround && NothingBelow())
            {
                FallingIntoVoid = true;
                VoidFallStarted?.Invoke();
            }
        }
    }

    // No ground under the player, however far (the meme in the hand and invisible lowered islands don't count)
    private bool NothingBelow()
    {
        int mask = SolidMask & ~(1 << 2);   // not the player, not Ignore Raycast, not props
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position, Vector3.down, 1000f, mask, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<Meme>() == null) return false;
        return true;
    }

    //---- What is under the player

    private void UpdateGroundInteractions()
    {
        // Un solo rayo hacia abajo para todo
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return;

        // Falling platforms (the meme in the hand tracks its own bridges and islands: Meme.TrackGround)
        if (!isGrounded) return;
        PathStone stone = hit.collider.GetComponentInParent<PathStone>();   // stone paths (StonePath) first: their models may carry a disabled FallingPlatform
        if (stone != null) stone.StepOn();
        else if (hit.transform.TryGetComponent(out FallingPlatform platform)) platform.StepOn();
    }

    //---- Respawn landing

    private bool SlamOn => slam != null && slam.isActiveAndEnabled;

    // Back in play after a respawn (RespawnSlam calls it once you got up)
    public void FinishRespawn()
    {
        if (!awaitingLanding) return;
        awaitingLanding = false;
        touchedDown = false;
        RespawnLanded?.Invoke();
    }

    // Only the vertical speed: it can keep moving sideways while it brakes, wherever it is now
    private void BrakeBeforeGround()
    {
        if (verticalVelocity >= -respawnLandingSpeed) return;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 200f, SolidMask, QueryTriggerInteraction.Ignore)) return;

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

    //---- Portal (PortalWindow)

    // What is right under the player (MemeRecall: which island or bridge you are on)
    public Transform GroundUnder => lastGroundHit;

    // Moved by something else: no walking, gravity, collisions or falls into the void until FallFrom
    public bool Scripted { get; private set; }

    public void BeginScripted()
    {
        Scripted = true;
        characterController.enabled = false;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        movingGround = null;
    }

    // A fall like into the void for what you carry (the meme window shows its Fall change; back when you land)
    public void AnnounceFall() => VoidFallStarted?.Invoke();

    // Comes out there, falling at Speed: it lands as from a respawn (RespawnSlam), and that point is its respawn now
    public void FallFrom(Vector3 position, Quaternion rotation, float speed, Transform respawn)
    {
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        characterController.enabled = true;
        Scripted = false;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = -speed;
        movingGround = null;
        lastGroundHit = null;
        bool groundBelow = Physics.Raycast(position, Vector3.down, out RaycastHit ground, 500f, SolidMask, QueryTriggerInteraction.Ignore);
        lastGroundY = groundBelow ? ground.point.y : position.y;   // falling onto it is not a fall into the void
        if (respawn != null) SetRespawn(respawn);
        landingBrake = false;
        awaitingLanding = true;
        touchedDown = false;
        look.ResetSmoothing();
        debugHeight = transform.position.y;
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
        bool groundBelow = Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 500f, SolidMask, QueryTriggerInteraction.Ignore);
        lastGroundY = groundBelow ? ground.point.y : transform.position.y;
        look.ResetSmoothing();
        debugHeight = transform.position.y;
    }

    //---- Respawn (before: PlayerRespawn)

    // ¿Cayó al vacío? Si no, actualiza el punto de respawn según la isla que pisa
    private bool CheckFallAndRespawn()
    {
        float depth = SlamOn ? slam.fallDepth : LevelManager.Instance != null ? LevelManager.Instance.dropDistance : float.MaxValue;
        if (LevelManager.Instance != null && transform.position.y < lastGroundY - depth)
        {
            Respawn();
            return true;
        }

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return false;
        lastGroundY = hit.point.y;                           // el piso de abajo (sigue a islas y ascensores que se mueven)
        if (hit.transform != lastGroundHit)                  // something new under: which island it is
        {
            lastGroundHit = hit.transform;
            groundIsland = FindParentWithTag(hit.transform.gameObject, "Isla");
            if (groundIsland == null && hit.transform.GetComponentInParent<Bridge>() == null)   // (an island without the Isla tag)
            {
                Island island = hit.transform.GetComponentInParent<Island>();
                if (island != null) groundIsland = island.gameObject;
            }
        }
        if (groundIsland == null) return false;              // a bridge

        // On an island: its respawn point nearest to you (a course can have one before each part), checked now and then
        if (groundIsland == lastIsland && Time.time < nextRespawnCheck) return false;
        lastIsland = groundIsland;
        nextRespawnCheck = Time.time + 0.25f;
        Transform point = LevelManager.Instance.RespawnPointOf(groundIsland, transform.position);
        if (point == null && isGrounded) point = SafeSpot(groundIsland, hit.point);   // no RespawnPoint on it: where you last stood
        if (point != null) respawnPoint = point;
        return false;
    }

    // An island without a RespawnPoint: you reappear where you last stood on it (it floats with it)
    private Transform safeSpot;
    private GameObject warnedIsland;
    private Transform SafeSpot(GameObject island, Vector3 ground)
    {
        if (safeSpot == null) safeSpot = new GameObject("PlayerSafeSpot").transform;
        if (safeSpot.parent != island.transform) safeSpot.SetParent(island.transform, true);
        safeSpot.position = ground;
        if (warnedIsland != island)
        {
            warnedIsland = island;
            Debug.LogWarning($"[PlayerMove] '{island.name}' has no RespawnPoint: falling, you reappear where you last stood on it (Memetica > Level > Respawn > New Respawn Point).", island);
        }
        return safeSpot;
    }

    // Fija el respawn a mano. Lo usa el ascensor: el jugador llega al Área 1 sin haberla pisado.
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;   // obliga a volver a revisar el piso en el próximo frame
    }

    private void Respawn()
    {
        // Its island went down meanwhile (invisible, nothing to land on): back to the start
        if (respawnPoint == null || Lift.IsHiddenAt(respawnPoint))
        {
            Debug.LogWarning($"[PlayerMove] Respawn '{(respawnPoint != null ? respawnPoint.name : "none")}' " +
                             (respawnPoint != null ? "is in an island that is down" : "not set") + ": back to the start respawn point.", respawnPoint);
            respawnPoint = LevelManager.Instance.startRespawnPoint; lastIsland = null; lastGroundHit = null;
        }

        // Drop Distance above the ground of the RespawnPoint: it falls down onto it (and brakes before landing)
        Vector3 from = LevelManager.Instance.RespawnPosition(respawnPoint, out float groundY);
        respawnPoint.TryGetComponent(out RespawnPoint spot);
        if (spot != null && spot.height > 0f) from.y = groundY + spot.height;   // its own height
        lastGroundY = groundY;
        // El CharacterController ignora el cambio de posición si está prendido
        characterController.enabled = false;
        transform.position = from;
        characterController.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        movingGround = null;
        landingBrake = respawnBrakeDistance > 0f;
        awaitingLanding = true;
        touchedDown = false;

        // Slam: from close and fast, no brake (RespawnSlam lands it)
        if (SlamOn)
        {
            characterController.enabled = false;
            transform.position = new Vector3(from.x, groundY + (spot != null && spot.height > 0f ? spot.height : slam.height), from.z);
            characterController.enabled = true;
            verticalVelocity = -slam.StartSpeed(gravity);
            landingBrake = false;
        }

        // Facing its arrow, looking ahead: ready to try again
        if (spot != null && spot.face)
        {
            transform.rotation = Quaternion.LookRotation(spot.Facing, Vector3.up);   // (the head keeps its angle: looking down, still down)
        }
        look.ResetSmoothing();
        debugHeight = transform.position.y;
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
