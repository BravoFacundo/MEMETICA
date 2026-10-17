//First Person Camera: looking around, zoom, and everything the player does by looking at things.
//Being able to see far away is necessary for players to solve the puzzles, it allows them to plan their moves.
//  Look & Zoom   mouse and right click
//  Hold          grab, carry and drop memes (before: PlayerHoldObjects)
//  Paintings     play a painting's video while looking at it (before: PlayerLookAtVideo)
//  Shake         camera shake when islands move (before: CameraShake)
//Goes on the Main Camera.

using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class PlayerLook : MonoBehaviour
{
    private Transform playerTransform;
    
    [Header("Player Look")]
    public bool playerCanLook = true;   // the game starts looking (tutorial popups no longer block it)
    [Tooltip("Base sensitivity: the value at the middle of the settings slider (the slider scales it x0.25 to x4)")]
    public float mouseSensitivity = 3f;
    private float sensitivityMultiplier = 1f;   // from the settings window
    private float sensitivityReduction = 1;
    private float xRotation = 0f;
    
    [Header("Player Zoom")]
    [SerializeField] private bool isZooming = false;
    [SerializeField] private float zoomingSpeed = 5;
    [SerializeField] private int defaultFOV = 90;
    [SerializeField] private int zoomFOV = 20;

    [Header("Hold Memes")]
    public bool canHold = true;
    public float pickUpRange = 2.5f;
    public float moveForce = 250;
    public GameObject heldObject;
    public bool FueAgarrada = false;   // stays on after snapping to a pedestal (PlayerMove keeps tracking bridges and islands)
    private Transform holdParent;

    [Header("Paintings")]
    [SerializeField] private float paintingLookRange = 10f;
    [SerializeField] private bool clueSolved;   // debug: the painting's pedestal is solved
    private Transform currentPainting;
    private VideoPlayer paintingVideo;
    private AudioSource paintingAudio;
    private bool wasPaused;

    private Camera playerCamera;

    // Read by RetroCursor to show the zoom and hand icons
    public bool IsZooming => isZooming;
    public float DefaultFOV => defaultFOV;
    public float ZoomFOV => zoomFOV;
    public bool LookingAtMeme { get; private set; }

    void Start()
    {
        playerTransform = transform.parent.parent.GetComponent<Transform>();
        playerCamera = GetComponent<Camera>();
        holdParent = GameObject.Find("GrabPivot").transform;
        Cursor.lockState = CursorLockMode.Locked;

        // Sensitivity comes from the saved settings and follows the settings window
        ApplySettings();
        GameSettings.Changed += ApplySettings;
    }

    void OnDestroy() => GameSettings.Changed -= ApplySettings;
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

        if (heldObject != null) MoveHeldObject();
    }

    private void MoveHeldObject()
    {
        if (Vector3.Distance(heldObject.transform.position, holdParent.position) > 0.1f)
        {
            Vector3 moveDirection = holdParent.position - heldObject.transform.position;
            heldObject.GetComponent<Rigidbody>().AddForce(moveDirection * moveForce);
        }
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
        FueAgarrada = true;

        if (heldObject.TryGetComponent(out Meme meme)) meme.PickUp();
    }

    public void DropObject()
    {
        Rigidbody heldRig = heldObject.GetComponent<Rigidbody>();
        heldRig.useGravity = true;
        heldRig.freezeRotation = false;
        heldRig.drag = 1;

        if (heldObject.TryGetComponent(out Meme meme)) meme.Drop();

        heldObject.transform.parent = null;
        heldObject = null;
        FueAgarrada = false;
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
            if (heldObject.transform.parent != null) heldObject.transform.parent = null;
            heldObject = null;
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
    public IEnumerator ShakeTheCamera(float duration, float magnitude)
    {
        Vector3 originalPosition = transform.localPosition;
        float elapsedTime = 0.0f;

        while (elapsedTime < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(x, y, originalPosition.z);
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        transform.localPosition = originalPosition;
    }
}
