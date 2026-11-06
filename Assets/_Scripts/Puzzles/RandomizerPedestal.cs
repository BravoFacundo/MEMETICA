using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The randomizer (the hub): a black pedestal that gives the meme window its meme (before: MemeRandomizer on a
// Pedestal). Put a meme window (empty or not) on it: the axes still to draw go by on the window, faster and then slower,
// it stops on one at random (or always on Force Result), the window becomes that meme (MemeChanger.SetMeme) and that
// axis' Way rises: islands, elevators and bridges mixed, dragged in the order you want.
// Timing: the platform turns (Turn Time) with the pedestal up, the pedestal slams in, the target (Aim At, else the
// first island) rises; once it arrived the next islands and elevators start in their order (Island Gap apart,
// overlapping). A bridge waits until everything before it arrived, then appears; the next one once it is in place.
// You can walk from the first bridge on (no bridges: once the islands arrived). Timeline (in the Inspector) adds it all up for each axis.
// Drawn axes don't come out again; once every axis was drawn, all of them can again.
// On an AimPlatform (the pedestal inside it): after the raffle the platform turns toward the winner's target, and then
// its way rises in front of you. With a PedestalRetire it slams into the ground first (meme and all), so you see it on
// the horizon. Lock Player: you can only look around until the whole way is up.
public class RandomizerPedestal : Pedestal
{
    [System.Serializable]
    public class Axis
    {
        public MemeData meme;
        [Tooltip("Islands, elevators and bridges that rise when this axis comes out, in the order you want (drag them " +
                 "in). The first island is the target, unless Aim At")]
        public List<Transform> way = new List<Transform>();
        [Tooltip("Where the platform points when this axis comes out (empty: the first island)")]
        public Transform aimAt;
        [HideInInspector] public string[] sequence = new string[0];   // (old: names, moved into Way by Migrate Pedestals)
    }

    static bool IsBridge(Transform t) => t != null && t.GetComponent<Bridge>() != null;

    [Section("Axes")]
    public List<Axis> axes = new List<Axis>();
    [Tooltip("Always this one comes out (the raffle still shows them all going by). Empty: at random")]
    public MemeData forceResult;

    [Section("Raffle")]
    [Tooltip("Seconds the images go by before it stops")]
    public float raffleTime = 3f;
    [Tooltip("Each image going by (empty: MemeSettings' sweep tick)")]
    public AudioClip tickClip;
    [Tooltip("When it stops on the winner (empty: MemeSettings' change sound). The winner's grab sound plays too")]
    public AudioClip winClip;

    [Section("Timing (after the raffle)")]
    [Tooltip("Seconds the platform takes to turn (0: its own Turn Speed)")]
    public float turnTime = 3f;
    [Tooltip("Seconds the target island takes to rise")]
    public float targetRise = 2.5f;
    [Tooltip("Seconds after the target arrived that the next island starts")]
    public float islandsAfter = 0.1f;
    [Tooltip("Seconds each of the other islands takes to rise")]
    public float islandRise = 2f;
    [Tooltip("Seconds between one island starting and the next")]
    public float islandGap = 0.5f;
    [Tooltip("Seconds after the last island arrived that the first bridge starts")]
    public float bridgesAfter = 0.2f;
    [Tooltip("Seconds each bridge takes to rise (Rise From Below; a stone path: its own StonePath > Appearance)")]
    public float bridgeRise = 1.5f;
    [Tooltip("Seconds after one bridge is in place before the next one starts")]
    public float bridgeGap = 0.1f;
    [Tooltip("When each part happens, added up for each axis (seconds from placing the meme)")]
    [ReadOnlyField, TextArea(2, 12)] public string timeline;

    [Section("Snap & Stand (on its platform)")]
    [Tooltip("Carrying a meme this close to the platform's middle (meters, flat), it snaps in by itself (PlayerGrab's " +
             "Snap In Range). 0: the usual reach from the pedestal. A bit less than the platform's radius: only once you are on it")]
    public float snapRadius = 0f;
    [Tooltip("When the raffle starts you walk by yourself to Stand Point, looking at the meme on the pedestal: you see " +
             "the raffle, then the platform turns you toward your way")]
    public bool walkToStand = true;
    [Tooltip("Where you end up (a child of the platform). Empty: Stand Distance behind the pedestal, opposite the " +
             "platform's front: once it turns, you look past the pedestal at your way")]
    public Transform standPoint;
    [Tooltip("Meters from the pedestal where you stand (no Stand Point)")]
    public float standDistance = 1f;
    [Tooltip("Meters per second of that walk (slow: you see yourself taken there)")]
    public float standWalkSpeed = 1.5f;

    [Section("After The Raffle")]
    [Tooltip("The player can't walk (only look around) from the raffle until its first bridge starts")]
    public bool lockPlayer = true;
    [Tooltip("Your view stays near what matters (PlayerLook Focus): the meme window while it raffles and the platform " +
             "turns, at its height (the horizon: when the pedestal goes down you keep looking ahead), then your way's " +
             "first island as the pedestal starts going down and the island rises. You can look around it, not away. " +
             "Free again once you can walk")]
    public bool focusView = true;
    [Tooltip("Degrees you can look away from it. -1: PlayerLook's Focus Radius")]
    public float focusRadius = -1f;
    [Tooltip("A small zoom meanwhile (field of view, e.g. 70). 0: none")]
    public float focusFov = 0f;

    // Its way started rising and you can walk again (GameManager: the virus alert comes then)
    public static event System.Action WayOpened;
    // From the raffle until its pedestal went down (or the platform stopped): the crosshair hides meanwhile (RetroCursor)
    public static bool Busy { get; private set; }

    private readonly List<Axis> drawn = new List<Axis>();
    private AimPlatform platform;
    private bool raffling;
    private PlayerMove held;

    protected override void Awake()
    {
        base.Awake();
        platform = GetComponentInParent<AimPlatform>();
    }

    void OnDestroy() => Busy = false;

    //---- As a pedestal

    // It doesn't judge, it gives: the raffle, then right (its axis' way already rose)
    protected override IEnumerator Judge()
    {
        Think(true);
        yield return Draw(CurrentMeme);
        SetCorrect();
    }

    protected override bool LowersWhenTakenOff(GameObject meme) => false;   // the way it opened stays
    protected override bool RetiresOnResult => false;                        // (it goes down during its way: OpenWay)
    protected override bool AutoSnapsAgain => retire != null;                // (it goes down and comes back each round)

    protected override bool AutoSnapsWhen(GameObject inHand, bool inRange) => snapRadius > 0f ? InSnapArea(hand.Eye.position) : inRange;

    // Taken off while it raffles: free again
    protected override void Update()
    {
        base.Update();
        if (raffling && !HasMeme) { raffling = false; Hold(false); Busy = false; ReleaseView(); }
    }

    //---- The raffle

    IEnumerator Draw(Meme meme)
    {
        if (!(meme is MemeChanger window)) yield break;   // a plain meme (the tutorial orb): nothing to show it on
        List<Axis> usable = axes.FindAll(a => a.meme != null && MemeImage.Exists(a.meme.baseImage));
        if (usable.Count == 0) { Debug.LogWarning($"[RandomizerPedestal] '{name}' has no axes with a meme (MemeData with a base image).", this); yield break; }

        List<Axis> left = usable.FindAll(a => !drawn.Contains(a));
        if (left.Count == 0) { drawn.Clear(); left = usable; }

        Axis winner = forceResult != null ? usable.Find(a => a.meme == forceResult) : null;
        if (forceResult != null && winner == null)
            Debug.LogWarning($"[RandomizerPedestal] '{name}': Force Result '{forceResult.name}' isn't one of its axes: at random.", this);
        if (winner != null) { if (!left.Contains(winner)) left.Add(winner); }
        else
        {
            List<Axis> pool = left.Count > 1 ? left.FindAll(a => a.meme != window.Data) : left;   // never the one it holds, if there is another
            winner = pool[Random.Range(0, pool.Count)];
        }

        // You stay on the platform from the raffle until your way starts (Lock Player)
        Hold(true);
        horizonY = MemeTarget().y;
        if (focusView && PlayerLook.Instance != null) PlayerLook.Instance.Focus(HorizonTarget, focusRadius, focusFov);   // (once the walk to the stand lets go of the head)
        Busy = true;   // (the crosshair hides until the pedestal goes down)
        if (walkToStand) StartCoroutine(WalkToStand());
        raffling = true;
        yield return window.Raffle(left.ConvertAll(a => a.meme), winner.meme, raffleTime, tickClip, winClip);
        raffling = false;
        if (window.Data != winner.meme) { Hold(false); Busy = false; ReleaseView(); yield break; }   // taken off halfway

        drawn.Add(winner);
        StartCoroutine(OpenWay(winner));   // its own (taking the meme off now doesn't stop it); "right" meanwhile
    }

    void Hold(bool on)
    {
        if (!lockPlayer) return;
        if (held == null) held = FindObjectOfType<PlayerMove>();
        if (held != null) held.InputLocked = on;
    }

    void ReleaseView() { if (focusView && PlayerLook.Instance != null) PlayerLook.Instance.ReleaseFocus(); }

    // The meme window where it turns, at the height it had when the raffle started (going down, you keep the horizon)
    float horizonY;
    Vector3 HorizonTarget()
    {
        Vector3 at = MemeTarget();
        at.y = horizonY;
        return at;
    }

    // The island it turned to and that rises (its middle, as it moves). None: the horizon stays
    void FocusIsland(Transform target)
    {
        if (!focusView || target == null || PlayerLook.Instance == null) return;
        Vector3 middle = AimPlatform.CenterOf(target) - target.position;
        PlayerLook.Instance.Focus(() => target != null ? target.position + middle : HorizonTarget(), focusRadius, focusFov);
    }

    // Snap Radius: you (the camera) are close enough to the platform's middle
    public bool InSnapArea(Vector3 position)
    {
        Vector3 d = position - Middle;
        d.y = 0f;
        return d.magnitude <= snapRadius;
    }

    Vector3 Middle => platform != null ? platform.transform.position : transform.position;

    // An inevitable little walk to Stand Point, turning to face the pedestal (you can't steer meanwhile)
    IEnumerator WalkToStand()
    {
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player == null) yield break;
        PlayerLook look = PlayerLook.Instance;
        if (look != null) look.LookLocked = true;

        player.AutoWalkSpeed = standWalkSpeed;
        for (float time = 0f; time < 8f; time += Time.deltaTime)
        {
            Vector3 stand = StandPosition(player.transform.position);   // (it turns with the platform)
            player.AutoWalkTo = stand;
            Vector3 to = stand - player.transform.position;
            to.y = 0f;
            // Looking at the meme (on the pedestal): turning the body to it, and the head up or down to it
            Vector3 target = MemeTarget();
            Vector3 face = target - player.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, Quaternion.LookRotation(face, Vector3.up), 240f * Time.deltaTime);
            if (look != null)
            {
                Vector3 sight = target - look.Head.position;
                float flat = new Vector2(sight.x, sight.z).magnitude;
                float pitch = Mathf.Atan2(-sight.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg;   // (down: positive)
                look.Pitch = Mathf.MoveTowards(look.Pitch, pitch, 120f * Time.deltaTime);
            }
            bool facing = face.sqrMagnitude <= 0.01f || Vector3.Angle(player.transform.forward, face) < 2f;
            if (to.magnitude < 0.05f && facing) break;
            yield return null;
        }
        player.AutoWalkTo = null;
        if (look != null) look.LookLocked = false;   // (you can look around again; walking waits for your way)
    }

    // The meme on it (the middle of what you see of it), or its socket
    Vector3 MemeTarget()
    {
        if (MemeObject != null)
        {
            Renderer shape = MemeObject.GetComponentInChildren<Renderer>();
            return shape != null ? shape.bounds.center : MemeObject.transform.position;
        }
        return socket != null ? socket.position : transform.position;
    }

    // Stand Point, or behind the pedestal opposite the platform's front (it turns with it); no platform: on your side
    Vector3 StandPosition(Vector3 from)
    {
        if (standPoint != null) return standPoint.position;
        Vector3 pedestal = transform.position;
        Vector3 away = platform != null ? -platform.Front : from - pedestal;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        return pedestal + away.normalized * standDistance;
    }

    //---- Its way

    // Into the ground (if it retires), the platform turns toward it (if on one), then its way rises
    IEnumerator OpenWay(Axis winner)
    {
        PlayerMove player = lockPlayer ? FindObjectOfType<PlayerMove>() : null;
        if (player != null) player.InputLocked = true;

        // Turns with the pedestal up, then the pedestal slams in, facing the target
        Transform target = Target(winner);
        if (platform != null)
        {
            if (target != null) yield return platform.AimAt(AimPlatform.CenterOf(target), turnTime);
            else Debug.LogWarning($"[RandomizerPedestal] '{name}': nothing to point the platform at for '{winner.meme.name}' (no islands in its axis, or set Aim At).", this);
        }
        FocusIsland(target);   // (as the pedestal starts going down: your eyes are already on the way)
        if (retire != null)
        {
            retire.Retire();
            while (retire.IsMoving) yield return null;
        }
        Busy = false;   // the pedestal is down (or the platform stopped): the crosshair again

        List<Transform> way = Way(winner);
        var up = new List<Transform>();   // islands and elevators raised so far
        bool walking = false;
        for (int i = 0; i < way.Count; i++)
        {
            Transform t = way[i];
            bool nextIsland = i + 1 < way.Count && !IsBridge(way[i + 1]);
            if (!IsBridge(t))
            {
                bool isTarget = up.Count == 0 && !walking;
                RaiseOne(t, isTarget ? targetRise : islandRise);
                up.Add(t);
                if (isTarget)   // the target: the rest once it arrived
                {
                    yield return null;
                    for (float time = 0f; time < 15f && Arriving(t); time += Time.deltaTime) yield return null;
                    if (nextIsland && islandsAfter > 0f) yield return new WaitForSeconds(islandsAfter);
                }
                else if (nextIsland && islandGap > 0f) yield return new WaitForSeconds(islandGap);
                continue;
            }

            // A bridge: once everything before it arrived. The first one lets you walk (the rest go on while you go)
            yield return null;
            for (float time = 0f; time < 15f && up.Exists(Arriving); time += Time.deltaTime) yield return null;
            if (!walking)
            {
                if (bridgesAfter > 0f) yield return new WaitForSeconds(bridgesAfter);
                walking = true;
                LetWalk(player);
            }
            Bridge bridge = t.GetComponent<Bridge>();
            bridge.waitForIslands = false;   // (the islands are up: this timing waited for them)
            RaiseOne(t, bridgeRise);
            yield return null;
            for (float time = 0f; time < 15f && Arriving(t); time += Time.deltaTime) yield return null;
            if (i < way.Count - 1 && bridgeGap > 0f) yield return new WaitForSeconds(bridgeGap);
        }
        if (!walking)   // no bridges: once the islands arrived
        {
            yield return null;
            for (float time = 0f; time < 15f && up.Exists(Arriving); time += Time.deltaTime) yield return null;
            LetWalk(player);
        }
    }

    // Free to walk (and look): its way is opening
    void LetWalk(PlayerMove player)
    {
        if (player != null) player.InputLocked = false;
        ReleaseView();
        WayOpened?.Invoke();
    }

    // Its axis' way, in order (an axis still with names: found by name)
    List<Transform> Way(Axis axis)
    {
        List<Transform> way = axis.way.FindAll(t => t != null);
        if (way.Count > 0 || axis.sequence == null) return way;
        LevelManager level = LevelManager.Instance;   // (old: by name)
        foreach (string entry in axis.sequence)
        {
            if (!level.Resolve(entry, out string id, out bool isIsland)) continue;
            Transform t = isIsland ? level.FindIsland(id) : level.FindBridge(id);
            if (t != null) way.Add(t);
        }
        return way;
    }

    // The first island of that meme's axis (CourseGuide: which level it belongs to), or null
    public Transform FirstIslandOf(MemeData data)
    {
        if (data == null) return null;
        Axis axis = axes.Find(a => a.meme == data);
        return axis != null ? Way(axis).Find(t => !IsBridge(t)) : null;
    }

    Transform Target(Axis axis)
    {
        if (axis.aimAt != null) return axis.aimAt;
        List<Transform> way = Way(axis);
        Transform island = way.Find(t => !IsBridge(t));
        return island != null ? island : way.Count > 0 ? way[0] : null;
    }
#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        timeline = Timeline();
    }

    // Seconds from placing the meme to each part, for each axis (an estimate)
    string Timeline()
    {
        var text = new System.Text.StringBuilder();
        PedestalRetire slam = GetComponent<PedestalRetire>();
        bool turns = GetComponentInParent<AimPlatform>() != null;
        foreach (Axis axis in axes)
        {
            List<Transform> way = axis.way.FindAll(t => t != null);
            if (way.Count == 0) continue;
            float t0 = raffleTime;
            text.Append($"{(axis.meme != null ? axis.meme.name : "?")}: raffle 0-{t0:0.#}");
            if (turns) { t0 += turnTime; text.Append($" · turn →{t0:0.#}{(turnTime > 0f ? "" : " (its speed)")}"); }
            if (slam != null && slam.mode == PedestalRetire.Mode.Slammed) { t0 += slam.delay + slam.slamTime; text.Append($" · slam →{t0:0.#}"); }

            // Islands overlap (Island Gap apart, after the target); a bridge waits for all before it, then the next one
            float clock = t0, allUp = t0, walk = -1f;
            bool first = true;
            for (int i = 0; i < way.Count; i++)
            {
                if (!IsBridge(way[i]))
                {
                    float rise = first ? targetRise : islandRise;
                    allUp = Mathf.Max(allUp, clock + rise);
                    clock = first ? clock + targetRise + islandsAfter : clock + islandGap;
                    first = false;
                    continue;
                }
                float start = Mathf.Max(clock, allUp) + (walk < 0f ? bridgesAfter : bridgeGap);
                if (walk < 0f) walk = start;
                float bridgeTime = bridgeRise;
                if (way[i].TryGetComponent(out StonePath stones)) bridgeTime = Mathf.Max(0, stones.Stones.Count - 1) * stones.stoneGap + stones.riseTime;
                allUp = clock = start + bridgeTime;
            }
            if (walk < 0f) walk = allUp;
            text.Append($" · {way.Count} part(s)  →  walk at {walk:0.#} s, all up at {allUp:0.#} s\n");
        }
        return text.ToString().TrimEnd();
    }

    // Selected: where you will stand, and a line from the platform to each axis' target (what it will point at)
    protected override void OnDrawGizmosSelected()
    {
        AimPlatform aim = GetComponentInParent<AimPlatform>();
        if (aim == null) return;
        if (snapRadius > 0f)   // Snap Radius: a circle around the platform's middle
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f);
            Vector3 c = aim.transform.position + Vector3.up * 0.1f;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f, b = (i + 1) * Mathf.PI * 2f / 48f;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * snapRadius, c + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * snapRadius);
            }
        }
        Gizmos.color = new Color(0.4f, 1f, 0.6f);
        Vector3 stand = standPoint != null ? standPoint.position : transform.position - aim.Front * standDistance;
        Gizmos.DrawWireSphere(new Vector3(stand.x, transform.position.y, stand.z), 0.4f);
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        foreach (Axis axis in axes)
        {
            Transform target = axis.aimAt;
            if (target == null) target = axis.way.Find(t => t != null && !IsBridge(t));
            if (target == null) continue;
            Vector3 center = AimPlatform.CenterOf(target);
            Gizmos.DrawLine(aim.transform.position, center);
            Gizmos.DrawWireSphere(center, 1f);
            if (axis.meme != null) UnityEditor.Handles.Label(center + Vector3.up * 1.5f, axis.meme.name);
        }
    }
#endif
}
