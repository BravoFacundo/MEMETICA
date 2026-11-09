using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base of everything that rises from the void and can go back down: Island and Bridge.
// Moves by duration, not speed: it takes the same time no matter how far it travels.
// Rising: starts slow, passes the top a little (Overshoot) and settles back. Lowering: speeds up as it sinks.
// Timing comes from the LevelManager (Lift section) unless Override Timing is on.
// Place it in the scene where it ends up (its final place): that is its top, and it waits the LevelManager's
// fall height (Top Y - Bottom Y) below it. So islands and bridges can be designed and tested all up.
// Inside something that moves (the tutorial bridge in its floating island) its place moves with it: it rises to
// where it was placed on its parent, wherever the parent is now.
// Down (waiting below): invisible and you fall through it (its renderers, colliders, lights and grass are off).
// It shows again as soon as it starts rising.
// Role: what it is for the meme window
//   Course    each new one you carry the window onto gives it its next Move (going back to one already visited: nothing)
//   Hub       never gives a Move (you start and come back here)
//   Optional  an optional course (raised by a black pedestal): no Move, no Leave, and your window, left on its black
//             pedestal, doesn't come back to you while you are here (PlayerGrab's Recall)
public abstract class Lift : MonoBehaviour
{
    public enum Role { Course, Hub, Optional }

    [Section("Role")]
    [Tooltip("Course: part of a level's way. Hub: where you start and come back. Optional: a side course (its things inside count as it too)")]
    public Role role = Role.Course;

    [Section("Rise & Fall")]
    [Tooltip("Off: uses the LevelManager values. On: the values below")]
    public bool overrideTiming = false;
    [ShowIf(nameof(overrideTiming))] public float riseDuration = 3f;
    [ShowIf(nameof(overrideTiming))] public float lowerDuration = 2.5f;
    [Tooltip("Meters it passes the top before settling")]
    [ShowIf(nameof(overrideTiming))] public float overshoot = 0.4f;
    public AudioClip riseClip;
    public AudioClip arriveClip;

    public bool IsUp { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsHidden { get; private set; }

    // The role of whatever that is part of: the first Hub or Optional among the lifts around it (a bridge inside an
    // optional island is optional too), else Course (nothing set, or not in a lift: Course)
    public static Role RoleOf(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
            if (p.TryGetComponent(out Lift lift) && lift.role != Role.Course) return lift.role;
        return Role.Course;
    }

    // Something inside a lift that is down (hidden): a respawn there would fall forever
    public static bool IsHiddenAt(Transform t)
    {
        Lift lift = t != null ? t.GetComponentInParent<Lift>() : null;
        return lift != null && lift.IsHidden;
    }

    protected LevelManager level;
    protected AudioSource audioSource;
    private Coroutine moving;

    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Collider> hiddenColliders = new List<Collider>();
    private readonly List<Light> hiddenLights = new List<Light>();
    private Vector3 designedPlace;   // where it was placed in the scene (on its parent) = where it ends up
    private FloatMotion floating;  // on the same object (Island_NN floating itself): the lift moves where it floats around

    protected virtual void Awake()
    {
        designedPlace = transform.localPosition;
        floating = GetComponent<FloatMotion>();
    }

    // Height without the floating
    protected float CurrentHeight => floating != null ? floating.RestHeight : transform.position.y;

    protected virtual void Start()
    {
        level = LevelManager.Instance;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (riseClip != null || arriveClip != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        AudioManager.SetUp(audioSource, AudioManager.World.Lift);   // heard near, not from the next course
    }

    //---- Called by the LevelManager

    public virtual void Raise()
    {
        if (IsUp) return;
        IsUp = true;
        SetPresent(true);
        StartMove(RiseDuration, true);
    }

    // Rises taking these seconds (from now on), whatever its timing says (RandomizerPedestal's timing)
    public void RaiseIn(float seconds)
    {
        riseFor = seconds;
        Raise();
    }
    private float riseFor = -1f;

    public virtual void Lower()
    {
        if (!IsUp) return;
        IsUp = false;
        StartMove(LowerDuration, false);
    }

    // Debug: already up, no animation (DebugTools starts)
    public virtual void SnapUp()
    {
        if (moving != null) { StopCoroutine(moving); moving = null; }
        IsMoving = false;
        IsUp = true;
        SetPresent(true);
        SetHeight(TopY);
    }

    //---- Heights and timing

    // Height (world) of its designed place now: follows a parent that moves
    protected float DesignedY => transform.parent != null ? transform.parent.TransformPoint(designedPlace).y : designedPlace.y;
    protected virtual float TopY => DesignedY;
    protected float BottomY => DesignedY - DropFrom(level);
    // How far below its place it waits (the LevelManager's Drop Distance; a bridge can change it)
    protected virtual float DropFrom(LevelManager lm) => lm.dropDistance;
    protected float RiseDuration => riseFor > 0f ? riseFor : overrideTiming ? riseDuration : level.liftRiseDuration;
    protected float LowerDuration => overrideTiming ? lowerDuration : level.liftLowerDuration;
    protected float Overshoot => overrideTiming ? overshoot : level.liftOvershoot;

    //---- Movement

    // Rising: to Top Y. Lowering: to Bottom Y
    protected void StartMove(float duration, bool rising)
    {
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(MoveRoutine(duration, rising));
    }

    // Heights are measured from the target each frame, so it lands right even if its parent moved meanwhile
    IEnumerator MoveRoutine(float duration, bool rising)
    {
        IsMoving = true;
        OnMoveStarted(rising);
        Play(rising ? riseClip : null);

        float Target() => rising ? TopY : BottomY;
        float start = CurrentHeight - Target();
        duration = Mathf.Max(duration, 0.01f);
        float settleTime = rising && Overshoot > 0f ? Mathf.Min(0.6f, duration * 0.25f) : 0f;
        float travelTime = duration - settleTime;
        float travelEnd = rising ? Overshoot : 0f;

        // Travel: smooth start and end when rising, speeding up when lowering
        bool arrived = false;
        for (float t = 0f; t < travelTime; t += Time.deltaTime)
        {
            if (rising && !arrived && t >= travelTime - ArriveLead) { arrived = true; OnArriving(); }
            float p = t / travelTime;
            float eased = rising ? p * p * (3f - 2f * p) : p * p;
            SetHeight(Target() + Mathf.LerpUnclamped(start, travelEnd, eased));
            yield return null;
        }

        if (rising && !arrived) OnArriving();

        // Settle: back down from the overshoot
        for (float t = 0f; t < settleTime; t += Time.deltaTime)
        {
            float p = t / settleTime;
            SetHeight(Target() + Mathf.Lerp(travelEnd, 0f, p * p * (3f - 2f * p)));
            yield return null;
        }

        SetHeight(Target());
        IsMoving = false;
        if (rising) Play(arriveClip);
        else if (HidesWhenDown) SetPresent(false);
        OnMoveFinished(rising);
        moving = null;
    }

    protected void SetUp(bool up) => IsUp = up;   // for subclasses that appear in their own way (Bridge: Assemble)

    protected void SetHeight(float y)
    {
        if (floating != null) floating.SetRestHeight(y);
        else transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    protected void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, AudioManager.WorldVolume);
    }

    // Its own meshes: not those of a lift inside it, a respawn point or a portal. Ground only (an island): just its
    // ground, the widest meshes (half the widest's footprint or more), not what stands on it (trees, rocks, pedestals):
    // its top is the ground's level. Widest Mesh: its ground's main mesh (its look)
    protected bool OwnBounds(bool groundOnly, out Bounds b, out MeshRenderer widestMesh)
    {
        b = default;
        widestMesh = null;
        var own = new List<MeshRenderer>();
        float widest = 0f;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
        {
            if (r.GetComponentInParent<Lift>() != this) continue;
            if (r.GetComponentInParent<RespawnPoint>() != null || r.GetComponentInParent<PortalWindow>() != null) continue;
            own.Add(r);
            if (Footprint(r) > widest) { widest = Footprint(r); widestMesh = r; }
        }
        bool any = false;
        foreach (MeshRenderer r in own)
        {
            if (groundOnly && Footprint(r) < widest * 0.5f) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }

    static float Footprint(Renderer r) => r.bounds.size.x * r.bounds.size.z;

    //---- Hidden while down

    // Bridges that extend from A hide in their own way
    protected virtual bool HidesWhenDown => true;

    // Off: everything inside it that shows or can be touched, remembering what was on (only that comes back)
    protected void SetPresent(bool present)
    {
        if (present != IsHidden) return;   // already so
        IsHidden = !present;
        if (!present)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; hiddenColliders.Add(c); }
            foreach (Light l in GetComponentsInChildren<Light>(true)) if (l.enabled) { l.enabled = false; hiddenLights.Add(l); }
        }
        else
        {
            foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (Collider c in hiddenColliders) if (c != null) c.enabled = true;
            foreach (Light l in hiddenLights) if (l != null) l.enabled = true;
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
            hiddenLights.Clear();
        }
        foreach (GrassField grass in GetComponentsInChildren<GrassField>(true)) grass.Hidden = !present;
    }

#if UNITY_EDITOR
    // Scene view (LevelManager > Debug > Show Drop Gizmos). An island: a see-through dark blue cylinder its ground's
    // size where it waits hidden. A bridge: a box and a line.
    // Measured from its own meshes only (not the islands inside it, its respawn points or its portals)
    static LevelManager gizmoLevel;
    void OnDrawGizmos()
    {
        if (Application.isPlaying || !HidesWhenDown) return;
        if (gizmoLevel == null) gizmoLevel = FindObjectOfType<LevelManager>();
        if (gizmoLevel == null || !gizmoLevel.showDropGizmos) return;
        if (!OwnBounds(this is Island, out Bounds b, out _)) return;

        float drop = DropFrom(gizmoLevel);
        Vector3 hidden = b.center + Vector3.down * drop;
        if (!(this is Island))   // a bridge: a box where it waits, and a line up to its place
        {
            Gizmos.color = Dark;
            Gizmos.DrawWireCube(hidden, b.size);
            Gizmos.color = Light;
            Gizmos.DrawLine(hidden, b.center);
            return;
        }

        float radius = Mathf.Max(b.extents.x, b.extents.z);

        // The island where it waits: dark blue, see-through all over (Unity's cylinder, sized by its own mesh), solid edges
        Vector3 top = hidden + Vector3.up * b.extents.y, bottom = hidden - Vector3.up * b.extents.y;
        if (cylinder == null) cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Gizmos.color = new Color(Dark.r, Dark.g, Dark.b, 0.35f);
        Bounds unit = cylinder.bounds;
        Vector3 scale = new Vector3(radius * 2f / unit.size.x, b.size.y / unit.size.y, radius * 2f / unit.size.z);
        Gizmos.DrawMesh(cylinder, hidden - Vector3.Scale(unit.center, scale), Quaternion.identity, scale);
        UnityEditor.Handles.color = Dark;
        UnityEditor.Handles.DrawWireDisc(top, Vector3.up, radius, 3f);
        UnityEditor.Handles.DrawWireDisc(bottom, Vector3.up, radius, 3f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 side = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius;
            UnityEditor.Handles.DrawLine(top + side, bottom + side, 3f);
        }
    }

    static readonly Color Dark = new Color(0.1f, 0.25f, 0.8f, 1f);     // where it waits
    static readonly Color Light = new Color(0.3f, 0.6f, 1f, 0.9f);     // a bridge's line up to its place
    static Mesh cylinder;


#endif

    // Hooks for Island and Bridge (camera shake, effects)
    protected virtual void OnMoveStarted(bool rising) { }
    protected virtual void OnMoveFinished(bool rising) { }
    // Rising: as it reaches the top (before settling back from the overshoot), Arrive Lead seconds early
    protected virtual void OnArriving() { }
    protected virtual float ArriveLead => 0f;
}
