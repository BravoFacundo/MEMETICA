//First Person Camera: looking around, zoom, and everything the player does by looking at things.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.
//  Look & Zoom   mouse and right click
//  Hold          grab, carry and drop memes (before: PlayerHoldObjects)
//  Paintings     tells the painting you look at that you are watching it (it plays and fades itself)
//  Shake         camera shake when islands move (before: CameraShake)
//  Smoothing     the camera absorbs small pops of the body in height (PlayerMove tells it what the ground carried)
//  Pose          someone else bends the head for a moment (RespawnSlam: face to the ground and back up)
//Goes on the Main Camera.

using System.Collections;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    private Transform playerTransform;
    
    [Section("Look")]
    public bool playerCanLook = true;   // the game starts looking (tutorial popups no longer block it)
    [Tooltip("Base sensitivity: the value at the middle of the settings slider (the slider scales it x0.25 to x4)")]
    public float mouseSensitivity = 3f;
    private float sensitivityMultiplier = 1f;   // from the settings window
    private float sensitivityReduction = 1;
    private float xRotation = 0f;

    [Section("Zoom")]
    [SerializeField] private float zoomingSpeed = 5;
    [SerializeField] private int defaultFOV = 90;
    [SerializeField] private int zoomFOV = 20;

    [Section("Hold Memes")]
    public bool canHold = true;
    public float pickUpRange = 2.5f;
    public float moveForce = 250;
    [Tooltip("The held meme never gets closer to the camera than this (meters), so it doesn't hit your face or cross the near clip plane")]
    public float minHoldDistance = 0.6f;
    [Tooltip("Letting go: it first goes to the Drop Pivot (child of the camera, in front of you) and is let go there. 0 = let go where it is")]
    public float dropTime = 0.12f;
    private Transform holdParent;   // GrabPivot: where it is carried
    private Transform dropParent;   // DropPivot: where it is let go
    private Coroutine dropping;
    private bool carriedInHud;      // a meme window: carried on the screen, as its HUD version (MemeDock)
    private Rigidbody heldBody;
    private Collider[] heldColliders;
    private Collider playerCollider;

    [Section("Paintings")]
    [SerializeField] private float paintingLookRange = 10f;
    private Painting currentPainting;

    private Camera playerCamera;

    [Section("Camera")]
    [Tooltip("Seconds the camera takes to catch up when the body pops in height (bumps riding rising islands, steps, rough ground). 0 = off. What the ground carries is never smoothed")]
    public float cameraSmoothTime = 0.08f;
    [Tooltip("Max meters the camera can lag behind the body")]
    public float cameraMaxLag = 0.25f;
    private float cameraLag;   // meters the camera is behind the body (vertical)
    private float lastBodyHeight;
    [Tooltip("How fast the shake wobbles. Lower = slow rumble, higher = buzz")]
    public float shakeFrequency = 18f;

    [Section("Debug", Closed = true)]
    [ReadOnlyField, SerializeField] private bool isZooming = false;
    [ReadOnlyField] public GameObject heldObject;

    private Vector3 cameraRestPosition;
    private Vector3 shakeOffset;
    private float poseDown, poseRoll, poseDip;   // SetPose: toward the ground (0-1), tilt (degrees), lower (meters)

    // On: the mouse doesn't turn the camera (an animation is using it, e.g. RespawnSlam)
    public bool LookLocked { get; set; }
    // On: the mouse moves the cursor instead of the camera (RetroCursor free, Ctrl held); clicks don't grab
    public bool CursorFree { get; set; }
    // Where the held meme is pulled to (MemeDock flies it back there)
    public Transform HoldPoint => holdParent;

    // Read by RetroCursor to show the zoom and hand icons
    public bool IsZooming => isZooming;
    public float DefaultFOV => defaultFOV;
    public float ZoomFOV => zoomFOV;
    public bool LookingAtMeme { get; private set; }
    // Clicking now would pick up what you look at: hands free, or an optional meme (DexCollectible: never in the hand)
    public bool CanGrabLooked { get; private set; }
    // Carrying a meme window on the screen (Game): the middle of the screen is free, the aim stays
    public bool CarriedInHud => carriedInHud;

    void Start()
    {
        playerTransform = transform.parent.parent.GetComponent<Transform>();
        playerCamera = GetComponent<Camera>();
        cameraRestPosition = transform.localPosition;
        lastBodyHeight = playerTransform.position.y;
        holdParent = GameObject.Find("GrabPivot").transform;
        GameObject dropPivot = GameObject.Find("DropPivot");
        if (dropPivot != null) dropParent = dropPivot.transform;
        playerCollider = playerTransform.GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;

        // Sensitivity comes from the saved settings and follows the settings window
        ApplySettings();
        GameSettings.Changed += ApplySettings;
    }

    void OnDestroy() => GameSettings.Changed -= ApplySettings;

    // Physics forces go in FixedUpdate: same pull at any frame rate (before: Update, stronger at high FPS)
    void FixedUpdate()
    {
        if (heldObject == null || dropping != null || carriedInHud || PauseMenu.GameIsPaused || !canHold) return;
        PullHeldObject();
        KeepHeldObjectOffFace();
    }
    void ApplySettings() => sensitivityMultiplier = GameSettings.MouseSensitivityMultiplier;
    
    void Update()
    {        
        if (playerCanLook && !LookLocked && !CursorFree)
        {
            MovePlayerView();
            ZoomOnPlayerView();
        }
        else if (LookLocked) ApplyRotation();
        UpdateHeldMeme();
        UpdateLookedPainting();
    }

    //---- Look & Zoom

    //In the project, the Player object moves according to its forward vector.
    //This function not only rotates the camera on the Y axis but also rotates the player's body on the X axis to not hinder their movement.
    private void MovePlayerView()
    {
        float sensitivity = mouseSensitivity * sensitivityMultiplier / sensitivityReduction;
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        ApplyRotation();
        playerTransform.Rotate(Vector3.up * mouseX);
    }

    // Where you look, bent by the pose (if any)
    void ApplyRotation() => transform.localRotation = Quaternion.Euler(Mathf.LerpUnclamped(xRotation, 85f, poseDown), 0f, poseRoll);

    private void ZoomOnPlayerView()
    {
        if (Input.GetMouseButtonDown(1))
            isZooming = true;
        else if (Input.GetMouseButtonUp(1))
            isZooming = false;

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, isZooming ? zoomFOV : defaultFOV, Time.deltaTime * zoomingSpeed);
        sensitivityReduction = isZooming ? 10 : 1;
    }

    //---- Hold memes

    private void UpdateHeldMeme()
    {
        if (PauseMenu.GameIsPaused || !canHold) return;   // no grabbing while paused (clicking the pause window) or riding an elevator
        if (CursorFree) { LookingAtMeme = CanGrabLooked = false; return; }   // the click is the cursor's (RetroCursor)

        // Ignora triggers: si no, la zona del pedestal (que envuelve al meme) tapa el rayo
        LookingAtMeme = Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, pickUpRange, ~PhysicsLayers.PropsMask, QueryTriggerInteraction.Ignore)
                        && hit.transform.CompareTag("Meme");
        DexCollectible dex = LookingAtMeme ? hit.transform.GetComponentInParent<DexCollectible>() : null;
        CanGrabLooked = LookingAtMeme && (heldObject == null || dex != null);

        if (Input.GetMouseButtonDown(0))
        {
            if (dex != null) dex.Collect();   // an optional meme: to its own HUD place, your hand stays as it is
            else if (heldObject == null) { if (LookingAtMeme) PickUpObject(hit.transform.gameObject); }
            else if (dropping == null) DropObject();
        }
    }

    // Same pull as always: a force toward the grab point, so it still feels physical
    private void PullHeldObject()
    {
        if (Vector3.Distance(heldObject.transform.position, holdParent.position) > 0.1f)
        {
            Vector3 moveDirection = holdParent.position - heldObject.transform.position;
            heldBody.AddForce(moveDirection * moveForce);
        }
    }

    // If it lags behind (walking backwards, turning fast) it is pushed out to the minimum distance,
    // and only its speed toward the camera is cancelled: the rest of the physics stays
    private void KeepHeldObjectOffFace()
    {
        Vector3 forward = transform.forward;
        float distance = Vector3.Dot(heldBody.position - transform.position, forward);
        if (distance >= minHoldDistance) return;

        heldBody.position += forward * (minHoldDistance - distance);
        float towardCamera = Vector3.Dot(heldBody.velocity, forward);
        if (towardCamera < 0f) heldBody.velocity -= forward * towardCamera;
    }

    // While held it passes through the player's body (before: it bumped into the capsule and got pushed to the face)
    private void IgnorePlayerCollision(bool ignore)
    {
        if (playerCollider == null || heldColliders == null) return;
        foreach (Collider c in heldColliders)
            if (c != null && !c.isTrigger) Physics.IgnoreCollision(c, playerCollider, ignore);
    }

    private void PickUpObject(GameObject pickObject, float hudSeconds = 0f)
    {
        if (!pickObject.TryGetComponent(out Rigidbody objectRb)) return;

        heldObject = pickObject;
        heldBody = objectRb;
        heldColliders = pickObject.GetComponentsInChildren<Collider>();
        objectRb.constraints = RigidbodyConstraints.None;

        // A meme window: on the screen, exactly as its HUD version (MemeDock). The rest: pulled to the GrabPivot
        carriedInHud = MemeDock.Instance != null && MemeDock.Instance.CanCarry(pickObject);
        if (carriedInHud) MemeDock.Instance.Carry(pickObject.GetComponent<Meme>(), hudSeconds);
        else
        {
            objectRb.isKinematic = false;
            objectRb.useGravity = false;
            objectRb.freezeRotation = true;
            objectRb.drag = 10;
            objectRb.transform.parent = holdParent;
            IgnorePlayerCollision(true);
        }

        if (heldObject.TryGetComponent(out Meme meme)) meme.PickUp();
    }

    // Out of the hand without letting it go anywhere (it is gone: DexCollectible)
    public void ForgetHeld(GameObject meme)
    {
        if (heldObject != meme) return;
        heldObject = null;
        heldBody = null;
        carriedInHud = false;
    }

    // Into the hand from anywhere (MemeRecall): as if picked up; a meme window flies to the HUD in Hud Seconds
    public void Grab(GameObject meme, float hudSeconds = 0f)
    {
        if (heldObject == null) PickUpObject(meme, hudSeconds);
    }

    // Let go: from the Drop Pivot if there is one (it goes there first, from the corner), else where it is
    public void DropObject()
    {
        if (dropping != null) return;
        Vector3 size = heldObject.transform.lossyScale;
        if (carriedInHud) { size = MemeDock.Instance.Uncarry(heldObject.GetComponent<Meme>(), dropTime); carriedInHud = false; }   // from the screen to the world
        if (dropParent == null || dropTime <= 0f) { heldObject.transform.localScale = size; LetGo(); return; }
        dropping = StartCoroutine(DropFromPivot(size));
    }

    // To the Drop Pivot (growing back to its size in the world, if it came from the screen), then let go
    IEnumerator DropFromPivot(Vector3 size)
    {
        heldBody.isKinematic = true;
        heldObject.transform.parent = null;
        Vector3 from = heldObject.transform.position;
        Vector3 fromSize = heldObject.transform.localScale;
        for (float t = 0f; t < dropTime && heldObject != null; t += Time.deltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / dropTime);
            heldObject.transform.position = Vector3.Lerp(from, dropParent.position, p);
            heldObject.transform.localScale = Vector3.Lerp(fromSize, size, p);
            yield return null;
        }
        if (heldObject != null) heldObject.transform.localScale = size;
        dropping = null;
        if (heldObject == null) yield break;   // snapped onto a pedestal meanwhile
        heldObject.transform.position = dropParent.position;
        heldBody.isKinematic = false;
        heldBody.velocity = Vector3.zero;
        LetGo();
    }

    void LetGo()
    {
        Rigidbody heldRig = heldObject.GetComponent<Rigidbody>();
        heldRig.useGravity = true;
        heldRig.freezeRotation = false;
        heldRig.drag = 1;

        if (heldObject.TryGetComponent(out Meme meme)) meme.Drop();

        IgnorePlayerCollision(false);
        heldObject.transform.parent = null;
        heldObject = null;
        heldBody = null;
    }

    //To avoid bugs or cheating, when the meme gets close enough to the pedestal it relocates until it is grabbed again.
    //The pedestal tells the Meme itself (Meme.PlaceOnPedestal); here only the physics and the hand are released.
    // Returns its size in the world. From the screen (a meme window in the HUD) it comes out where the HUD one is, as
    // big as it showed: the pedestal flies it to its socket in Snap Time, growing back to that size
    public Vector3 SnapObject(GameObject memeObject, float snapTime = 0f)
    {
        Vector3 size = memeObject.transform.lossyScale;
        if (carriedInHud && heldObject == memeObject)
        {
            size = MemeDock.Instance.Uncarry(memeObject.GetComponent<Meme>(), snapTime);
            carriedInHud = false;
        }
        Rigidbody snapRig = memeObject.GetComponent<Rigidbody>();
        snapRig.useGravity = true;
        snapRig.drag = 1;
        snapRig.constraints = RigidbodyConstraints.FreezeAll;

        if (heldObject != null && heldObject == memeObject)
        {
            IgnorePlayerCollision(false);
            if (heldObject.transform.parent != null) heldObject.transform.parent = null;
            heldObject = null;
            heldBody = null;
        }
        return size;
    }

    //---- Paintings

    private void UpdateLookedPainting()
    {
        if (PauseMenu.GameIsPaused) return;   // the painting pauses itself
        Painting looked = GetLookedPainting();
        if (looked == currentPainting) return;

        if (currentPainting != null) currentPainting.StopWatching();   // its music fades out
        currentPainting = looked;
        if (currentPainting != null) currentPainting.Watch();
    }

    // The painting the camera points at: its canvas or its image (not the easel or the chair)
    private Painting GetLookedPainting()
    {
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, paintingLookRange, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return null;
        Painting painting = hit.transform.GetComponentInParent<Painting>();
        return painting != null && painting.Shows(hit.transform) ? painting : null;
    }

    //---- Shake

    //Simulates in the camera the shaking of a large moving object (the islands), as feedback of something the player can't see yet.
    // Smooth rumble (noise, not a random jump every frame) that comes in fast and dies out.
    // Always around the camera's rest place, so two shakes at once never leave it displaced
    public IEnumerator ShakeTheCamera(float duration, float magnitude)
    {
        float seed = Random.value * 100f;
        duration = Mathf.Max(duration, 0.01f);

        for (float elapsed = 0f; elapsed < duration; )
        {
            if (!PauseMenu.GameIsPaused)   // paused: still camera until the game resumes
            {
                elapsed += Time.deltaTime;
                float strength = magnitude * Mathf.Clamp01(elapsed / 0.1f) * Mathf.Clamp01((duration - elapsed) / (duration * 0.4f));
                float x = Mathf.PerlinNoise(seed, elapsed * shakeFrequency) * 2f - 1f;
                float y = Mathf.PerlinNoise(seed + 37f, elapsed * shakeFrequency) * 2f - 1f;
                shakeOffset = new Vector3(x, y, 0f) * strength;
            }
            else shakeOffset = Vector3.zero;
            ApplyCameraOffset();
            yield return null;
        }

        shakeOffset = Vector3.zero;
        ApplyCameraOffset();
    }

    //---- Smoothing

    // Called by PlayerMove every frame, after it followed the ground. The body can pop up a few centimeters in one frame;
    // the camera absorbs that and catches up. What the ground carried (rising, floating) moves it exactly, so it never lags behind
    public void FollowBody(float carriedHeight, bool onGround)
    {
        float ownMove = playerTransform.position.y - lastBodyHeight - carriedHeight;
        lastBodyHeight = playerTransform.position.y;

        if (cameraSmoothTime <= 0f) cameraLag = 0f;
        else
        {
            if (onGround) cameraLag = Mathf.Clamp(cameraLag - ownMove, -cameraMaxLag, cameraMaxLag);   // jumps and falls: exact
            cameraLag *= Mathf.Exp(-Time.deltaTime / (onGround ? cameraSmoothTime : cameraSmoothTime * 0.3f));
        }
        ApplyCameraOffset();
    }

    // After a teleport or a respawn: no catching up from the old place
    public void ResetSmoothing()
    {
        lastBodyHeight = playerTransform.position.y;
        cameraLag = 0f;
        ApplyCameraOffset();
    }

    void ApplyCameraOffset() => transform.localPosition = cameraRestPosition + Vector3.up * (cameraLag - poseDip) + shakeOffset;

    //---- Pose

    // Looking straight ahead (a respawn facing its course)
    public void LookLevel()
    {
        xRotation = 0f;
        ApplyRotation();
    }

    // Bends the head: down 0 = where you look, 1 = at the ground at your feet (past 0 the other way, a bit up);
    // roll tilts it (degrees), dip lowers it (meters). All 0: back to normal
    public void SetPose(float down, float roll, float dip)
    {
        poseDown = down; poseRoll = roll; poseDip = dip;
        ApplyRotation();
        ApplyCameraOffset();
    }
}
