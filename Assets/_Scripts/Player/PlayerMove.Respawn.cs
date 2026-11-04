//The respawn part of PlayerMove (same component, its own file so PlayerMove.cs stays about moving):
//  Points   the last island you stood on gives your respawn point (its RespawnPoint nearest to you, or where you stood)
//  Classic  (OriginalGame) you reappear high above (LevelManager's Drop Distance) and brake before the ground
//  Slam     (Game) after falling Slam Fall Depth into the void you reappear Slam Height above your island, slam down
//           fast and land on your face: the head goes to the floor (tilted, low, wobbling), then you get up quickly
//           (a little past, like shaking it off) and only then you can move and look again. About a second. The meme
//           in your hand keeps its Fall change until you are up (RespawnLanded comes then)
//  Sounds   (Slam) every time you leave the ground a falling sound starts, muted; it is turned up once it is a real
//           fall (into the void, the respawn or portal fall, or Big Fall Height down). A big fall lands with the slam
//           and one of the Landing Sounds; a small one just stops it, unheard
//(before: the RespawnSlam component)

using System.Collections;
using UnityEngine;

public enum RespawnStyle { Classic, Slam }

public partial class PlayerMove
{
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
    [Tooltip("Seconds a landing sound plays at most (then it fades out quickly): a long one doesn't run into your next fall")]
    public float landingSoundMax = 0.7f;
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

    //---- Respawn: where and how you come back

    // ¿Cayó al vacío? Si no, actualiza el punto de respawn según la isla que pisa
    private bool CheckFallAndRespawn()
    {
        float depth = SlamOn ? slamFallDepth : LevelManager.Instance != null ? LevelManager.Instance.dropDistance : float.MaxValue;
        if (LevelManager.Instance != null && transform.position.y < lastGroundY - depth)
        {
            Respawn();
            return true;
        }

        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers & ~PhysicsLayers.PropsMask)) return false;
        lastGroundY = hit.point.y;                           // el piso de abajo (sigue a islas y ascensores que se mueven)
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
        if (point == null && isGrounded) point = SafeSpot(groundIsland, hit.point);   // no RespawnPoint on it: where you last stood
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
            Debug.LogWarning($"[PlayerMove] '{island.name}' has no RespawnPoint: falling, you reappear where you last stood on it (Memetica > Level > Respawn > New Respawn Point).", island);
        }
        return safeSpot;
    }

    // Fija el respawn a mano. Lo usa el ascensor: el jugador llega al Área 1 sin haberla pisado.
    public void SetRespawn(Transform point)
    {
        respawnPoint = point;
        lastGroundHit = null;   // obliga a volver a revisar el piso en el próximo frame
    }

    private void Respawn()
    {
        VoidRespawned?.Invoke();
        // Its island went down meanwhile (invisible, nothing to land on): back to the start
        if (respawnPoint == null || Lift.IsHiddenAt(respawnPoint))
        {
            Debug.LogWarning($"[PlayerMove] Respawn '{(respawnPoint != null ? respawnPoint.name : "none")}' " +
                             (respawnPoint != null ? "is in an island that is down" : "not set") + ": back to the start respawn point.", respawnPoint);
            respawnPoint = LevelManager.Instance.startRespawnPoint; lastIsland = null; lastGroundHit = null;
        }

        // Drop Distance above the ground of the RespawnPoint: it falls down onto it (and brakes before landing)
        Vector3 from = LevelManager.Instance.RespawnPosition(respawnPoint, out float groundY);
        respawnPoint.TryGetComponent(out RespawnPoint spot);
        if (spot != null && spot.height > 0f) from.y = groundY + spot.height;   // its own height
        lastGroundY = groundY;
        // El CharacterController ignora el cambio de posición si está prendido
        characterController.enabled = false;
        transform.position = from;
        characterController.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        movingGround = null;
        landingBrake = respawnBrakeDistance > 0f;
        awaitingLanding = true;
        touchedDown = false;

        // Slam: from close and fast, no brake (Land lands it)
        if (SlamOn)
        {
            characterController.enabled = false;
            transform.position = new Vector3(from.x, groundY + (spot != null && spot.height > 0f ? spot.height : slamHeight), from.z);
            characterController.enabled = true;
            verticalVelocity = -SlamStartSpeed;
            landingBrake = false;
        }

        // Facing its arrow, looking ahead: ready to try again
        if (spot != null && spot.face)
        {
            transform.rotation = Quaternion.LookRotation(spot.Facing, Vector3.up);   // (the head keeps its angle: looking down, still down)
        }
        look.ResetSmoothing();
        debugHeight = transform.position.y;
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
        if (verticalVelocity >= -respawnLandingSpeed) return;
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 200f, SolidMask, QueryTriggerInteraction.Ignore)) return;

        float height = ground.distance - characterController.height * 0.5f - 0.6f;   // brakes until 0.6 m above, then drops
        if (height > respawnBrakeDistance) return;
        if (height <= 0f) { landingBrake = false; return; }

        float speed = -verticalVelocity;
        float braking = (speed * speed - respawnLandingSpeed * respawnLandingSpeed) / (2f * height);   // stops right at that height
        verticalVelocity = -Mathf.Max(respawnLandingSpeed, speed - braking * Time.deltaTime);
    }

    //---- Slam landing

    private AudioSource landingSource, fallSource;
    private Coroutine landing, landingFade, fallFade;
    private int lastLanding = -1, lastFalling = -1;
    private bool airborne, loud;
    private float topY, lastY, aheadTime;

    // The speed to start falling with so it hits the ground in Slam Fall Time (Gravity, negative, does the rest)
    float SlamStartSpeed => Mathf.Max(0f, slamHeight / Mathf.Max(slamFallTime, 0.05f) + 0.5f * gravity * slamFallTime);

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
        bool inAir = landing == null && (respawnFall || (!isGrounded && !Scripted));
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
            fallSource.volume = loud ? fallSoundsVolume : 0f;
        }
        else if (airborne && landing == null)   // touched down (a respawn lands through Land instead)
        {
            airborne = false;
            if (topY - y >= bigFallHeight && bigFallHeight > 0f) Land();   // a big fall indeed: the slam
            else if (loud) fallFade = StartCoroutine(FadeOut(fallSource, 0.15f));   // heard, but you saved yourself: it just ends
            else fallSource.Stop();
        }
    }

    // Along the fall from here (your speed now, Gravity), the ground you would land on: none within Look Ahead
    // seconds, or Big Fall Height below the highest point of this jump: a big fall
    bool BigFallAhead(float rising)
    {
        float radius = characterController.radius * 0.8f;
        Vector3 feet = transform.position + characterController.center + Vector3.down * (characterController.height * 0.5f - radius);   // the bottom sphere
        Vector3 flat = horizontalVelocity;
        const float step = 0.1f;
        Vector3 from = feet;
        for (float t = step; t <= lookAhead + 0.001f; t += step)
        {
            Vector3 to = feet + flat * t + Vector3.up * (rising * t + 0.5f * gravity * t * t);
            Vector3 path = to - from;
            if (Physics.SphereCast(from, radius, path.normalized, out RaycastHit hit, path.magnitude, SolidMask, QueryTriggerInteraction.Ignore))
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
        InputLocked = true;
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
        InputLocked = false;
        look.SetPose(0f, 0f, 0f);
        look.LookLocked = false;
    }

    // On its own source (not one shot): Landing Sound Max cuts a long one, a new fall cuts it too
    void PlayLandingSound()
    {
        AudioClip clip = Pick(landingSounds, ref lastLanding);
        if (clip == null) return;
        if (landingFade != null) StopCoroutine(landingFade);
        landingSource.Stop();
        landingSource.clip = clip;
        landingSource.volume = fallSoundsVolume;
        landingSource.Play();
        if (landingSoundMax > 0f && clip.length > landingSoundMax) landingFade = StartCoroutine(FadeOut(landingSource, 0.15f, landingSoundMax));
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
