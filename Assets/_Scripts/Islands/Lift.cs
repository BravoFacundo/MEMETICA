using System.Collections;
using UnityEngine;

// Base of everything that rises from the void and can go back down: Island and Bridge.
// Moves by duration, not speed: it takes the same time no matter how far it travels.
// Rising: starts slow, passes the top a little (Overshoot) and settles back. Lowering: speeds up as it sinks.
// Timing comes from the LevelManager (Lift section) unless Override Timing is on.
// Place it in the scene where it ends up (its final place): that is its top, and it waits the LevelManager's
// fall height (Top Y - Bottom Y) below it. So islands and bridges can be designed and tested all up.
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

    protected LevelManager level;
    protected AudioSource audioSource;
    private Coroutine moving;

    protected float designedY;   // where it was placed in the scene = where it ends up
    private FloatMotion floating;  // on the same object (Island_NN floating itself): the lift moves where it floats around

    protected virtual void Awake()
    {
        designedY = transform.position.y;
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
        StartMove(TopY, RiseDuration, true);
    }

    public virtual void Lower()
    {
        if (!IsUp) return;
        IsUp = false;
        StartMove(BottomY, LowerDuration, false);
    }

    // Debug: already up, no animation (DebugTools starts)
    public virtual void SnapUp()
    {
        if (moving != null) { StopCoroutine(moving); moving = null; }
        IsMoving = false;
        IsUp = true;
        SetHeight(TopY);
    }

    //---- Heights and timing

    protected virtual float TopY => designedY;
    protected virtual float BottomY => designedY - (level.topY - level.bottomY);
    protected float RiseDuration => overrideTiming ? riseDuration : level.liftRiseDuration;
    protected float LowerDuration => overrideTiming ? lowerDuration : level.liftLowerDuration;
    protected float Overshoot => overrideTiming ? overshoot : level.liftOvershoot;

    //---- Movement

    protected void StartMove(float targetY, float duration, bool rising)
    {
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(MoveRoutine(targetY, duration, rising));
    }

    IEnumerator MoveRoutine(float targetY, float duration, bool rising)
    {
        IsMoving = true;
        OnMoveStarted(rising);
        Play(rising ? riseClip : null);

        float startY = CurrentHeight;
        duration = Mathf.Max(duration, 0.01f);
        float settleTime = rising && Overshoot > 0f ? Mathf.Min(0.6f, duration * 0.25f) : 0f;
        float travelTime = duration - settleTime;
        float travelTarget = rising ? targetY + Overshoot : targetY;

        // Travel: smooth start and end when rising, speeding up when lowering
        for (float t = 0f; t < travelTime; t += Time.deltaTime)
        {
            float p = t / travelTime;
            float eased = rising ? p * p * (3f - 2f * p) : p * p;
            SetHeight(Mathf.LerpUnclamped(startY, travelTarget, eased));
            yield return null;
        }

        // Settle: back down from the overshoot
        for (float t = 0f; t < settleTime; t += Time.deltaTime)
        {
            float p = t / settleTime;
            SetHeight(Mathf.Lerp(travelTarget, targetY, p * p * (3f - 2f * p)));
            yield return null;
        }

        SetHeight(targetY);
        IsMoving = false;
        if (rising) Play(arriveClip);
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

    // Hooks for Island and Bridge (camera shake, effects)
    protected virtual void OnMoveStarted(bool rising) { }
    protected virtual void OnMoveFinished(bool rising) { }
}
