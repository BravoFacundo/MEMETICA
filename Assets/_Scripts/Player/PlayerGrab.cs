//Grabbing, carrying and dropping memes (before: part of PlayerLook, and before that PlayerHoldObjects).
//  Grab      click on a meme you look at (within Pick Up Range): it comes to your hand
//  Carry     pulled to the Grab Pivot by a force (it still feels physical), or a meme window on the screen (MemeDock)
//  Drop      click again: it goes to the Drop Pivot and falls from there
//  Snap      a pedestal takes it out of your hand (Pedestal)
//  Recall    an auto grab (PlayerGrab.Recall.cs, same component): the meme window comes back to your hand (Game)
//Goes on the Player (root), with PlayerMove and PlayerLook. Grab Pivot and Drop Pivot are children of the Main Camera.

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerLook))]
public partial class PlayerGrab : MonoBehaviour
{
    // The player's (found once: there is one per game scene, none in the Menu)
    private static PlayerGrab instance;
    public static PlayerGrab Instance => instance != null ? instance : (instance = FindObjectOfType<PlayerGrab>());

    [Section("Hold Memes")]
    public bool canHold = true;
    public float pickUpRange = 2.5f;
    public float moveForce = 250;
    [Tooltip("The held meme never gets closer to the camera than this (meters), so it doesn't hit your face or cross the near clip plane")]
    public float minHoldDistance = 0.6f;
    [Tooltip("Letting go: it first goes to the Drop Pivot (child of the camera, in front of you) and is let go there. 0 = let go where it is")]
    public float dropTime = 0.12f;
    [Tooltip("Where it is carried (child of the Main Camera). Empty: the GrabPivot child")]
    [SerializeField] private Transform holdParent;
    [Tooltip("Where it is let go (child of the Main Camera). Empty: the DropPivot child")]
    [SerializeField] private Transform dropParent;

    [Section("Debug", Closed = true)]
    [Tooltip("Each click to grab says in the Console what it hit and what you hold")]
    public bool logGrabs = false;
    [ReadOnlyField] public GameObject heldObject;

    private PlayerLook look;
    private Coroutine dropping;
    private bool carriedInHud;      // a meme window: carried on the screen, as its HUD version (MemeDock)
    private Rigidbody heldBody;
    private Collider[] heldColliders;
    private Collider playerCollider;

    // Where the rays start and point (the camera)
    public Transform Eye => look.Head;
    // Where the held meme is pulled to (MemeDock flies it back there)
    public Transform HoldPoint => holdParent;
    // The ray hits a meme within reach
    public bool LookingAtMeme { get; private set; }
    // Clicking now would pick up what you look at: hands free, or an optional meme (DexCollectible: never in the hand)
    public bool CanGrabLooked { get; private set; }
    // Carrying a meme window on the screen (Game): the middle of the screen is free, the aim stays
    public bool CarriedInHud => carriedInHud;

    // What the grab ray can hit: not props, and not the player's own body (its layer, Ignore Raycast: looking down at a
    // meme at your feet the ray used to hit your capsule first)
    static int GrabMask => Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask;

    void Awake()
    {
        instance = this;
        look = GetComponent<PlayerLook>();
        playerCollider = GetComponent<CharacterController>();
    }

    void Start()
    {
        if (holdParent == null) holdParent = FindChild("GrabPivot");
        if (dropParent == null) dropParent = FindChild("DropPivot");
        SetUpRecall();
    }

    Transform FindChild(string childName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    // Physics forces go in FixedUpdate: same pull at any frame rate (before: Update, stronger at high FPS)
    void FixedUpdate()
    {
        if (heldObject == null || dropping != null || carriedInHud || UIManager.GameIsPaused || !canHold) return;
        PullHeldObject();
        KeepHeldObjectOffFace();
    }

    void Update()
    {
        UpdateHeldMeme();
        UpdateRecall();
    }

    //---- Grab

    private void UpdateHeldMeme()
    {
        if (UIManager.GameIsPaused || ModalPopup.Open || !canHold)
        {
            if (logGrabs && GameInput.GrabDown) Debug.Log($"[PlayerGrab] Click ignored: paused {UIManager.GameIsPaused}, modal popup {ModalPopup.Open}, can hold {canHold}", this);
            return;
        }   // no grabbing while paused, a modal popup is up (its click closes it) or riding an elevator
        if (look.CursorFree) { if (logGrabs && GameInput.GrabDown) Debug.Log("[PlayerGrab] Click ignored: free cursor", this); LookingAtMeme = CanGrabLooked = false; return; }   // the click is the cursor's (RetroCursor)

        // Ignores triggers: otherwise the pedestal's zone (around the meme) blocks the ray
        LookingAtMeme = Physics.Raycast(Eye.position, Eye.forward, out RaycastHit hit, pickUpRange, GrabMask, QueryTriggerInteraction.Ignore)
                        && hit.transform.CompareTag("Meme");
        DexCollectible dex = LookingAtMeme ? hit.transform.GetComponentInParent<DexCollectible>() : null;
        CanGrabLooked = LookingAtMeme && (heldObject == null || dex != null);

        if (GameInput.GrabDown)
        {
            if (logGrabs) Debug.Log($"[PlayerGrab] Click: looking at '{(hit.transform != null ? hit.transform.name : "nothing")}' " +
                                    $"(tag {(hit.transform != null ? hit.transform.tag : "-")}, {hit.distance:F1} m of {pickUpRange}), holding '{(heldObject != null ? heldObject.name : "nothing")}', dex {dex != null}", this);
            if (dex != null) dex.Collect();   // an optional meme: to its own HUD place, your hand stays as it is
            else if (heldObject == null) { if (LookingAtMeme) PickUpObject(hit.transform.gameObject); }
            else if (dropping == null) DropObject();
        }
    }

    //---- Carry

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
        Vector3 forward = Eye.forward;
        float distance = Vector3.Dot(heldBody.position - Eye.position, forward);
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

    // Into the hand from anywhere (Recall): as if picked up; a meme window flies to the HUD in Hud Seconds
    public void Grab(GameObject meme, float hudSeconds = 0f)
    {
        if (heldObject == null) PickUpObject(meme, hudSeconds);
    }

    //---- Drop

    // Let go: from the Drop Pivot if there is one (it goes there first, from the corner), else where it is
    public void DropObject()
    {
        if (dropping != null || heldObject == null) return;
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

    //---- Snap

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
}
