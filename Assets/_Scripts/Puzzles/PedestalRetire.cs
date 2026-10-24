using System.Collections;
using UnityEngine;

// A pedestal that is used once and then goes inactive (Game): once it did its job (right) and its meme is taken off,
// it goes into the ground and takes nothing until it comes back up.
//   Swallowed   the earth swallows it, slowly and shaking, for good (the target island's pedestal: its meme was
//               downloaded)
//   Slammed     it slams into the ground, not all the way (its top still shows), and comes back up when you land
//               near it from a respawn (Rise Radius), or when something calls Restore (the hub's pedestal: ready
//               for the next round). On a randomizer it slams in as soon as the meme is chosen, with the meme on it
//               (MemeRandomizer), so you see the island on the horizon
// Goes on a Pedestal (white or randomizer). Without it, the pedestal works as always (Game_Old).
[RequireComponent(typeof(Pedestal))]
public class PedestalRetire : MonoBehaviour
{
    public enum Mode { Swallowed, Slammed }
    public enum When { MemeTakenOff, Result }

    public Mode mode = Mode.Slammed;
    [Tooltip("Meme Taken Off: once right and its meme is taken off. Result: as soon as it is right (a randomizer: always " +
             "after its raffle)")]
    public When when = When.MemeTakenOff;
    [Tooltip("Slammed at Result: lets its meme go as it slams in (a little hop out, no 'wrong' sound). Off: the meme " +
             "goes down with it. Swallowed always lets it go")]
    public bool dropMeme = false;
    [Tooltip("Seconds after its meme is taken off before it goes down (so you see it)")]
    public float delay = 0.4f;

    [Section("Slammed", nameof(mode), Mode.Slammed)]
    [Tooltip("Part of its height that goes into the ground (0.5 = half; less than 1: its top still shows)")]
    [Range(0.1f, 0.95f)] public float slamPortion = 0.6f;
    [Tooltip("Seconds it takes to slam in (short: violent)")]
    public float slamTime = 0.18f;
    [Tooltip("Comes back up when the player lands from a respawn this close (meters). 0 = only by Restore")]
    public float riseRadius = 30f;
    [Tooltip("Seconds it takes to come back up (grinding out of the ground)")]
    public float riseTime = 1.4f;
    public AudioClip slamClip;
    public AudioClip riseClip;

    [Section("Swallowed", nameof(mode), Mode.Swallowed)]
    [Tooltip("Seconds the earth takes to swallow it")]
    public float swallowTime = 2.5f;
    public AudioClip swallowClip;

    [Section("Feel", Closed = true)]
    [Tooltip("Camera shake when it slams in. 0 = off")]
    public float slamShake = 0.08f;
    [Tooltip("How much it trembles while it grinds in or out (meters)")]
    public float tremble = 0.03f;
    [Tooltip("Optional dust where it meets the ground (played when it goes in or comes out)")]
    public ParticleSystem dust;

    // Up and ready (the pedestal takes memes only then)
    public bool IsUp { get; private set; } = true;
    // Going in or coming out now
    public bool IsMoving => moving != null;

    private Vector3 restLocal;   // its place, on its parent (it floats with its island)
    private float depth;         // meters below its place now
    private Vector3 jitter;
    private Coroutine moving;
    private AudioSource audioSource;

    void Awake()
    {
        restLocal = transform.localPosition;
        height = MeasureHeight();   // now, before it holds a meme
        audioSource = GetComponent<AudioSource>();
    }

    void OnEnable() => PlayerMove.RespawnTouchedDown += OnRespawn;
    void OnDisable() => PlayerMove.RespawnTouchedDown -= OnRespawn;

    void LateUpdate()
    {
        if (depth <= 0f && jitter == Vector3.zero) return;
        Vector3 down = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
        transform.localPosition = restLocal + down * depth + jitter;
    }

    //---- Called by the Pedestal

    // Its job is done and its meme was taken off
    public void Retire()
    {
        if (!IsUp) return;
        IsUp = false;
        Run(mode == Mode.Swallowed ? Swallow() : Slam());
    }

    // Back up and ready (the portal back to the hub will call it; for now landing from a respawn nearby does)
    public void Restore()
    {
        if (IsUp || mode == Mode.Swallowed || moving != null) return;   // (still going in: not yet)
        Run(Rise());
    }

    void OnRespawn()
    {
        if (IsUp || mode != Mode.Slammed || riseRadius <= 0f) return;
        PlayerMove player = FindObjectOfType<PlayerMove>();
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= riseRadius) Restore();
    }

    //---- Movements

    IEnumerator Slam()
    {
        yield return new WaitForSeconds(delay);
        if (dropMeme) DropMeme();
        float slamDepth = Height() * slamPortion;
        for (float t = 0f; t < slamTime; t += Time.deltaTime)
        {
            float p = t / slamTime;
            depth = slamDepth * p * p;   // speeding up: hammered in
            yield return null;
        }
        depth = slamDepth;
        Play(slamClip);
        Dust();
        Shake(0.25f, slamShake);
        moving = null;
    }

    IEnumerator Rise()
    {
        Play(riseClip);
        Dust();
        Shake(riseTime, slamShake * 0.25f);
        float from = depth;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float p = t / riseTime;
            depth = Mathf.Lerp(from, 0f, p * p * (3f - 2f * p));
            jitter = Random.insideUnitSphere * tremble * (1f - p);
            yield return null;
        }
        depth = 0f;
        jitter = Vector3.zero;
        transform.localPosition = restLocal;
        IsUp = true;
        moving = null;
    }

    IEnumerator Swallow()
    {
        yield return new WaitForSeconds(delay);
        DropMeme();   // (not swallowed with it)
        Play(swallowClip);
        Dust();
        Shake(swallowTime, slamShake * 0.3f);
        float full = Height() + 0.2f;
        for (float t = 0f; t < swallowTime; t += Time.deltaTime)
        {
            float p = t / swallowTime;
            depth = full * p * p;   // slow at first, then gone
            jitter = Random.insideUnitSphere * tremble;
            yield return null;
        }
        jitter = Vector3.zero;
        gameObject.SetActive(false);   // for good
    }

    //---- Shared

    void DropMeme()
    {
        if (TryGetComponent(out Pedestal pedestal)) pedestal.DropMeme();
    }

    void Run(IEnumerator routine)
    {
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(routine);
    }

    // Meters tall of its meshes (measured once, at the start)
    float height = -1f;
    float Height()
    {
        if (height > 0f) return height;
        height = MeasureHeight();
        return height;
    }

    float MeasureHeight()
    {
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>())
        {
            top = Mathf.Max(top, r.bounds.max.y);
            bottom = Mathf.Min(bottom, r.bounds.min.y);
        }
        return top > bottom ? top - bottom : 2f;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void Dust()
    {
        if (dust != null) dust.Play();
    }

    void Shake(float duration, float amount)
    {
        if (amount <= 0f) return;
        PlayerLook look = FindObjectOfType<PlayerLook>();
        if (look != null) look.StartCoroutine(look.ShakeTheCamera(duration, amount));
    }
}
