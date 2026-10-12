using System.Collections;
using UnityEngine;

// Tutorial elevator: carries the player (and anything riding with it) up to its island and docks there.
// There are two, both in the tutorial:
//   Elevator_Tutorial_01  intro: starts riding as soon as the game starts (look + zoom popups on the way)
//   Elevator_Tutorial_02  tutorial → Island_1: started by its pedestal (LevelManager.SubirIsla("Tutorial_02"))
// It starts where it is placed in the scene and ends at Dock Point, an empty child of the destination island.
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

    [Header("Tutorial")]
    [Tooltip("Popups shown while it rides (children of Canvas_Tutorial)")]
    public GameObject[] ridePopups;

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
        if (rideOnStart) StartRide();
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
        PlayerHoldObjects hold = Camera.main != null ? Camera.main.GetComponent<PlayerHoldObjects>() : null;
        GameObject pedestalOrb = pedestal != null ? pedestal.Orb : null;   // read before the pedestal stops working

        // Board: player can look and zoom but not move or grab; everything rides parented to the elevator
        if (player != null) { player.playerCanMove = false; player.transform.SetParent(transform); }
        foreach (Transform p in passengers) if (p != null) p.SetParent(transform);
        if (hold != null) hold.canHold = false;
        if (locks != null) locks.SetActive(true);
        if (pedestal != null && pedestal.TryGetComponent(out SphereCollider trigger)) trigger.enabled = false;
        foreach (GameObject popup in ridePopups) if (TutorialManager.Instance != null) TutorialManager.Instance.Show(popup);

        // Ride along an S curve (scaled time: it stops while the game is paused).
        // It moves away from the island it leaves, rises, and comes in to the destination from outside,
        // so it never cuts through either island. Starts slow and docks slow (smoothstep).
        Vector3 from = transform.position, to = dockPoint.position;
        Island destination = dockPoint.GetComponentInParent<Island>();
        Vector3 leaveDir = departFrom != null ? Flat(from - departFrom.position) : Vector3.zero;
        Vector3 arriveDir = destination != null ? Flat(to - destination.transform.position) : Vector3.zero;
        Vector3 p1 = from + leaveDir * clearance;    // out from the start island
        Vector3 p2 = to + arriveDir * clearance;     // out from the destination island

        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(rideDuration, 0.01f));
            transform.position = Bezier(from, p1, p2, to, t * t * (3f - 2f * t));
            yield return null;
        }

        // Arrive: the elevator becomes part of the destination island
        Island island = dockPoint.GetComponentInParent<Island>();
        Transform respawn = island != null ? LevelManager.Instance.RespawnPointOf(island.gameObject) : null;
        if (island != null) transform.SetParent(island.transform);

        if (player != null)
        {
            player.transform.SetParent(null);
            player.playerCanMove = true;
            if (respawn != null && player.TryGetComponent(out PlayerRespawn playerRespawn)) playerRespawn.SetRespawn(respawn);
        }
        foreach (Transform p in passengers)
            if (p != null && respawn != null && p.TryGetComponent(out PlayerRespawn r)) r.SetRespawn(respawn);
        if (hold != null) hold.canHold = true;
        if (locks != null) locks.SetActive(false);
        foreach (GameObject popup in ridePopups) if (TutorialManager.Instance != null) TutorialManager.Instance.Hide(popup);

        IsRiding = false;
        if (pedestal != null && pedestalOrb != null) StartCoroutine(BreakPedestalWhenOrbTaken(pedestalOrb));
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
        Island destination = dockPoint.GetComponentInParent<Island>();
        Vector3 p1 = from + (departFrom != null ? Flat(from - departFrom.position) : Vector3.zero) * clearance;
        Vector3 p2 = to + (destination != null ? Flat(to - destination.transform.position) : Vector3.zero) * clearance;
        Gizmos.color = new Color(0.4f, 0.9f, 1f);
        Vector3 prev = from;
        for (int i = 1; i <= 24; i++) { Vector3 p = Bezier(from, p1, p2, to, i / 24f); Gizmos.DrawLine(prev, p); prev = p; }
        Gizmos.DrawWireSphere(to, 0.3f);
    }
#endif

    // Already docked: when the player takes the orb off the elevator pedestal, the pedestal topples and stays broken
    IEnumerator BreakPedestalWhenOrbTaken(GameObject orb)
    {
        OrbMemes memes = orb.GetComponent<OrbMemes>();
        Vector3 startPos = orb.transform.position;

        // Wait until it is grabbed or moved from its place
        while ((memes == null || !memes.isHolded) && Vector3.Distance(orb.transform.position, startPos) < 1f)
            yield return null;

        PlayerMove player = FindObjectOfType<PlayerMove>();
        pedestal.Break(player != null ? player.transform.position : transform.position);
    }
}
