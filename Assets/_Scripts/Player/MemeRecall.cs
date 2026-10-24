using System.Collections;
using UnityEngine;

// Anti softlock (Game): the meme window never stays behind, and if you forget how to use it, the game uses it for you.
//   Far          farther than Distance from it for a moment: it comes back to your hand (the HUD)
//   Assist (each optional; together, no softlocks):
//     Bring When Leaving   you step from where it is onto another bridge or island: it comes (e.g. you leave the
//                          randomizer without taking it: on the first bridge it comes). From a black pedestal it only
//                          comes if you step somewhere that pedestal didn't raise (it is holding your way up)
//     Bring For Portal     standing on the portal without it: it comes first (PortalWindow)
//     Snap In Range        carrying it within reach of a pedestal that waits for it (the randomizer, or a white one
//                          whose Key Meme it shows), it goes in even if you don't look at it (once per pedestal)
// Close, it flies straight to its place on your HUD; farther than Fly Up To, it just appears there.
// Optionally it brings its Fall change if it hasn't shown it yet (you never fell with it: you see it anyway).
// Goes on the Player, with MemeDock.
public class MemeRecall : MonoBehaviour
{
    public static MemeRecall Instance { get; private set; }

    public enum FromPedestals { Never, UsedOnce, Always }

    [Section("Far")]
    [Tooltip("Meters from you: farther than this it comes back")]
    public float distance = 35f;
    [Tooltip("Seconds you must be that far (standing on the ground) before it comes")]
    public float delay = 1f;
    [Tooltip("From a pedestal: Never; Used Once (only from a randomizer or a pedestal with PedestalRetire); Always. " +
             "A black pedestal holding your way up: see Bring When Leaving")]
    public FromPedestals fromPedestals = FromPedestals.UsedOnce;

    [Section("Assist")]
    [Tooltip("You step onto a bridge or island other than where it is: it comes")]
    public bool bringWhenLeaving = true;
    [Tooltip("Standing on the portal without it: it comes first")]
    public bool bringForPortal = true;
    [Tooltip("Carrying it within reach of a pedestal that waits for it: it goes in without looking at it")]
    public bool snapInRange = true;

    [Section("Flight")]
    [Tooltip("Speed it flies at (m/s); the flight takes 0.4 to 1.5 seconds anyway")]
    public float speed = 40f;
    [Tooltip("Meters: farther than this it doesn't fly, it just appears in your hand")]
    public float flyUpTo = 25f;
    [Tooltip("When it comes back with its meme, it shows its Fall change if it never did")]
    public bool fallOnArrival = false;
    [Tooltip("When it sets off toward you (not 3D)")]
    public AudioClip comeClip;

    public bool IsBringing => flying != null;

    private PlayerMove player;
    private PlayerLook look;
    private Camera cam;
    private float away;
    private Coroutine flying;
    private AudioSource source;
    private MemeWindow[] windows = new MemeWindow[0];
    private float nextSearch;
    private Transform lastPlace;

    void Awake() => Instance = this;

    void Start()
    {
        player = GetComponent<PlayerMove>();
        look = FindObjectOfType<PlayerLook>();
        cam = look != null ? look.GetComponent<Camera>() : Camera.main;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void Update()
    {
        if (flying != null || player == null || look == null || MemeDock.Instance == null) return;
        if (Time.time >= nextSearch) { windows = FindObjectsOfType<MemeWindow>(); nextSearch = Time.time + 2f; }
        bool free = FreeToCome(false);

        // You stepped somewhere new: the one left where you were comes
        Transform place = PlaceOf(player.GroundUnder);
        if (place != null && place != lastPlace)
        {
            Transform from = lastPlace;
            lastPlace = place;
            if (free && bringWhenLeaving && from != null)
                foreach (MemeWindow window in windows)
                    if (CanCome(window) && LeftBehind(window, from, place)) { flying = StartCoroutine(Fly(window)); return; }
        }
        if (!FreeToCome(true)) { away = 0f; return; }

        // Far
        MemeWindow far = null;
        foreach (MemeWindow window in windows)
            if (CanCome(window) && Vector3.Distance(window.transform.position, transform.position) >= distance) { far = window; break; }
        away = far != null ? away + Time.deltaTime : 0f;
        if (far != null && away >= delay) flying = StartCoroutine(Fly(far));
    }

    // Brings one left behind now (the portal asks). True while one is on its way
    public bool BringLeftBehind()
    {
        if (flying != null) return true;
        if (!FreeToCome(false)) return false;
        foreach (MemeWindow window in windows)
            if (CanCome(window)) { flying = StartCoroutine(Fly(window)); return true; }
        return false;
    }

    // Your hands are free (and you stand on something)
    bool FreeToCome(bool onGround) => look.heldObject == null && !player.Scripted && !player.Respawning && (!onGround || player.isGrounded);

    bool CanCome(MemeWindow window)
    {
        if (window == null || window.IsHeld || !window.gameObject.activeInHierarchy) return false;
        if (window.TryGetComponent(out Rigidbody body) && !body.isKinematic && body.velocity.sqrMagnitude > 1f) return false;   // still falling
        Pedestal pedestal = window.CurrentPedestal;
        if (pedestal == null || HoldsTheWay(pedestal)) return true;   // (a black one: LeftBehind decides)
        if (fromPedestals == FromPedestals.Always) return true;
        if (fromPedestals == FromPedestals.Never) return false;
        return pedestal.GetComponent<MemeRandomizer>() != null || pedestal.GetComponent<PedestalRetire>() != null;
    }

    // A black pedestal: holds its islands and bridges up while it has a meme
    static bool HoldsTheWay(Pedestal pedestal) =>
        pedestal.KeylessPedestal && pedestal.GetComponent<MemeRandomizer>() == null && pedestal.GetComponent<PedestalRetire>() == null;

    // You went From where it is To somewhere else (on a black pedestal: from its island or what it raised, to
    // somewhere it didn't raise)
    bool LeftBehind(MemeWindow window, Transform from, Transform to)
    {
        Pedestal pedestal = window.CurrentPedestal;
        if (pedestal != null && HoldsTheWay(pedestal)) return Raised(pedestal, from) && !Raised(pedestal, to);
        Transform where = pedestal != null ? PlaceOf(pedestal.transform) : PlaceUnder(window);
        return where != null && where == from && where != to;
    }

    // Its own island, or one of the islands and bridges it raises
    static bool Raised(Pedestal pedestal, Transform place)
    {
        if (place == PlaceOf(pedestal.transform)) return true;
        LevelManager level = LevelManager.Instance;
        foreach (string id in pedestal.ControlledIslands) if (level.FindIsland(id) == place) return true;
        foreach (string id in pedestal.ControlledBridges) if (level.FindBridge(id) == place) return true;
        return false;
    }

    // The island or bridge something is part of
    static Transform PlaceOf(Transform t)
    {
        if (t == null) return null;
        Bridge bridge = t.GetComponentInParent<Bridge>();
        if (bridge != null) return bridge.transform;
        Island island = t.GetComponentInParent<Island>();
        return island != null ? island.transform : null;
    }

    // What a loose window lies on
    static Transform PlaceUnder(MemeWindow window)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(window.transform.position + Vector3.up * 0.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(window.transform)) continue;
            Transform place = PlaceOf(hit.transform);
            if (place != null) return place;
        }
        return null;
    }

    // Snap In Range (Pedestal asks): carrying it near a pedestal that waits for it
    public bool AutoSnaps => snapInRange && isActiveAndEnabled;

    // Straight to its place on your HUD (MemeDock flies it there and swaps it for its HUD version); far: it appears
    // just behind you and slips in from the edge
    IEnumerator Fly(MemeWindow window)
    {
        if (window.CurrentPedestal != null) window.CurrentPedestal.DropMeme();
        if (comeClip != null) source.PlayOneShot(comeClip);
        yield return null;   // (off the pedestal first)

        Transform t = window.transform;
        t.SetParent(null, true);
        if (window.TryGetComponent(out Rigidbody body)) body.velocity = Vector3.zero;
        float meters = Vector3.Distance(t.position, cam.transform.position);
        float seconds = Mathf.Clamp(meters / Mathf.Max(speed, 1f), 0.4f, 1.5f);
        if (meters > flyUpTo)
        {
            t.position = cam.transform.position - cam.transform.forward * 0.6f + cam.transform.up * 0.3f;
            seconds = 0.3f;
        }
        if (look.heldObject == null) look.Grab(window.gameObject, seconds);
        if (fallOnArrival) window.PendFall();
        yield return new WaitForSeconds(seconds);
        away = 0f;
        flying = null;
    }
}
