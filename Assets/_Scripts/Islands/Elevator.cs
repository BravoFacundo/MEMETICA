using System.Collections;
using UnityEngine;

// Tutorial elevator: carries the player (and anything riding with it) up to its island and docks there.
// There are two, both in the tutorial:
//   Elevator_Tutorial_01  intro: starts riding as soon as the game starts (look + zoom popups on the way)
//   Elevator_Tutorial_02  tutorial → Island_1: started by its pedestal (LevelManager.SubirIsla("Tutorial_02"))
// It starts where it is placed in the scene and ends at Dock Point, an empty child of the destination island.
// Dock Point, Leave Via and Arrive Via go inside the floating part of their island (e.g. Island_01/Island/Pivots),
// so the path follows the island while it floats.
// On arrival: the player can move again, respawns on that island, and the elevator becomes part of it.
public class Elevator : MonoBehaviour
{
    [Header("Ride")]
    [Tooltip("Where it ends: an empty child of the destination island")]
    public Transform dockPoint;
    [Tooltip("Seconds to go up (eases in and out)")]
    public float rideDuration = 4f;
    [Tooltip("Start riding as soon as the game starts (intro elevator)")]
    public bool rideOnStart = false;

    [Header("Path (curve)")]
    [Tooltip("Island it leaves from: it first moves away from it. Empty = leaves straight up")]
    public Transform departFrom;
    [Tooltip("Meters it moves away from the islands when leaving and when arriving")]
    public float clearance = 3f;
    [Tooltip("Optional: empty object that pulls the first half of the curve (replaces the automatic one)")]
    public Transform leaveVia;
    [Tooltip("Optional: empty object that pulls the second half of the curve, e.g. beside the island so it docks from outside")]
    public Transform arriveVia;

    [System.Serializable]
    public class RidePopup
    {
        [Tooltip("Popup (child of Canvas_Tutorial)")]
        public GameObject popup;
        [Tooltip("When it appears, as a fraction of the ride (0 = start, 1 = docked)")]
        [Range(0f, 1f)] public float showAt = 0.1f;
        [Tooltip("When it disappears, as a fraction of the ride")]
        [Range(0f, 1f)] public float hideAt = 0.5f;
    }

    [Header("Tutorial")]
    [Tooltip("Popups shown in sequence during the ride. Times are fractions of Ride Duration, so they scale with it")]
    public RidePopup[] ridePopups;

    [Header("Passengers")]
    [Tooltip("Objects that ride along besides the player (e.g. the orb on its pedestal)")]
    public Transform[] passengers;
    [Tooltip("Invisible walls: on during the ride, off on arrival")]
    public GameObject locks;
    [Tooltip("Pedestal on the elevator: stops working during the ride and topples when its orb is taken")]
    public Pedestal pedestal;

    public bool IsRiding { get; private set; }
    bool used;

    void Start()
    {
        if (locks != null) locks.SetActive(false);
        if (rideOnStart && !LevelManager.SkippingTutorial) StartRide();   // tutorial skipped: no intro ride
    }

    // Only once per game
    public void StartRide()
    {
        if (used) return;
        if (dockPoint == null) { Debug.LogWarning("[Elevator] '" + name + "' has no Dock Point.", this); return; }
        used = true;
        StartCoroutine(Ride());
    }

    IEnumerator Ride()
    {
        IsRiding = true;
        PlayerMove player = FindObjectOfType<PlayerMove>();
        PlayerLook hold = Camera.main != null ? Camera.main.GetComponent<PlayerLook>() : null;
        GameObject pedestalMeme = pedestal != null ? pedestal.MemeObject : null;   // read before the pedestal stops working

        // Board: player can look and zoom but not move or grab; everything rides parented to the elevator
        if (player != null) { player.playerCanMove = false; player.transform.SetParent(transform); }
        foreach (Transform p in passengers) if (p != null) p.SetParent(transform);
        if (hold != null) hold.canHold = false;
        if (locks != null) locks.SetActive(true);
        if (pedestal != null && pedestal.TryGetComponent(out SphereCollider trigger)) trigger.enabled = false;

        // Ride along an S curve (scaled time: it stops while the game is paused).
        // It moves away from the island it leaves, rises, and comes in to the destination from outside,
        // so it never cuts through either island. Starts slow and docks slow (smoothstep).
        // The curve is worked out again every frame: the start, the Dock Point and the Via points follow
        // their floating islands, so it lands exactly on the Dock Point wherever the island floated to
        Transform startParent = transform.parent;
        Vector3 startLocal = startParent != null ? startParent.InverseTransformPoint(transform.position) : transform.position;
        bool[] shown = new bool[ridePopups.Length];

        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime * DebugTools.ElevatorSpeed / Mathf.Max(rideDuration, 0.01f));   // faster when testing (DebugTools)
            Vector3 from = startParent != null ? startParent.TransformPoint(startLocal) : startLocal;
            Vector3 to = dockPoint.position;
            ControlPoints(from, to, out Vector3 p1, out Vector3 p2);
            transform.position = Bezier(from, p1, p2, to, t * t * (3f - 2f * t));
            UpdatePopups(t, shown);
            yield return null;
        }

        // Arrive: exactly on the Dock Point, and part of the destination island (its floating part) from now on
        transform.position = dockPoint.position;
        Island island = dockPoint.GetComponentInParent<Island>();
        Transform respawn = island != null ? LevelManager.Instance.RespawnPointOf(island.gameObject) : null;
        if (island != null) transform.SetParent(DockParent(island));

        if (player != null)
        {
            player.transform.SetParent(null);
            player.playerCanMove = true;
            if (respawn != null) player.SetRespawn(respawn);
        }
        foreach (Transform p in passengers)
            if (p != null && respawn != null && p.TryGetComponent(out Meme meme)) meme.SetRespawn(respawn);
        if (hold != null) hold.canHold = true;
        if (locks != null) locks.SetActive(false);
        for (int i = 0; i < ridePopups.Length; i++) if (shown[i]) SetPopup(ridePopups[i].popup, false);

        IsRiding = false;
        if (pedestal != null && pedestalMeme != null) StartCoroutine(BreakPedestalWhenMemeTaken(pedestalMeme));
    }

    // Debug: as if it had already arrived, without the player (DebugTools starts)
    public void DockInstantly()
    {
        if (used || dockPoint == null) return;
        used = true;

        foreach (Transform p in passengers) if (p != null) p.SetParent(transform);
        transform.position = dockPoint.position;
        Island island = dockPoint.GetComponentInParent<Island>();
        Transform respawn = island != null ? LevelManager.Instance.RespawnPointOf(island.gameObject) : null;
        if (island != null) transform.SetParent(DockParent(island));
        foreach (Transform p in passengers)
            if (p != null && respawn != null && p.TryGetComponent(out Meme meme)) meme.SetRespawn(respawn);
        if (locks != null) locks.SetActive(false);
        if (pedestal != null) StartCoroutine(BreakPedestalOnceMemeIsTaken());
    }

    // Tutorial skipped (LevelManager): its pedestal is gone from the start
    public void RemovePedestal()
    {
        if (pedestal == null) return;
        StopAllCoroutines();   // (its "break when the meme is taken": docked, nothing else runs here)
        pedestal.gameObject.SetActive(false);
    }

    // Docked, it becomes part of whatever holds its Dock Point (a floating part of the island bobs it along)
    Transform DockParent(Island island) => dockPoint.parent != null ? dockPoint.parent : island.transform;

    // The meme may be placed on its pedestal a moment later (DebugTools): wait for it, then break when taken
    IEnumerator BreakPedestalOnceMemeIsTaken()
    {
        while (!pedestal.HasMeme) yield return null;
        yield return BreakPedestalWhenMemeTaken(pedestal.MemeObject);
    }

    // Shows each popup between its showAt and hideAt (only on changes, so the fades work)
    void UpdatePopups(float t, bool[] shown)
    {
        for (int i = 0; i < ridePopups.Length; i++)
        {
            bool visible = t >= ridePopups[i].showAt && t < ridePopups[i].hideAt;
            if (visible == shown[i]) continue;
            shown[i] = visible;
            SetPopup(ridePopups[i].popup, visible);
        }
    }

    static void SetPopup(GameObject popup, bool visible)
    {
        if (TutorialManager.Instance == null) return;
        if (visible) TutorialManager.Instance.Show(popup); else TutorialManager.Instance.Hide(popup);
    }

    // The two points that bend the curve: the Via objects if set, otherwise automatic
    // (out from the island it leaves, and out from the destination island)
    void ControlPoints(Vector3 from, Vector3 to, out Vector3 p1, out Vector3 p2)
    {
        Island destination = dockPoint.GetComponentInParent<Island>();
        p1 = leaveVia != null ? leaveVia.position
           : from + (departFrom != null ? Flat(from - departFrom.position) : Vector3.zero) * clearance;
        p2 = arriveVia != null ? arriveVia.position
           : to + (destination != null ? Flat(to - destination.transform.position) : Vector3.zero) * clearance;
    }

    // Horizontal direction, normalized (zero if there is none)
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero; }

    // Cubic Bezier: passes through a and d, pulled towards b and c
    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

#if UNITY_EDITOR
    // Selected: draws the path it will take
    void OnDrawGizmosSelected()
    {
        if (dockPoint == null || Application.isPlaying) return;
        Vector3 from = transform.position, to = dockPoint.position;
        ControlPoints(from, to, out Vector3 p1, out Vector3 p2);
        Gizmos.color = new Color(0.4f, 0.9f, 1f);
        Vector3 prev = from;
        for (int i = 1; i <= 32; i++) { Vector3 p = Bezier(from, p1, p2, to, i / 32f); Gizmos.DrawLine(prev, p); prev = p; }
        Gizmos.DrawWireSphere(to, 0.3f);

        // Handles: where each half of the curve is pulled to
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawLine(from, p1); Gizmos.DrawWireSphere(p1, 0.25f);
        Gizmos.DrawLine(to, p2);   Gizmos.DrawWireSphere(p2, 0.25f);
    }
#endif

    // Already docked: when the player takes the meme off the elevator pedestal, the pedestal topples and stays broken
    IEnumerator BreakPedestalWhenMemeTaken(GameObject memeObject)
    {
        Meme meme = memeObject.GetComponent<Meme>();
        Vector3 startPos = memeObject.transform.position;

        // Wait until it is grabbed or moved from its place
        while ((meme == null || !meme.IsHeld) && Vector3.Distance(memeObject.transform.position, startPos) < 1f)
            yield return null;

        pedestal.Break();
    }
}
