//First Person Camera: looking around, zoom, and what the camera does on its own.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.
//  Look & Zoom   mouse and right click
//  Focus         someone wants you to look at something (the raffle, your way rising): you can still look around it,
//                within Focus Radius. Near its edge the mouse turns you less and less (a soft wall), and outside it
//                (the focus moved, or a new one) the view eases back, speeding up and slowing down, never in jumps.
//                Optionally a small zoom meanwhile (its FOV). No Cinemachine: it only limits the angles
//  Paintings     tells the painting you look at that you are watching it (it plays and fades itself)
//  Shake         camera shake when islands move (before: CameraShake)
//  Smoothing     the camera absorbs small pops of the body in height (PlayerMove tells it what the ground carried)
//  Pose          someone else bends the head for a moment (RespawnSlam: face to the ground and back up)
//Goes on the Player (root), with PlayerMove and PlayerGrab. The Main Camera is its child (CameraHolder > Main Camera).

using System;
using System.Collections;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    // The player's (found once: there is one per game scene, none in the Menu)
    private static PlayerLook instance;
    public static PlayerLook Instance => instance != null ? instance : (instance = FindObjectOfType<PlayerLook>());

    [Section("Look")]
    public bool playerCanLook = true;   // the game starts looking (tutorial popups no longer block it)
    [Tooltip("Base sensitivity: the value at the middle of the settings slider (the slider scales it x0.25 to x4)")]
    public float mouseSensitivity = 3f;
    [Tooltip("Gamepad: degrees per second the right stick turns you, all the way (the settings slider scales it too)")]
    public float padLookSpeed = 160f;
    [Tooltip("The player's camera. Empty: the Camera among its children (Main Camera)")]
    [SerializeField] private Camera playerCamera;
    private float sensitivityMultiplier = 1f;   // from the settings window
    private float sensitivityReduction = 1;
    private float xRotation = 0f;

    [Section("Zoom")]
    [SerializeField] private float zoomingSpeed = 5;
    [SerializeField] private int defaultFOV = 90;
    [SerializeField] private int zoomFOV = 20;

    [Section("Focus")]
    [Tooltip("Degrees you can look away from a focus, sideways (up and down: Focus Vertical of it)")]
    public float focusRadius = 25f;
    [Range(0f, 1f)] public float focusVertical = 0.6f;
    [Tooltip("Seconds the view takes to ease back inside the radius (it speeds up, then slows down)")]
    public float focusPull = 0.6f;
    [Tooltip("Part of the radius (0-1) from where the mouse starts turning you less (at the edge: nothing)")]
    [Range(0f, 1f)] public float focusSoftEdge = 0.6f;
    [Tooltip("Seconds the zoom of a focus takes (Focus with a FOV)")]
    public float focusZoomTime = 0.8f;
    private Func<Vector3> focus;
    private float focusRadiusNow, focusFov, yawSpeed, pitchSpeed;
    private bool easingFocusZoom;

    [Section("Paintings")]
    [SerializeField] private float paintingLookRange = 10f;
    private Painting currentPainting;

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

    private Vector3 cameraRestPosition;
    private Vector3 shakeOffset;
    private float poseDown, poseRoll, poseDip;   // SetPose: toward the ground (0-1), tilt (degrees), lower (meters)

    // The camera, and the head that turns up and down (its transform)
    public Camera Camera => playerCamera;
    public Transform Head => playerCamera.transform;

    // On: the mouse doesn't turn the camera (an animation is using it, e.g. RespawnSlam)
    public bool LookLocked { get; set; }
    // On: the mouse moves the cursor instead of the camera (RetroCursor free, Ctrl held); clicks don't grab
    public bool CursorFree { get; set; }

    // Read by RetroCursor to show the zoom icons (a focus' zoom is the normal view meanwhile)
    public bool IsZooming => isZooming;
    public float DefaultFOV => focus != null && focusFov > 0f ? focusFov : defaultFOV;
    public float ZoomFOV => zoomFOV;

    void Awake()
    {
        instance = this;
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
    }

    void Start()
    {
        cameraRestPosition = Head.localPosition;
        lastBodyHeight = transform.position.y;
        Cursor.lockState = CursorLockMode.Locked;

        // Sensitivity comes from the saved settings and follows the settings window
        ApplySettings();
        SettingsManager.Changed += ApplySettings;
    }

    void OnDestroy() => SettingsManager.Changed -= ApplySettings;
    void ApplySettings() => sensitivityMultiplier = SettingsManager.MouseSensitivityMultiplier;

    void Update()
    {
        if (playerCanLook && !LookLocked && !CursorFree)
        {
            MovePlayerView();
            ZoomOnPlayerView();
        }
        else
        {
            if (LookLocked) ApplyRotation();
            if (!isZooming) EaseFov();   // (a focus' zoom also while the head is held)
        }
        UpdateLookedPainting();
    }

    // After everything moved you (the platform turning, the ground carrying): the focus pulls from where you ended up
    void LateUpdate() => ApplyFocus();

    //---- Look & Zoom

    //In the project, the Player object moves according to its forward vector.
    //This function not only rotates the camera on the X axis but also rotates the player's body on the Y axis to not hinder their movement.
    private void MovePlayerView()
    {
        float sensitivity = mouseSensitivity * sensitivityMultiplier / sensitivityReduction;
        Vector2 stick = GameInput.LookStick * (padLookSpeed * sensitivityMultiplier / sensitivityReduction * Time.deltaTime);
        float mouseX = GameInput.Mouse.x * sensitivity + stick.x;
        float mouseY = GameInput.Mouse.y * sensitivity + stick.y;
        if (focus != null) SoftWall(ref mouseX, ref mouseY);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        ApplyRotation();
        transform.Rotate(Vector3.up * mouseX);
    }

    // Where you look, bent by the pose (if any)
    void ApplyRotation() => Head.localRotation = Quaternion.Euler(Mathf.LerpUnclamped(xRotation, 85f, poseDown), 0f, poseRoll);

    private void ZoomOnPlayerView()
    {
        isZooming = GameInput.ZoomHeld;   // right click or a left trigger, held

        if (isZooming) playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, zoomFOV, Time.deltaTime * zoomingSpeed);
        else EaseFov();
        sensitivityReduction = isZooming ? 10 : 1;
    }

    // Back to the normal view (or a focus' zoom)
    void EaseFov()
    {
        bool focusZoom = focus != null && focusFov > 0f;
        if (focusZoom) easingFocusZoom = true;
        float target = focusZoom ? focusFov : defaultFOV;
        float rate = easingFocusZoom ? 3f / Mathf.Max(focusZoomTime, 0.01f) : zoomingSpeed;   // (a focus' zoom, in and out: slow)
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, Time.deltaTime * rate);
        if (!focusZoom && Mathf.Abs(playerCamera.fieldOfView - defaultFOV) < 0.1f) easingFocusZoom = false;
    }

    //---- Focus

    // Look around this, never far from it (radius in degrees; -1: Focus Radius). It can move (a rising island).
    // FOV above 0: a small zoom meanwhile (eased in Focus Zoom Time); 0: as you are
    public void Focus(Func<Vector3> target, float radius = -1f, float fov = 0f)
    {
        focus = target;
        focusRadiusNow = radius >= 0f ? radius : focusRadius;
        focusFov = fov;
    }
    public void Focus(Transform target, float radius = -1f, float fov = 0f) { if (target != null) Focus(() => target.position, radius, fov); }
    public void Focus(Vector3 point, float radius = -1f, float fov = 0f) => Focus(() => point, radius, fov);

    // Free again (a focus' zoom eases back to the normal view)
    public void ReleaseFocus() { focus = null; yawSpeed = pitchSpeed = 0f; }
    public bool Focused => focus != null;

    // How far you look from the focus now (degrees: sideways, and down/up as xRotation), and where it is
    bool FocusOffsets(out float yawOff, out float pitchOff, out float targetPitch)
    {
        yawOff = pitchOff = targetPitch = 0f;
        Vector3 to = focus() - Head.position;
        if (to.sqrMagnitude < 0.01f) return false;
        float flat = new Vector2(to.x, to.z).magnitude;
        float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        targetPitch = Mathf.Atan2(-to.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg;   // (down: positive, as xRotation)
        yawOff = Mathf.DeltaAngle(targetYaw, transform.eulerAngles.y);
        pitchOff = xRotation - targetPitch;
        return true;
    }

    // The mouse turns you less and less toward the edge, nothing at it (turning back in: always full)
    void SoftWall(ref float mouseX, ref float mouseY)
    {
        if (!FocusOffsets(out float yawOff, out float pitchOff, out _)) return;
        float wideYaw = focusRadiusNow, widePitch = focusRadiusNow * focusVertical;
        if (mouseX * yawOff > 0f) mouseX *= Resistance(Mathf.Abs(yawOff), wideYaw);
        if (-mouseY * pitchOff > 0f) mouseY *= Resistance(Mathf.Abs(pitchOff), widePitch);   // (mouse up: xRotation goes down)
    }

    float Resistance(float off, float wide)
    {
        float soft = wide * focusSoftEdge;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(soft, Mathf.Max(wide, soft + 0.01f), off));
    }

    // Outside the radius (the focus moved, or a new one): eased back to its edge, speeding up and slowing down
    void ApplyFocus()
    {
        if (focus == null || LookLocked || Time.deltaTime <= 0f) { yawSpeed = pitchSpeed = 0f; return; }   // (an animation has the head; the pause: still)
        if (!FocusOffsets(out float yawOff, out float pitchOff, out float targetPitch)) return;

        float wideYaw = focusRadiusNow, widePitch = focusRadiusNow * focusVertical;
        float edgeYaw = Mathf.Clamp(yawOff, -wideYaw, wideYaw), edgePitch = Mathf.Clamp(pitchOff, -widePitch, widePitch);
        float newYawOff = edgeYaw != yawOff ? Mathf.SmoothDamp(yawOff, edgeYaw, ref yawSpeed, focusPull) : yawOff;
        float newPitchOff = edgePitch != pitchOff ? Mathf.SmoothDamp(pitchOff, edgePitch, ref pitchSpeed, focusPull) : pitchOff;
        if (edgeYaw == yawOff) yawSpeed = 0f;
        if (edgePitch == pitchOff) pitchSpeed = 0f;

        transform.Rotate(Vector3.up * (newYawOff - yawOff));
        xRotation = Mathf.Clamp(targetPitch + newPitchOff, -90f, 90f);
        ApplyRotation();
    }

    //---- Paintings

    private void UpdateLookedPainting()
    {
        if (UIManager.GameIsPaused) return;   // the painting pauses itself
        Painting looked = GetLookedPainting();
        if (looked == currentPainting) return;

        if (currentPainting != null) currentPainting.StopWatching();   // its music fades out
        currentPainting = looked;
        if (currentPainting != null) currentPainting.Watch();
    }

    // The painting the camera points at: its canvas or its image (not the easel or the chair)
    private Painting GetLookedPainting()
    {
        if (!Physics.Raycast(Head.position, Head.forward, out RaycastHit hit, paintingLookRange, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return null;
        Painting painting = hit.transform.GetComponentInParent<Painting>();
        return painting != null && painting.Shows(hit.transform) ? painting : null;
    }

    //---- Shake

    // The player's camera shakes (islands, bridges, stones…): nothing if there is no player or no amount
    public static void Shake(float duration, float magnitude)
    {
        if (magnitude <= 0f || Instance == null) return;
        Instance.StartCoroutine(Instance.ShakeTheCamera(duration, magnitude));
    }

    //Simulates in the camera the shaking of a large moving object (the islands), as feedback of something the player can't see yet.
    // Smooth rumble (noise, not a random jump every frame) that comes in fast and dies out.
    // Always around the camera's rest place, so two shakes at once never leave it displaced
    public IEnumerator ShakeTheCamera(float duration, float magnitude)
    {
        float seed = UnityEngine.Random.value * 100f;
        duration = Mathf.Max(duration, 0.01f);

        for (float elapsed = 0f; elapsed < duration; )
        {
            if (!UIManager.GameIsPaused)   // paused: still camera until the game resumes
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
        float ownMove = transform.position.y - lastBodyHeight - carriedHeight;
        lastBodyHeight = transform.position.y;

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
        lastBodyHeight = transform.position.y;
        cameraLag = 0f;
        ApplyCameraOffset();
    }

    void ApplyCameraOffset() => Head.localPosition = cameraRestPosition + Vector3.up * (cameraLag - poseDip) + shakeOffset;

    //---- Pose

    // Degrees the head looks down (up: negative), as the mouse leaves it. Set: turned there (a scripted look)
    public float Pitch
    {
        get => xRotation;
        set { xRotation = Mathf.Clamp(value, -90f, 90f); ApplyRotation(); }
    }

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
