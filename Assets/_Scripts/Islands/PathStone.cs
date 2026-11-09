using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One stone of a stone path (StonePath). Its Motion is what it does when the player uses it:
//   None   safe: it only rises with the path and floats with it
//   Fall   stepped on, it warns (shake, crack) and drops, then rises back (Bridge > Falling Platforms values)
//   Slide  glides between its points, pausing at each one: ride it, or get pushed off the path if it hits you
//   Blink  stays at a point for Interval seconds, shakes for Warning Time, vanishes (whoever stands on it falls) and
//          shows up at the next point after Gap Time: it can't carry you there, only timing works
// How each kind looks and sounds (tint, mark, sounds): Resources/StoneTypes, the same for every stone of that kind.
// And what tells them apart in play (StoneTypes): a Fall stone always shows cracks, and comes apart as it drops; a Slide
// one leaves a winding trail and drops pieces as it goes; a Blink one shows where it will appear (a rock grows there).
// Its points: its place (0) and its Shadows (1, 2…: ghosts of the stone, Memetica > Level > Stone Path > Add Shadow).
// In the Scene view stone 6's shadows are 61, 62… Move them anywhere; they float with the path and hide while playing.
// Route: the order it goes through its points (with one shadow they are all the same: place ↔ 61).
// Its look is a child, Model: one of the StonePath's Models, picked here (Model).
// StonePath places it every frame (its place on the path + what it is doing now), so it floats with the islands.
[ExecuteAlways]
[SelectionBase]   // clicking any of its parts in the Scene view selects this
public class PathStone : MonoBehaviour
{
    // Numbers fixed: they are what the scene saves (3 was Hop, removed: never used)
    public enum Motion { None = 0, Fall = 1, Slide = 2, Blink = 4 }
    public enum Route { BackAndForth, ThroughPlace, Loop, Random }

    [Tooltip("Its look: one of the StonePath's Models")]
    [StoneModel] public int model = 0;
    [Tooltip("What it does. Its look and sounds: Resources/StoneTypes")]
    public Motion motion = Motion.None;

    [Section("Points", nameof(motion), Motion.Slide, Motion.Blink)]
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

    [Section("Blink", nameof(motion), Motion.Blink)]
    [Tooltip("Seconds it stays at each point: x = y fixed, else random between them")]
    public Vector2 interval = new Vector2(1.5f, 1.5f);
    [Tooltip("Seconds it shakes before vanishing (the end of its Interval)")]
    public float warningTime = 0.6f;
    [Tooltip("Meters it shakes")]
    public float blinkShake = 0.06f;
    [Tooltip("Seconds it is gone before showing up at the next point")]
    public float gapTime = 0.5f;

    // What StonePath reads every frame: between point From and point To (0 = its place, 1.. = its shadows)
    public int From { get; private set; }
    public int To { get; private set; }
    public float Progress { get; private set; }   // 0 at From, 1 at To
    public float Down { get; private set; }       // meters below its place (rising with the path, falling)
    public Vector3 Shake { get; private set; }    // the warning before a fall
    public bool IsPresent { get; private set; } = true;
    // Its motion may run (StonePath's rise wave: the moving stones start one after another once the path is up)
    public bool Started { get; set; } = true;
    public bool Moves => (motion == Motion.Slide || motion == Motion.Blink) && PointCount > 1;
    // A turn on top of its place (StonePath applies it; none for now)
    public Quaternion Tumble { get; private set; } = Quaternion.identity;
    // Blink: the point where it will show up, while that is shown (its warning and while it is gone); -1: none
    public int PreviewPoint => motion == Motion.Blink && IsPresent && previewGrow >= 0f ? (moving ? To : upcoming) : -1;
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
        AudioManager.SetUp(audioSource, AudioManager.World.Stone);   // heard near, not from the next course

        if (Moves && motion == Motion.Slide && path != null && StoneTypes.Instance.trails) MakeTrail();

        // Its route, already Start Phase along (from its last point, backwards, if Start At End)
        timer = WaitTime();
        if (startAtEnd && Moves) goToEnd = true;   // (once its bridge is up: it rises in its own place)
        else if (Moves) Advance(startPhase * RouteSteps * (AverageWait + MoveTime));
        if (motion == Motion.Fall) Crack(StoneTypes.Instance.fallIdleCrack);   // always cracked: you see it will break
        lastPosition = transform.position;
    }

    //---- Every frame (StonePath)

    public void Tick()
    {
        Down = riseDown + fallDown;   // (hidden too: it waits down there, so it never shows a frame up in its place)
        bool settling = rising != null || (path != null && path.Appearing) || !Started;   // its bridge still rising, or not its turn: it waits in place
        SetTrail(IsPresent && !settling && fallState == FallState.Resting);
        if (!IsPresent) return;
        if (Moves && !settling && goToEnd) GoToEnd();
        float idle = path != null ? path.SpeedFactor : 1f;   // (its path's Idle Speed: the same for all its stones)
        if (Moves && !settling) Advance(Time.deltaTime * LevelManager.PlatformSpeed * idle);   // (Platform Speed: the whole route, waits too)
        else
        {
            previewGrow = -1f;   // (not running its route: nothing to show)
            if (!Moves) { From = To = 0; Progress = 0f; }
        }
    }

    //---- Trail (StonePath > Look: only while it is up and moving its route)

    private TrailRenderer trail;
    private static Material trailMaterial;

    void MakeTrail()
    {
        var holder = new GameObject("Trail").transform;   // a bit down, inside the model
        holder.SetParent(transform, false);
        holder.position = transform.position + Vector3.down * path.trailDrop;
        trail = holder.gameObject.AddComponent<TrailRenderer>();
        if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default")) { name = "StoneTrail" };
        trail.sharedMaterial = trailMaterial;
        trail.time = StoneTypes.Instance.trailSeconds;
        trail.minVertexDistance = 0.1f;
        trail.widthMultiplier = path.trailThickness;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        Color tint = StoneTypes.Instance.trailColor;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                         new[] { new GradientAlphaKey(tint.a, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;
    }

    // On: it starts fresh (no line from where it rose or waited hidden)
    void SetTrail(bool on)
    {
        if (trail == null || trail.emitting == on) return;
        if (on) trail.Clear();
        trail.emitting = on;
    }

    // Start At End, once up: to its last point while the others wait at theirs, then on at once, backwards
    void GoToEnd()
    {
        goToEnd = false;
        upcoming = -1;
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
    private int upcoming = -1;   // the next point, chosen as soon as it arrives at one (Blink shows it)
    private float endWait;

    // Waits at a point, moves to the next one, waits there…
    void Advance(float seconds)
    {
        timer -= seconds;
        while (timer <= 0f)
        {
            if (moving) { moving = false; From = To; upcoming = NextPoint(From); timer += skipWait ? endWait : WaitTime(); skipWait = false; if (motion == Motion.Slide) Play(Kind.backClip); }
            else { To = upcoming >= 0 ? upcoming : NextPoint(From); upcoming = -1; moving = true; moveDuration = MoveTime; timer += moveDuration; if (motion == Motion.Slide) Play(Kind.goClip); }
        }
        if (!moving && upcoming < 0) upcoming = NextPoint(From);   // (where it goes next is known all through the wait)
        if (motion == Motion.Blink) { Blink(); return; }
        Progress = moving ? Smooth(1f - timer / moveDuration) : 0f;
    }

    // Blink: gone while "moving", still where it was (so whoever stood on it falls right there, never carried), and
    // only once it shows up again it is at the next point. Shaking at the end of each wait
    private bool blinkGone, blinkWarned;
    void Blink()
    {
        Progress = 0f;   // (at From: the jump happens when the wait starts at the next point, while nobody is on it)
        bool warning = !moving && timer < warningTime;
        Shake = warning ? Jitter() : Vector3.zero;
        float span = Mathf.Max(warningTime + moveDuration, 0.01f);   // (from its warning until it shows up there)
        previewGrow = warning ? (warningTime - timer) / span : moving ? (warningTime + moveDuration - timer) / span : -1f;
        if (warning && !blinkWarned) Play(Kind.warnClip);   // once, as it starts shaking
        blinkWarned = warning;
        if (moving == blinkGone) return;
        blinkGone = moving;
        if (!blinkGone) PopPreview();   // it shows up where the rock grew
        foreach (Renderer r in renderers) if (r != null) r.enabled = !blinkGone;
        SetSolid(!blinkGone);   // whoever stands on it falls
        Play(blinkGone ? Kind.goClip : Kind.backClip);
    }

    Vector3 Jitter()
    {
        Vector2 j = Random.insideUnitCircle * blinkShake;
        return new Vector3(j.x, 0f, j.y);
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

    bool Blinks => motion == Motion.Blink;   // it waits Interval at each point
    float WaitTime() => Mathf.Max(Blinks ? Interval() : pauseTime, 0.01f);
    float AverageWait => Blinks ? (interval.x + Mathf.Max(interval.x, interval.y)) * 0.5f : pauseTime;
    float MoveTime => Mathf.Max(Blinks ? gapTime : travelTime, 0.01f);
    float Interval() => interval.y > interval.x ? Random.Range(interval.x, interval.y) : interval.x;

    static float Smooth(float p) => p * p * (3f - 2f * p);

    //---- Rising and sinking with the path (StonePath)

    public void SetPresent(bool present)
    {
        IsPresent = present;
        foreach (Renderer r in renderers) if (r != null) r.enabled = present;
        SetSolid(present && fallState == FallState.Resting);
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
        Play(Kind.warnClip);
        PlayerLook.Shake(0.15f, s.cameraShakeAmount);
        for (float t = 0f; t < s.warningTime; t += Time.deltaTime * LevelManager.PlatformSpeed)
        {
            Vector2 jitter = Random.insideUnitCircle * s.shakeAmount;
            Shake = new Vector3(jitter.x, 0f, jitter.y);
            Crack(Mathf.Lerp(StoneTypes.Instance.fallIdleCrack, 1f, Mathf.Clamp01(t / Mathf.Max(s.warningTime * 0.6f, 0.01f))));   // the cracks open up
            yield return null;
        }
        Shake = Vector3.zero;

        // It bursts: hidden, its shards fly out to the sides and down (whoever stood on it falls)
        fallState = FallState.Falling;
        SetSolid(false);
        Play(Kind.goClip);
        SetLook(false);
        StartCoroutine(Shatter());
        fallDown = depth;   // (hidden down there, as before)

        fallState = FallState.Down;
        for (float t = 0f; t < s.returnDelay; t += Time.deltaTime * LevelManager.PlatformSpeed) yield return null;

        // Back: in its place, its shards fly together into it, then it is there again
        fallState = FallState.Rising;
        fallDown = 0f;
        yield return null;   // (StonePath puts it back in its place first: the shards close there, not down below)
        Crack(StoneTypes.Instance.fallIdleCrack);   // whole again (cracked as always)
        Play(Kind.backClip);
        yield return Assemble();   // (it shows itself before they touch)
        while (PlayerInside()) yield return null;   // someone inside it (jumped on it while it rose): solid once they are out
        SetSolid(true);   // only once back in its place (rising, it never catches someone falling)
        fallState = FallState.Resting;
        ApplyColor();   // its own look again
        falling = null;
    }

    //---- Shared

    void SetSolid(bool solid)
    {
        if (colliders == null) return;
        foreach (Collider c in colliders) if (c != null) c.enabled = solid;
    }

    // Several stones at once (a row blinking together) don't pile up: on time, but each a slightly different pitch, and
    // the same sound started by other stones in the same moment comes in quieter (Max Voices at most; the rest: silent)
    const float PitchRange = 0.08f;     // ±8%
    const float SameMoment = 0.08f;     // seconds
    const int MaxVoices = 3;
    const float ExtraVolume = 0.35f;    // each one after the first
    static readonly Dictionary<AudioClip, (float time, int count)> recent = new Dictionary<AudioClip, (float, int)>();

    void Play(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        int count = recent.TryGetValue(clip, out var last) && Time.time - last.time < SameMoment ? last.count + 1 : 1;
        if (count == 1) recent[clip] = (Time.time, 1); else recent[clip] = (last.time, count);
        if (count > MaxVoices) return;
        audioSource.pitch = 1f + Random.Range(-PitchRange, PitchRange);
        audioSource.PlayOneShot(clip, (count == 1 ? 1f : ExtraVolume) * AudioManager.WorldVolume);
    }

    // Cracks (StonePath > Crack Decal), 0 none, 1 fully cracked. On the stone's own material when it is Memetica/Stone
    // (the cracks are part of the stone, at any size); else a flat quad over the model
    private Renderer crack;
    private static Material crackMaterial;
    private static readonly int CrackId = Shader.PropertyToID("_Crack"), CrackTexId = Shader.PropertyToID("_CrackTex"),
                                CrackFitId = Shader.PropertyToID("_CrackFit");
    private float crackTurn = -1f;   // (< 0: not picked yet)
    private static readonly int CrackOpenId = Shader.PropertyToID("_CrackOpen");   // (the picture's size: the material's Picture Size)
    void Crack(float amount, float open = 0f)
    {
        Texture2D decal = StoneTypes.Instance.crackDecal;
        if (decal == null) return;
        bool onMaterial = false;
        var stoneBlock = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null || r is TrailRenderer || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(CrackId)) continue;
            r.GetPropertyBlock(stoneBlock);
            stoneBlock.SetTexture(CrackTexId, decal);
            stoneBlock.SetFloat(CrackId, amount);
            stoneBlock.SetFloat(CrackOpenId, open);
            if (crackTurn < 0f) crackTurn = (Mathf.Abs(name.GetHashCode()) % 6283) / 1000f;   // each stone its own way (always the same)
            Vector3 middle = r.bounds.center - r.transform.position;   // its top's middle from the model's origin
            stoneBlock.SetVector(CrackFitId, new Vector4(middle.x, Mathf.Min(r.bounds.size.x, r.bounds.size.z), middle.z, crackTurn));
            r.SetPropertyBlock(stoneBlock);
            onMaterial = true;
        }
        if (onMaterial || !Application.isPlaying) return;   // (the flat quad only while playing)
        if (crack == null)
        {
            if (amount <= 0f) return;
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in renderers) if (r != null && !(r is TrailRenderer)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any) return;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Cracks";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 0.01f, b.center.z), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            float size = Mathf.Min(b.size.x, b.size.z);
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.transform.SetParent(transform, true);
            if (crackMaterial == null) crackMaterial = new Material(Shader.Find("Sprites/Default")) { name = "StoneCracks" };
            crack = quad.GetComponent<Renderer>();
            crack.sharedMaterial = crackMaterial;
            crack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        crack.enabled = amount > 0f;
        var block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", decal);
        block.SetColor("_Color", new Color(1f, 1f, 1f, amount));
        crack.SetPropertyBlock(block);
    }

    // The player overlaps the stone's look (made solid now, they would be stuck inside it)
    private static CharacterController playerBody;
    bool PlayerInside()
    {
        if (playerBody == null)
        {
            PlayerMove player = FindObjectOfType<PlayerMove>();
            if (player == null || !player.TryGetComponent(out playerBody)) return false;
        }
        Bounds stone = default;
        bool any = false;
        foreach (Renderer r in renderers)
            if (r != null && !(r is TrailRenderer)) { if (!any) { stone = r.bounds; any = true; } else stone.Encapsulate(r.bounds); }
        if (!any) return false;
        stone.Expand(0.1f);
        return stone.Intersects(playerBody.bounds);
    }

    // Its kind's look (Resources/StoneTypes): the tint, and its mark on a Memetica/Stone material
    private StoneTypes.Kind Kind => StoneTypes.Instance.Of(motion);
    private static readonly int ColorId = Shader.PropertyToID("_Color"), MarkTexId = Shader.PropertyToID("_MarkTex"),
                                MarkColorId = Shader.PropertyToID("_MarkColor"), MarkSizeId = Shader.PropertyToID("_MarkSize"),
                                MarkReliefId = Shader.PropertyToID("_MarkRelief");
    public void ApplyColor()
    {
        if (!Application.isPlaying || renderers == null || System.Array.Exists(renderers, r => r == null))
            renderers = GetComponentsInChildren<Renderer>(true);   // (its Model may have changed)
        StoneTypes.Kind kind = Kind;
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null || r is TrailRenderer) continue;
            block.Clear();   // (from scratch: anything set before, e.g. an old crack size, no longer hides the material's)
            block.SetColor(ColorId, kind.tint);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(MarkTexId))
            {
                bool marked = kind.mark != null;
                if (marked) block.SetTexture(MarkTexId, kind.mark);
                block.SetColor(MarkColorId, marked ? kind.markColor : Color.clear);   // (alpha 0: no mark)
                block.SetFloat(MarkSizeId, kind.markSize);
                block.SetFloat(MarkReliefId, kind.relief);
            }
            r.SetPropertyBlock(block);
        }
        if (motion == Motion.Fall) Crack(StoneTypes.Instance.fallIdleCrack);   // (the Scene view shows it cracked too)
        else Crack(0f);
    }

    //---- What tells it apart in play (StoneTypes)

    private float previewGrow = -1f;   // Blink: 0 → 1 from its warning until it shows up (-1: no preview now)
    private Transform preview;
    private Vector3 previewFull;        // its scale when grown
    private Vector3 lastPosition;
    private float wave, debrisDue;

    // Blink: a tiny copy of it where it will show up, shaking; at the end it grows at once into the stone (StonePath gives
    // the place and its turn every frame)
    public void ShowPreview(Vector3 at, Quaternion turn)
    {
        StoneTypes types = StoneTypes.Instance;
        if (!types.blinkPreview || previewGrow < 0f) { HidePreview(); return; }
        if (preview == null) MakePreview();
        if (preview == null) return;
        if (!preview.gameObject.activeSelf) preview.gameObject.SetActive(true);
        float span = Mathf.Max(warningTime + gapTime, 0.01f);
        float growFrom = 1f - Mathf.Clamp01(types.previewGrowTime / span);   // (the last Grow Time of it)
        float grow = previewGrow <= growFrom ? 0f : Smooth((previewGrow - growFrom) / Mathf.Max(1f - growFrom, 0.001f));
        float size = Mathf.Lerp(types.previewSmall, 1f, grow);
        Vector3 shake = grow < 1f ? Random.insideUnitSphere * types.previewShake * (1f - grow) : Vector3.zero;
        preview.SetPositionAndRotation(at + shake, turn);
        preview.localScale = previewFull * size;

        // Small and shaking: pebbles fly off to the sides, as if it ground its way there
        if (grow > 0f || types.previewPebbleRate <= 0f || types.slideDebris == null || types.slideDebris.Length == 0) return;
        pebblesDue += types.previewPebbleRate * Time.deltaTime;
        if (pebblesDue < 1f) return;
        pebblesDue -= 1f;
        if (!InRange(at, types)) return;
        Vector2 side = Random.insideUnitCircle.normalized;
        DebrisBurst.Spawn(types.slideDebris, 1, at, new Vector3(side.x, 0.4f, side.y), 2.5f, 0.3f, Width() * types.previewPebbleSize, 0.8f, 0f,
                          PieceMaterial(), PieceLook());
    }
    private float pebblesDue;

    public void HidePreview()
    {
        if (preview != null && preview.gameObject.activeSelf) preview.gameObject.SetActive(false);
    }

    void MakePreview()
    {
        preview = CloneLook(name + "_Preview");
        previewFull = transform.lossyScale;
    }

    // A copy of its look (its Model), nothing that collides or runs, in its colors, hidden (the preview, the shards)
    Transform CloneLook(string copyName, bool cracked = true)
    {
        Transform copy = new GameObject(copyName).transform;
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Renderer>(true) == null || child.GetComponent<TrailRenderer>() != null || child.name == "Cracks") continue;
            Transform part = Instantiate(child.gameObject, copy).transform;
            part.localPosition = child.localPosition;
            part.localRotation = child.localRotation;
            part.localScale = child.localScale;
        }
        foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        var block = new MaterialPropertyBlock();
        Renderer own = System.Array.Find(renderers, r => r != null && !(r is TrailRenderer));
        if (own != null) own.GetPropertyBlock(block);   // (its tint, mark and cracks)
        if (!cracked) { block.SetFloat(CrackId, 0f); block.SetFloat(CrackOpenId, 0f); }   // (already broken: pieces, no cracks)
        foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true)) { r.enabled = true; r.SetPropertyBlock(block); }
        copy.gameObject.SetActive(false);
        return copy;
    }

    // Its own look shown or not (the shards take its place meanwhile)
    void SetLook(bool on)
    {
        foreach (Renderer r in renderers) if (r != null && !(r is TrailRenderer)) r.enabled = on;
    }

    //---- Fall: it bursts into shards, and they come back together into it

    private readonly List<Transform> shards = new List<Transform>();
    private readonly List<Vector3> shardVelocity = new List<Vector3>();
    private readonly List<float> shardSpin = new List<float>();
    static readonly Vector3 ShardGravity = new Vector3(0f, -9.8f, 0f);

    // Smaller copies of it, flat side up, each thrown its own way: to the sides and down (a cone under it)
    void ThrowShards(StoneTypes types)
    {
        int count = Random.Range(Mathf.Max(1, types.shardCount.x), Mathf.Max(types.shardCount.x, types.shardCount.y) + 1);
        while (shards.Count < count) { shards.Add(CloneLook(name + "_Shard", false)); shardVelocity.Add(Vector3.zero); shardSpin.Add(0f); }
        float turn = Random.Range(0f, 360f);
        for (int i = 0; i < shards.Count; i++)
        {
            bool used = i < count;
            shards[i].gameObject.SetActive(used);
            if (!used) continue;
            float angle = turn + 360f * i / count + Random.Range(-20f, 20f);   // spread all around
            Vector3 side = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            shardVelocity[i] = (side + Vector3.down * 0.5f) * types.shardSpeed * Random.Range(0.8f, 1.2f);
            shardSpin[i] = Random.Range(-90f, 90f);
        }
    }

    // Where a shard is Time seconds after the burst, from Center (flat side up, turning slowly around its vertical)
    void PlaceShard(int i, Vector3 center, float time, float size)
    {
        Transform shard = shards[i];
        shard.SetPositionAndRotation(center + shardVelocity[i] * time + 0.5f * ShardGravity * time * time,
                                     transform.rotation * Quaternion.Euler(0f, shardSpin[i] * time, 0f));
        shard.localScale = transform.lossyScale * size;
    }

    IEnumerator Shatter()
    {
        StoneTypes types = StoneTypes.Instance;
        ThrowShards(types);
        Vector3 center = transform.position;
        float duration = Mathf.Max(types.shardTime, 0.05f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float shrink = t < duration * 0.7f ? 1f : 1f - (t - duration * 0.7f) / (duration * 0.3f);   // gone at the end
            for (int i = 0; i < shards.Count; i++) if (shards[i].gameObject.activeSelf) PlaceShard(i, center, t, types.shardSize * shrink);
            yield return null;
        }
        foreach (Transform shard in shards) shard.gameObject.SetActive(false);
    }

    // The burst backwards, in its place: they fly in from below and around and close into the stone
    IEnumerator Assemble()
    {
        StoneTypes types = StoneTypes.Instance;
        ThrowShards(types);
        float duration = Mathf.Max(types.assembleTime, 0.05f);
        float showAt = duration * Mathf.Clamp01(types.assembleShowAt);
        for (float t = 0f; t < showAt; t += Time.deltaTime)
        {
            float left = 1f - t / duration;                       // 1 → 0: from far, into it
            float time = left * duration;
            float size = Mathf.Lerp(types.shardSize, Mathf.Max(types.shardSize, 0.6f), 1f - left);   // a little bigger as they close (they fill it)
            for (int i = 0; i < shards.Count; i++) if (shards[i].gameObject.activeSelf) PlaceShard(i, transform.position, time, size);
            yield return null;
        }
        SetLook(true);   // the stone itself is their closing up
        foreach (Transform shard in shards) shard.gameObject.SetActive(false);
    }

    // It showed up: the copy (grown into it by now) goes
    void PopPreview() => HidePreview();

    // Slide: its trail sways side to side, and pieces drop off it while it moves
    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        if (motion != Motion.Slide || !IsPresent || !moving) return;
        StoneTypes types = StoneTypes.Instance;

        Vector3 flat = new Vector3(moved.x, 0f, moved.z);
        if (trail != null && path != null && flat.sqrMagnitude > 1e-8f)
        {
            wave += flat.magnitude / Mathf.Max(types.trailWaveLength, 0.1f) * Mathf.PI * 2f;
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            trail.transform.position = Middle() + Vector3.down * path.trailDrop + side * Mathf.Sin(wave) * types.trailWobble;
        }

        // Pieces off the trail, only while it really goes (not slowing into a point or setting off: they'd only drop)
        if (types.slideDebris == null || types.slideDebris.Length == 0 || types.slideDebrisRate <= 0f) return;
        float speed = Time.deltaTime > 0f ? flat.magnitude / Time.deltaTime : 0f;
        if (speed < types.slideDebrisMinSpeed) { debrisDue = 0f; return; }
        debrisDue += types.slideDebrisRate * Time.deltaTime;
        if (debrisDue < 1f) return;
        debrisDue -= 1f;
        Vector3 from = trail != null ? trail.transform.position : Middle();   // on the trail (winding with it), behind the stone
        if (InRange(from, types))
            DebrisBurst.Float(types.slideDebris[Random.Range(0, types.slideDebris.Length)], from, Width() * types.slideDebrisSize,
                              types.slideDebrisHang, types.slideDebrisDrop, PieceMaterial(), PieceLook());
    }

    // Meters wide its look is (measured once, while it shows)
    private float width = -1f;
    float Width()
    {
        if (width > 0f) return width;
        Renderer[] own = System.Array.FindAll(renderers, r => r != null && r.enabled && !(r is TrailRenderer));
        if (own.Length == 0) return 1f;
        Bounds b = own[0].bounds;
        foreach (Renderer r in own) b.Encapsulate(r.bounds);
        width = Mathf.Max(b.size.x, b.size.z, 0.1f);
        return width;
    }

    // Its middle (not its pivot, which may be on its top): where the trail comes out
    private Vector3 middleOffset;
    private bool middleKnown;
    Vector3 Middle()
    {
        if (!middleKnown)
        {
            Renderer[] own = System.Array.FindAll(renderers, r => r != null && r.enabled && !(r is TrailRenderer));
            if (own.Length == 0) return transform.position;
            Bounds b = own[0].bounds;
            foreach (Renderer r in own) b.Encapsulate(r.bounds);
            middleOffset = transform.InverseTransformPoint(b.center);
            middleKnown = true;
        }
        return transform.TransformPoint(middleOffset);
    }

    // Pieces that come off it: its material and colors, no cracks (the same stone, broken)
    Material PieceMaterial()
    {
        Renderer own = System.Array.Find(renderers, r => r != null && !(r is TrailRenderer));
        return own != null ? own.sharedMaterial : null;
    }

    private MaterialPropertyBlock pieceLook;
    MaterialPropertyBlock PieceLook()
    {
        if (pieceLook != null) return pieceLook;
        pieceLook = new MaterialPropertyBlock();
        Renderer own = System.Array.Find(renderers, r => r != null && !(r is TrailRenderer));
        if (own != null) own.GetPropertyBlock(pieceLook);
        pieceLook.SetFloat(CrackId, 0f);
        pieceLook.SetFloat(CrackOpenId, 0f);
        return pieceLook;
    }

    static Camera viewer;
    static bool InRange(Vector3 at, StoneTypes types)
    {
        if (viewer == null) viewer = Camera.main;
        return viewer == null || (viewer.transform.position - at).sqrMagnitude < types.effectsRange * types.effectsRange;
    }

    void OnDestroy()
    {
        if (preview != null) Destroy(preview.gameObject);
        foreach (Transform shard in shards) if (shard != null) Destroy(shard.gameObject);
    }
}

// PathStone.model: a dropdown with the StonePath's Models (drawn by StonePathTool)
public class StoneModelAttribute : PropertyAttribute { }
