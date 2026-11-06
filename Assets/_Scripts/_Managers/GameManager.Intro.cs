using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// How the run starts (part of the GameManager): the game's view fades in from the frame's colour, and Skip Tutorial
// starts at the hub instead (inside Skip Elevator, docked, the meme on the floor in front of you, facing the hub's
// pedestal). The menu's tutorial popup decides it; pressing Play in the scene, DebugTools' Skip Tutorial does
// (a DebugTools start, Start At, wins over it). Moved here from the LevelManager.
public partial class GameManager
{
    [Section("Intro")]
    [Tooltip("Seconds the game's view takes to fade in at the start, with or without the tutorial (it hides the setting up)")]
    public float startFadeTime = 1f;

    [Section("Skip Tutorial", Closed = true)]
    [Tooltip("The elevator from the tutorial to the hub (Elevator_Tutorial_02). Empty: the first one that doesn't ride on start")]
    public Elevator skipElevator;
    [Tooltip("What you face (the hub's spinning pedestal). Empty: Start Pedestal")]
    public Transform skipLookAt;
    [Tooltip("Put a meme on the floor in front of you (Game). Off: every meme stays where it is, the ones riding Skip " +
             "Elevator arrive in it (OriginalGame: both island 1 memes)")]
    public bool skipMemeInFront = true;
    [Tooltip("The meme you start with (Meme Window or orb). Empty: the one riding Skip Elevator, else the tutorial's (MemeDelivery)")]
    public Meme skipMeme;
    [Tooltip("Meters in front of you where it lies")]
    public float skipMemeDistance = 1.5f;
    [Tooltip("Extra meters between its lowest point and the floor (it stays still there until picked up: raise it if it still sinks)")]
    public float skipMemeLift = 0.05f;

    // Set by the menu's tutorial popup before loading the scene (null: DebugTools' Skip Tutorial)
    public static bool? SkipFromMenu;
    // This run started without the tutorial (the intro elevator and MemeDelivery read it)
    public static bool SkippingTutorial { get; private set; }
    // This scene was opened from the menu (its popup chose): DebugTools' Start At doesn't apply
    public static bool FromMenu { get; private set; }

    // Awake: coming from the menu its popup decides (DebugTools' Start At is ignored); pressing Play in the scene,
    // DebugTools' Skip Tutorial, unless a DebugTools start (Start At) is set
    void DecideIntro()
    {
        FromMenu = SkipFromMenu.HasValue;
        SkippingTutorial = SkipFromMenu ?? DebugTools.SkipTutorial;
        SkipFromMenu = null;
        DebugTools debug = FindObjectOfType<DebugTools>();
        if (!FromMenu && debug != null && debug.enabled && debug.startAt >= 0) SkippingTutorial = false;
    }

    // Start: every start fades in (skipping: once everything is in place)
    void BeginIntro()
    {
        CanvasGroup fade = Blackout();
        if (SkippingTutorial) StartCoroutine(SkipTutorial(fade));
        else StartCoroutine(StartFade(fade));
    }

    static void EndIntro() => SkippingTutorial = FromMenu = false;

    IEnumerator SkipTutorial(CanvasGroup fade)
    {
        yield return null;   // the player, islands, elevators and memes set themselves up first

        Elevator elevator = skipElevator != null ? skipElevator : HubElevator();
        PlayerMove player = this.player != null ? this.player : FindObjectOfType<PlayerMove>();
        if (elevator == null || elevator.dockPoint == null || player == null)
        {
            Debug.LogWarning("[GameManager] Skip Tutorial: no Skip Elevator (with a Dock Point) or no player: starting with the tutorial's start.", this);
            yield return FadeIn(fade);
            yield break;
        }
        Meme meme = !skipMemeInFront ? null : skipMeme != null ? skipMeme : PassengerMeme(elevator);
        if (skipMemeInFront && meme == null)   // (Game: the one the tutorial drops on its pedestal, MemeDelivery)
        {
            MemeDelivery delivery = FindObjectOfType<MemeDelivery>();
            if (delivery != null) meme = delivery.meme;
        }
        if (meme != null && !meme.gameObject.activeSelf) meme.gameObject.SetActive(true);   // (hidden until delivered: MemeDelivery)
        Pedestal holder = meme != null ? meme.CurrentPedestal : null;

        // The elevator at the hub, as if it had arrived
        elevator.DockInstantly();
        yield return null;
        Island hub = elevator.dockPoint.GetComponentInParent<Island>();
        Transform respawn = hub != null && LevelManager.Instance != null ? LevelManager.Instance.RespawnPointOf(hub.gameObject, elevator.transform.position) : null;

        // Its pedestal is gone from the start (the meme off it first)
        if (meme != null && holder != null && holder == elevator.pedestal)
        {
            meme.transform.SetParent(null, true);
            meme.RemoveFromPedestal(holder);
        }
        elevator.RemovePedestal();

        // You, standing on its floor, facing the hub's pedestal, looking straight ahead
        CharacterController body = player.GetComponent<CharacterController>();
        Vector3 floor = FloorUnder(elevator.transform.position + Vector3.up * 2f, player.transform, meme, out Vector3 hit) ? hit : elevator.transform.position;
        float feet = body != null ? body.height * 0.5f - body.center.y + body.skinWidth + 0.05f : 1f;
        Transform spot = new GameObject("SkipTutorialSpot").transform;
        spot.position = floor + Vector3.up * feet;
        player.TeleportTo(spot);
        if (respawn != null) { PlayerRespawn.Instance.SetRespawn(respawn); Destroy(spot.gameObject); }

        Transform target = skipLookAt;
        if (target == null && startPedestal != null) target = startPedestal.transform;
        Vector3 facing = target != null ? target.position - player.transform.position : elevator.transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
        facing.Normalize();
        player.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        PlayerLook look = PlayerLook.Instance;
        if (look != null) { look.LookLevel(); look.ResetSmoothing(); }

        // The meme on the floor in front of you
        if (meme != null)
        {
            if (holder != null && holder != elevator.pedestal) holder.Break();   // (another pedestal: it lets it go next frame)
            Vector3 place = player.transform.position + facing * skipMemeDistance;
            meme.transform.SetParent(null, true);
            meme.transform.position = place;   // off its pedestal: it lets it go next frame
            // Solid and grabbable whatever state it was left in (hidden by the tutorial, carried on the HUD…)
            int off = 0;
            foreach (Collider c in meme.GetComponentsInChildren<Collider>(true)) if (!c.enabled) { c.enabled = true; off++; }
            yield return null;
            if (FloorUnder(place + Vector3.up * 2f, player.transform, meme, out Vector3 ground))
                place = ground + Vector3.up * (PivotAboveBottom(meme) + skipMemeLift);   // its lowest point on the floor, plus Skip Meme Lift
            meme.PlaceAt(place, Quaternion.LookRotation(facing, Vector3.up), respawn);   // its front to you
            Debug.Log($"[GameManager] Skip meme '{meme.name}' at {Vector3.Distance(player.transform.position, meme.transform.position):F1} m " +
                      $"(colliders turned back on: {off}, tag {meme.tag}, layer {LayerMask.LayerToName(meme.gameObject.layer)}).", meme);
            if (meme is MemeWindow window && PlayerGrab.Instance != null) PlayerGrab.Instance.WaitForPickUp(window);   // (it stays until you take it)
        }

        yield return new WaitForSecondsRealtime(0.2f);
        yield return FadeIn(fade);
        Debug.Log($"[GameManager] Tutorial skipped: in '{elevator.name}'" + (meme != null ? $", '{meme.name}' in front" : ", no meme") + ".", this);
    }

    // How far its pivot is above its lowest point (solid colliders and visible meshes; colliders measured once enabled)
    static float PivotAboveBottom(Meme meme)
    {
        bool found = false;
        Bounds all = new Bounds();
        void Add(Bounds b) { if (b.size == Vector3.zero) return; if (found) all.Encapsulate(b); else { all = b; found = true; } }
        foreach (Collider c in meme.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) Add(c.bounds);
        foreach (Renderer r in meme.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)) Add(r.bounds);
        return found ? Mathf.Max(0f, meme.transform.position.y - all.min.y) : 0.5f;
    }

    // The first elevator that doesn't ride on start (the one from the tutorial to the hub)
    Elevator HubElevator()
    {
        foreach (Elevator e in FindObjectsOfType<Elevator>()) if (!e.rideOnStart && e.dockPoint != null) return e;
        return null;
    }

    // The meme riding it (its passengers, or on its pedestal)
    static Meme PassengerMeme(Elevator elevator)
    {
        foreach (Transform p in elevator.passengers) if (p != null && p.TryGetComponent(out Meme m)) return m;
        if (elevator.pedestal != null && elevator.pedestal.MemeObject != null) return elevator.pedestal.MemeObject.GetComponent<Meme>();
        return null;
    }

    // The ground under a point (not the player nor the meme)
    static bool FloorUnder(Vector3 from, Transform player, Meme meme, out Vector3 point)
    {
        point = from;
        float nearest = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(player) || (meme != null && h.transform.IsChildOf(meme.transform))) continue;
            if (h.distance < nearest) { nearest = h.distance; point = h.point; }
        }
        return nearest < float.MaxValue;
    }

    // The game's view covered (it hides the setting up), then FadeIn. Only the view, in the frame's colour
    // (DisplayManager.BarColor): the frame and the Classic bars stay as they are, the game "turns on" inside them
    static CanvasGroup Blackout()
    {
        var go = new GameObject("SkipTutorialFade", typeof(Canvas), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // (with the frame: same colour, so their order doesn't matter)
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = group.interactable = false;

        var view = new GameObject("View", typeof(RectTransform), typeof(Image), typeof(AspectArea));
        var rect = (RectTransform)view.transform;
        rect.SetParent(go.transform, false);
        rect.anchorMin = DisplayManager.View.min;   // (AspectArea keeps it there if the window changes)
        rect.anchorMax = DisplayManager.View.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = view.GetComponent<Image>();
        image.color = DisplayManager.BarColor;
        image.raycastTarget = false;
        return group;
    }

    // Normal start: one frame so everything sets itself up, then the fade
    IEnumerator StartFade(CanvasGroup fade)
    {
        yield return null;
        yield return FadeIn(fade);
    }

    IEnumerator FadeIn(CanvasGroup fade)
    {
        if (fade == null) yield break;
        for (float t = 0f; t < startFadeTime; t += Time.unscaledDeltaTime)
        {
            fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(startFadeTime, 0.01f));
            yield return null;
        }
        Destroy(fade.gameObject);
    }
}
