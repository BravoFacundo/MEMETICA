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
    [Tooltip("One of them, at random (never the same twice in a row), on impact")]
    public AudioClip[] sounds;
    [Range(0f, 1f)] public float volume = 1f;

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

    void PlaySound()
    {
        if (sounds == null || sounds.Length == 0) return;
        int i = Random.Range(0, sounds.Length);
        if (sounds.Length > 1 && i == lastSound) i = (i + 1) % sounds.Length;
        lastSound = i;
        if (sounds[i] != null) source.PlayOneShot(sounds[i], volume);
    }

    static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        p -= 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
