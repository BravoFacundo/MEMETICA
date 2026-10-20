//First Person Camera: looking around, zoom, and everything the player does by looking at things.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.
//  Look & Zoom   mouse and right click
//  Hold          grab, carry and drop memes (before: PlayerHoldObjects)
//  Paintings     play a painting's video while looking at it (before: PlayerLookAtVideo)
//  Shake         camera shake when islands move (before: CameraShake)
//  Smoothing     the camera absorbs small pops of the body in height (PlayerMove tells it what the ground carried)
//Goes on the Main Camera.

using System.Collections;
using UnityEngine;
using UnityEngine.Video;

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
    private Transform holdParent;
    private Rigidbody heldBody;
    private Collider[] heldColliders;
    private Collider playerCollider;

    [Section("Paintings")]
    [SerializeField] private float paintingLookRange = 10f;
    private Transform currentPainting;
    private VideoPlayer paintingVideo;
    private AudioSource paintingAudio;
    private bool wasPaused;

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
    [ReadOnlyField, SerializeField] private bool clueSolved;   // the painting's pedestal is solved

    private Vector3 cameraRestPosition;
    private Vector3 shakeOffset;

    // Read by RetroCursor to show the zoom and hand icons
    public bool IsZooming => isZooming;
    public float DefaultFOV => defaultFOV;
    public float ZoomFOV => zoomFOV;
    public bool LookingAtMeme { get; private set; }

    void Start()
    {
        playerTransform = transform.parent.parent.GetComponent<Transform>();
        playerCamera = GetComponent<Camera>();
        cameraRestPosition = transform.localPosition;
        lastBodyHeight = playerTransform.position.y;
        holdParent = GameObject.Find("GrabPivot").transform;
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
        if (heldObject == null || PauseMenu.GameIsPaused || !canHold) return;
        PullHeldObject();
        KeepHeldObjectOffFace();
    }
    void ApplySettings() => sensitivityMultiplier = GameSettings.MouseSensitivityMultiplier;
    
    void Update()
    {        
        if (playerCanLook)
        {
            MovePlayerView();
            ZoomOnPlayerView();
        }
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

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerTransform.Rotate(Vector3.up * mouseX);
    }

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

        // Ignora triggers: si no, la zona del pedestal (que envuelve al meme) tapa el rayo
        LookingAtMeme = Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, pickUpRange, ~0, QueryTriggerInteraction.Ignore)
                        && hit.transform.CompareTag("Meme");

        if (Input.GetMouseButtonDown(0))
        {
            if (heldObject == null) { if (LookingAtMeme) PickUpObject(hit.transform.gameObject); }
            else DropObject();
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

    private void PickUpObject(GameObject pickObject)
    {
        if (!pickObject.TryGetComponent(out Rigidbody objectRb)) return;

        objectRb.constraints = RigidbodyConstraints.None;
        objectRb.isKinematic = false;
        objectRb.useGravity = false;
        objectRb.freezeRotation = true;
        objectRb.drag = 10;

        objectRb.transform.parent = holdParent;
        heldObject = pickObject;
        heldBody = objectRb;
        heldColliders = pickObject.GetComponentsInChildren<Collider>();
        IgnorePlayerCollision(true);

        if (heldObject.TryGetComponent(out Meme meme)) meme.PickUp();
    }

    public void DropObject()
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
    public void SnapObject(GameObject memeObject)
    {
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
    }

    //---- Paintings

    private void UpdateLookedPainting()
    {
        if (PauseMenu.GameIsPaused)
        {
            if (!wasPaused && paintingVideo != null) paintingVideo.Pause();
            if (!wasPaused && paintingAudio != null) paintingAudio.Pause();
            wasPaused = true;
            return;
        }
        if (wasPaused)
        {
            wasPaused = false;
            if (paintingVideo != null) paintingVideo.Play();
            if (paintingAudio != null) paintingAudio.Play();
        }

        Transform lookedPainting = GetLookedPainting();
        if (lookedPainting == currentPainting) return;

        StopPainting();
        if (lookedPainting != null) PlayPainting(lookedPainting);
    }

    private Transform GetLookedPainting()
    {
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, paintingLookRange)) return null;
        if (!hit.transform.CompareTag("Pintura") || hit.transform.GetComponentInChildren<VideoPlayer>() == null) return null;
        return hit.transform;
    }

    private void PlayPainting(Transform painting)
    {
        currentPainting = painting;
        paintingVideo = painting.GetComponentInChildren<VideoPlayer>();
        paintingAudio = painting.GetComponentInChildren<AudioSource>();
        Pedestal pedestal = Pedestal.WithClue(painting);
        clueSolved = pedestal != null && pedestal.IsCorrect;

        paintingVideo.Play();
        if (paintingAudio != null) paintingAudio.Play();
    }

    // A solved painting keeps where it was; an unsolved one starts over next time
    private void StopPainting()
    {
        if (paintingVideo != null)
        {
            if (!clueSolved) paintingVideo.frame = 0;
            paintingVideo.Pause();
        }
        if (paintingAudio != null)
        {
            if (!clueSolved) paintingAudio.time = 0;
            paintingAudio.Pause();
        }
        currentPainting = null;
        paintingVideo = null;
        paintingAudio = null;
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

    void ApplyCameraOffset() => transform.localPosition = cameraRestPosition + Vector3.up * cameraLag + shakeOffset;
}
