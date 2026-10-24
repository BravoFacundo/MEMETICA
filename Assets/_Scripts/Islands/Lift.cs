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
public abstract class Lift : MonoBehaviour
{
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
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 10f;
        }
    }

    //---- Called by the LevelManager

    public virtual void Raise()
    {
        if (IsUp) return;
        IsUp = true;
        SetPresent(true);
        StartMove(RiseDuration, true);
    }

    // Rises taking these seconds (from now on), whatever its timing says (MemeRandomizer's timing)
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
        for (float t = 0f; t < travelTime; t += Time.deltaTime)
        {
            float p = t / travelTime;
            float eased = rising ? p * p * (3f - 2f * p) : p * p;
            SetHeight(Target() + Mathf.LerpUnclamped(start, travelEnd, eased));
            yield return null;
        }

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
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

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
    // Scene view (LevelManager > Debug > Show Drop Gizmos): a shadow where it waits hidden
    static LevelManager gizmoLevel;
    void OnDrawGizmos()
    {
        if (Application.isPlaying || !HidesWhenDown) return;
        if (gizmoLevel == null) gizmoLevel = FindObjectOfType<LevelManager>();
        if (gizmoLevel == null || !gizmoLevel.showDropGizmos) return;

        Renderer[] parts = GetComponentsInChildren<Renderer>();
        if (parts.Length == 0) return;
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        Vector3 down = Vector3.down * DropFrom(gizmoLevel);
        Gizmos.color = new Color(0.45f, 0.7f, 1f, 0.15f);
        Gizmos.DrawCube(b.center + down, b.size);
        Gizmos.color = new Color(0.45f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(b.center + down, b.size);
        Gizmos.DrawLine(b.center, b.center + down);
    }
#endif

    // Hooks for Island and Bridge (camera shake, effects)
    protected virtual void OnMoveStarted(bool rising) { }
    protected virtual void OnMoveFinished(bool rising) { }
}
