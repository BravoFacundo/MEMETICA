//First Person Player Movement (CharacterController).
//  Move     walk / run, keeps the take-off speed in the air with some air control
//  Jump     coyote time (jump a moment after leaving a ledge) and jump buffer (press a moment before landing)
//  Ground   anything you stand on counts (optional: only the Ground layer), snaps down slopes,
//           and carries you with moving ground (bridges, islands, platforms)
//  Respawn  back to the last island stepped on after falling into the void (PlayerMove.Respawn.cs, same component):
//           Classic falls from high and brakes before the ground (OriginalGame); Slam slams down from close and lands
//           you on your face, with falling and landing sounds on every big fall (Game)

using UnityEngine;

public partial class PlayerMove : MonoBehaviour
{
    [Section("Move")]
    public bool playerCanMove = true;
    public float walkSpeed = 5f;
    [Tooltip("With Shift, on the ground. In the air you keep the speed you jumped with")]
    public float runSpeed = 10f;
    [Tooltip("How fast you can change direction in the air (m/s²). Higher = more control, lower = more inertia")]
    public float airAcceleration = 15f;
    [Tooltip("In the air, letting go of the keys doesn't brake you: you keep your momentum until you land (pressing " +
             "another way still steers)")]
    public bool keepAirMomentum = true;
    [Tooltip("Pressing Shift in the air (late) gives you run speed too")]
    public bool runInAir = true;

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
    [Tooltip("Classic: you reappear high above (LevelManager's Drop Distance) and brake before the ground. Slam: you " +
             "reappear close above and slam down on your face (Slam Respawn below)")]
    public RespawnStyle respawnStyle = RespawnStyle.Slam;
    [Tooltip("After reappearing it falls fast, then brakes over these last meters before the ground. 0 = no brake")]
    [ShowIf(nameof(respawnStyle), RespawnStyle.Classic)] public float respawnBrakeDistance = 6f;
    [Tooltip("Falling speed it brakes down to (m/s); the last bit it falls on its own")]
    [ShowIf(nameof(respawnStyle), RespawnStyle.Classic)] public float respawnLandingSpeed = 3f;
    [Tooltip("Meters below the last ground, with nothing under you, when the fall counts as into the void (the meme in your hand turns into its Fall change). Low: right away; too low: the end of a jump over a gap counts too")]
    public float voidFallMargin = 1.5f;

    [Section("Ground")]
    [Tooltip("Meters it sticks down when walking off a slope or step, instead of flying off for a moment. 0 = off")]
    public float groundSnapDistance = 0.3f;
    [Tooltip("It moves with the ground you stand on (rising bridges, floating islands). If the ground drops faster than this (m/s), you let go of it and fall on your own")]
    public float maxGroundFollowDownSpeed = 6f;

    [Section("Advanced", Closed = true)]
    [Tooltip("Height of the ground check sphere's middle, from the Player's middle (meters, below: negative). It used to be the GroundCheck child")]
    public float groundCheckHeight = -0.821f;
    [Tooltip("Radius of that sphere")]
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
    // Fell into the void and reappeared (a fall for the score: RunStats). Not the portal's way back
    public static event System.Action VoidRespawned;
    // After that fall and the respawn, it touches the ground (Slam: you land on your face here)
    public static event System.Action RespawnTouchedDown;
    // Back in play after the respawn: touched the ground, or got up after the slam (the meme in the hand goes back)
    public static event System.Action RespawnLanded;
    // From the respawn until back in play
    public bool Respawning => awaitingLanding;
    public bool FallingIntoVoid { get; private set; }
    // On: walking and jumping keys do nothing (gravity and moving ground still do), e.g. getting up after the slam
    public bool InputLocked { get; set; }
    // A walk it does by itself (MemeRandomizer: to its stand point), whatever InputLocked says. Null: none
    public Vector3? AutoWalkTo { get; set; }
    public float AutoWalkSpeed { get; set; } = 1.5f;

    // The island it stepped on last (null before any)
    public GameObject CurrentIsland => lastIsland;

    private CharacterController characterController;
    private PlayerLook look;   // on the Player too

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    // Meters per second it is falling now (0 going up or on the ground)
    public float FallSpeed => Mathf.Max(0f, -verticalVelocity);
    private float airSpeed;
    private bool thrown;   // knocked back, not landed yet
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

    // Where the ground check sphere is (at your feet)
    Vector3 GroundCheckPoint => transform.TransformPoint(0f, groundCheckHeight, 0f);

#if UNITY_EDITOR
    // Selected: the ground check sphere
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(GroundCheckPoint, groundDistance);
    }
#endif

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        look = PlayerLook.Instance;
        SetUpSounds();
        airSpeed = walkSpeed;
        debugHeight = transform.position.y;

        // El juego arranca en el tutorial (la isla del tutorial no tiene el tag "Isla", así que se fija acá)
        respawnPoint = LevelManager.Instance.startRespawnPoint;
        lastGroundY = transform.position.y;
    }

    void Update()
    {
        LogStep("between frames");
        UpdateFallSound();   // (Slam: every big fall is heard, also while moved by something else)
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

            CollisionFlags flags = characterController.Move((horizontalVelocity + slideOff + Vector3.up * verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;   // bumped the ceiling
            LogStep("Move");

            SnapToGround();
            LogStep("SnapToGround");
            FindGroundBelow();
        }

        if (ascend)   // the end (LevelManager): up into the sky, speeding up to its Ending Rise Speed
        {
            float accel = LevelManager.Instance != null ? LevelManager.Instance.endingRiseAcceleration : 25f;
            float top = LevelManager.Instance != null ? LevelManager.Instance.endingRiseSpeed : float.MaxValue;
            verticalVelocity = Mathf.Min(verticalVelocity + accel * Time.deltaTime, top);
        }

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

    // Memes and obstacles are never ground: you can't stand or jump on them (dropping the window and jumping on it broke
    // the parkour; Meme also makes the player's body pass through them). On an obstacle you slide off (Obstacle)
    static bool IsMeme(Collider c) => c != null && (c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<Meme>() != null : c.GetComponentInParent<Meme>() != null)
                                      || (c != null && c.GetComponentInParent<Obstacle>() != null);
    private static readonly Collider[] overlaps = new Collider[16];
    private static readonly RaycastHit[] castHits = new RaycastHit[16];

    static bool SolidSphere(Vector3 position, float radius, int mask)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) if (!IsMeme(overlaps[i])) return true;
        return false;
    }

    // As Physics.SphereCast down, skipping memes (and what it starts inside of, as SphereCast does)
    static bool CastDown(Vector3 origin, float radius, float distance, int mask, out RaycastHit hit)
    {
        hit = default;
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, castHits, distance, mask, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = castHits[i];
            if (h.distance <= 0f && h.point == Vector3.zero) continue;   // started inside it
            if (h.distance >= nearest || IsMeme(h.collider)) continue;
            nearest = h.distance;
            hit = h;
        }
        return nearest < float.MaxValue;
    }

    // Standing on an obstacle (its top): pushed off it this frame
    private Vector3 slideOff;

    bool ObstacleUnder(out Obstacle obstacle, out Vector3 point)
    {
        obstacle = null; point = default;
        float radius = characterController.radius * 0.9f;
        Vector3 origin = transform.position + characterController.center;
        float reach = characterController.height * 0.5f - radius + 0.3f;
        if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, reach, SolidMask, QueryTriggerInteraction.Ignore)) return false;
        obstacle = hit.collider.GetComponentInParent<Obstacle>();
        point = hit.point;
        return obstacle != null;
    }

    // What it moves sideways now (the portal keeps it through)
    public Vector3 HorizontalVelocity => horizontalVelocity;

    private void UpdateGrounded()
    {
        int mask = onlyGroundLayer ? groundMask.value : SolidMask;   // never the player itself nor props
        bool sphereHit = SolidSphere(GroundCheckPoint, groundDistance, mask);
        isGrounded = onlyGroundLayer ? sphereHit : sphereHit || characterController.isGrounded;
        slideOff = Vector3.zero;
        if (!sphereHit && characterController.isGrounded && ObstacleUnder(out Obstacle obstacle, out Vector3 point))
        {
            isGrounded = false;   // resting on an obstacle: not ground, you slide off it
            Vector3 away = Vector3.ProjectOnPlane(transform.position - obstacle.transform.position, Vector3.up);
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            slideOff = away.normalized * obstacle.slideSpeed;
        }
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
        if (!CastDown(bottomSphere + Vector3.up * lift, radius, reach, SolidMask, out RaycastHit hit)) return;
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
        if (!CastDown(origin, castRadius, toFeet + 0.3f + characterController.skinWidth, SolidMask, out RaycastHit hit)) return;
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

        if (AutoWalkTo.HasValue && isGrounded)   // walked there by the game (it moves with the ground like any walk)
        {
            Vector3 to = AutoWalkTo.Value - transform.position;
            to.y = 0f;
            float speed = Mathf.Min(AutoWalkSpeed, to.magnitude / Mathf.Max(Time.deltaTime, 0.001f));   // (no overshoot)
            horizontalVelocity = to.sqrMagnitude > 0.0001f ? to.normalized * speed : Vector3.zero;
            return;
        }

        if (isGrounded)
        {
            thrown = false;
            airSpeed = GameInput.Run ? runSpeed : walkSpeed;
            horizontalVelocity = input * airSpeed;
        }
        else
        {
            // Air: keeps the take-off speed, steering is gradual. No keys: the momentum goes on until landing
            // (not after being thrown: that one brakes as before). Shift late, in the air: run speed too
            if (runInAir && GameInput.Run && !InputLocked) airSpeed = Mathf.Max(airSpeed, runSpeed);
            bool coasting = keepAirMomentum && !thrown && input.sqrMagnitude < 0.01f;
            Vector3 target = coasting ? horizontalVelocity : input * airSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, airAcceleration * Time.deltaTime);
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
                if (SlamOn) Land();   // back in play once you got up
                else FinishRespawn();
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

    //---- Thrown

    // Something throws the player (a bridge bursting out of the ground): off the ground with this velocity,
    // without steering for a moment so the throw is felt
    public void Knockback(Vector3 velocity)
    {
        horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        verticalVelocity = Mathf.Max(velocity.y, 0.5f);
        airSpeed = Mathf.Max(walkSpeed, horizontalVelocity.magnitude * 0.5f);
        knockbackUntil = Time.time + 0.35f;
        thrown = true;   // (no free momentum in the air after this)
        movingGround = null;
        lastGroundedTime = -99f;   // no coyote jump out of it
        wasGrounded = false;
    }

    //---- Portal (PortalWindow)

    // What is right under the player (PlayerGrab's recall: which island or bridge you are on)
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

    // Comes out there, falling at Speed: it lands as from a respawn (the slam), and that point is its respawn now
    public void FallFrom(Vector3 position, Quaternion rotation, float speed, Transform respawn, Vector3 carry = default)
    {
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        characterController.enabled = true;
        Scripted = false;
        horizontalVelocity = new Vector3(carry.x, 0f, carry.z);   // (a portal: the momentum you went in with)
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
