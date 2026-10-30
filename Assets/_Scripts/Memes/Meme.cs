using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Anything the player carries that holds a meme: MemeOrb (Game_Old) or MemeWindow (Game).
// The environment (hold, pedestals, respawn, bridges and islands) only talks to this class,
// so it works the same with both. Each representation decides what to do in the On... methods.
// On its own (no MemeOrb / MemeWindow) it is a plain carryable meme with no visual logic, e.g. the tutorial orb.
// Falling into the void: it comes back to the last island it touched as a meteor: a rock (MeteorRock, the meme hidden
// in it) glowing with a trail, falling at an angle onto open ground near the island's middle (not trees, not pedestals).
// On impact the ground breaks, the rock bursts in a dark cloud and the meme comes out; smoke rises there until it is picked up.
// While held it travels with the player: it notices on its own the bridges and islands it is carried to,
// and the player falling into the void (PlayerMove.VoidFallStarted, then RespawnLanded). Shared values: MemeSettings (Assets/Resources).
// This class only detects (what happened, where the player is, whether they can see it); each representation
// (MemeOrb, MemeWindow) decides what each change looks like and when it shows.
// Object name: Meme_<ID> (e.g. Meme_3Aa). Pedestals compare their Key ID with that ID: the ID is the orb's place in
// the level, the meme in it (MemeData) can be any. Data: a fixed one, or a spare from a MemeGroup (Reserve Slot).
[RequireComponent(typeof(Rigidbody))]
[SelectionBase]   // clicking any of its parts in the Scene view selects this
public class Meme : MonoBehaviour
{
    public const string NamePrefix = "Meme_";

    [Section("Meme")]
    [Tooltip("The meme it holds: its images and sounds (Art/Memes/<Meme>/<Meme>.asset)")]
    public MemeData data;
    [Tooltip("Takes a spare meme from this group instead (Data is then used only if the group has none for its slot)")]
    public MemeGroup reserveGroup;
    [ShowIf(nameof(reserveGroup))]
    [Tooltip("Which spare: 1 = the first of the reserve (or the first drawn), 2 = the second…")]
    public int reserveSlot = 1;

    // The meme it holds this game (Data, or the spare it got)
    public MemeData Data { get; protected set; }   // MemeChanger.SetMeme swaps it (the randomizer)

    public string ID => IDOf(gameObject);
    public bool IsHeld { get; private set; }
    public Pedestal CurrentPedestal { get; private set; }

    protected static MemeSettings Settings => MemeSettings.Instance;

    protected Rigidbody body;
    private LayerMask groundMask;
    private float groundedTime;
    private Island startIsland;   // held still on it until it is up and still

    private Transform respawnPoint;
    private Coroutine meteor;
    private Transform lastGroundHit;
    private GameObject lastIsland;   // the island under it last (respawn there; carried to a new one: OnIslandVisited)
    private GameObject lastBridge;   // the bridge carried over last (a new one: OnBridgeCrossed)
    private static PlayerMove player;
    private static readonly System.Collections.Generic.List<Meme> all = new System.Collections.Generic.List<Meme>();
    private float lastGroundY;
    private float lastIslandY;   // the last island ground under it (a safety net: far below it, it is lost)

    // "Meme_3Aa" → "3Aa". Everything after the first "_", so old names ("Orb_3Aa") still work
    public static string IDOf(GameObject meme)
    {
        int separator = meme.name.IndexOf('_');
        return separator >= 0 ? meme.name.Substring(separator + 1) : meme.name;
    }

    // The meme in the player's hand, or null
    public static Meme Held => all.Find(m => m.IsHeld);

    // DebugTools: as if it had just been carried onto another island (it changes there, wherever you are)
    public void DebugIslandChange()
    {
        IslandChanges++;
        OnIslandVisited(lastIsland, true);
    }

    // The meme a white pedestal waits for (its ID starts with the Key ID), or null
    public static Meme WithKey(string keyID) =>
        string.IsNullOrEmpty(keyID) ? null : all.Find(m => m.ID.StartsWith(keyID));

    protected virtual void Awake()
    {
        Data = reserveGroup != null ? reserveGroup.Reserve(reserveSlot) ?? data : data;
        body = GetComponent<Rigidbody>();
        groundMask = LayerMask.GetMask("Ground");
        lastGroundY = lastIslandY = transform.position.y;
        ownInterpolation = body.interpolation;
        HoldOnStartIsland();
        homeParent = transform.parent;
        homePosition = transform.localPosition;
        homeRotation = transform.localRotation;
    }

    // On an island in the scene (its child, or resting on it): put down on its ground and held still, part of the island,
    // before the islands move (Awake runs before their Start). It goes down and comes up with its island, and once the
    // island has arrived it is let go with physics. Before: the island dropping 30 m at start counted as a fall into the void
    // Keeps Its Place (MemeWindow): it stays exactly where it was placed (a tree branch…), still, until picked up
    private void HoldOnStartIsland()
    {
        if (KeepsItsPlace) { AttachStill(); body.constraints = RigidbodyConstraints.FreezeAll; }
        bool hasGround = FindGroundBelow(20f, out RaycastHit ground);   // (none: e.g. hidden inside the ground)
        Island island = transform.parent != null ? transform.parent.GetComponentInParent<Island>() : null;
        if (island == null && hasGround) island = ground.transform.GetComponentInParent<Island>();
        if (island == null) return;
        Transform under = hasGround ? ground.transform : transform.parent;

        if (KeepsItsPlace)   // part of its island (it goes down and up with it), where it is
        {
            if (transform.parent == null || transform.parent.GetComponentInParent<Island>() != island)
            {
                FloatMotion floats = under.GetComponentInParent<FloatMotion>();
                transform.SetParent(floats != null ? floats.transform : island.transform, true);
            }
            lastIsland = FindParentWithTag(transform, "Isla") ?? island.gameObject;
            return;
        }

        // On the ground, not floating above it (Snaps To Ground off: exactly where it was placed)
        if (hasGround && SnapsToGround)
        {
            Collider shape = GetComponentInChildren<Collider>();
            float pivotToBottom = shape != null ? transform.position.y - shape.bounds.min.y : 0f;
            transform.position = ground.point + Vector3.up * (pivotToBottom + 0.02f);
        }

        if (transform.parent == null || transform.parent.GetComponentInParent<Island>() != island)
        {
            FloatMotion floating = under.GetComponentInParent<FloatMotion>();
            transform.SetParent(floating != null ? floating.transform : island.transform, true);
        }

        AttachStill();
        body.constraints = RigidbodyConstraints.FreezeAll;
        startIsland = island;
        lastIsland = FindParentWithTag(transform, "Isla") ?? island.gameObject;
    }

    // Stays still where it was placed until picked up (a representation that lies on branches, shelves…)
    protected virtual bool KeepsItsPlace => false;

    // At start it is set on the ground under it (the orbs). Off (MemeWindow with physics): it rides exactly where it was
    // placed, and once its island arrives it only falls if nothing holds it there
    protected virtual bool SnapsToGround => true;

    // Its island is up and still: physics again (it settles on the ground and freezes there as usual)
    private void ReleaseWhenIslandArrives()
    {
        if (startIsland == null) return;
        if (IsHeld || CurrentPedestal != null) { startIsland = null; return; }   // someone else took over
        if (!startIsland.IsUp || startIsland.IsMoving) return;

        startIsland = null;
        if (!SnapsToGround && Supported()) return;   // already resting where it was placed: it stays part of its island, still
        transform.SetParent(null, true);
        body.isKinematic = false;
        RestoreInterpolation();
        body.constraints = RigidbodyConstraints.None;
        groundedTime = 0f;
        OnReleasedFromIsland();
    }

    // Its island arrived: it is loose with physics now (MemeWindow: lies face up once still)
    protected virtual void OnReleasedFromIsland() { }

    // Part of something that moves (its island): kinematic and NOT interpolated. Interpolated, a kinematic body doesn't
    // follow its parent: it stays behind or jumps (it showed far above or below where it belongs)
    private RigidbodyInterpolation ownInterpolation;
    private void AttachStill()
    {
        if (body.interpolation != RigidbodyInterpolation.None) ownInterpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
    }

    // With physics again: its own interpolation back
    private void RestoreInterpolation()
    {
        if (ownInterpolation != RigidbodyInterpolation.None) body.interpolation = ownInterpolation;
    }

    // Its lowest point is on (or inside) the ground under it
    private bool Supported()
    {
        Collider shape = GetComponentInChildren<Collider>();
        if (shape == null) return false;
        Bounds bounds = shape.bounds;
        Vector3 from = new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);   // from above it: also when half buried
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, bounds.size.y + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.point.y > bounds.min.y - 0.1f) return true;
        return false;
    }

    // The nearest solid thing below that is not this meme
    private bool FindGroundBelow(float distance, out RaycastHit ground)
    {
        ground = default;
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.MaxValue;
    }

    protected virtual void OnEnable()
    {
        all.Add(this);
        PlayerMove.VoidFallStarted += OnPlayerVoidFall;
        PlayerMove.RespawnLanded += OnPlayerLanded;
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
        PlayerMove.VoidFallStarted -= OnPlayerVoidFall;
        PlayerMove.RespawnLanded -= OnPlayerLanded;
    }

    protected virtual void Update()
    {
        ReleaseWhenIslandArrives();
        FreezeWhenRestingOnGround();
        TrackGround();
        CheckFall();
        CheckLost();
        IgnorePlayer();
    }

    // The player never stands on a meme nor gets pushed up by one (dropping the window and jumping on it broke the
    // parkour): their collisions are ignored. Again every frame, because Unity forgets it when a collider is turned
    // off and on (carried, on a pedestal, a meteor…). Grabbing still works (it is a ray)
    private static CharacterController playerBody;
    private static float nextPlayerLookup;
    private Collider[] ownColliders;
    private void IgnorePlayer()
    {
        if (playerBody == null)
        {
            if (Time.time < nextPlayerLookup) return;
            nextPlayerLookup = Time.time + 1f;
            PlayerMove player = FindObjectOfType<PlayerMove>();
            if (player == null || !player.TryGetComponent(out playerBody)) return;
        }
        if (ownColliders == null) ownColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider c in ownColliders)
            if (c != null && c.enabled && !c.isTrigger) Physics.IgnoreCollision(playerBody, c, true);
    }

    // Instead of coming back as a meteor, back to the player's hand (MemeWindow). False: the meteor
    protected virtual bool ComesBackToPlayer() => false;

    // Instead of coming back as a meteor, it goes back where it was placed (an optional meme, DexCollectible)
    protected virtual bool ReturnsHome => false;
    private Transform homeParent;
    private Vector3 homePosition;
    private Quaternion homeRotation;

    private void GoHome()
    {
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        AttachStill();
        body.constraints = RigidbodyConstraints.FreezeAll;
        startIsland = null;
        transform.SetParent(homeParent, false);
        transform.localPosition = homePosition;
        transform.localRotation = homeRotation;
        lastGroundY = lastIslandY = transform.position.y;
    }

    // Safety net: far below the last island it was on (whatever kept it from respawning: it fell onto something down
    // there, or it was stuck), it comes back as a meteor. Says why in the console
    private void CheckLost()
    {
        if (IsHeld || CurrentPedestal != null || meteor != null || LevelManager.Instance == null) return;
        if (MemeRecall.Instance != null && MemeRecall.Instance.IsBringing) return;   // (flying to you)
        float below = lastIslandY - transform.position.y;
        if (below < LevelManager.Instance.dropDistance + 5f) return;
        string on = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) ? hit.collider.name : "nothing";
        Debug.LogWarning($"[Meme] '{name}' is {below:F0} m below its last island (kinematic {body.isKinematic}, constraints {body.constraints}, " +
                         $"parent '{(transform.parent != null ? transform.parent.name : "none")}', on '{on}', last ground {lastGroundY:F1}): respawning it.", this);
        lastGroundY = transform.position.y + LevelManager.Instance.dropDistance;   // (Respawn's own checks)
        Respawn();
    }

    // In the hand, the player's fall is its fall too (it travels with them)
    private void OnPlayerVoidFall() { if (IsHeld) OnFell(true); }
    private void OnPlayerLanded() { if (IsHeld) OnFallLanded(); }

    //---- What the player is doing (for the representations)

    protected static PlayerMove CurrentPlayer => player != null ? player : (player = FindObjectOfType<PlayerMove>());
    // The island the player stepped on last, and the one this meme is on (or was carried over last)
    protected GameObject PlayerIsland => CurrentPlayer != null ? CurrentPlayer.CurrentIsland : null;
    protected GameObject MemeIsland => lastIsland;
    // Times it has been carried onto an island other than the last one (not the tutorial ones, nor the letter ones
    // unless MemeSettings' Letter Islands Unlock is on)
    protected int IslandChanges { get; set; }
    // The course islands it has already been carried onto (each gives its Move once: going back gives nothing)
    protected readonly HashSet<GameObject> visitedIslands = new HashSet<GameObject>();

    // The player is this close and it is on their screen
    protected bool PlayerSees(float distance)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 toMeme = transform.position - cam.transform.position;
        if (toMeme.magnitude > distance) return false;
        Vector3 view = cam.WorldToViewportPoint(transform.position);
        return view.z > 0f && view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f;
    }

    //---- Called by the environment

    public void PickUp()
    {
        IsHeld = true;
        groundedTime = 0f;
        if (MemeDock.Instance == null || !MemeDock.Instance.OnScreen(this)) RestoreInterpolation();   // (on the screen: MemeDock handles it)
        OnPickedUp();
    }

    public void Drop()
    {
        if (!IsHeld) return;
        IsHeld = false;
        OnDropped();
    }

    // Pedestal: the meme snaps in (if it was in the hand, it counts as dropped first)
    public void PlaceOnPedestal(Pedestal pedestal)
    {
        Drop();
        CurrentPedestal = pedestal;
        OnPlaced(pedestal);
    }

    public void RemoveFromPedestal(Pedestal pedestal)
    {
        if (CurrentPedestal != pedestal) return;
        CurrentPedestal = null;
        OnRemovedFromPedestal(pedestal);
    }

    public virtual void OnPedestalResult(Pedestal pedestal, bool isCorrect) { }
    public virtual void Sweep(float duration) { }

    //---- Overridden by each representation

    protected virtual void OnBridgeCrossed() { }                  // carried over a new bridge
    protected virtual void OnIslandVisited(GameObject island, bool counts) { }    // carried onto an island other than the last one (counts: not the tutorial)
    protected virtual void OnFell(bool withPlayer) { }            // fell into the void: with the player (nothing below them: they will reappear) or alone (it just landed as a meteor)
    protected virtual void OnFallLanded() { }                     // in the hand after that fall: the player reappeared and touched the ground
    protected virtual void OnPickedUp() { }
    protected virtual void OnDropped() { }
    protected virtual void OnPlaced(Pedestal pedestal) { }
    protected virtual void OnRemovedFromPedestal(Pedestal pedestal) { }

    // Put down at a spot, lying still there as if it had been dropped and settled (LevelManager: the tutorial skipped)
    public void PlaceAt(Vector3 position, Quaternion rotation, Transform respawn)
    {
        if (IsHeld) return;
        if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        AttachStill();
        StickToGround();   // part of the island under it (it floats with it)
        lastGroundY = lastIslandY = position.y;
        if (respawn != null) SetRespawn(respawn);
    }

    // Sets the respawn by hand. The elevator uses it for the memes riding it
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;
    }

    //---- Where it is (before: PlayerMove tracked bridges and islands for the meme in the hand)

    // What is under it: its island (it respawns there) and, while carried, the new bridges and islands it is taken to.
    // Picked up on an island, that island doesn't count: only the ones you carry it to
    private void TrackGround()
    {
        if (LevelManager.Instance == null) return;
        if (body.isKinematic && !IsHeld) { lastGroundY = transform.position.y; return; }   // attached (island, pedestal): moves with it

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore)) return;
        lastGroundY = hit.point.y;
        if (FindParentWithTag(hit.transform, "Isla") != null || hit.transform.GetComponentInParent<Island>() != null) lastIslandY = hit.point.y;
        if (hit.transform == lastGroundHit) return;   // still over the same thing
        lastGroundHit = hit.transform;

        GameObject bridge = FindParentWithTag(hit.transform, "Puente");
        if (bridge != null && IsHeld && bridge != lastBridge)
        {
            lastBridge = bridge;
            OnBridgeCrossed();
        }

        GameObject island = FindParentWithTag(hit.transform, "Isla");
        if (island == null || island == lastIsland) return;   // a bridge, or the same island
        lastIsland = island;
        Transform point = LevelManager.Instance.RespawnPointOf(island);
        if (point != null) respawnPoint = point;
        if (!IsHeld) return;
        bool counts = Unlocks(island) && visitedIslands.Add(island);   // the tutorial, the hub, an optional one, or one it already visited: only a pause in the changes
        if (counts) IslandChanges++;
        OnIslandVisited(island, counts);
    }

    // The tutorial never unlocks a change; the letter islands (Island_A…, between bridges) only if set so
    public static bool Unlocks(GameObject island)
    {
        if (island.name.Contains("Tutorial")) return false;
        if (IslandRole.Of(island.transform) != IslandRole.Role.Course) return false;   // the hub, an optional course
        int separator = island.name.LastIndexOf('_');
        bool letter = separator >= 0 && island.name.Length - separator - 1 == 1 && char.IsLetter(island.name[separator + 1]);
        return !letter || Settings.letterIslandsUnlock;
    }

    //---- Respawn

    private void CheckFall()
    {
        if (IsHeld || body.isKinematic || LevelManager.Instance == null) return;   // in the hand: it falls with the player
        if (respawnPoint == null)   // the island it started on (before: always the tutorial)
            respawnPoint = lastIsland != null ? LevelManager.Instance.RespawnPointOf(lastIsland) : LevelManager.Instance.startRespawnPoint;
        if (respawnPoint == null) respawnPoint = LevelManager.Instance.startRespawnPoint;

        if (transform.position.y < lastGroundY - LevelManager.Instance.dropDistance) Respawn();
    }

    private void Respawn()
    {
        // Its island went down meanwhile (invisible, nothing to land on): to the start
        if (Lift.IsHiddenAt(respawnPoint)) { respawnPoint = LevelManager.Instance.startRespawnPoint; lastIsland = null; lastGroundHit = null; }
        if (respawnPoint == null) { Debug.LogWarning($"[Meme] '{name}' fell but has no respawn point (and no Start Respawn Point in the LevelManager).", this); return; }
        if (ReturnsHome) { GoHome(); return; }
        if (ComesBackToPlayer()) return;   // (Game: the meme window flies back to your hand, no meteor)
        if (respawnPoint == null || meteor != null) return;
        meteor = StartCoroutine(MeteorFall());
    }

    //---- Meteor

    IEnumerator MeteorFall()
    {
        transform.SetParent(null, true);
        startIsland = null;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        if (!FindLanding(out Vector3 start, out Vector3 landing)) { start = respawnPoint.position; landing = start; }   // (colliders still on: it measures itself)
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;   // no pedestal or tree catches it on the way

        // Hidden inside its rock while it falls (the rock is what you see tumbling)
        MeteorRock rock = MeteorRock.Spawn(this);
        var hidden = new System.Collections.Generic.List<Renderer>();
        if (rock.HasModel)
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r.enabled && !r.transform.IsChildOf(rock.transform)) { r.enabled = false; hidden.Add(r); }

        // Glow and trail while it falls
        Light glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = Settings.meteorColor;
        glow.range = 10f;
        glow.intensity = 6f;
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.startWidth = 0.7f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = Settings.meteorColor;
        trail.endColor = new Color(Settings.meteorColor.r, Settings.meteorColor.g * 0.5f, 0f, 0f);

        for (float t = 0f; t < Settings.meteorDuration; t += Time.deltaTime)
        {
            float p = t / Mathf.Max(Settings.meteorDuration, 0.01f);
            transform.position = Vector3.Lerp(start, landing, p * p);   // speeds up like a meteor
            transform.Rotate(Vector3.one * 720f * Time.deltaTime, Space.Self);
            yield return null;
        }
        transform.position = landing;

        // Impact: the ground breaks (pieces thrown up, like the bridges), the rock bursts and the meme comes out of it
        DebrisBurst.Spawn(Settings.impactDebris, Settings.impactDebrisCount, landing, Vector3.up * 2f, 6f, 0.8f);
        rock.Land(landing);
        if (Settings.impactClip != null) AudioSource.PlayClipAtPoint(Settings.impactClip, landing);
        foreach (Collider c in colliders) c.enabled = true;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;
        body.velocity = (landing - start).normalized * 2f;   // a little roll after the hit
        lastGroundY = lastIslandY = landing.y;
        groundedTime = 0f;
        trail.emitting = false;
        OnFell(false);

        // It shows again once the rock's cloud is around it, so you never see the swap
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            if (t >= 0.08f && hidden.Count > 0) { foreach (Renderer r in hidden) if (r != null) r.enabled = true; hidden.Clear(); }
            glow.intensity = Mathf.Lerp(6f, 0f, t / 0.4f);
            yield return null;
        }
        foreach (Renderer r in hidden) if (r != null) r.enabled = true;
        Destroy(glow);
        Destroy(trail, trail.time);
        meteor = null;
    }

    // Open ground of its island near the middle: not steep (tree tops, rocks), not near a pedestal, and a clear
    // diagonal path from the sky. A few random tries, so it doesn't always land on the same spot
    private bool FindLanding(out Vector3 start, out Vector3 landing)
    {
        start = landing = Vector3.zero;
        Collider shape = GetComponentInChildren<Collider>(true);
        float pivotToBottom = shape != null ? shape.bounds.extents.y : 0.3f;
        Vector3 middle = respawnPoint.position;

        for (int i = 0; i < 20; i++)
        {
            Vector2 offset = Random.insideUnitCircle * Settings.landingRadius;
            Vector3 above = new Vector3(middle.x + offset.x, middle.y + 5f, middle.z + offset.y);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit ground, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (lastIsland != null && FindParentWithTag(ground.transform, "Isla") != lastIsland) continue;
            if (ground.normal.y < 0.85f || NearPedestal(ground.point)) continue;

            Vector2 side = Random.insideUnitCircle.normalized;
            Vector3 land = ground.point + Vector3.up * (pivotToBottom + 0.02f);
            Vector3 from = land + Vector3.up * LevelManager.Instance.dropDistance + new Vector3(side.x, 0f, side.y) * Settings.meteorDrift;   // from as high as the player reappears
            Vector3 path = land - from;
            if (Physics.SphereCast(from, 0.5f, path.normalized, out _, path.magnitude - 0.8f, ~0, QueryTriggerInteraction.Ignore)) continue;   // trees, rocks in the way

            start = from;
            landing = land;
            return true;
        }
        return false;
    }

    private bool NearPedestal(Vector3 point)
    {
        foreach (Pedestal pedestal in FindObjectsOfType<Pedestal>())
        {
            Vector3 offset = pedestal.transform.position - point; offset.y = 0f;
            if (offset.magnitude < Settings.pedestalClearance) return true;
        }
        return false;
    }

    private static GameObject FindParentWithTag(Transform child, string tag)
    {
        for (Transform t = child.parent; t != null; t = t.parent)
            if (t.CompareTag(tag)) return t.gameObject;
        return null;
    }

    //---- Rest (before: OrbPosition)

    private void FreezeWhenRestingOnGround()
    {
        bool isResting = !IsHeld && CurrentPedestal == null && ReadyToRest && Physics.CheckSphere(transform.position, Settings.groundCheckRadius, groundMask);
        groundedTime = isResting ? groundedTime + Time.deltaTime : 0f;
        if (groundedTime >= Settings.restDelay && body.constraints != RigidbodyConstraints.FreezeAll)
        {
            body.constraints = RigidbodyConstraints.FreezeAll;
            StickToGround();
        }
    }

    // Done moving on its own (MemeWindow: once it lay down face up)
    protected virtual bool ReadyToRest => true;

    // Resting on an island (or a floating one: FloatMotion): it becomes part of it, still, so it goes down and up with
    // it (frozen) and bobs with the ground, instead of staying in the air when its island goes down
    private void StickToGround()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, Settings.groundCheckRadius + 1f, ~0, QueryTriggerInteraction.Ignore)) return;
        FloatMotion floating = ground.transform.GetComponentInParent<FloatMotion>();
        Island island = ground.transform.GetComponentInParent<Island>();
        if (floating == null && island == null) return;
        bool onIsland = island != null && (floating == null || !floating.transform.IsChildOf(island.transform));   // the one nearer the ground
        transform.SetParent(onIsland ? island.transform : floating.transform, true);
        AttachStill();   // PlayerLook turns it back to physics when picked up
    }
}
