using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A pedestal: a meme goes in, it judges it and raises its way. This one (the black, stone one) takes any meme; the
// others are its kinds, each in its own script:
//   KeyPedestal         (white, marble) waits for one meme (its Key) and gives the clue on an easel (its Hint)
//   RandomizerPedestal  (black, the hub) gives the meme window a meme: a raffle, then that meme's way rises
// Optional, on the same object: PedestalRetire (used once, then into the ground), MemeDelivery (a meme falls onto it).
// Grouped pedestals (a PedestalGroup lists them) complete together, once all of them have a meme.
// What it raises: its Raises list, islands, elevators and bridges mixed, dragged in the order you want (each one is
// raised its own way: an island rises, an elevator starts its ride, a bridge appears). In Order: one after the other,
// each once the previous arrived; off: all at once. Taken off, its bridges go down again (not a randomizer's).
// Placing: a meme in your hand goes in when you look at the zone (the sphere around the Socket, within reach), not when
// it touches it. One that falls or is thrown into the zone goes in on its own. The meme window carried in the HUD
// (Game) flies from its HUD place to the socket (Snap Fly Time), then stops dead there.
// Reactions: icons that pop out above it and float up: question marks while it thinks, likes when it takes the meme,
// dislikes when it doesn't (your cursor pictures). Without them the Socket's old sparks play.
// Prefab: Pedestal (+ AudioSource + SphereCollider trigger centered on the Socket) > Visual_Keyed (white),
// Visual_Keyless (black), Socket (where the meme sits).
[RequireComponent(typeof(AudioSource), typeof(SphereCollider))]
[SelectionBase]   // clicking any of its parts in the Scene view selects this
public class Pedestal : MonoBehaviour
{
    [Section("What It Raises")]
    [Tooltip("Islands, elevators and bridges, in the order you want (drag them in): an island rises, an elevator starts " +
             "its ride, a bridge appears (and goes down again when its meme is taken off)")]
    public List<Transform> raises = new List<Transform>();
    [Tooltip("One after the other, in list order, each once the previous arrived. Off: all at once")]
    public bool inOrder = false;
    [Tooltip("Right: the game is won (the end)")]
    public bool completesGame = false;

    [Section("Sounds", Closed = true)]
    public AudioClip chargeClip;
    public AudioClip correctClip;
    public AudioClip incorrectClip;
    public AudioClip snapClip;
    [Tooltip("Game: seconds the meme window takes to fly from its HUD place to the socket")]
    public float snapFlyTime = 0.2f;

    [Section("Reactions", Closed = true)]
    [Tooltip("Icons (sprites) that pop out above it. Empty: the Socket's old sparks")]
    public Sprite like;
    public Sprite dislike;
    public Sprite question;
    [Tooltip("Meters above the socket where they come out (over the meme)")]
    public float reactionHeight = 1.2f;
    [Tooltip("Meters wide each icon")]
    public float reactionSize = 0.45f;
    [Tooltip("Meters around that point they come out from")]
    public float reactionSpread = 0.5f;
    [Tooltip("Meters per second they rise")]
    public float reactionRise = 1f;
    [Tooltip("Seconds each one lasts")]
    public float reactionLifetime = 1.3f;
    [Tooltip("Like / Dislike: how many at once")]
    public int reactionBurst = 5;
    [Tooltip("Question: how many per second while it thinks")]
    public float questionRate = 3f;
    [Tooltip("Order in Layer: over the meme window (its title is 10)")]
    public int reactionOrder = 20;

    [Section("Break (elevator pedestal)", Closed = true)]
    [Tooltip("Thud when it hits the ground on its side")]
    public AudioClip breakClip;
    [Tooltip("Seconds it takes to tip over (heavy stone: slow start, then it drops)")]
    public float toppleTime = 1.1f;
    [Tooltip("How fast it scrapes along the ground toward the edge after tipping over (m/s²)")]
    public float slideAcceleration = 2.5f;

    [Section("References", Closed = true)]
    public Transform socket;
    public GameObject visualKeyed;
    public GameObject visualKeyless;

    // Old (before the kinds): Memetica > Check > Migrate Pedestals moves them into the new fields and kinds.
    // Kept until it ran in every scene; meanwhile a pedestal still with names raises them by name
    [HideInInspector] public bool KeylessPedestal = false;
    [HideInInspector] public string KeyID;
    [HideInInspector] public MemeData keyMeme;
    [HideInInspector] public bool drainMeme = false;
    [HideInInspector] public string[] ControlledIslands = new string[0];
    [HideInInspector] public string[] ControlledBridges = new string[0];
    [HideInInspector] public string groupID;
    [HideInInspector] public Transform cluePainting;

    // State (read only from outside)
    public GameObject MemeObject { get; private set; }
    public Meme CurrentMeme { get; private set; }   // null if the object has no Meme component
    public bool HasMeme => MemeObject != null;
    public bool IsCorrect { get; private set; }
    // The easel with its clue (KeyPedestal), or null
    public virtual Transform Hint => null;

    protected static readonly List<Pedestal> all = new List<Pedestal>();

    protected AudioSource audioSource;
    protected SphereCollider trigger;
    protected PedestalRetire retire;
    protected PedestalGroup group;
    ParticleSystem sparks;
    ParticleSystem likes, dislikes, questions;
    Coroutine evaluating;
    bool rejecting;
    bool broken;
    bool groupCompleted;
    GameObject releasedMeme;   // just taken off: not snapped again until it leaves the trigger
    bool seating;              // the meme window flying from the HUD to the socket
    RigidbodyInterpolation seatedInterpolation = RigidbodyInterpolation.Interpolate;   // what it had before (given back when let go)
    protected bool everRight;  // it was right once (no more auto snapping)

    // The player's hand (its camera: Eye, not the MainCamera tag: other cameras may have it)
    protected static PlayerGrab hand => PlayerGrab.Instance;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        trigger = GetComponent<SphereCollider>();
        sparks = socket != null ? socket.GetComponent<ParticleSystem>() : null;
        retire = GetComponent<PedestalRetire>();
        MakeReactions();
        ApplyVisual();
        SyncTrigger();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    protected virtual void Start()
    {
        group = PedestalGroup.Of(this);
    }

    // The pedestal whose clue is this painting (or one of its parents), or null
    public static Pedestal WithClue(Transform painting)
    {
        foreach (Pedestal p in all)
        {
            Transform hint = p.Hint;
            if (hint != null && (hint == painting || hint.IsChildOf(painting) || painting.IsChildOf(hint))) return p;
        }
        return null;
    }

    //---- What it raises

    // Something it raises (an island, elevator or bridge): PlayerGrab's recall asks
    public bool Raises(Transform place)
    {
        if (place == null) return false;
        if (raises.Contains(place)) return true;
        LevelManager level = LevelManager.Instance;
        if (level == null) return false;
        foreach (string id in ControlledIslands) if (level.FindIsland(id) == place) return true;   // (old: by name)
        foreach (string id in ControlledBridges) if (level.FindBridge(id) == place) return true;
        return false;
    }

    bool ByName => raises.Count == 0 && (ControlledIslands.Length > 0 || ControlledBridges.Length > 0);

    void RaiseWay()
    {
        LevelManager level = LevelManager.Instance;
        if (completesGame && level != null) level.CompleteGame();
        if (ByName)   // (old: not migrated yet)
        {
            if (inOrder) level.RaiseInOrder(ControlledIslands, ControlledBridges);
            else
            {
                foreach (string id in ControlledIslands) level.SubirIsla(id);
                foreach (string id in ControlledBridges) level.SubirPuente(id);
            }
            return;
        }
        if (inOrder) { StartCoroutine(RaiseInOrder()); return; }
        foreach (Transform t in raises) RaiseOne(t);
    }

    IEnumerator RaiseInOrder()
    {
        float gap = LevelManager.Instance != null ? LevelManager.Instance.sequenceGap : 0.3f;
        foreach (Transform t in raises)
        {
            if (!RaiseOne(t)) continue;
            yield return null;
            while (Arriving(t)) yield return null;
            yield return new WaitForSeconds(gap);
        }
    }

    //---- One of what it raises: what it is decides how (an island, an elevator or a bridge)

    // Raises it its own way, in Seconds (0: its own time). False: it's none of them (or empty)
    public static bool RaiseOne(Transform t, float seconds = 0f)
    {
        if (t == null) return false;
        if (t.TryGetComponent(out Elevator elevator)) { elevator.StartRide(); return true; }
        if (t.TryGetComponent(out Lift lift)) { if (seconds > 0f) lift.RaiseIn(seconds); else lift.Raise(); return true; }   // (an island or a bridge)
        return false;
    }

    // Still on its way up (rising, appearing, riding)
    public static bool Arriving(Transform t)
    {
        if (t == null) return false;
        if (t.TryGetComponent(out Bridge bridge)) return bridge.IsAppearing || bridge.IsMoving;
        if (t.TryGetComponent(out Elevator elevator)) return elevator.IsRiding;
        return t.TryGetComponent(out Lift lift) && lift.IsMoving;
    }

    // Something a pedestal can raise
    public static bool IsRaisable(Transform t) => t != null && (t.GetComponent<Lift>() != null || t.GetComponent<Elevator>() != null);

    protected void LowerBridges()
    {
        foreach (Transform t in raises) if (t != null && t.TryGetComponent(out Bridge bridge)) bridge.Lower();
        if (LevelManager.Instance != null) foreach (string id in ControlledBridges) LevelManager.Instance.BajarPuente(id);   // (old)
    }

    //---- What each kind decides

    // Its way goes down when the meme is taken off (a black one: always; a white one: if it was its key; a randomizer: never)
    protected virtual bool LowersWhenTakenOff(GameObject meme) => true;

    // Judges the meme that just went in (after its snap): a black one takes anything
    protected virtual IEnumerator Judge()
    {
        Play(chargeClip);
        SetCorrect();
        yield break;
    }

    // Right: anything else it does (a white one: into the MemeDex, the meme downloaded)
    protected virtual void OnRight() { }

    // With a PedestalRetire set to Result, it goes into the ground as soon as it is right (a randomizer: after its way)
    protected virtual bool RetiresOnResult => true;

    // PlayerGrab's Snap In Range: carried within reach (In Range), does it go in without looking? A black one: only when
    // you bring it in, not when you pick it up already near it
    protected virtual bool AutoSnapsWhen(GameObject inHand, bool inRange)
    {
        bool entering = inRange && !(rangeMeme == inHand && rangeWas);
        rangeMeme = inHand;
        rangeWas = inRange;
        return entering && hand.snapOnBlack;
    }
    GameObject rangeMeme;   // (the meme in your hand was already in range last frame)
    bool rangeWas;

    // Auto snap again after it was right once (a randomizer that goes down: every round it is back up)
    protected virtual bool AutoSnapsAgain => false;

    // Is the visual the white one?
    protected virtual bool IsWhite => false;

    //---- Auto snap

    bool AutoSnap(GameObject inHand)
    {
        PlayerGrab assist = hand;
        if (assist == null || !assist.AutoSnaps) return false;
        if (everRight && !AutoSnapsAgain) return false;
        return AutoSnapsWhen(inHand, Vector3.Distance(assist.Eye.position, socket.position) <= assist.pickUpRange + ZoneRadius);
    }

    float ZoneRadius => trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

    // Takes memes: not broken, and not in the ground (PedestalRetire)
    bool Usable => !broken && (retire == null || retire.IsUp);

    //---- A meme goes in / out

    // Loose (falling, thrown) into the zone: in. In the hand: only by looking (Update)
    void OnTriggerEnter(Collider other)
    {
        if (!Usable || HasMeme || other.GetComponent<Meme>() == null || other.gameObject == releasedMeme) return;
        if (hand != null && hand.heldObject == other.gameObject) return;
        Snap(other.gameObject);
    }

    // A meme brought by a script (MemeDelivery): seated as if it had been placed. False if it can't take it now
    public bool Receive(GameObject memeObject)
    {
        if (!Usable || HasMeme || memeObject == null) return false;
        Snap(memeObject);
        return true;
    }

    // The camera looks at the zone (the sphere around the Socket), within reach
    bool LookedAt()
    {
        Transform cam = hand.Eye;
        float radius = ZoneRadius;
        Vector3 toSocket = socket.position - cam.position;
        float along = Vector3.Dot(toSocket, cam.forward);
        if (along < 0f || along > hand.pickUpRange + radius) return false;
        return (toSocket - cam.forward * along).sqrMagnitude <= radius * radius;
    }

    void Snap(GameObject memeObject)
    {
        bool fromHud = MemeDock.Instance != null && memeObject.TryGetComponent(out Meme inHud) && MemeDock.Instance.OnScreen(inHud);
        Vector3 size = hand != null ? hand.SnapObject(memeObject, fromHud ? snapFlyTime : 0f) : memeObject.transform.lossyScale;
        MemeObject = memeObject;   // taken: nothing else snaps in meanwhile
        if (fromHud) { seating = true; evaluating = StartCoroutine(FlyIn(memeObject, size)); return; }
        Seat(memeObject);
    }

    // From the HUD place to the socket, growing back to its size in the world; it stops dead there
    IEnumerator FlyIn(GameObject memeObject, Vector3 size)
    {
        if (memeObject.TryGetComponent(out Rigidbody flying)) flying.isKinematic = true;
        Transform t = memeObject.transform;
        Vector3 from = t.position, fromSize = t.localScale;
        for (float time = 0f; time < snapFlyTime; time += Time.deltaTime)
        {
            float p = 1f - (1f - time / snapFlyTime) * (1f - time / snapFlyTime);   // fast, then slowing down
            t.position = Vector3.Lerp(from, socket.position, p);
            t.localScale = Vector3.Lerp(fromSize, size, p);
            yield return null;
        }
        t.localScale = size;
        seating = false;
        Seat(memeObject);
    }

    void Seat(GameObject memeObject)
    {
        memeObject.transform.position = socket.position;
        memeObject.transform.SetParent(socket);   // moves with the pedestal (floating islands, elevators, the turning platform)
        if (memeObject.TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = true;
            if (body.interpolation != RigidbodyInterpolation.None) seatedInterpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;   // (interpolated, it lags behind a pedestal that moves)
        }
        if (sparks != null && like == null) sparks.Play();   // (the reactions replace the old sparks)

        MemeObject = memeObject;
        CurrentMeme = memeObject.GetComponent<Meme>();
        if (CurrentMeme != null) CurrentMeme.PlaceOnPedestal(this);
        Play(snapClip);
        evaluating = StartCoroutine(Evaluate());
    }

    // Taken off (picked up, pushed out by a rejection) is checked here, not with OnTriggerExit:
    // turning a meme's physics on or off makes Unity report a fake exit and entry at once
    protected virtual void Update()
    {
        if (hand == null) return;
        if (releasedMeme != null && !InsideZone(releasedMeme) && !LookedAt()) releasedMeme = null;   // it really left: it can be placed again
        if (!HasMeme)
        {
            // In the hand: in when you look at the zone
            GameObject inHand = hand.heldObject;
            if (Usable && inHand != null && inHand != releasedMeme && inHand.GetComponent<Meme>() != null && (LookedAt() || AutoSnap(inHand))) Snap(inHand);
            return;
        }

        if (seating) return;   // still flying in
        bool held = CurrentMeme != null && CurrentMeme.IsHeld;
        bool movedAway = Vector3.Distance(MemeObject.transform.position, socket.position) > 0.5f;
        if (held || movedAway) Release();
    }

    void Release()
    {
        releasedMeme = MemeObject;   // not snapped again until it leaves the zone
        if (evaluating != null) StopCoroutine(evaluating);
        Think(false);
        bool lower = LowersWhenTakenOff(MemeObject);
        bool done = IsCorrect;

        if (MemeDock.Instance == null || !MemeDock.Instance.OnScreen(CurrentMeme))   // (carried to the HUD: MemeDock takes care of it)
            if (MemeObject.TryGetComponent(out Rigidbody body)) body.interpolation = seatedInterpolation;
        if (CurrentMeme != null) CurrentMeme.RemoveFromPedestal(this);
        MemeObject = null;
        CurrentMeme = null;
        IsCorrect = false;
        if (!rejecting && !broken && !(done && retire != null)) Play(incorrectClip);   // (pushed out by us: its "wrong" already sounded)
        rejecting = false;

        if (done && retire != null) { retire.Retire(); return; }   // used: into the ground, its way stays up
        if (lower && !broken) LowerBridges();
        OnTakenOff();
    }

    // Its meme was taken off (a randomizer stops a raffle in the middle)
    protected virtual void OnTakenOff() { }

    bool InsideZone(GameObject meme) => Vector3.Distance(meme.transform.position, transform.TransformPoint(trigger.center)) < ZoneRadius + 0.2f;

    IEnumerator Evaluate()
    {
        yield return new WaitForSeconds(Length(snapClip));

        // Grouped: charging until the whole group has memes
        if (group != null || IsGroupedByName)
        {
            Play(chargeClip, true);
            Think(true);   // waiting for the rest of the group
            yield return new WaitForSeconds(Length(chargeClip));
            if (group != null) group.CompleteIfFull();
            else CompleteGroupByName();
            yield break;
        }
        yield return Judge();
    }

    //---- Group (PedestalGroup; old: by Group ID)

    bool IsGroupedByName => group == null && !string.IsNullOrEmpty(groupID);

    // The whole group has memes (PedestalGroup)
    public void CompleteFromGroup()
    {
        if (groupCompleted) return;
        groupCompleted = true;
        SetCorrect();
    }

    void CompleteGroupByName()
    {
        List<Pedestal> members = all.FindAll(p => p.groupID == groupID);
        if (members.Exists(p => !p.HasMeme)) return;
        foreach (Pedestal member in members) member.CompleteFromGroup();
    }

    //---- Results

    protected void SetCorrect()
    {
        IsCorrect = true;
        everRight = true;
        Play(correctClip);
        Like();
        OnRight();
        if (retire != null && retire.when == PedestalRetire.When.Result && RetiresOnResult) retire.Retire();
        RaiseWay();
        if (CurrentMeme != null) CurrentMeme.OnPedestalResult(this, true);
    }

    protected void SetIncorrect()
    {
        Play(incorrectClip);
        Dislike();
        LowerBridges();
        if (CurrentMeme != null) CurrentMeme.OnPedestalResult(this, false);
        Reject();
    }

    // Lets its meme go with a little hop, no "wrong" sound (a pedestal going into the ground: PedestalRetire)
    public void DropMeme()
    {
        if (MemeObject == null || seating) return;
        GameObject meme = MemeObject;
        rejecting = true;   // (no "incorrect")
        meme.transform.SetParent(null, true);
        Release();
        if (meme.TryGetComponent(out Rigidbody rb))
        {
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.AddForce(transform.forward * 60f + transform.up * 220f);
        }
    }

    void Reject()
    {
        if (MemeObject == null) return;
        rejecting = true;
        MemeObject.transform.SetParent(null);
        Rigidbody rb = MemeObject.GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;
        rb.AddForce(transform.forward * 100f + transform.up * 300f);
    }

    protected void Play(AudioClip clip, bool loop = false)
    {
        audioSource.loop = loop;
        audioSource.clip = clip;
        if (clip != null) audioSource.Play();
    }

    protected static float Length(AudioClip clip) => clip != null ? clip.length : 0f;

    //---- Reactions: icons that pop out above it and float up (like the orb's puffs)

    void Like() { Think(false); Burst(likes); }
    void Dislike() { Think(false); Burst(dislikes); }

    // Question marks while it thinks
    protected void Think(bool on)
    {
        if (questions == null) return;
        var emission = questions.emission;
        emission.rateOverTime = on ? questionRate : 0f;
        if (on && !questions.isPlaying) questions.Play();
    }

    void Burst(ParticleSystem ps)
    {
        if (ps == null) return;
        if (!ps.isPlaying) ps.Play();
        ps.Emit(reactionBurst);
    }

    void MakeReactions()
    {
        Transform at = socket != null ? socket : transform;
        likes = MakeReaction("Likes", like, at);
        dislikes = MakeReaction("Dislikes", dislike, at);
        questions = MakeReaction("Questions", question, at);
    }

    // A system of flat icons facing the camera, floating up and away (world space: they stay as the pedestal moves)
    ParticleSystem MakeReaction(string systemName, Sprite sprite, Transform at)
    {
        if (sprite == null) return null;
        var go = new GameObject(systemName);
        go.transform.SetParent(at, false);
        go.transform.localPosition = at.InverseTransformVector(Vector3.up * reactionHeight);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // its circle lies flat
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(reactionLifetime * 0.8f, reactionLifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(reactionSize * 0.8f, reactionSize * 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);   // a little tilted, each its own way
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;   // sizes in meters, whatever the pedestal's scale
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = reactionSpread;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(reactionRise * 0.7f, reactionRise);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var sizeOver = ps.sizeOverLifetime;   // pops in small, swells, shrinks away
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1.15f), new Keyframe(0.35f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        var sheet = ps.textureSheetAnimation;   // the sprite itself (its part of an atlas too)
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(sprite);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Sprites/Default")) { name = systemName, mainTexture = sprite.texture };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = reactionOrder;
        return ps;
    }

    //---- Look and trigger

    public void ApplyVisual()
    {
        SetActive(visualKeyed, IsWhite);
        SetActive(visualKeyless, !IsWhite);
    }

    public void SyncTrigger()
    {
        if (trigger == null) trigger = GetComponent<SphereCollider>();
        if (trigger == null || socket == null) return;
        Vector3 center = transform.InverseTransformPoint(socket.position);
        if (trigger.isTrigger && trigger.center == center) return;
        Record(trigger);
        trigger.isTrigger = true;
        trigger.center = center;
    }

    void SetActive(GameObject go, bool on)
    {
        if (go == null || go.activeSelf == on) return;
        Record(go);
        go.SetActive(on);
    }

    // In the editor it records the change (Ctrl+Z and prefab overrides). Playing it does nothing
    static void Record(Object obj)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        UnityEditor.Undo.RecordObject(obj, "Pedestal");
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (obj != null) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        };
#endif
    }

#if UNITY_EDITOR
    // Moving the Socket (or changing its kind): the model and the trigger update by themselves
    protected virtual void OnValidate()
    {
        foreach (Transform t in raises)
            if (t != null && !IsRaisable(t)) Debug.LogWarning($"[Pedestal] '{name}': '{t.name}' in Raises isn't an island, an elevator or a bridge (it does nothing).", this);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || UnityEditor.EditorUtility.IsPersistent(this)) return;
            ApplyVisual();
            SyncTrigger();
        };
    }

    // The zone (where a meme goes in) around the Socket; selected: a line to everything it raises
    protected virtual void OnDrawGizmos()
    {
        if (socket == null) return;
        SphereCollider col = GetComponent<SphereCollider>();
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float radius = col != null ? col.radius * scale : 0.2f;
        Gizmos.color = IsWhite ? Color.white : new Color(0.2f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(socket.position, radius);
        Gizmos.DrawSphere(socket.position, 0.03f);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (socket == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        foreach (Transform t in raises) if (t != null) Gizmos.DrawLine(socket.position, AimPlatform.CenterOf(t));
    }
#endif

    //---- Break (elevator pedestal)
    // A heavy stone giving way: it always tips over on its side toward the nearest edge, scrapes along the ground until
    // it is over the void, and falls off with gravity, slowly rolling over the edge, until it is gone (Drop Distance
    // below). It touches nothing: nobody gets pushed.
    public void Break()
    {
        if (broken) return;
        broken = true;

        if (evaluating != null) StopCoroutine(evaluating);
        seating = false;
        trigger.enabled = false;
        if (sparks != null) sparks.Stop();
        Think(false);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        transform.SetParent(null, true);   // not riding the elevator any more

        Vector3 start = Center();
        StartCoroutine(FallOff(NearestEdge(start), start.y));
    }

    IEnumerator FallOff(Vector3 dir, float startY)
    {
        // 1. Tips over its base edge on that side, speeding up like a falling weight
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(transform.position, Vector3.one);
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);
        float edge = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
        Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z) + dir * edge;
        Vector3 axis = Vector3.Cross(Vector3.up, dir);

        float angle = 0f;
        for (float t = 0f; t < toppleTime; t += Time.deltaTime)
        {
            float target = 90f * Mathf.Pow(t / toppleTime, 2.5f);
            transform.RotateAround(pivot, axis, target - angle);
            angle = target;
            yield return null;
        }
        transform.RotateAround(pivot, axis, 90f - angle);
        if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, pivot);

        // 2. Scrapes toward the edge until its middle is over the void
        float speed = 0f;
        for (float t = 0f; t < 15f && HasGround(Center()); t += Time.deltaTime)
        {
            speed = Mathf.Min(speed + slideAcceleration * Time.deltaTime, 3f);
            transform.position += dir * speed * Time.deltaTime;
            yield return null;
        }

        // 3. Off the edge: gravity, rolling slowly over it
        if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.velocity = dir * Mathf.Max(speed, 0.5f);
        body.angularVelocity = axis * 1.2f;

        float bottom = startY - LevelManager.Instance.dropDistance;
        while (transform.position.y > bottom) yield return null;
        gameObject.SetActive(false);
    }

    // Middle of its meshes (no particles)
    Vector3 Center()
    {
        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>();
        if (rends.Length == 0) return transform.position;
        Bounds b = rends[0].bounds;
        foreach (MeshRenderer r in rends) b.Encapsulate(r.bounds);
        return b.center;
    }

    // Of 16 directions, the one with the void closest (a gap at least 2 m long)
    Vector3 NearestEdge(Vector3 from)
    {
        Vector3 best = Vector3.forward;
        float nearest = float.MaxValue;
        for (int k = 0; k < 16; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(k * 22.5f, Vector3.up) * Vector3.forward;
            float d = VoidDistance(from, dir);
            if (d < nearest) { nearest = d; best = dir; }
        }
        return best;
    }

    // How far along dir there's nothing under (2 m of void in a row), up to 40 m
    float VoidDistance(Vector3 from, Vector3 dir)
    {
        const float step = 0.5f;
        float gapStart = -1f;
        for (float d = 0f; d <= 40f; d += step)
        {
            if (HasGround(from + dir * d)) { gapStart = -1f; continue; }
            if (gapStart < 0f) gapStart = d;
            if (d - gapStart >= 2f) return gapStart;
        }
        return 40f;
    }

    bool HasGround(Vector3 point)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider is CharacterController || hit.transform.IsChildOf(transform)) continue;   // the player, itself
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;                           // memes
            return true;
        }
        return false;
    }
}
