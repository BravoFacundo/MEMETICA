using System.Collections;
using UnityEngine;

// The funny respawn (Game): after falling into the void you reappear close above your island, slam down fast, and
// land on your face. One of the Sounds plays as you hit the ground and the head goes to the floor (tilted, low,
// wobbling), then you get up quickly (a little past, like shaking it off) and only then you can move and look again.
// It can't be skipped, but the whole thing is about a second (Fall Time + the landing). The meme in your hand keeps
// its Fall change until you are up (PlayerMove.RespawnLanded comes then).
// The void fall itself is short too: Fall Depth meters below the island and you reappear.
// Goes on the Player (with PlayerMove). Without it, or disabled (Game_Old), the respawn is the old one: from high up,
// braking before the ground.
[RequireComponent(typeof(PlayerMove))]
public class RespawnSlam : MonoBehaviour
{
    [Section("Fall")]
    [Tooltip("Meters you fall into the void, below the last ground, before you reappear (the old respawn: LevelManager's Drop Distance)")]
    public float fallDepth = 12f;
    [Tooltip("Meters above the island's ground you reappear")]
    public float height = 8f;
    [Tooltip("Seconds it takes to hit the ground")]
    public float fallTime = 0.45f;

    [Section("Landing")]
    [Tooltip("Seconds to hit the ground with the face")]
    public float downTime = 0.08f;
    [Tooltip("Seconds lying there, dizzy")]
    public float stayTime = 0.17f;
    [Tooltip("Seconds to get up")]
    public float getUpTime = 0.25f;
    [Tooltip("Degrees the head tilts to one side (random side)")]
    public float roll = 14f;
    [Tooltip("Meters the head drops")]
    public float dip = 0.6f;
    [Tooltip("Camera shake on impact (0 = none)")]
    public float shake = 0.12f;

    [Section("Sounds")]
    [Tooltip("Landing: one of them, at random (never the same twice in a row), on impact")]
    [UnityEngine.Serialization.FormerlySerializedAs("sounds")] public AudioClip[] landingSounds;
    [Tooltip("Falling: one of them, at random, as you start falling into the void (or out of a portal); cut on impact")]
    public AudioClip[] fallingSounds;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Seconds a landing sound plays at most (then it fades out quickly): a long one doesn't run into your next fall")]
    public float landingSoundMax = 0.7f;

    [Section("Every Fall")]
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

    private PlayerMove move;
    private PlayerLook look;
    private AudioSource source;
    private Coroutine landing;
    private int lastSound = -1;

    void Awake()
    {
        move = GetComponent<PlayerMove>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // your own fall: in your head
        body = GetComponent<CharacterController>();
        fallSource = gameObject.AddComponent<AudioSource>();
        fallSource.playOnAwake = false;
        fallSource.spatialBlend = 0f;
    }

    // Every time you leave the ground a falling sound starts, muted. It is turned up once it is a real fall: into the void,
    // the respawn or portal fall, or Big Fall Height meters down. So you hear it already going (its end), not starting
    // and cut at once. Touching down: a big fall lands with the slam and its sound; a small one just stops it, unheard
    private AudioSource fallSource;
    private Coroutine fallFade;
    private CharacterController body;
    private bool airborne, loud;
    private float topY, lastY, aheadTime;
    private int lastFall = -1;
    void Update()
    {
        bool respawnFall = move.FallingIntoVoid || move.Respawning;
        bool inAir = landing == null && (respawnFall || (!move.isGrounded && !move.Scripted));
        float y = transform.position.y;
        float rising = Time.deltaTime > 0f ? (y - lastY) / Time.deltaTime : 0f;   // (m/s, up)
        lastY = y;
        if (inAir)
        {
            if (!airborne) { airborne = true; loud = false; topY = y; aheadTime = 0f; StartFallSound(); }
            topY = Mathf.Max(topY, y);
            bool fell = bigFallHeight > 0f && topY - y >= bigFallHeight;
            bool ahead = bigFallHeight > 0f && !loud && rising <= 0f && BigFallAhead(rising);   // (coming down: where to?)
            aheadTime = ahead ? aheadTime + Time.deltaTime : 0f;   // (it must keep finding nothing a moment)
            bool coming = ahead && aheadTime >= lookAheadDelay;
            if (!loud && (respawnFall || fell || coming)) { loud = true; StopLandingSound(); }
            fallSource.volume = loud ? volume : 0f;
        }
        else if (airborne && landing == null)   // touched down (a respawn lands through RespawnTouchedDown instead)
        {
            airborne = false;
            if (topY - y >= bigFallHeight && bigFallHeight > 0f) Land();   // a big fall indeed: the slam
            else if (loud) fallFade = StartCoroutine(FadeOut(fallSource, 0.15f));   // heard, but you saved yourself: it just ends
            else fallSource.Stop();
        }
    }

    // Along the fall from here (your speed now, PlayerMove's gravity), the ground you would land on: none within Look
    // Ahead seconds, or Big Fall Height below the highest point of this jump: a big fall
    bool BigFallAhead(float rising)
    {
        if (body == null) return false;
        float radius = body.radius * 0.8f;
        Vector3 feet = transform.position + body.center + Vector3.down * (body.height * 0.5f - radius);   // the bottom sphere
        Vector3 flat = move.HorizontalVelocity;
        int mask = ~(1 << gameObject.layer | PhysicsLayers.PropsMask);
        const float step = 0.1f;
        Vector3 from = feet;
        for (float t = step; t <= lookAhead + 0.001f; t += step)
        {
            Vector3 to = feet + flat * t + Vector3.up * (rising * t + 0.5f * move.gravity * t * t);
            Vector3 path = to - from;
            if (Physics.SphereCast(from, radius, path.normalized, out RaycastHit hit, path.magnitude, mask, QueryTriggerInteraction.Ignore))
                return topY - (hit.point.y + (transform.position.y - feet.y) + radius) >= bigFallHeight;   // lands there: that deep below the top?
            from = to;
        }
        return true;   // nothing to land on that soon: a long way down
    }

    void StartFallSound()
    {
        if (fallFade != null) { StopCoroutine(fallFade); fallFade = null; }   // (the last one fading: this one takes over)
        if (fallingSounds == null || fallingSounds.Length == 0) return;
        int i = Random.Range(0, fallingSounds.Length);
        if (fallingSounds.Length > 1 && i == lastFall) i = (i + 1) % fallingSounds.Length;
        lastFall = i;
        if (fallingSounds[i] == null) return;
        fallSource.clip = fallingSounds[i];
        fallSource.volume = 0f;   // (muted until it is a real fall)
        fallSource.Play();
    }

    void Start() => look = Camera.main.GetComponent<PlayerLook>();

    void OnEnable() => PlayerMove.RespawnTouchedDown += Land;

    void OnDisable()
    {
        PlayerMove.RespawnTouchedDown -= Land;
        if (landing != null) { StopCoroutine(landing); landing = null; }
        Release();
        if (move != null) move.FinishRespawn();   // turned off mid-landing: back in play anyway
    }

    // The speed to start falling with so it hits the ground in Fall Time (PlayerMove's gravity, negative, does the rest)
    public float StartSpeed(float gravity) => Mathf.Max(0f, height / Mathf.Max(fallTime, 0.05f) + 0.5f * gravity * fallTime);

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
        if (look != null) look.LookLocked = true;
        PlaySound();
        if (look != null && shake > 0f) look.StartCoroutine(look.ShakeTheCamera(downTime + stayTime, shake));
        float side = Random.value < 0.5f ? -1f : 1f;

        // Face to the ground: fast, speeding up
        for (float t = 0f; t < downTime; t += Time.deltaTime)
        {
            float p = t / downTime; p *= p;
            Pose(p, roll * side * p, dip * p);
            yield return null;
        }

        // Lying there: the head wobbles, less and less
        for (float t = 0f; t < stayTime; t += Time.deltaTime)
        {
            float fade = 1f - t / stayTime;
            Pose(1f, roll * side * (1f + Mathf.Sin(t * 40f) * 0.3f * fade), dip);
            yield return null;
        }

        // Up quickly, a little past (looking up a moment), and settles
        for (float t = 0f; t < getUpTime; t += Time.deltaTime)
        {
            float left = 1f - EaseOutBack(t / getUpTime);   // 1 → a bit below 0 → 0
            Pose(left, roll * side * left, dip * Mathf.Max(left, 0f));
            yield return null;
        }

        Release();
        landing = null;
        move.FinishRespawn();   // now the meme in your hand goes back
    }

    void Pose(float down, float tilt, float drop)
    {
        if (look != null) look.SetPose(down, tilt, drop);
    }

    // Back to normal: looking where you looked, moving
    void Release()
    {
        if (move != null) move.InputLocked = false;
        if (look != null) { look.SetPose(0f, 0f, 0f); look.LookLocked = false; }
    }

    // On its own source (not one shot): Landing Sound Max cuts a long one, a new fall cuts it too
    private Coroutine landingFade;
    void PlaySound()
    {
        if (landingSounds == null || landingSounds.Length == 0) return;
        int i = Random.Range(0, landingSounds.Length);
        if (landingSounds.Length > 1 && i == lastSound) i = (i + 1) % landingSounds.Length;
        lastSound = i;
        if (landingSounds[i] == null) return;
        if (landingFade != null) StopCoroutine(landingFade);
        source.Stop();
        source.clip = landingSounds[i];
        source.volume = volume;
        source.Play();
        if (landingSoundMax > 0f && landingSounds[i].length > landingSoundMax) landingFade = StartCoroutine(FadeOut(source, 0.15f, landingSoundMax));
    }

    void StopLandingSound()
    {
        if (!source.isPlaying) return;
        if (landingFade != null) StopCoroutine(landingFade);
        landingFade = StartCoroutine(FadeOut(source, 0.1f));
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
