using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One stone of a stone path (StonePath). Its Motion is what it does when the player uses it:
//   None   safe: it only rises with the path and floats with it (light grey, StonePath > Look)
//   Fall   stepped on, it warns (shake, crack) and drops, then rises back (Bridge > Falling Platforms values)
//   Slide  glides between its points, pausing at each one: ride it, or get pushed off the path if it hits you
//   Hop    stays at a point for Interval seconds, then jumps quickly to the next: time your jump
// Its points: its place (0) and its Shadows (1, 2…: ghosts of the stone, Memetica > Level > Stone Path > Add Shadow).
// In the Scene view stone 6's shadows are 61, 62… Move them anywhere; they float with the path and hide while playing.
// Route: the order it goes through its points (with one shadow they are all the same: place ↔ 61).
// Its look is a child, Model: one of the StonePath's Models, picked here (Model).
// StonePath places it every frame (its place on the path + what it is doing now), so it floats with the islands.
[ExecuteAlways]
public class PathStone : MonoBehaviour
{
    public enum Motion { None, Fall, Slide, Hop }
    public enum Route { BackAndForth, ThroughPlace, Loop, Random }

    [Tooltip("Its look: one of the StonePath's Models")]
    [StoneModel] public int model = 0;
    [Tooltip("None: safe (lighter). The rest: interactive (darker)")]
    public Motion motion = Motion.None;

    [Section("Points", nameof(motion), Motion.Slide, Motion.Hop)]
    [Tooltip("Back And Forth: 0 → 61 → 62 → 61 → 0…\nThrough Place: back to its place between shadows: 61 → 0 → 62 → 0 → 61…\n" +
             "Loop: 0 → 61 → 62 → 0 → 61… (always the same way)\nRandom: any other point each time")]
    public Route route = Route.BackAndForth;
    [Tooltip("Ghosts of the stone where it goes, in order (Memetica > Level > Stone Path > Add Shadow)")]
    public List<Transform> shadows = new List<Transform>();
    [Tooltip("Where it starts in its route (0-1), so stones next to each other don't move together")]
    [Range(0f, 1f)] public float startPhase = 0f;
    [Tooltip("Goes the other way: it rises in its place, then goes to its last point (in the time the others wait) and " +
             "from there backwards (e.g. the middle one of three hopping together: opposite). Start Phase isn't used")]
    public bool startAtEnd = false;
    [Tooltip("Pushes the player when it hits them (not when they stand on it)")]
    public bool pushPlayer = true;

    [Section("Slide", nameof(motion), Motion.Slide)]
    [Tooltip("Seconds from one point to the next")]
    public float travelTime = 2.5f;
    [Tooltip("Seconds it stops at each point")]
    public float pauseTime = 0.5f;

    [Section("Hop", nameof(motion), Motion.Hop)]
    [Tooltip("Seconds it stays at each point: x = y fixed, else random between them")]
    public Vector2 interval = new Vector2(1.5f, 1.5f);
    [Tooltip("Seconds the jump takes")]
    public float hopTime = 0.15f;

    // What StonePath reads every frame: between point From and point To (0 = its place, 1.. = its shadows)
    public int From { get; private set; }
    public int To { get; private set; }
    public float Progress { get; private set; }   // 0 at From, 1 at To
    public float Down { get; private set; }       // meters below its place (rising with the path, falling)
    public Vector3 Shake { get; private set; }    // the warning before a fall
    public bool IsPresent { get; private set; } = true;
    public bool Moves => (motion == Motion.Slide || motion == Motion.Hop) && PointCount > 1;
    int PointCount => 1 + shadows.Count;

    private enum FallState { Resting, Warning, Falling, Down, Rising }
    private FallState fallState = FallState.Resting;
    private float fallDown;     // meters fallen
    private float riseDown;     // meters below while rising with the path
    private Coroutine rising, falling;
    private Collider[] colliders;
    private Renderer[] renderers;
    private AudioSource audioSource;
    private StonePath path;
    private Bridge bridge;
    private bool moving;        // between two points (else waiting at From)
    private float timer;        // seconds left of the wait or the move
    private float moveDuration;
    private int direction = 1;  // Back And Forth
    private int lastShadow;     // Through Place

    void Awake()
    {
        path = GetComponentInParent<StonePath>();
        bridge = GetComponentInParent<Bridge>();
        colliders = System.Array.FindAll(GetComponentsInChildren<Collider>(true), c => !c.isTrigger);
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnEnable() => ApplyColor();
    void OnValidate() => ApplyColor();

    void Start()
    {
        if (!Application.isPlaying) return;
        shadows.RemoveAll(s => s == null);
        foreach (Transform shadow in shadows)
            foreach (Renderer r in shadow.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 3f;

        // Its route, already Start Phase along (from its last point, backwards, if Start At End)
        timer = WaitTime();
        if (startAtEnd && Moves) goToEnd = true;   // (once its bridge is up: it rises in its own place)
        else if (Moves) Advance(startPhase * RouteSteps * (AverageWait + MoveTime));
    }

    //---- Every frame (StonePath)

    public void Tick()
    {
        Down = riseDown + fallDown;   // (hidden too: it waits down there, so it never shows a frame up in its place)
        if (!IsPresent) return;
        bool settling = rising != null || (path != null && path.Appearing);   // its bridge still rising: it waits in place
        if (Moves && !settling && goToEnd) GoToEnd();
        if (Moves && !settling) Advance(Time.deltaTime);
        else if (!Moves) { From = To = 0; Progress = 0f; }
    }

    // Start At End, once up: to its last point while the others wait at theirs, then on at once, backwards
    void GoToEnd()
    {
        goToEnd = false;
        To = PointCount - 1;
        direction = -1;
        lastShadow = PointCount - 1;
        moving = true;
        moveDuration = Mathf.Clamp(MoveTime, 0.05f, Mathf.Max(timer, 0.05f));   // its usual move (squeezed into the wait if longer)
        endWait = Mathf.Max(0f, timer - moveDuration);                           // the rest of the wait, at the end
        timer = moveDuration;
        skipWait = true;
    }
    private bool goToEnd, skipWait;
    private float endWait;

    // Waits at a point, moves to the next one, waits there…
    void Advance(float seconds)
    {
        timer -= seconds;
        while (timer <= 0f)
        {
            if (moving) { moving = false; From = To; timer += skipWait ? endWait : WaitTime(); skipWait = false; }
            else { To = NextPoint(From); moving = true; moveDuration = MoveTime; timer += moveDuration; }
        }
        Progress = moving ? Smooth(1f - timer / moveDuration) : 0f;
    }

    int NextPoint(int current)
    {
        int count = PointCount;
        switch (route)
        {
            case Route.BackAndForth:
                if (current + direction >= count || current + direction < 0) direction = -direction;
                return current + direction;
            case Route.ThroughPlace:
                if (current != 0) return 0;
                lastShadow = lastShadow % (count - 1) + 1;
                return lastShadow;
            case Route.Loop:
                return (current + (startAtEnd ? -1 : 1) + count) % count;
            default:
                int next = Random.Range(0, count - 1);
                return next >= current ? next + 1 : next;
        }
    }

    // Moves in one whole round of its route
    int RouteSteps => route == Route.BackAndForth || route == Route.ThroughPlace ? 2 * (PointCount - 1) : PointCount;

    float WaitTime() => Mathf.Max(motion == Motion.Hop ? Interval() : pauseTime, 0.01f);
    float AverageWait => motion == Motion.Hop ? (interval.x + Mathf.Max(interval.x, interval.y)) * 0.5f : pauseTime;
    float MoveTime => Mathf.Max(motion == Motion.Hop ? hopTime : travelTime, 0.01f);
    float Interval() => interval.y > interval.x ? Random.Range(interval.x, interval.y) : interval.x;

    static float Smooth(float p) => p * p * (3f - 2f * p);

    //---- Rising and sinking with the path (StonePath)

    public void SetPresent(bool present)
    {
        IsPresent = present;
        foreach (Renderer r in renderers) if (r != null) r.enabled = present;
        SetSolid(present && fallState != FallState.Falling && fallState != FallState.Down);
    }

    // From Drop meters below to its place, passing it a little (Overshoot) and settling
    public void Rise(float duration, float overshoot, float drop)
    {
        if (rising != null) StopCoroutine(rising);
        rising = StartCoroutine(Rising(duration, overshoot, drop));
    }

    IEnumerator Rising(float duration, float overshoot, float drop)
    {
        SetPresent(true);
        float travel = duration * 0.8f, settle = duration * 0.2f;
        for (float t = 0f; t < travel; t += Time.deltaTime)
        {
            riseDown = Mathf.LerpUnclamped(drop, -overshoot, Smooth(t / travel));
            yield return null;
        }
        for (float t = 0f; t < settle; t += Time.deltaTime)
        {
            riseDown = Mathf.Lerp(-overshoot, 0f, Smooth(t / settle));
            yield return null;
        }
        riseDown = 0f;
        rising = null;
    }

    // Down to Drop meters below, speeding up, then hidden
    public void Sink(float duration, float drop)
    {
        if (rising != null) StopCoroutine(rising);
        rising = StartCoroutine(Sinking(duration, drop));
    }

    IEnumerator Sinking(float duration, float drop)
    {
        float from = riseDown;
        SetSolid(false);   // whoever stands on it falls
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            riseDown = Mathf.Lerp(from, drop, p * p);
            yield return null;
        }
        riseDown = drop;
        SetPresent(false);
        rising = null;
    }

    // Hidden, waiting below / in its place at once (debug starts)
    public void Place(bool present, float drop)
    {
        if (rising != null) { StopCoroutine(rising); rising = null; }
        riseDown = present ? 0f : drop;
        Down = riseDown + fallDown;
        SetPresent(present);
    }

    public bool IsRising => rising != null;

    // Its island rising turns back on what it had hidden (its shadows, a stone still down): kept as they should be
    private Renderer[] shadowRenderers;
    public void Enforce()
    {
        if (shadowRenderers == null)
        {
            var list = new List<Renderer>();
            foreach (Transform shadow in shadows) if (shadow != null) list.AddRange(shadow.GetComponentsInChildren<Renderer>(true));
            shadowRenderers = list.ToArray();
        }
        foreach (Renderer r in shadowRenderers) if (r != null && r.enabled) r.enabled = false;
        if (IsPresent) return;
        foreach (Renderer r in renderers) if (r != null && r.enabled) r.enabled = false;
        foreach (Collider c in colliders) if (c != null && c.enabled) c.enabled = false;
    }

    //---- Fall

    // The player is standing on it (PlayerMove)
    public void StepOn()
    {
        if (motion != Motion.Fall || !IsPresent || rising != null) return;
        if (fallState != FallState.Resting && fallState != FallState.Rising) return;
        if (falling != null) StopCoroutine(falling);
        falling = StartCoroutine(FallSequence());
    }

    IEnumerator FallSequence()
    {
        FallingPlatformSettings s = bridge != null ? bridge.platformSettings : new FallingPlatformSettings();
        float depth = path != null ? path.fallDepth : 8f;

        fallState = FallState.Warning;
        Play(s.crackClip);
        if (s.cameraShakeAmount > 0f && Camera.main != null && Camera.main.TryGetComponent(out PlayerLook look))
            look.StartCoroutine(look.ShakeTheCamera(0.15f, s.cameraShakeAmount));
        for (float t = 0f; t < s.warningTime; t += Time.deltaTime)
        {
            Vector2 jitter = Random.insideUnitCircle * s.shakeAmount;
            Shake = new Vector3(jitter.x, 0f, jitter.y);
            yield return null;
        }
        Shake = Vector3.zero;

        fallState = FallState.Falling;
        SetSolid(false);   // whoever is on it falls through
        Play(s.fallClip);
        float speed = 0f;
        while (fallDown < depth)
        {
            speed = Mathf.Min(speed + s.fallAcceleration * Time.deltaTime, s.maxFallSpeed);
            fallDown = Mathf.Min(fallDown + speed * Time.deltaTime, depth);
            yield return null;
        }

        fallState = FallState.Down;
        yield return new WaitForSeconds(s.returnDelay);

        fallState = FallState.Rising;
        SetSolid(true);
        Play(s.riseClip);
        while (fallDown > 0f)
        {
            fallDown = Mathf.MoveTowards(fallDown, 0f, s.riseSpeed * Time.deltaTime);
            yield return null;
        }
        fallState = FallState.Resting;
        falling = null;
    }

    //---- Shared

    void SetSolid(bool solid)
    {
        if (colliders == null) return;
        foreach (Collider c in colliders) if (c != null) c.enabled = solid;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    // Safe ones light grey, every interactive one the same darker grey (StonePath > Look)
    public void ApplyColor()
    {
        if (!Application.isPlaying || renderers == null || System.Array.Exists(renderers, r => r == null))
            renderers = GetComponentsInChildren<Renderer>(true);   // (its Model may have changed)
        StonePath owner = path != null ? path : GetComponentInParent<StonePath>();
        if (owner == null || !owner.tint) return;
        Color color = motion == Motion.None ? owner.safeColor : owner.interactiveColor;
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }
    }
}

// PathStone.model: a dropdown with the StonePath's Models (drawn by StonePathTool)
public class StoneModelAttribute : PropertyAttribute { }
