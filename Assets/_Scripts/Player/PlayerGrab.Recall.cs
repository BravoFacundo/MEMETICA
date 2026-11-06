//The recall part of PlayerGrab (same component, its own file): an auto grab, so the meme window never stays behind
//and if you forget how to use it, the game uses it for you (Game; before: the MemeRecall component).
//  Far          farther than Recall Distance from it for a moment: it comes back to your hand (the HUD)
//  Assist (each optional; together, no softlocks):
//     Bring When Leaving   you step from where it is onto another bridge or island: it comes (e.g. you leave the
//                          randomizer without taking it: on the first bridge it comes). From a black pedestal it only
//                          comes if you step somewhere that pedestal didn't raise (it is holding your way up)
//     Bring For Portal     standing on the portal without it: it comes first (PortalWindow)
//     Snap In Range        carrying it within reach of a pedestal that waits for it (the randomizer, or a white one
//                          whose Key Meme it shows), it goes in even if you don't look at it (once per pedestal)
//Close, it flies straight to its place on your HUD; farther than Fly Up To, it just appears there.
//Coming back it shows its Leave change (you forgot it) if it hasn't yet; optionally else its Fall change.
//Only with a MemeDock on the Player (the HUD version of the window): without it (OriginalGame) none of this happens.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerGrab
{
    public enum FromPedestals { Never, UsedOnce, Always }

    [Section("Recall")]
    [Tooltip("The meme window comes back to your hand by itself (needs a MemeDock: Game)")]
    public bool recall = true;

    [Section("Recall: Far", nameof(recall), true)]
    [Tooltip("Meters from you: farther than this it comes back")]
    public float recallDistance = 35f;
    [Tooltip("Seconds you must be that far (standing on the ground) before it comes")]
    public float recallDelay = 1f;
    [Tooltip("From a pedestal: Never; Used Once (only from a randomizer or a pedestal with PedestalRetire); Always. " +
             "A black pedestal holding your way up: see Bring When Leaving")]
    public FromPedestals fromPedestals = FromPedestals.UsedOnce;

    [Section("Recall: Assist", nameof(recall), true)]
    [Tooltip("You step onto a bridge or island other than where it is: it comes")]
    public bool bringWhenLeaving = true;
    [Tooltip("Standing on the portal without it: it comes first")]
    public bool bringForPortal = true;
    [Tooltip("Carrying it within reach of a pedestal that waits for it: it goes in without looking at it")]
    public bool snapInRange = true;
    [Tooltip("Snap In Range also on the black / grey pedestals (any meme goes on them: e.g. the ones that open the " +
             "optional islands), not only on the randomizer and the white ones waiting for it")]
    public bool snapOnBlack = true;

    [Section("Recall: Flight", nameof(recall), true)]
    [Tooltip("Speed it flies at (m/s); the flight takes 0.4 to 1.5 seconds anyway")]
    public float recallSpeed = 40f;
    [Tooltip("Meters: farther than this it doesn't fly, it just appears in your hand")]
    public float flyUpTo = 25f;
    [Tooltip("When it comes back with its meme, it shows its Leave change (you forgot it) if it never did")]
    public bool leaveOnArrival = true;
    [Tooltip("…else its Fall change, if it never did")]
    public bool fallOnArrival = false;
    [Tooltip("When it sets off toward you (not 3D)")]
    public AudioClip comeClip;

    // On, and with a HUD version to come to (MemeDock)
    public bool RecallOn => recall && MemeDock.Instance != null && isActiveAndEnabled;
    public bool IsBringing => flying != null;

    private PlayerMove player;
    private float away;
    private Coroutine flying;
    private AudioSource recallSource;
    private MemeWindow[] windows = new MemeWindow[0];
    private float nextSearch;
    private Transform lastPlace;
    // Not brought (Far, Bring When Leaving) until picked up once: the tutorial skipped leaves it on the floor in front of you
    private readonly HashSet<MemeWindow> waitingPickUp = new HashSet<MemeWindow>();

    public void WaitForPickUp(MemeWindow window) { if (window != null) waitingPickUp.Add(window); }

    void SetUpRecall()
    {
        player = GetComponent<PlayerMove>();
        recallSource = gameObject.AddComponent<AudioSource>();
        recallSource.playOnAwake = false;
        recallSource.spatialBlend = 0f;
    }

    void UpdateRecall()
    {
        if (!RecallOn) return;
        if (waitingPickUp.Count > 0) waitingPickUp.RemoveWhere(w => w == null || w.IsHeld);   // picked up: as always from now on
        if (flying != null || player == null) return;
        if (Time.time >= nextSearch) { windows = FindObjectsOfType<MemeWindow>(); nextSearch = Time.time + 2f; }
        bool free = FreeToCome(false);

        // You stepped somewhere new: the one left where you were comes
        Transform place = PlaceOf(player.GroundUnder);
        if (place != null && place != lastPlace)
        {
            Transform from = lastPlace;
            lastPlace = place;
            bool optional = Lift.RoleOf(place) == Lift.Role.Optional;   // an optional course: it waits
            if (free && bringWhenLeaving && from != null && !optional)
                foreach (MemeWindow window in windows)
                    if (CanCome(window) && LeftBehind(window, from, place)) { flying = StartCoroutine(Fly(window)); return; }
        }
        if (!FreeToCome(true)) { away = 0f; return; }
        if (lastPlace != null && Lift.RoleOf(lastPlace) == Lift.Role.Optional) { away = 0f; return; }   // an optional course: it waits

        // Far
        MemeWindow far = null;
        foreach (MemeWindow window in windows)
        {
            if (!CanCome(window) || Vector3.Distance(window.transform.position, transform.position) < recallDistance) continue;
            Pedestal holder = window.CurrentPedestal;
            if (holder != null && HoldsTheWay(holder) && Raised(holder, lastPlace)) continue;   // holding up the way you are on
            far = window;
            break;
        }
        away = far != null ? away + Time.deltaTime : 0f;
        if (far != null && away >= recallDelay) flying = StartCoroutine(Fly(far));
    }

    // Brings one left behind now (the portal asks). True while one is on its way
    public bool BringLeftBehind()
    {
        if (!RecallOn) return false;
        if (flying != null) return true;
        if (!FreeToCome(false)) return false;
        foreach (MemeWindow window in windows)
            if (CanCome(window)) { flying = StartCoroutine(Fly(window)); return true; }
        return false;
    }

    // Your hands are free (and you stand on something)
    bool FreeToCome(bool onGround) => heldObject == null && !player.Scripted && !player.Respawning && (!onGround || player.isGrounded);

    // Right now, whatever it is doing (it fell into the void, or got lost: Meme): flies to your hand. False: it can't
    public bool BringNow(MemeWindow window)
    {
        if (!RecallOn) return false;
        if (flying != null) return true;
        if (window == null || window.IsHeld || !window.gameObject.activeInHierarchy || window.TryGetComponent(out DexCollectible _)) return false;
        flying = StartCoroutine(Fly(window));
        return true;
    }

    bool CanCome(MemeWindow window)
    {
        if (window == null || window.IsHeld || !window.gameObject.activeInHierarchy) return false;
        if (window.TryGetComponent(out DexCollectible _)) return false;   // an optional meme: not yours
        if (waitingPickUp.Contains(window)) return false;   // (not picked up yet: it waits where it was put)
        if (window.TryGetComponent(out Rigidbody body) && !body.isKinematic && body.velocity.sqrMagnitude > 1f) return false;   // still falling
        Pedestal pedestal = window.CurrentPedestal;
        if (pedestal == null || HoldsTheWay(pedestal)) return true;   // (a black one: LeftBehind decides)
        if (fromPedestals == FromPedestals.Always) return true;
        if (fromPedestals == FromPedestals.Never) return false;
        return pedestal is RandomizerPedestal || pedestal.GetComponent<PedestalRetire>() != null;
    }

    // A black pedestal: holds its islands and bridges up while it has a meme
    static bool HoldsTheWay(Pedestal pedestal) =>
        !(pedestal is KeyPedestal) && !(pedestal is RandomizerPedestal) && pedestal.GetComponent<PedestalRetire>() == null;

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
        return place == PlaceOf(pedestal.transform) || pedestal.Raises(place);
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
    public bool AutoSnaps => RecallOn && snapInRange;

    // Straight to its place on your HUD (MemeDock flies it there and swaps it for its HUD version); far: it appears
    // just behind you and slips in from the edge
    IEnumerator Fly(MemeWindow window)
    {
        if (window.CurrentPedestal != null) window.CurrentPedestal.DropMeme();
        if (comeClip != null) recallSource.PlayOneShot(comeClip);
        yield return null;   // (off the pedestal first)

        Transform t = window.transform;
        t.SetParent(null, true);
        if (window.TryGetComponent(out Rigidbody body)) body.velocity = Vector3.zero;
        float meters = Vector3.Distance(t.position, Eye.position);
        float seconds = Mathf.Clamp(meters / Mathf.Max(recallSpeed, 1f), 0.4f, 1.5f);
        if (meters > flyUpTo)
        {
            t.position = Eye.position - Eye.forward * 0.6f + Eye.up * 0.3f;
            seconds = 0.3f;
        }
        bool left = leaveOnArrival && window.PendLeave();   // you forgot it: its Leave change (before the grab: only its sound)
        if (!left && fallOnArrival) window.PendFall();
        if (heldObject == null) Grab(window.gameObject, seconds);
        yield return new WaitForSeconds(seconds);
        away = 0f;
        flying = null;
    }
}
