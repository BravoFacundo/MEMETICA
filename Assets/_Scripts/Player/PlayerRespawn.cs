// Falling into the void and coming back (on the Player, next to PlayerMove; before: part of PlayerMove):
//  Points   the last island you stood on gives your respawn point (its RespawnPoint nearest to you, or where you stood)
//  Void     with nothing below you, Void Fall Margin under the last ground, the fall counts as into the void (Void Fall
//           Started: the meme in your hand turns into its Fall change); deeper still, you reappear
//  Classic  (OriginalGame) you reappear high above (LevelManager's Drop Distance) and brake before the ground
//  Slam     (Game) after falling Slam Fall Depth into the void you reappear Slam Height above your island, slam down
//           fast and land on your face: the head goes to the floor (tilted, low, wobbling), then you get up quickly
//           (a little past, like shaking it off) and only then you can move and look again. About a second. The meme
//           in your hand keeps its Fall change until you are up (RespawnLanded comes then)
//  Sounds   (Slam) every time you leave the ground a falling sound starts, muted; it is turned up once it is a real
//           fall (into the void, the respawn or portal fall, or Big Fall Height down). A big fall lands with the slam
//           and one of the Landing Sounds; a small one stops it if it was unheard. Each one plays to its end, however
//           long: only the other one cuts it (a landing cuts the falling sound, a new real fall cuts the landing one)
// PlayerMove moves the body and calls this at the right moments (CheckFall, Grounded, Airborne); everything else
// asks this (or PlayerMove, which forwards the old names).

using System.Collections;
using UnityEngine;

public enum RespawnStyle { Classic, Slam }

[RequireComponent(typeof(PlayerMove))]
public class PlayerRespawn : MonoBehaviour
{
    public static PlayerRespawn Instance { get; private set; }

    [Section("Respawn")]
    [Tooltip("Classic: you reappear high above (Respawn Height) and brake before the ground. Slam: you " +
             "reappear close above and slam down on your face (Slam Respawn below)")]
    public RespawnStyle respawnStyle = RespawnStyle.Slam;
    [Tooltip("Classic: meters above the ground of the RespawnPoint you reappear (its own Height, if it has one, wins). " +
             "Before: the LevelManager's Drop Distance, the same as the islands' (30: a very long fall)")]
    [ShowIf(nameof(respawnStyle), RespawnStyle.Classic)] public float respawnHeight = 12f;
    [Tooltip("After reappearing it falls fast, then brakes over these last meters before the ground. 0 = no brake")]
    [ShowIf(nameof(respawnStyle), RespawnStyle.Classic)] public float respawnBrakeDistance = 6f;
    [Tooltip("Falling speed it brakes down to (m/s); the last bit it falls on its own")]
    [ShowIf(nameof(respawnStyle), RespawnStyle.Classic)] public float respawnLandingSpeed = 3f;
    [Tooltip("Meters below the last ground, with nothing under you, when the fall counts as into the void (the meme in your hand turns into its Fall change). Low: right away; too low: the end of a jump over a gap counts too")]
    public float voidFallMargin = 1.5f;

    [Section("Slam Respawn", nameof(respawnStyle), RespawnStyle.Slam)]
    [Tooltip("Meters you fall into the void, below the last ground, before you reappear (Classic: LevelManager's Drop Distance)")]
    public float slamFallDepth = 12f;
    [Tooltip("Meters above the island's ground you reappear (a RespawnPoint's own Height wins)")]
    public float slamHeight = 8f;
    [Tooltip("Seconds it takes to hit the ground")]
    public float slamFallTime = 0.45f;
    [Tooltip("Seconds to hit the ground with the face")]
    public float slamDownTime = 0.08f;
    [Tooltip("Seconds lying there, dizzy")]
    public float slamStayTime = 0.17f;
    [Tooltip("Seconds to get up")]
    public float slamGetUpTime = 0.25f;
    [Tooltip("Degrees the head tilts to one side (random side)")]
    public float slamRoll = 14f;
    [Tooltip("Meters the head drops")]
    public float slamDip = 0.6f;
    [Tooltip("Camera shake on impact (0 = none)")]
    public float slamShake = 0.12f;

    [Section("Fall Sounds", nameof(respawnStyle), RespawnStyle.Slam)]
    [Tooltip("Landing: one of them, at random (never the same twice in a row), on impact")]
    public AudioClip[] landingSounds;
    [Tooltip("Falling: one of them, at random, as you start falling into the void (or out of a portal); cut on impact")]
    public AudioClip[] fallingSounds;
    [Range(0f, 1f)] public float fallSoundsVolume = 1f;
    [Tooltip("Meters fallen (from the highest point) from which any fall, not only into the void, is heard (its falling " +
             "sound) and lands with the slam and its landing sound. 0: only falls into the void")]
    public float bigFallHeight = 6f;
    [Tooltip("Coming down from a jump or a ledge, it looks ahead along your fall (where you are going, as you move): " +
             "no ground there, or ground Big Fall Height below where you started, and the falling sound is heard right " +
             "away, not once you have fallen that far. Seconds of fall it looks ahead")]
    public float lookAhead = 1.5f;
    [Tooltip("Seconds the look ahead must keep finding no ground before the falling sound is heard: a stone about to " +
             "appear (Blink) or come under you in that time cancels it")]
    public float lookAheadDelay = 0.25f;


    // Falling with nothing below: it will reappear (the meme in the hand turns into its Fall change on the way down)
    public static event System.Action VoidFallStarted;
    // Fell into the void and reappeared (a fall for the score: RunStats). Not the portal's way back
    public static event System.Action VoidRespawned;
    // After that fall and the respawn, it touches the ground (Slam: you land on your face here)
    public static event System.Action RespawnTouchedDown;
    // Back in play after the respawn: touched the ground, or got up after the slam (the meme in the hand goes back)
    public static event System.Action RespawnLanded;

    // From the respawn until back in play
    public bool Respawning => awaitingLanding;
    public bool FallingIntoVoid { get; private set; }
    // The island it stepped on last (null before any)
    public GameObject CurrentIsland => lastIsland;
    // What is right under the player (PlayerGrab's recall, CourseGuide, StonePath: which island or bridge you are on)
    public Transform GroundUnder => lastGroundHit;

    private PlayerMove move;
    private CharacterController characterController;
    private PlayerLook look;

    private Transform respawnPoint;
    private Transform lastGroundHit;
    private GameObject lastIsland;
    private GameObject groundIsland;      // the island under the player now (null on a bridge)
    private float nextRespawnCheck;
    private float lastGroundY;            // the last ground's height: falling far below it = into the void
    private bool landingBrake;            // just respawned: brakes before touching the ground
    private bool awaitingLanding;         // just respawned: RespawnLanded not sent yet
    private bool touchedDown;             // just respawned: RespawnTouchedDown already sent

    void Awake()
    {
        Instance = this;
        move = GetComponent<PlayerMove>();
        characterController = GetComponent<CharacterController>();
    }

    void Start()
    {
        look = PlayerLook.Instance;
        SetUpSounds();
        // The game starts in the tutorial: its start point, before standing on any island
        respawnPoint = LevelManager.Instance != null ? LevelManager.Instance.startRespawnPoint : null;
        lastGroundY = transform.position.y;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update() => UpdateFallSound();   // (Slam: every big fall is heard, also while moved by something else)

    //---- Called by PlayerMove

    // Grounded this frame: no brake, not falling; just respawned: touched down (Slam: the landing, back in play once up)
    public void Grounded()
    {
        landingBrake = false;
        FallingIntoVoid = false;
        if (awaitingLanding && !touchedDown)
        {
            touchedDown = true;
            RespawnTouchedDown?.Invoke();
            if (SlamOn) Land();
            else FinishRespawn();
        }
    }

    // In the air this frame: the Classic brake, and whether this is a fall into the void
    public void Airborne()
    {
        if (landingBrake) BrakeBeforeGround();
        // Already well below where it stood (not just the end of a jump over a gap), with nothing under it
        bool belowLastGround = transform.position.y < lastGroundY - voidFallMargin;
        if (!FallingIntoVoid && !awaitingLanding && move.VerticalVelocity < 0f && belowLastGround && NothingBelow())
        {
            FallingIntoVoid = true;
            VoidFallStarted?.Invoke();
        }
    }

    // A fall like into the void for what you carry (the portal: the meme window shows its Fall change; back when you land)
    public void AnnounceFall() => VoidFallStarted?.Invoke();

    // Coming out of a portal at Position, falling: it lands as from a respawn (the slam), Respawn is its respawn now
    public void ArriveFalling(Vector3 position, Transform respawn)
    {
        lastGroundHit = null;
        lastGroundY = GroundYBelow(position);   // falling onto it is not a fall into the void
        if (respawn != null) SetRespawn(respawn);
        landingBrake = false;
        awaitingLanding = true;
        touchedDown = false;
    }

    // After a teleport or the noclip: the fall reference is the ground below, however far (a spawn point high in the
    // air must not count as falling into the void)
    public void ResetGroundReference()
    {
        lastGroundHit = null;
        lastGroundY = GroundYBelow(transform.position);
    }

    float GroundYBelow(Vector3 from) =>
        Physics.Raycast(from, Vector3.down, out RaycastHit ground, 500f, move.SolidMask, QueryTriggerInteraction.Ignore) ? ground.point.y : from.y;

    // No ground under the player, however far (the meme in the hand and invisible lowered islands don't count)
    private bool NothingBelow()
    {
        int mask = move.SolidMask & ~(1 << 2);   // not the player, not Ignore Raycast, not props
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position, Vector3.down, 1000f, mask, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<Meme>() == null) return false;
        return true;
    }

    //---- Respawn: where and how you come back

    // Fell into the void? (then it reappears: true, PlayerMove skips this frame). If not, the respawn point follows the island under you
    public bool CheckFall()
    {
        float depth = SlamOn ? slamFallDepth : LevelManager.Instance != null ? LevelManager.Instance.dropDistance : float.MaxValue;
        if (LevelManager.Instance != null && transform.position.y < lastGroundY - depth)
        {
            Respawn();
            return true;
        }

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return false;
        lastGroundY = hit.point.y;                           // the ground below (it follows islands and elevators that move)
        if (hit.transform != lastGroundHit)                  // something new under: which island it is
        {
            lastGroundHit = hit.transform;
            Transform island = Island.Of(hit.transform);   // (before: the Isla tag)
            groundIsland = island != null ? island.gameObject : null;
        }
        if (groundIsland == null) return false;              // a bridge

        // On an island: its respawn point nearest to you (a course can have one before each part), checked now and then
        if (groundIsland == lastIsland && Time.time < nextRespawnCheck) return false;
        lastIsland = groundIsland;
        nextRespawnCheck = Time.time + 0.25f;
        Transform point = LevelManager.Instance.RespawnPointOf(groundIsland, transform.position);
        // No RespawnPoint on it: where you last stood (not on an elevator: it moves, and it isn't a place to come back to)
        if (point == null && move.isGrounded && !groundIsland.TryGetComponent(out Elevator _)) point = SafeSpot(groundIsland, hit.point);
        if (point != null) respawnPoint = point;
        return false;
    }

    // An island without a RespawnPoint: you reappear where you last stood on it (it floats with it)
    private Transform safeSpot;
    private GameObject warnedIsland;
    private Transform SafeSpot(GameObject island, Vector3 ground)
    {
        if (safeSpot == null) safeSpot = new GameObject("PlayerSafeSpot").transform;
        if (safeSpot.parent != island.transform) safeSpot.SetParent(island.transform, true);
        safeSpot.position = ground;
        if (warnedIsland != island)
        {
            warnedIsland = island;
            Debug.LogWarning($"[PlayerRespawn] '{island.name}' has no RespawnPoint: falling, you reappear where you last stood on it (Memetica > Level > Respawn > New Respawn Point).", island);
        }
        return safeSpot;
    }

    // Sets the respawn by hand (the elevator: you arrive on an island you haven't stepped on; a teleport)
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;   // the ground is checked again next frame
    }

    // Back to that point now, as after a fall (PedestalRetire's Respawn Swap: down on a floor you can't climb back from)
    public void RespawnAt(Transform point)
    {
        if (point == null || Respawning) return;
        respawnPoint = point;
        Respawn();
    }

    private void Respawn()
    {
        VoidRespawned?.Invoke();
        // Its island went down meanwhile (invisible, nothing to land on): back to the start
        if (respawnPoint == null || Lift.IsHiddenAt(respawnPoint))
        {
            Debug.LogWarning($"[PlayerRespawn] Respawn '{(respawnPoint != null ? respawnPoint.name : "none")}' " +
                             (respawnPoint != null ? "is in an island that is down" : "not set") + ": back to the start respawn point.", respawnPoint);
            respawnPoint = LevelManager.Instance.startRespawnPoint; lastIsland = null; lastGroundHit = null;
        }

        // Respawn Height above the ground of the RespawnPoint: it falls down onto it (and brakes before landing)
        Vector3 from = LevelManager.Instance.RespawnPosition(respawnPoint, out float groundY);
        respawnPoint.TryGetComponent(out RespawnPoint spot);
        from.y = groundY + (spot != null && spot.height > 0f ? spot.height : respawnHeight);   // its own height, or Respawn Height
        lastGroundY = groundY;
        awaitingLanding = true;
        touchedDown = false;

        if (SlamOn)   // Slam: from close and fast, no brake (Land lands it)
        {
            from.y = groundY + (spot != null && spot.height > 0f ? spot.height : slamHeight);
            move.PlaceAt(from, -SlamStartSpeed);
            landingBrake = false;
        }
        else   // Classic: from Drop Distance above, braking before the ground
        {
            move.PlaceAt(from, 0f);
            landingBrake = respawnBrakeDistance > 0f;
        }

        // Facing its arrow, looking ahead: ready to try again
        if (spot != null && spot.face)
            transform.rotation = Quaternion.LookRotation(spot.Facing, Vector3.up);   // (the head keeps its angle: looking down, still down)
        if (look != null) look.ResetSmoothing();
    }

    //---- Respawn landing

    private bool SlamOn => respawnStyle == RespawnStyle.Slam;

    // Back in play after a respawn (Slam: once you got up)
    public void FinishRespawn()
    {
        if (!awaitingLanding) return;
        awaitingLanding = false;
        touchedDown = false;
        RespawnLanded?.Invoke();
    }

    // Only the vertical speed: it can keep moving sideways while it brakes, wherever it is now
    private void BrakeBeforeGround()
    {
        float verticalVelocity = move.VerticalVelocity;
        if (verticalVelocity >= -respawnLandingSpeed) return;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 200f, move.SolidMask, QueryTriggerInteraction.Ignore)) return;

        float height = ground.distance - characterController.height * 0.5f - 0.6f;   // brakes until 0.6 m above, then drops
        if (height > respawnBrakeDistance) return;
        if (height <= 0f) { landingBrake = false; return; }

        float speed = -verticalVelocity;
        float braking = (speed * speed - respawnLandingSpeed * respawnLandingSpeed) / (2f * height);   // stops right at that height
        move.VerticalVelocity = -Mathf.Max(respawnLandingSpeed, speed - braking * Time.deltaTime);
    }

    //---- Slam landing

    private AudioSource landingSource, fallSource;
    private Coroutine landing, landingFade, fallFade;
    private int lastLanding = -1, lastFalling = -1;
    private bool airborne, loud;
    private float topY, lastY, aheadTime;

    // The speed to start falling with so it hits the ground in Slam Fall Time (Gravity, negative, does the rest)
    float SlamStartSpeed => Mathf.Max(0f, slamHeight / Mathf.Max(slamFallTime, 0.05f) + 0.5f * move.gravity * slamFallTime);

    void SetUpSounds()
    {
        landingSource = gameObject.AddComponent<AudioSource>();
        landingSource.playOnAwake = false;
        landingSource.spatialBlend = 0f;   // your own fall: in your head
        fallSource = gameObject.AddComponent<AudioSource>();
        fallSource.playOnAwake = false;
        fallSource.spatialBlend = 0f;
    }

    void OnDisable()
    {
        if (landing != null) { StopCoroutine(landing); landing = null; }
        if (look != null && SlamOn) ReleaseSlam();
        FinishRespawn();   // turned off mid-landing: back in play anyway
    }

    // Every frame (Slam): the falling sound, muted until it is a real fall; a big fall lands with the slam
    void UpdateFallSound()
    {
        if (!SlamOn || fallSource == null) return;
        bool respawnFall = FallingIntoVoid || Respawning;
        bool inAir = landing == null && (respawnFall || (!move.isGrounded && !move.Scripted));
        float y = transform.position.y;
        float rising = Time.deltaTime > 0f ? (y - lastY) / Time.deltaTime : 0f;   // (m/s, up)
        lastY = y;
        if (inAir)
        {
            if (!airborne)
            {
                airborne = true; topY = y; aheadTime = 0f;
                loud = fallSource.isPlaying && fallSource.volume > 0f;   // (the last falling sound still heard: it goes on)
                if (!loud) StartFallSound();
            }
            topY = Mathf.Max(topY, y);
            bool fell = bigFallHeight > 0f && topY - y >= bigFallHeight;
            bool ahead = bigFallHeight > 0f && !loud && rising <= 0f && BigFallAhead(rising);   // (coming down: where to?)
            aheadTime = ahead ? aheadTime + Time.deltaTime : 0f;   // (it must keep finding nothing a moment)
            bool coming = ahead && aheadTime >= lookAheadDelay;
            if (!loud && (respawnFall || fell || coming)) { loud = true; StopLandingSound(); }
            fallSource.volume = loud ? fallSoundsVolume : 0f;
        }
        else if (airborne && landing == null)   // touched down (a respawn lands through Land instead)
        {
            airborne = false;
            if (topY - y >= bigFallHeight && bigFallHeight > 0f) Land();   // a big fall indeed: the slam
            else if (!loud) fallSource.Stop();   // (never heard: gone; heard, but you saved yourself: it plays to its end)
        }
    }

    // Along the fall from here (your speed now, Gravity), the ground you would land on: none within Look Ahead
    // seconds, or Big Fall Height below the highest point of this jump: a big fall
    bool BigFallAhead(float rising)
    {
        float radius = characterController.radius * 0.8f;
        Vector3 feet = transform.position + characterController.center + Vector3.down * (characterController.height * 0.5f - radius);   // the bottom sphere
        Vector3 flat = move.HorizontalVelocity;
        const float step = 0.1f;
        Vector3 from = feet;
        for (float t = step; t <= lookAhead + 0.001f; t += step)
        {
            Vector3 to = feet + flat * t + Vector3.up * (rising * t + 0.5f * move.gravity * t * t);
            Vector3 path = to - from;
            if (Physics.SphereCast(from, radius, path.normalized, out RaycastHit hit, path.magnitude, move.SolidMask, QueryTriggerInteraction.Ignore))
                return topY - (hit.point.y + (transform.position.y - feet.y) + radius) >= bigFallHeight;   // lands there: that deep below the top?
            from = to;
        }
        return true;   // nothing to land on that soon: a long way down
    }

    void StartFallSound()
    {
        if (fallFade != null) { StopCoroutine(fallFade); fallFade = null; }   // (the last one fading: this one takes over)
        AudioClip clip = Pick(fallingSounds, ref lastFalling);
        if (clip == null) return;
        fallSource.clip = clip;
        fallSource.volume = 0f;   // (muted until it is a real fall)
        fallSource.Play();
    }

    // One of them at random, never the same twice in a row
    static AudioClip Pick(AudioClip[] clips, ref int last)
    {
        if (clips == null || clips.Length == 0) return null;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
        last = i;
        return clips[i];
    }

    // On the ground after a respawn (or a big fall): face down, up again, then back in play
    void Land()
    {
        airborne = false;
        if (fallSource != null) fallSource.Stop();   // the landing sound takes over
        if (landing != null) StopCoroutine(landing);
        landing = StartCoroutine(Landing());
    }

    IEnumerator Landing()
    {
        move.InputLocked = true;
        look.LookLocked = true;
        PlayLandingSound();
        PlayerLook.Shake(slamDownTime + slamStayTime, slamShake);
        float side = Random.value < 0.5f ? -1f : 1f;

        // Face to the ground: fast, speeding up
        for (float t = 0f; t < slamDownTime; t += Time.deltaTime)
        {
            float p = t / slamDownTime; p *= p;
            look.SetPose(p, slamRoll * side * p, slamDip * p);
            yield return null;
        }

        // Lying there: the head wobbles, less and less
        for (float t = 0f; t < slamStayTime; t += Time.deltaTime)
        {
            float fade = 1f - t / slamStayTime;
            look.SetPose(1f, slamRoll * side * (1f + Mathf.Sin(t * 40f) * 0.3f * fade), slamDip);
            yield return null;
        }

        // Up quickly, a little past (looking up a moment), and settles
        for (float t = 0f; t < slamGetUpTime; t += Time.deltaTime)
        {
            float left = 1f - EaseOutBack(t / slamGetUpTime);   // 1 → a bit below 0 → 0
            look.SetPose(left, slamRoll * side * left, slamDip * Mathf.Max(left, 0f));
            yield return null;
        }

        ReleaseSlam();
        landing = null;
        FinishRespawn();   // now the meme in your hand goes back
    }

    // Back to normal: looking where you looked, moving
    void ReleaseSlam()
    {
        move.InputLocked = false;
        look.SetPose(0f, 0f, 0f);
        look.LookLocked = false;
    }

    // On its own source (not one shot): it plays to its end, unless a new real fall comes first (StopLandingSound)
    void PlayLandingSound()
    {
        AudioClip clip = Pick(landingSounds, ref lastLanding);
        if (clip == null) return;
        if (landingFade != null) StopCoroutine(landingFade);
        landingSource.Stop();
        landingSource.clip = clip;
        landingSource.volume = fallSoundsVolume;
        landingSource.Play();
    }

    void StopLandingSound()
    {
        if (!landingSource.isPlaying) return;
        if (landingFade != null) StopCoroutine(landingFade);
        landingFade = StartCoroutine(FadeOut(landingSource, 0.1f));
    }

    // Down to silence in Seconds (after Wait), then stopped
    IEnumerator FadeOut(AudioSource audio, float seconds, float wait = 0f)
    {
        if (wait > 0f) yield return new WaitForSeconds(wait);
        float from = audio.volume;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            audio.volume = from * (1f - t / seconds);
            yield return null;
        }
        audio.Stop();
        audio.volume = from;
    }

    static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        p -= 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
